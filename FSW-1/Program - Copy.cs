using FSWatcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

    [Flags]
    public enum FSWXEventTypes
    {
        Null = 0,
        Created = 1,
        Deleted = 2,
        Changed = 4,
        Renamed = 8,
        Move = 16
    }
    class FSWXEvent
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string OldPath { get; set; }
        public string OldName { get; set; }
        public FSWXEventTypes Event { get; set; }
        public bool isFile { get; set; }
        public FSWXEvent(FSWXEventTypes Event, string FullPath, string OldPath = "")
        {
            try
            {
                if (File.Exists(FullPath) || Directory.Exists(FullPath) || Event == FSWXEventTypes.Null)
                {
                    this.Event = Event;
                    this.Name = Path.GetFileName(FullPath);
                    this.FullPath = FullPath;
                    this.OldName = Path.GetFileName(OldPath);
                    this.OldPath = OldPath;
                    this.isFile = File.Exists(FullPath) ? true : false;
                }
                else
                    throw new FSWXException("File or Directory not found");
            }
            catch
            {
                throw new FSWXException("FSWX object could not defined");
            }
        }
    }

    class Program
    {
        // 
        private static int index = 0;
        private static int index2 = 0;
        private static string FSWX_LastPath = "";
        private static FSWXEventTypes FSWX_LastEvent = FSWXEventTypes.Null;

        // Core events
        private static Action<string> FSWX_Created = (x) =>
        {
            //FSWX_LastEvent = ;
            // Prevent duplication
            FSWX_LastEvent = FSWXEventTypes.Created;

            while (FSWX_LastEvent != FSWXEventTypes.Created && FSWX_LastEvent != FSWXEventTypes.Null)
                Thread.Sleep(1);

            FSWXEvent z = new FSWXEvent(FSWXEventTypes.Null, x);
            if (FSWX_LastPath.Length > 0)
            {
                string fn = Path.GetFileName(x);
                string dn = Path.GetDirectoryName(x);

                if (fn != Path.GetFileName(FSWX_LastPath) && dn == Path.GetDirectoryName(FSWX_LastPath))
                {
                    FSWX_Rename(x);
                    // rename
                }
                else if (dn != Path.GetDirectoryName(FSWX_LastPath) && fn == Path.GetFileName(FSWX_LastPath))
                {
                    FSWX_Move(x);
                    // move
                }
            }
            else
            {
                z.Event = FSWXEventTypes.Created;
                System.Console.WriteLine("File created: " + x);
            }
            FSWX_LastEvent = FSWXEventTypes.Null;
            FSWX_LastPath = "";
        };

        private static bool isExist(string z)
        {
            if (!File.Exists(z) && !Directory.Exists(z))
                return false;
            else return true;
        }

        private static Action<string> FSWX_Delete = (x) =>
        {
            //if (FSWX_LastEvent != FSWXEventTypes.Deleted) Thread.Sleep(1000);
            while (FSWX_LastEvent != FSWXEventTypes.Null) Thread.Sleep(100);
            FSWX_LastPath = x;
            FSWX_LastEvent = FSWXEventTypes.Deleted;
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                Thread.Sleep(1);
                if (FSWX_LastEvent == FSWXEventTypes.Deleted)
                {
                    System.Console.WriteLine("File delete: " + FSWX_LastPath);
                    FSWX_LastEvent = FSWXEventTypes.Null;
                    FSWX_LastPath = "";
                }
            }).Start();

        };

        // Additional fake events
        private static Action<string> FSWX_Rename = (x) =>
        {
            FSWXEvent e = new FSWXEvent(FSWXEventTypes.Deleted, x, FSWX_LastPath);
            Console.WriteLine(++index + "]\tFile rename: " + x);
        };

        private static Action<string> FSWX_Move = (x) =>
        {
            FSWXEvent e = new FSWXEvent(FSWXEventTypes.Deleted, x);
            Console.WriteLine(++index2 + "]\tFile move: " + x);
        };

        public static void Main(string[] args)
        {

            var watcher =
                new Watcher(
                    @"C:\Users\Waleed\Desktop\#bmt-temp",
                    FSWX_Created,
                    FSWX_Delete,
                    FSWX_Created,
                    (s) => System.Console.WriteLine("File changed " + s),
                    FSWX_Delete);
            watcher.ErrorNotifier((path, ex) => { System.Console.WriteLine("{0}\n{1}", path, ex); });

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

            watcher.Watch();
            var command = System.Console.ReadLine();
            if (command == "refresh")
                watcher.ForceRefresh();
            watcher.StopWatching();

        }
    }
}
