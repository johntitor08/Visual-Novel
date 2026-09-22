// VNWebGL.jslib -- browser-side helpers for the visual novel.
mergeInto(LibraryManager.library, {

  // In a browser, Application.persistentDataPath is an in-memory filesystem backed by
  // IndexedDB, and a write only becomes durable once the two are synced. Without this a
  // save made just before the tab closes can be silently lost.
  VN_SyncFS__deps: ['$FS'],
  VN_SyncFS: function () {
    if (typeof FS === 'undefined' || !FS.syncfs) return;
    FS.syncfs(false, function (err) {
      if (err) console.warn('[VN] IndexedDB sync failed: ' + err);
    });
  }
});
