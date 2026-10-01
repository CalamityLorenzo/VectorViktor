using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace World.Maps.Files
{
    // Watches a map's files (see Map.Files) for being saved, by anything - the map studio, a text editor - so a running
    // app can build the map again. The operating system says when a file changes, on a thread of its own; this only
    // notes the time, and the app asks each frame (Changed). It waits until the files have been quiet for a moment,
    // because one save can be several writes, and building from a half-written file would fail.
    public sealed class MapWatcher : IDisposable
    {
        private const double Quiet = 0.25;   // seconds

        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly HashSet<string> _files;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _changedAt = -1;   // seconds on the clock, or -1 if nothing's changed
        private readonly object _lock = new object();

        public MapWatcher(IEnumerable<string> files)
        {
            _files = new HashSet<string>(files.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            foreach (var folder in _files.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(folder))
                    continue;
                var watcher = new FileSystemWatcher(folder, "*.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.Renamed += (_, e) => OnChanged(null, e);   // how some editors save: write a copy, then swap it in
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (!_files.Contains(Path.GetFullPath(e.FullPath)))
                return;
            lock (_lock)
                _changedAt = _clock.Elapsed.TotalSeconds;
        }

        // Whether any of the files has changed, and been left alone since for long enough to read: once for each change.
        public bool Changed()
        {
            lock (_lock)
            {
                if (_changedAt < 0 || _clock.Elapsed.TotalSeconds - _changedAt < Quiet)
                    return false;
                _changedAt = -1;
                return true;
            }
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
        }
    }
}
