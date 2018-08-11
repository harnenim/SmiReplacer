using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TextFile;
using Subtitle;

namespace SmiReplacer
{
    public partial class MainForm : WebForm.WebForm
    {
        #region 초기화

        public MainForm()
        {
            ClientSize = new System.Drawing.Size(1600, 800);
            Name = "MainForm";
            Text = "Smi Replacer";
        }

        public override void InitAfterLoad()
        {
            SetDragEvent("listFile", DragDropEffects.All, new DropActionDelegate(DropListFile));
            SetClickEvent("btnSubmit", "DoReplace");
        }

        #endregion


        #region 기본값 관련

        public string[][] JsonReplacersToArray(string json)
        {
            JArray jarr = JArray.Parse(json);
            string[][] replacers = new string[jarr.Count][];
            for (int i = 0; i < jarr.Count; i++)
            {
                JArray jarr1 = (JArray)jarr[i];
                replacers[i] = new string[] { jarr1[0].ToString(), jarr1[1].ToString() };
            }
            return replacers;
        }

        private static string defaultReplacersFile = "conf/replacers.json";

        public void LoadDefaultReplacers()
        {
            string json = "[]";
            try
            {
                StreamReader sr = new StreamReader(defaultReplacersFile, Encoding.UTF8);
                json = sr.ReadToEnd();
                sr.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            string[][] replacers = JsonReplacersToArray(json);
            foreach (string[] replacer in replacers)
            {
                Script("setReplacer", new object[] { replacer[0], replacer[1] });
            }
        }
        public void SaveDefaultReplacers(string json)
        {
            StreamWriter sw = new StreamWriter(defaultReplacersFile);
            sw.WriteLine(json, Encoding.UTF8);
            sw.Close();
        }

        #endregion


        #region 파일 드래그

        private void DropListFile(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                LoadFileList((string[])e.Data.GetData(DataFormats.FileDrop));
            }
        }

