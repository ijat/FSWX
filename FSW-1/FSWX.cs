using FSWatcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using System.Threading;

namespace FSWX
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
        private ConcurrentQueue<FSWXEvent> currentQ;

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
                OnWork(this, new EventArgs());
            }
        }

        public class FSWXListEvent
        {
            public FSWXEventTypes Type { get; set; }
            public string Path { get; set; }
        }

        public FSWX(string monitorPath)
        {
            Instance = this;
            eventlist = new ConcurrentQueue<FSWXEvent>();
            currentQ = new ConcurrentQueue<FSWXEvent>();
            workThread = new Thread(DoWork);
            OnWork += FSWX_OnWork;
            isActive = false;
            watcher =
                new Watcher(
                    monitorPath,
                    FSWX_DirCreated,
                    FSWX_DirDelete,
                    FSWX_FileCreated,
                    FSWX_FileChanged,
                    FSWX_FileDelete);
            //watcher.ErrorNotifier((path, ex) => { System.Console.WriteLine("{0}\n{1}", path, ex); });
            watcher.Settings.SetPollFrequencyTo(1000);
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

        protected virtual bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
            return false;
        }

        private void DoWork()
        {
            //eventlist = new ConcurrentQueue<FSWXEvent>();
            ConcurrentQueue<FSWXEvent> realQ = new ConcurrentQueue<FSWXEvent>();
            ConcurrentQueue<FSWXEvent> finishedQ = new ConcurrentQueue<FSWXEvent>();

            bool skipNextQ = false;
            bool restartWork = false;

            foreach (var x in eventlist)
            {
                if (x.Event != FSWXEventTypes.Deleted && x.Type == FSWXFileTypes.File)
                {
                    var fslock = new FileInfo(x.FullPath);
                    while (IsFileLocked(fslock))
                    {
                        Thread.Sleep(500);
                        restartWork = true;
                    }
                    if (restartWork)
                    {
                        DoWork();
                    }
                }
            }

            //Console.WriteLine("No more file lock");
            // put in data in currentQ
            currentQ = new ConcurrentQueue<FSWXEvent>();
            foreach (var x in eventlist)
            {
                currentQ.Enqueue(x);
                var y = (FSWXEvent) x.Clone();
                eventlist.TryDequeue(out y);
            }

            // Find the max count and index
            int currentQ_count = currentQ.Count() - 1; // get the max index (not count)
            int currentQ_index = 0;

            //Console.WriteLine("Ok or not?");
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

            //historyQ = new ConcurrentQueue<FSWXEvent>(historyQ.Except(realQ));

            // fix subdir events and historyQ past subdir event
            List<string> rootFolder = new List<string>();
            //if (historyQ.Count > 0)
            //    foreach (FSWXEvent x in historyQ)
            //        rootFolder.Add((x.OldPath != null) ? x.OldPath : x.FullPath);

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
                    if (rootFolder.Count == 0 || !isSubFolderArray(rootFolder, y))
                    {
                        rootFolder.Add(y);
                        filterQ.Enqueue(x);
                    }
                    else
                        continue;
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
            var dchanged = realQ.GroupBy(c => c.FullPath).Where(g => g.Skip(1).Any()).SelectMany(c => c).ToList();

            // fire up events to seperate event handlers
            foreach (var x in realQ)
            {
                switch (x.Event)
                {
                    case FSWXEventTypes.Created:
                        try
                        {
                            //if (!IsFileLocked(new FileInfo(x.FullPath))) {
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
            QTimer.Interval = 5000;
            QTimer.Enabled = true;
            QTimer.Start();
            watcher.Watch();
        }
    }
}
