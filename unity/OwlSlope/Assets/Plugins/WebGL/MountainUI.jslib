mergeInto(LibraryManager.library, {
  MountainState: function (json) {
    if (window.onMountainState) window.onMountainState(JSON.parse(UTF8ToString(json)));
  }
});
