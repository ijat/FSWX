using FSWatcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using System.Threading;

namespace FSW_1
{
    [Serializable()]
    public class FSWXException : Exception
    {
        public FSWXException()
        {
            // Add any type-specific logic, and supply the default message.
        }

        public FSWXException(string message) : base(message)
        {
            // Add any type-specific logic.
        }
        public FSWXException(string message, Exception innerException) :
           base(message, innerException)
        {
            // Add any type-specific logic for inner exceptions.
        }
        protected FSWXException(SerializationInfo info,
           StreamingContext context) : base(info, context)
        {
            // Implement type-specific serialization constructor logic.
        }
    }

    public class FSWXEventArgs : EventArgs
    {
        public FSWXEventTypes EventType { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public string OldPath { get; set; }
        public string OldName { get; set; }
    }

    [Flags]
    public enum FSWXEventTypes
    {
        Null = 0,
        Created = 1,
        Deleted = 2,
        Changed = 4,
        Renamed = 8,
        Moved = 16
    }
    [Flags]
    public enum FSWXFileTypes
    {
        Folder = 0,
        File = 1,
        SymbolicLink = 2,
        HardLink = 4,
        DirectoryJunction = 8,
        Unknown = 16
    }
    public class FSWXEvent : ICloneable
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string OldPath { get; set; }
        public string OldName { get; set; }
        public FSWXEventTypes Event { get; set; }
        public FSWXFileTypes Type { get; set; }
        //public bool isFile { get; set; }
        public FSWXEvent(FSWXEventTypes ev, string cur, FSWXFileTypes t = FSWXFileTypes.Unknown)
        {
            try
            {
                Event = ev;
                Name = Path.GetFileName(cur);
                FullPath = cur;
                Type = t;
            }
            catch
            {
                throw new FSWXException("FSWX object could not defined");
            }
        }
        public FSWXEvent(FSWXEventTypes ev, string cur, string old = "", FSWXFileTypes t = FSWXFileTypes.Unknown)
        {
            try
            {
                Event = ev;
                Name = Path.GetFileName(cur);
                FullPath = cur;
                OldName = Path.GetFileName(old);
                OldPath = old;
                Type = t;
            }
            catch
            {
                throw new FSWXException("FSWX object could not defined");
            }
        }

        public FSWXEvent()
        {
            Event = FSWXEventTypes.Null;
            Name = "";
            FullPath = "";
            OldName = "";
            OldPath = "";
            Type = FSWXFileTypes.Unknown;
        }

        public object Clone()
        {
            return new FSWXEvent
            {
                Event = this.Event,
                Name = this.Name,
                FullPath = this.FullPath,
                OldName = this.OldName,
                OldPath = this.OldPath,
                Type = this.Type
            };
        }
    }

    public class FSWX
    {
        private Watcher watcher;
        private ConcurrentQueue<FSWXEvent> eventlist;
        private ConcurrentQueue<FSWXEvent> historyQ;
        //private ConcurrentQueue<FSWXEvent> EventList;

        Thread workThread;

        // Events
        public event EventHandler<FSWXEvent> OnRenamed;
        public event EventHandler<FSWXEvent> OnMoved;
        public event EventHandler<FSWXEvent> OnCreated;
        public event EventHandler<FSWXEvent> OnDeleted;
        public event EventHandler<FSWXEvent> OnChanged;
        private event EventHandler OnWork;

        public static FSWX Instance;
        System.Timers.Timer QTimer;
        System.Timers.Timer HistoryTimer;
        bool isactive;
        bool isActive
        {
            get
            {
                return isactive;
            }
            set
            {
                isactive = value;
                if (HistoryTimer != null)
                {
                    HistoryTimer.Stop();
                    HistoryTimer.Start();
                }
                OnWork(this, new EventArgs());
            }
        }

        public class FSWXListEvent
        {
            public FSWXEventTypes Type { get; set; }
            public string Path { get; set; }
        }

        public FSWX()
        {
            Instance = this;
            eventlist = new ConcurrentQueue<FSWXEvent>();
            historyQ = new ConcurrentQueue<FSWXEvent>();
            workThread = new Thread(DoWork);
            OnWork += FSWX_OnWork;
            isActive = false;
            watcher =
                new Watcher(
                    @"C:\Users\Waleed\Desktop\#bmt-temp",
                    FSWX_DirCreated,
                    FSWX_DirDelete,
                    FSWX_FileCreated,
                    FSWX_FileChanged,
                    FSWX_FileDelete);
            watcher.ErrorNotifier((path, ex) => { System.Console.WriteLine("{0}\n{1}", path, ex); });
            watcher.Settings.SetPollFrequencyTo(100);
            // Print strategy
            System.Console.WriteLine(
                "Will poll continuously: {0}",
                watcher.Settings.ContinuousPolling);
            System.Console.WriteLine(
                "Poll frequency: {0} milliseconds",
                watcher.Settings.PollFrequency);

            System.Console.WriteLine(
                "Evented directory create: {0}",
                watcher.Settings.CanDetectEventedDirectoryCreate);
            System.Console.WriteLine(
                "Evented directory delete: {0}",
                watcher.Settings.CanDetectEventedDirectoryDelete);
            System.Console.WriteLine(
                "Evented directory rename: {0}",
                watcher.Settings.CanDetectEventedDirectoryRename);
            System.Console.WriteLine(
                "Evented file create: {0}",
                watcher.Settings.CanDetectEventedFileCreate);
            System.Console.WriteLine(
                "Evented file change: {0}",
                watcher.Settings.CanDetectEventedFileChange);
            System.Console.WriteLine(
                "Evented file delete: {0}",
                watcher.Settings.CanDetectEventedFileDelete);
            System.Console.WriteLine(
                "Evented file rename: {0}",
                watcher.Settings.CanDetectEventedFileRename);
        }

