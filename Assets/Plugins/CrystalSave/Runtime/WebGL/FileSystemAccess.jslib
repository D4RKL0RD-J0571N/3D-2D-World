// State is stored on Module to avoid missing-global issues in Unity’s loader.
var crystalSaveSyncing = 0;

mergeInto(LibraryManager.library, {
  CrystalSave_WriteFile: function(pathPtr, dataPtr, length) {
    const path = UTF8ToString(pathPtr);
    const data = HEAPU8.slice(dataPtr, dataPtr + length);
    try {
      const dir = path.substring(0, path.lastIndexOf('/'));
      if (dir) FS.mkdirTree(dir);
      FS.writeFile(path, data, { encoding: 'binary' });
      return 1;
    } catch (e) {
      console.error('CrystalSave_WriteFile error', e); return 0;
    }
  },
  CrystalSave_WriteText: function(pathPtr, textPtr) {
    const path = UTF8ToString(pathPtr);
    const text = UTF8ToString(textPtr);
    try {
      const dir = path.substring(0, path.lastIndexOf('/'));
      if (dir) FS.mkdirTree(dir);
      FS.writeFile(path, text);
      return 1;
    } catch (e) {
      console.error('CrystalSave_WriteText error', e); return 0;
    }
  },
  CrystalSave_ReadFile: function(pathPtr, outPtrPtr) {
    const path = UTF8ToString(pathPtr);
    try {
      const data = FS.readFile(path, { encoding: 'binary' });
      const ptr = _malloc(data.length);
      HEAPU8.set(data, ptr);
      HEAPU32[outPtrPtr >> 2] = ptr;
      return data.length;
    } catch (e) {
      HEAPU32[outPtrPtr >> 2] = 0;
      return 0;
    }
  },
  CrystalSave_ReadText: function(pathPtr, outPtrPtr) {
    const path = UTF8ToString(pathPtr);
    try {
      const text = FS.readFile(path, { encoding: 'utf8' });
      const len = lengthBytesUTF8(text) + 1;
      const ptr = _malloc(len);
      stringToUTF8(text, ptr, len);
      HEAPU32[outPtrPtr >> 2] = ptr;
      return len - 1;
    } catch (e) {
      HEAPU32[outPtrPtr >> 2] = 0;
      return 0;
    }
  },
  CrystalSave_FileExists: function(pathPtr) {
    const path = UTF8ToString(pathPtr);
    try {
      FS.lookupPath(path);
      return 1;
    } catch (e) {
      return 0;
    }
  },
  CrystalSave_DeleteFile: function(pathPtr) {
    const path = UTF8ToString(pathPtr);
    try {
      if (FS.analyzePath(path).exists) FS.unlink(path);
      return 1;
    } catch (e) { return 0; }
  },
  CrystalSave_SyncFs: function(populate) {
    if (typeof indexedDB === 'undefined') return; // no persistent storage
    if (typeof FS === 'undefined' || typeof IDBFS === 'undefined') return;
    // Ensure /idbfs is mounted
    try { FS.lookupPath('/idbfs'); }
    catch (e) {
      try { FS.mkdir('/idbfs'); } catch (e2) {}
      try { FS.mount(IDBFS, {}, '/idbfs'); } catch (e3) {}
    }
    // cross-file busy flag
    var m = (typeof Module !== 'undefined') ? Module : {};
    if (m.__CrystalSaveFSBusy) return;
    m.__CrystalSaveFSBusy = 1;
    crystalSaveSyncing = 1;
    FS.syncfs(!!populate, function (err) {
      if (err) console.error('CrystalSave_SyncFs error', err);
      if (!!populate) {
        try { if (typeof Module !== 'undefined') Module.__CrystalSaveFSReady = 1; } catch (e) {}
      }
      crystalSaveSyncing = 0;
      try { if (typeof Module !== 'undefined') Module.__CrystalSaveFSBusy = 0; } catch (e) {}
    });
  },
  // One-time initialisation for IDBFS: ensure mount and install
  // page lifecycle listeners to flush on hide. Do not force a
  // populate here to avoid racing Unity's own initial sync.
  CrystalSave_InitFs: function() {
    try {
      if (typeof indexedDB === 'undefined') return; // nothing we can do
      if (typeof FS === 'undefined' || typeof IDBFS === 'undefined') return;
      // Ensure /idbfs is mounted
      try { FS.lookupPath('/idbfs'); }
      catch (e) {
        try { FS.mkdir('/idbfs'); } catch (e2) {}
        try { FS.mount(IDBFS, {}, '/idbfs'); } catch (e3) {}
      }
      // Install lifecycle flush hooks once
      var m = (typeof Module !== 'undefined') ? Module : {};
      if (!m.__CrystalSaveEventsInstalled && typeof addEventListener === 'function') {
        var doFlush = function() {
          if (typeof indexedDB === 'undefined') return;
          var m = (typeof Module !== 'undefined') ? Module : {};
          if (m.__CrystalSaveFSBusy) return;
          try {
            m.__CrystalSaveFSBusy = 1;
            FS.syncfs(false, function() {
              try { if (typeof Module !== 'undefined') Module.__CrystalSaveFSBusy = 0; } catch (e) {}
            });
          } catch (e) {
            try { if (typeof Module !== 'undefined') Module.__CrystalSaveFSBusy = 0; } catch (e2) {}
          }
        };
        try {
          addEventListener('pagehide', doFlush);
          addEventListener('beforeunload', doFlush);
          if (typeof document !== 'undefined' && document.addEventListener) {
            document.addEventListener('visibilitychange', function() {
              if (document.visibilityState === 'hidden') doFlush();
            });
          }
          try { if (typeof Module !== 'undefined') Module.__CrystalSaveEventsInstalled = 1; } catch (e) {}
        } catch (e) { /* ignore */ }
      }
    } catch (e) {
      console.error('CrystalSave_InitFs exception', e);
    }
  },
  CrystalSave_IsSyncing: function() {
    if (typeof Module !== 'undefined' && Module.__CrystalSaveFSBusy) return 1;
    return crystalSaveSyncing;
  },
  CrystalSave_BeginFlush: function() {
    var state = Module.__crystalSaveFlushes || (Module.__crystalSaveFlushes = {next: 1, results: {}, timers: {}});
    state.timers = state.timers || {};
    var id = state.next++;
    state.results[id] = 0;
    var start = function() {
      delete state.timers[id];
      if (!Object.prototype.hasOwnProperty.call(state.results, id)) return;
      if (typeof indexedDB === 'undefined' || typeof FS === 'undefined' || typeof IDBFS === 'undefined') { state.results[id] = -1; return; }
      if (Module.__CrystalSaveFSBusy) { state.timers[id] = setTimeout(start, 10); return; }
      Module.__CrystalSaveFSBusy = 1;
      try {
        FS.syncfs(false, function(error) {
          Module.__CrystalSaveFSBusy = 0;
          if (Object.prototype.hasOwnProperty.call(state.results, id)) state.results[id] = error ? -1 : 1;
        });
      } catch (error) {
        Module.__CrystalSaveFSBusy = 0;
        if (Object.prototype.hasOwnProperty.call(state.results, id)) state.results[id] = -1;
      }
    };
    start(); return id;
  },
  CrystalSave_FlushStatus: function(id) { return Module.__crystalSaveFlushes.results[id]; },
  CrystalSave_ForgetFlush: function(id) {
    var state = Module.__crystalSaveFlushes;
    if (!state) return;
    if (state.timers && Object.prototype.hasOwnProperty.call(state.timers, id)) {
      clearTimeout(state.timers[id]);
      delete state.timers[id];
    }
    delete state.results[id];
  },
  CrystalSave_Free: function(ptr) {
    _free(ptr);
  }
});
