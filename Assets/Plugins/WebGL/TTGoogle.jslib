// Google sign-in for the web build. The page (Assets/WebGLTemplates/TT/index.html) defines
// window.ttGoogleSignIn(clientId, done); this hands its answer back to GoogleSignIn.cs.
mergeInto(LibraryManager.library, {
  TT_GoogleSignIn: function (clientIdPtr, callback) {
    var clientId = UTF8ToString(clientIdPtr);
    function reply(ok, text) {
      var buf = stringToNewUTF8(text || "");
      {{{ makeDynCall('vii', 'callback') }}}(ok ? 1 : 0, buf);
      _free(buf);
    }
    if (typeof window.ttGoogleSignIn !== "function") {
      reply(false, "failed|Google sign-in isn't ready on this page. Reload and try again.");
      return;
    }
    try { window.ttGoogleSignIn(clientId, reply); }
    catch (e) { reply(false, "failed|" + e); }
  }
});
