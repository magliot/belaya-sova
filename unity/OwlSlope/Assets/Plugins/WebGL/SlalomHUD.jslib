mergeInto(LibraryManager.library, {
  SlalomHUD: function (phase, score, metres, caught, hint) {
    window.showSlalom(phase, score, metres, caught, UTF8ToString(hint));
  }
});