        private void FSWX_OnWork(object sender, EventArgs e)
        {
            if (isactive)
                QTimer.Stop();
            else if (QTimer != null && !isactive)
                QTimer.Start();
        }

        private bool IsSubfolder(string parentPath, string childPath)
        {
            var parentUri = new Uri(parentPath);
            var childUri = new DirectoryInfo(childPath).Parent;
            while (childUri != null)
            {
                if (new Uri(childUri.FullName) == parentUri)
                {
                    return true;
                }
                childUri = childUri.Parent;
            }
            return false;
        }

        private void QTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (eventlist.Count() > 0 && !workThread.IsAlive && !isActive)
            {
                workThread = new Thread(new ThreadStart(DoWork));
                workThread.Start();
            }
        }

        private void DoWork()
        {
            ////////////////////

            ConcurrentQueue<FSWXEvent> currentQ = new ConcurrentQueue<FSWXEvent>(eventlist);
            eventlist = new ConcurrentQueue<FSWXEvent>();
            ConcurrentQueue<FSWXEvent> realQ = new ConcurrentQueue<FSWXEvent>();
            ConcurrentQueue<FSWXEvent> finishedQ = new ConcurrentQueue<FSWXEvent>();

            int currentQ_count = currentQ.Count() - 1; // get the max index (not count)
            int currentQ_index = 0;
            bool skipNextQ = false;

            foreach (var x in currentQ) //.AsParallel().AsOrdered())
            {
                if (skipNextQ)
                {
                    if (currentQ_index + 1 <= currentQ_count)
                    {
                        if (currentQ.ElementAt(currentQ_index + 1).Event == x.Event)
                            skipNextQ = true;
                        else
                            skipNextQ = false;
                    }
                    else skipNextQ = false;
                    ++currentQ_index;
                    continue; // continue;
                }

                if (!(currentQ_index + 1 > currentQ_count))
                {
                    // Check rename / move / delete (folder)
                    FSWXEvent nextQ = currentQ.ElementAt(currentQ_index + 1);
                    FSWXEvent newEvent;
                    if (x.Event == FSWXEventTypes.Deleted && nextQ.Event == FSWXEventTypes.Created)
                    {
                        string cfname = Path.GetFileName(x.FullPath).ToLower();
                        string nfname = Path.GetFileName(nextQ.FullPath).ToLower();

                        if (cfname == nfname) // move
                            newEvent = new FSWXEvent(FSWXEventTypes.Moved, nextQ.FullPath, x.FullPath, x.Type);
                        else //rename 
                            newEvent = new FSWXEvent(FSWXEventTypes.Renamed, nextQ.FullPath, x.FullPath, x.Type);
                        realQ.Enqueue(newEvent);
                        skipNextQ = true;
                        ++currentQ_index;
                        continue;
                    }
                    else if (x.Event == FSWXEventTypes.Created && nextQ.Event == FSWXEventTypes.Changed && x.Name.ToLower() == nextQ.Name.ToLower())
                    {
                        realQ.Enqueue(x);
                        skipNextQ = true;
                        ++currentQ_index;
                        continue;
                    }
                }
                realQ.Enqueue(x);
                ++currentQ_index;
            }

            // fix subdir events and historyQ past subdir event
            List<string> rootFolder = new List<string>();
            if (historyQ.Count > 0)
                foreach (FSWXEvent x in historyQ)
                {
                    //if (realQ.Any(o => o.FullPath == x.FullPath))
                    //{
                    //   FSWXEvent o = (FSWXEvent)x.Clone();
                    //    historyQ.TryDequeue(out o);
                    //}
                    //else
                    //{
                    rootFolder.Add((x.OldPath != null) ? x.OldPath : x.FullPath);
                    //}
                    //rootFolder.Add(x.FullPath);
                }
            ConcurrentQueue<FSWXEvent> filterQ = new ConcurrentQueue<FSWXEvent>();
            foreach (var x in realQ)
            {
                // Does not require created event
                if (x.Event == FSWXEventTypes.Created)
                {
                    filterQ.Enqueue(x);
                    continue;
                }
                // oldpath might be required for comparison
                var y = (x.OldPath != null) ? x.OldPath : x.FullPath;
                if (x.Type == FSWXFileTypes.Folder)
                {
                    if (rootFolder.Count == 0)
                    {
                        rootFolder.Add(y);
                        filterQ.Enqueue(x);
                        historyQ.Enqueue(x);
                    }
                    else if (!isSubFolderArray(rootFolder, y))
                    {
                        rootFolder.Add(y);
                        filterQ.Enqueue(x);
                        historyQ.Enqueue(x);
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    if (isSubFolderArray(rootFolder, y))
                        continue;
                    else
                        filterQ.Enqueue(x);
                }
            }

            // give filtered events back to realQ
            realQ = new ConcurrentQueue<FSWXEvent>(filterQ);
            //var dchanged = realQ.GroupBy(c => c.Name).Where(g => g.Skip(1).Any()).SelectMany(c => c).ToList();

            // fire up events to seperate event handlers
            foreach (var x in realQ)
            {
                switch (x.Event)
                {
                    case FSWXEventTypes.Created:
                        try
                        {
                            OnCreated(this, x);
                            finishedQ.Enqueue(x);
                        }
                        catch { }
                        break;
                    case FSWXEventTypes.Deleted:
                        try
                        {
                            OnDeleted(this, x);
                            finishedQ.Enqueue(x);
                        }
                        catch { }
                        break;
                    case FSWXEventTypes.Changed:
                        try
                        {
                            OnChanged(this, x);
                            finishedQ.Enqueue(x);
                        }
                        catch { }
                        break;
                    case FSWXEventTypes.Moved:
                        try
                        {
                            OnMoved(this, x);
                            finishedQ.Enqueue(x);
                        }
                        catch { }
                        break;
                    case FSWXEventTypes.Renamed:
                        try
                        {
                            OnRenamed(this, x);
                            finishedQ.Enqueue(x);
                        }
                        catch { }
                        break;
                    default:
                        break;
                }
            }

            //historyQ = new ConcurrentQueue<FSWXEvent>(finishedQ);

            var diffQ = realQ.Except(finishedQ);
            currentQ = new ConcurrentQueue<FSWXEvent>();
            if (diffQ.Count() > 0)
                foreach (var x in diffQ)
                    eventlist.Enqueue(x);
        }

        private bool isSubFolderArray(List<string> parent, string test)
        {
            int subIndex = 0;
            foreach (var y in parent)
            {
                if (IsSubfolder(y, test))
                    ++subIndex;
                else
                    continue;
            }
            if (subIndex > 0) return true;
            else return false;
        }

        // Core events
        private Action<string> FSWX_FileCreated = (x) =>
        {
            Instance.isActive = true;
            Instance.eventlist.Enqueue(new FSWXEvent(FSWXEventTypes.Created, x, FSWXFileTypes.File));
            Instance.isActive = false;
        };
        private Action<string> FSWX_DirCreated = (x) =>
        {
            Instance.isActive = true;
            Instance.eventlist.Enqueue(new FSWXEvent(FSWXEventTypes.Created, x, FSWXFileTypes.Folder));
            Instance.isActive = false;
        };

        private Action<string> FSWX_FileChanged = (x) =>
        {
            Instance.isActive = true;
            Instance.eventlist.Enqueue(new FSWXEvent(FSWXEventTypes.Changed, x, FSWXFileTypes.File));
            Instance.isActive = false;
        };

        private Action<string> FSWX_FileDelete = (x) =>
        {
            Instance.isActive = true;
            Instance.eventlist.Enqueue(new FSWXEvent(FSWXEventTypes.Deleted, x, FSWXFileTypes.File));
            Instance.isActive = false;
        };

        private Action<string> FSWX_DirDelete = (x) =>
        {
            Instance.isActive = true;
            Instance.eventlist.Enqueue(new FSWXEvent(FSWXEventTypes.Deleted, x, FSWXFileTypes.Folder));
            Instance.isActive = false;
        };

        private bool isExist(string z)
        {
            if (!File.Exists(z) && !Directory.Exists(z))
                return false;
            else return true;
        }

        public void start()
        {
            QTimer = new System.Timers.Timer();
            QTimer.Elapsed += QTimer_Elapsed;
            QTimer.Interval = 1500;
            QTimer.Enabled = true;
            QTimer.Start();
            HistoryTimer = new System.Timers.Timer();
            HistoryTimer.Elapsed += HistoryTimer_Elapsed;
            HistoryTimer.Interval = 1000 * 30;
            HistoryTimer.Start();
            watcher.Watch();
        }

        private void HistoryTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!isActive)
            {
                historyQ = new ConcurrentQueue<FSWXEvent>();
            }
        }

        ////////////////////
    }

    class Program
    {
        public static void Main(string[] args)
        {
            var fs = new FSWX();
            fs.OnCreated += Fs_OnCreated;
            fs.OnDeleted += Fs_OnDeleted;
            fs.OnChanged += Fs_OnChanged;
            fs.OnMoved += Fs_OnMoved;
            fs.OnRenamed += Fs_OnRenamed;
            fs.start();
            System.Console.WriteLine("Ready");
        }

        private static void Fs_OnRenamed(object sender, FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        private static void Fs_OnMoved(object sender, FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        private static void Fs_OnChanged(object sender, FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        private static void Fs_OnDeleted(object sender, FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        static int ii = 0;
        private static void Fs_OnCreated(object sender, FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }
    }
}
