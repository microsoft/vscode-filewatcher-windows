/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using FileWatcher;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace VSCode.FileSystem
{
    class Program
    {
        private static BlockingCollection<FileEvent> fileEventQueue = new BlockingCollection<FileEvent>();

        static int Main(string[] args)
        {
            // We want Unicode for the output
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Validate args length
            if (args.Length == 0 || args.Length > 2)
            {
                Console.Error.WriteLine("CodeHelper needs exactly one argument of the directory to watch recursively.");
                return 1;
            }

            // Validate provided path
            var path = args[0];
            if (!Directory.Exists(path))
            {
                Console.Error.WriteLine("Path '{0}' does not exist.", path);
                return 1;
            }

            var verboseLogging = (args.Length > 1 && args[1] == "-verbose");

            // Event processor deals with buffering and normalization of events
            var processor = new EventProcessor((e) => {
                Console.WriteLine("{0}|{1}", e.changeType, e.path);

                if (verboseLogging)
                {
                    Console.WriteLine("{0}| >> normalized {1} {2}", (int)ChangeType.LOG, e.changeType == (int)ChangeType.CREATED ? "[ADDED]" : e.changeType == (int)ChangeType.DELETED ? "[DELETED]" : "[CHANGED]", e.path);
                }
            }, (msg) => {
                Console.WriteLine("{0}|{1}", (int)ChangeType.LOG, msg);
            });

            // Use a thread to unblock producer
            var thread = new Thread(() =>
            {
                while (true)
                {
                    var e = fileEventQueue.Take();
                    processor.ProcessEvent(e);

                    if (verboseLogging)
                    {
                        Console.WriteLine("{0}|{1} {2}", (int)ChangeType.LOG, e.changeType == (int)ChangeType.CREATED ? "[ADDED]" : e.changeType == (int)ChangeType.DELETED ? "[DELETED]" : "[CHANGED]", e.path);
                    }
                }
            });
            thread.IsBackground = true; // this ensures the thread does not block the process from terminating!
            thread.Start();

            // Log each event in our special format to output queue
            Action<FileEvent> onEvent = (e) =>
            {
                fileEventQueue.Add(e);
            };

            Action<ErrorEventArgs> onError = (e) =>
            {
                if (e != null)
                {
                    Console.WriteLine("{0}|{1}", (int)ChangeType.LOG, e.GetException().ToString());
                }
            };

            // Start watching
            var watcher = new FileWatcher();
            var watcherImpl = watcher.Create(path, onEvent, onError);
            watcherImpl.EnableRaisingEvents = true;

            // Quit after any input
            Console.Read();

            return 0;
        }
    }
}
