javascript: (function () {
  if (window._bkmkSearch_is_active) {
    return;
  }
  try {
    window._bkmkSearch_selects = [];
    window._bkmkSearch_lastValues = [];
    var d = window.frames["mainFrame"].document;
    if (!d) {
      throw new Error("mainFrame not found");
    }
    var s = d.getElementsByTagName("select");
    for (var i = 0; i < s.length; i++) {
      window._bkmkSearch_selects.push(s[i]);
      window._bkmkSearch_lastValues.push("");
      var r = s[i].getBoundingClientRect();
      var st = d.documentElement.scrollTop || d.body.scrollTop;
      var sl = d.documentElement.scrollLeft || d.body.scrollLeft;
      var ip = d.createElement("input");
      ip.id = "_bkmk_search_input_" + i;
      ip.type = "text";
      ip.style.cssText =
        "position:absolute;top:" +
        (r.top + st - 15) +
        "px;left:" +
        (r.left + sl) +
        "px;width:180px;height:16px;border:1px solid #000;padding:2px;z-index:99999;";
      
      // 【新增逻辑】为搜索框绑定 focus 事件，使用闭包确保 select 元素引用正确
      ip.onfocus = (function(targetSelect) {
        return function() {
          var sibling = targetSelect.previousSibling;
          // 向前遍历寻找单选框
          while (sibling) {
            if (sibling.nodeType === 1 && sibling.tagName.toLowerCase() === 'input' && sibling.type === 'radio') {
              // 找到对应单选框且未被选中时，触发原生点击
              if (!sibling.checked) {
                sibling.click();
              }
              break;
            }
            sibling = sibling.previousSibling;
          }
        };
      })(s[i]);

      d.body.appendChild(ip);
    }
    
    // 原有的定时器监听逻辑保持不变
    window._bkmkSearch_interval = setInterval(function () {
      var doc = window.frames["mainFrame"].document;
      if (!doc) {
        return;
      }
      for (var i = 0; i < window._bkmkSearch_selects.length; i++) {
        var inp = doc.getElementById("_bkmk_search_input_" + i);
        var sel = window._bkmkSearch_selects[i];
        if (inp && inp.value !== window._bkmkSearch_lastValues[i]) {
          window._bkmkSearch_lastValues[i] = inp.value;
          var k = inp.value.toLowerCase();
          if (k === "") {
            continue;
          }
          var f = false;
          var opts = sel.getElementsByTagName("option");
          for (var j = 0; j < opts.length; j++) {
            if (opts[j].text.toLowerCase().indexOf(k) !== -1) {
              opts[j].selected = true;
              f = true;
              break;
            }
          }
        }
      }
    }, 200);
    window._bkmkSearch_is_active = true;
  } catch (e) {
    alert("Bookmarklet Error: " + (e.message || e));
  }
})();