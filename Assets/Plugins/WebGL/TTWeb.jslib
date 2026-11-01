mergeInto(LibraryManager.library, {
  TT_WebInit: function () {
    if (window.__ttWeb) return;
    window.__ttWeb = { back: 0, shown: 0 };
    window.addEventListener("popstate", function () { window.__ttWeb.back++; });
    document.addEventListener("visibilitychange", function () { if (!document.hidden) window.__ttWeb.shown++; });
  },
  TT_WebGo: function (p, replace) {
    var path = UTF8ToString(p);
    if (location.pathname === path && !location.search) return;
    try {
      if (replace) history.replaceState({ tt: 1 }, "", path);
      else history.pushState({ tt: 1 }, "", path);
    } catch (e) {}
  },
  TT_WebShownCount: function () {
    var w = window.__ttWeb; if (!w) return 0;
    var n = w.shown; w.shown = 0; return n;
  },
  TT_WebBackCount: function () {
    var w = window.__ttWeb; if (!w) return 0;
    var n = w.back; w.back = 0; return n;
  }
});
