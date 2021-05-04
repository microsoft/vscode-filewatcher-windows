/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using System;
using System.Collections.Generic;
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

    public class FileWatcher : IDisposable
    {
        private string _watchPath;
        private Action<FileEvent> _eventCallback = null;
        private Dictionary<string, FileSystemWatcher> _fileSystemWatcherDictionary = new Dictionary<string, FileSystemWatcher>();
        private Action<ErrorEventArgs> _onError = null;

        public FileSystemWatcher Create(string path, Action<FileEvent> onEvent, Action<ErrorEventArgs> onError)
        {
            this._watchPath = path;
            this._eventCallback = onEvent;
            this._onError = onError;

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = this._watchPath;
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            // Bind internal events to manipulate the possible symbolic links
            watcher.Created += new FileSystemEventHandler(makeWatcher_Created);
            watcher.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

            watcher.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.CHANGED); });
            watcher.Created += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.CREATED); });
            watcher.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.DELETED); });
            watcher.Renamed += new RenamedEventHandler((object source, RenamedEventArgs e) => { ProcessEvent(e); });
            watcher.Error += new ErrorEventHandler((object source, ErrorEventArgs e) => { onError(e); });

            watcher.InternalBufferSize = 32768; // changing this to a higher value can lead into issues when watching UNC drives

            this._fileSystemWatcherDictionary.Add(path, watcher);

            foreach (DirectoryInfo directoryInfo in new DirectoryInfo(path).GetDirectories())
            {
                FileAttributes fileAttributes = File.GetAttributes(directoryInfo.FullName);

                if (fileAttributes.HasFlag(FileAttributes.Directory) && fileAttributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    this.MakeWatcher(directoryInfo.FullName);
                }
            }

            return watcher;
        }

        private void ProcessEvent(FileSystemEventArgs e, ChangeType changeType)
        {
            this._eventCallback(new FileEvent
            {
                changeType = (int)changeType,
                path = e.FullPath
            });
        }

        private void ProcessEvent(RenamedEventArgs e)
        {
            var newInPath = e.FullPath.StartsWith(_watchPath);
            var oldInPath = e.OldFullPath.StartsWith(_watchPath);

            if (newInPath)
            {
                this._eventCallback(new FileEvent
                {
                    changeType = (int)ChangeType.CREATED,
                    path = e.FullPath
                });
            }

            if (oldInPath)
            {
                this._eventCallback(new FileEvent
                {
                    changeType = (int)ChangeType.DELETED,
                    path = e.OldFullPath
                });
            }
        }

        private void MakeWatcher(string path)
        {
            if (!this._fileSystemWatcherDictionary.ContainsKey(path))
            {
                FileSystemWatcher fileSystemWatcherRoot = new FileSystemWatcher();
                fileSystemWatcherRoot.Path = path;
                fileSystemWatcherRoot.IncludeSubdirectories = true;
                fileSystemWatcherRoot.EnableRaisingEvents = true;

                // Bind internal events to manipulate the possible symbolic links
                fileSystemWatcherRoot.Created += new FileSystemEventHandler(makeWatcher_Created);
                fileSystemWatcherRoot.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

                fileSystemWatcherRoot.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CHANGED); });
                fileSystemWatcherRoot.Created += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CREATED); });
                fileSystemWatcherRoot.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.DELETED); });
                fileSystemWatcherRoot.Renamed += new RenamedEventHandler((object source, RenamedEventArgs eva) => { ProcessEvent(eva); });
                fileSystemWatcherRoot.Error += new ErrorEventHandler((object source, ErrorEventArgs eva) => { this._onError(eva); });

                this._fileSystemWatcherDictionary.Add(path, fileSystemWatcherRoot);
            }

            foreach (DirectoryInfo item in new DirectoryInfo(path).GetDirectories())
            {
                FileAttributes attributes = File.GetAttributes(item.FullName);

                // If is a directory and symbolic link
                if (attributes.HasFlag(FileAttributes.Directory) && attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    if (!this._fileSystemWatcherDictionary.ContainsKey(item.FullName))
                    {
                        FileSystemWatcher fileSystemWatcherItem = new FileSystemWatcher();
                        fileSystemWatcherItem.Path = item.FullName;
                        fileSystemWatcherItem.IncludeSubdirectories = true;
                        fileSystemWatcherItem.EnableRaisingEvents = true;

                        // Bind internal events to manipulate the possible symbolic links
                        fileSystemWatcherItem.Created += new FileSystemEventHandler(makeWatcher_Created);
                        fileSystemWatcherItem.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

                        fileSystemWatcherItem.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CHANGED); });
                        fileSystemWatcherItem.Created += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CREATED); });
                        fileSystemWatcherItem.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.DELETED); });
                        fileSystemWatcherItem.Renamed += new RenamedEventHandler((object source, RenamedEventArgs eva) => { ProcessEvent(eva); });
                        fileSystemWatcherItem.Error += new ErrorEventHandler((object source, ErrorEventArgs eva) => { this._onError(eva); });

                        this._fileSystemWatcherDictionary.Add(item.FullName, fileSystemWatcherItem);
                    }

                    this.MakeWatcher(item.FullName);
                }
            }
        }

        private void makeWatcher_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(e.FullPath);
                if (attributes.HasFlag(FileAttributes.Directory) && attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    FileSystemWatcher watcherCreated = new FileSystemWatcher();
                    watcherCreated.Path = e.FullPath;
                    watcherCreated.IncludeSubdirectories = true;
                    watcherCreated.EnableRaisingEvents = true;

                    // Bind internal events to manipulate the possible symbolic links
                    watcherCreated.Created += new FileSystemEventHandler(makeWatcher_Created);
                    watcherCreated.Deleted += new FileSystemEventHandler(makeWatcher_Deleted);

                    watcherCreated.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CHANGED); });
                    watcherCreated.Created += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.CREATED); });
                    watcherCreated.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs eva) => { ProcessEvent(eva, ChangeType.DELETED); });
                    watcherCreated.Renamed += new RenamedEventHandler((object source, RenamedEventArgs eva) => { ProcessEvent(eva); });
                    watcherCreated.Error += new ErrorEventHandler((object source, ErrorEventArgs eva) => { this._onError(eva); });

                    this._fileSystemWatcherDictionary.Add(e.FullPath, watcherCreated);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void makeWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            // If object removed, then I will dispose and remove them from dictionary
            if (this._fileSystemWatcherDictionary.ContainsKey(e.FullPath))
            {
                this._fileSystemWatcherDictionary[e.FullPath].Dispose();
                this._fileSystemWatcherDictionary.Remove(e.FullPath);
            }
        }

        public void Dispose()
        {
            foreach (var item in this._fileSystemWatcherDictionary)
            {
                item.Value.Dispose();
            }
        }
    }
}