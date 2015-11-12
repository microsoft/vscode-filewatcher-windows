/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using System;
using System.IO;

namespace VSCode.FileSystem
{

    public class FileEvent
    {
        public int changeType { get; set; }
        public string path { get; set; }
    }

    public enum ChangeType : int
    {
        CHANGED = 0,
        CREATED = 1,
        DELETED = 2,
        LOG = 3
    }

    public class FileWatcher
    {
        private string watchPath;
        private int prefixLength = 0;
        private Action<FileEvent> eventCallback = null;

        public FileSystemWatcher Create(string path, Action<FileEvent> onEvent, Action<ErrorEventArgs> onError)
        {

            watchPath = path;
            prefixLength = watchPath.Length + 1;
            eventCallback = onEvent;

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = watchPath;
            watcher.IncludeSubdirectories = true;

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            watcher.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.CHANGED); });
            watcher.Created += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.CREATED); });
            watcher.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.DELETED); });
            watcher.Renamed += new RenamedEventHandler((object source, RenamedEventArgs e) => { ProcessEvent(e); });
            watcher.Error += new ErrorEventHandler((object source, ErrorEventArgs e) => { onError(e); });

            watcher.InternalBufferSize = 32768; // changing this to a higher value can lead into issues when watching UNC drives

            return watcher;
        }

        private void ProcessEvent(FileSystemEventArgs e, ChangeType changeType)
        {
            this.eventCallback(new FileEvent
            {
                changeType = (int)changeType,
                path = e.FullPath
            });
        }

        private void ProcessEvent(RenamedEventArgs e)
        {
            var newInPath = e.FullPath.StartsWith(watchPath);
            var oldInPath = e.OldFullPath.StartsWith(watchPath);

            if (newInPath)
            {
                this.eventCallback(new FileEvent
                {
                    changeType = (int)ChangeType.CREATED,
                    path = e.FullPath
                });
            }

            if (oldInPath)
            {
                this.eventCallback(new FileEvent
                {
                    changeType = (int)ChangeType.DELETED,
                    path = e.OldFullPath
                });
            }
        }
    }
}