var ListView = {
    "run": {}
};

$(function () {
    $(".list-selectable").each(function() {
        var listView = $(this).attr("tabindex", "0").attr("onselectstart", "return false");

        var id = listView.attr("id");
        if (!id) {
            id = "id_" + Math.random();
            listView.attr("id", id);
        }

        listView.css({"position": "relative"});
        listView.on("click", function() {
            if (onMouseUp) {
                onMouseUp = null;
                onMouseMove = null;
                return false;
            }
            listView.addClass("selected");
            listView.find("li").removeClass("selected");
            return false;
        });
        $(document).on("click", function() {
            listView.removeClass("selected");
        });
        listView.on("click", "li", function() {
            onMouseUp = null;
            onMouseMove = null;
            selectItem($(this), true);
            return false;
        });
        listView.on("dblclick", "li", function () {
            var run = ListView.run[id];
            if (run) {
                run($(this));
            }
        });
        function selectItem(li, isClick) {
            var last = listView.find("li.last-selected");
            var lis = listView.find("li").removeClass("last-clicked");
            li.addClass("last-clicked");
            if (shift) {
                if (!ctrl) {
                    lis.removeClass("selected");
                }
                var from = null, to = null;
                lis.each(function() {
                    if (from && !to) {
                        $(this).addClass("selected");
                    }
                    if (this == last[0]) {
                        $(this).addClass("selected");
                        if (from) {
                            to = this;
                        } else {
                            from = this;
                        }
                    }
                    if (this == li[0]) {
                        $(this).addClass("selected");
                        if (from) {
                            to = this;
                        } else {
                            from = this;
                        }
                    }
                });
            } else {
                last.removeClass("last-selected");
                li.addClass("last-selected");
                if (ctrl) {
                    if (isClick) {
                        if (li.hasClass("selected")) {
                            li.removeClass("selected");
                        } else {
                            li.addClass("selected");
                        }
                    } else {
                    }
                } else {
                    lis.removeClass("selected");
                    li.addClass("selected");
                }
            }
            listView.addClass("selected");
        };
        function lastClickedIndex() {
            var index = -1;
            listView.find("li").each(function(i) {
                if ($(this).hasClass("last-clicked")) {
                    index = i;
                }
            });
            return index;
        }
        listView.on("keydown", function(e) {
            switch (e.keyCode) {
                case 13:
                    var index = lastClickedIndex();
                    if (index >= 0) selectItem(listView.find("li:eq(" + index + ")"), true);
                    var run = ListView.run[id];
                    if (run) {
                        run(listView.find("li.last-selected"));
                    }
                    break;
                case 38:
                    var index = lastClickedIndex();
                    if (index > 0) selectItem(listView.find("li:eq("+(index-1)+")"), false);
                    break;
                case 40:
                    var index = lastClickedIndex();
                    if (index < listView.find("li").length-1) selectItem(listView.find("li:eq("+(index+1)+")"), false);
                    break;
                case 32:
                    var index = lastClickedIndex();
                    var li = listView.find("li:eq("+index+")");
                    if (li.hasClass("selected")) {
                        li.removeClass("selected");
                    } else {
                        li.addClass("selected");
                    }
                    break;
                case 46:
                    listView.find("li.selected").remove();
                    break;
                case 65:
                    if (ctrl) {
                        listView.find("li").addClass("selected");
                    }
                    break;
            }
        });
        var dragSelection;
        var listViewRect;
        var startPos = null;
        var startIndex = null;
        function getIndex(y) {
            var top = y - listView.position().top;
            var index = 0;
            listView.find("li").each(function(i) {
                var li = $(this);
                if (top > li.position().top + li.outerHeight()) {
                    index = i + 1;
                }
            });
            return index;
        }
        function selectRange(a, b) {
            var selected = [];
            listView.find("li").each(function(i) {
                if (i >= a && i <= b) {
                    selected.push($(this).addClass("selected"));
                } else {
                    $(this).removeClass("selected");
                }
            });
            return selected;
        }
        function selectionRect(x1, y1, x2, y2) {
            var rect = {
                    "top": Math.min(y1, y2)
                ,   "left": Math.min(x1, x2)
                ,   "width": (((x1<x2 ? 1 : -1) * (x2 - x1)) - 2)
                ,   "height": (((y1<y2 ? 1 : -1) * (y2 - y1)) - 2)
            };
            if (rect.top  < listViewRect[1]) { rect.height -= (listViewRect[1] - rect.top ); rect.top  = listViewRect[1]; }
            if (rect.left < listViewRect[0]) { rect.width  -= (listViewRect[0] - rect.left); rect.left = listViewRect[0]; }
            if (rect.top  + rect.height + 2> listViewRect[1] + listViewRect[3]) { rect.height = listViewRect[1] + listViewRect[3] - rect.top  - 2; }
            if (rect.left + rect.width  + 2> listViewRect[0] + listViewRect[2]) { rect.width  = listViewRect[0] + listViewRect[2] - rect.left - 2; }
            return rect;
        }
        listView.on("mousedown", function(e) {
            if (e.button == 0) {
                var offset = listView.offset();
                listViewRect =  [offset.left, offset.top, listView.outerWidth(), listView.outerHeight()];
                listView.addClass("selected");
                startPos = {"x":e.clientX,"y":e.clientY};
                dragSelection = dragSelection ? dragSelection : $("<div>").addClass("selection");
                $("body").append(dragSelection.css(selectionRect(startPos.x, startPos.y, startPos.x, startPos.y)));
                onMouseUp = onListViewMouseUp;
                onMouseMove = onListViewMouseMove;
                startIndex = getIndex(startPos.y);
                if (startIndex < listView.find("li").length) {
                    selectItem(listView.find("li:eq("+startIndex+")"), true);
                } else {
                    listView.find("li.selected").removeClass("selected");
                }
            }
        });
        function onListViewMouseMove(e) {
            if (onMouseUp && typeof onMouseUp == "function") {
                var r = selectDragRange(startPos.x, startPos.y, e.clientX, e.clientY);
                selectRange(r[0], r[1]);
            }
        }
        function onListViewMouseUp(e) {
            if (onMouseUp && typeof onMouseUp == "function" && e.button == 0) {
                dragSelection.remove();
            }
        };
        function selectDragRange(x1, y1, x2, y2) {
            dragSelection.css(selectionRect(x1, y1, x2, y2));
            var endIndex = getIndex(y2);
            var a = startIndex < endIndex ? startIndex : endIndex;
            var b = startIndex < endIndex ? endIndex : startIndex;
            return [a, b];
        }
    });
});