        private void LoadFileList(string[] strFiles)
        {
            foreach (string strFile in strFiles)
            {
                string[] fileExists = Script("getFiles").ToString().Split('?');
                if (!fileExists.Contains(strFile))
                {
                    if (Directory.Exists(strFile))
                    {
                        DirectoryInfo[] dirInfos = new DirectoryInfo(strFile).GetDirectories();
                        string[] innerDirs = new string[dirInfos.Length];
                        for (int i = 0; i < dirInfos.Length; i++)
                        {
                            innerDirs[i] = dirInfos[i].FullName;
                        }
                        LoadFileList(innerDirs);

                        FileInfo[] fileInfos = new DirectoryInfo(strFile).GetFiles();
                        string[] innerFiles = new string[fileInfos.Length];
                        for (int i=0; i<fileInfos.Length; i++)
                        {
                            innerFiles[i] = fileInfos[i].FullName;
                        }
                        LoadFileList(innerFiles);
                    }
                    else
                    {
                        string[] filenames = strFile.Split('\\');
                        string filename = filenames[filenames.Length - 1];
                        if (System.Text.RegularExpressions.Regex.IsMatch(filename, ".*\\.smi", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        {
                            Script("addFile", new object[] { strFile });
                        }
                    }
                }
            }
        }

        #endregion

        // html 뷰에서 문자열 쌍(json) 가져오기
        public string GetReplacersJson()
        {
            return "[" + Script("getReplacer").ToString() + "]";
        }
        // json 문자열 쌍을 이중배열로 변환(string[][2]로 나와야 함)
        public string[][] GetReplacers(string json)
        {
            return JsonReplacersToArray(json);
        }

        // 변환 및 저장
        public void DoReplace()
        {
            string replacersJson = GetReplacersJson();
            string[] replacer = GetReplacers(replacersJson)[0];
            List<Smi> originRange = new SmiFile().FromTxt(replacer[0]).body;
            List<Smi> targetRange = new SmiFile().FromTxt(replacer[1]).body;

            bool complete = true;
            Dictionary<int, int> matches = new Dictionary<int, int>();
            {
                int i = 0, j = 0;
                for (; j < targetRange.Count; j++)
                {
                    if (targetRange[j].syncType != SyncType.frame)
                        continue;
                    while (i < originRange.Count && originRange[i].start < targetRange[j].start)
                        i++;
                    if (i >= originRange.Count)
                        break;
                    if (originRange[i].syncType != SyncType.frame)
                    {   // 목표 화면 싱크가 원본 화면 싱크와 겹치지 않으면 -1
                        matches.Add(j, -1);
                        complete = false;
                        continue;
                    }

                    if (originRange[i].start == targetRange[j].start)
                    {   // 목표 화면 싱크가 원본 화면 싱크와 겹치는 경우
                        matches.Add(j, i);
                    }
                    else
                    {   // 목표 화면 싱크가 원본 화면 싱크와 겹치지 않으면 -1
                        matches.Add(j, -1);
                        complete = false;
                    }
                }
            }
            
            string filesString = Script("getFiles").ToString();
            if (filesString.Length == 0)
            {
                Script("alert", new object[] { "파일이 없습니다." });
                return;
            }
            string[] files = filesString.Split('?');

            int success = 0;
            List<string> skips = new List<string>();

            foreach (string file in files)
            {
                Encoding encoding = BOM.DetectEncoding(file);

                bool isChanged = false;
                SmiFile originSmi = null;
                int i = 0, j = 0, shift = 0;

                StreamReader sr = null;
                try
                {
                    sr = new StreamReader(file, encoding);
                    string origin = sr.ReadToEnd();
                    originSmi = new SmiFile().FromTxt(origin);

                    for (; i < originSmi.body.Count; i++)
                    {
                        if (originSmi.body[i].text.Equals(originRange[0].text))
                        {
                            shift = originSmi.body[i].start - originRange[0].start;
                            bool isCorrect = true;

                            for (; j < originRange.Count; j++)
                            {
                                if (!originSmi.body[i + j].text.Equals(originRange[j].text))
                                {
                                    isCorrect = false;
                                    break;
                                }

                                if ((originSmi.body[i + j].syncType == SyncType.normal) &&
                                    (originSmi.body[i + j].start != originRange[j].start + shift))
                                {
                                    isCorrect = false;
                                    break;
                                }

                            }

                            if (isCorrect)
                            {
                                isChanged = true;
                                break;
                            }
                        }
                    }
                }
                catch { }
                finally { if (sr != null) sr.Close(); }

                if (isChanged)
                {
                    SmiFile targetSmi = new SmiFile()
                    {
                        header = originSmi.header,
                        footer = originSmi.footer
                    };

                    for (int k = 0; k < i; k++)
                        targetSmi.body.Add(originSmi.body[k]);

                    for (int k = 0; k < targetRange.Count; k++)
                    {
                        if (targetRange[k].syncType == SyncType.frame && matches[k] >= 0)
                            targetSmi.body.Add(new Smi() {
                                  start = originSmi.body[i + matches[k]].start
                                , syncType = targetRange[k].syncType
                                , text = targetRange[k].text
                            });
                        else
                            targetSmi.body.Add(new Smi() {
                                  start = targetRange[k].start + shift
                                , syncType = targetRange[k].syncType
                                , text = targetRange[k].text
                            });
                    }

                    for (int k = i + j; k < originSmi.body.Count; k++)
                        targetSmi.body.Add(originSmi.body[k]);

                    StreamWriter sw = null;
                    try
                    {
                        // 원본 파일의 인코딩대로 저장
                        sw = new StreamWriter(file, false, encoding);
                        sw.Write(targetSmi.ToTxt());
                        success++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally
                    {
                        if (sw != null) sw.Close();
                    }
                }
                else
                {
                    skips.Add(file);
                }
            }

            // 변환했으면 변환 문자열 쌍을 기억해둠
            SaveDefaultReplacers(replacersJson);

            string msg = "파일 " + files.Length + "개 중 " + success + "개의 작업이 완료됐습니다.";

            if (!complete)
            {
                msg += "\n화면 싱크를 조정할 부분이 있습니다.";
            }
            
            if (skips.Count > 0)
            {
                msg += "\n제외 파일";
                foreach (string file in skips)
                {
                    msg += "\n" + file;
                }
            }

            Script("alert", new object[] { msg });
        }
    }
}
