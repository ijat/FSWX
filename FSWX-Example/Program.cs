using System;
using FSWX;

namespace FSWX_Example
{
    class Program
    {
        public static void Main(string[] args)
        {
            var fs = new FSWX.FSWX(@"E:\monitor");
            fs.OnCreated += Fs_OnCreated;
            fs.OnDeleted += Fs_OnDeleted;
            fs.OnChanged += Fs_OnChanged;
            fs.OnMoved += Fs_OnMoved;
            fs.OnRenamed += Fs_OnRenamed;
            fs.start();
            System.Console.WriteLine("Ready");
        }
  
        private static void Fs_OnRenamed(object sender, FSWX.FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        private static void Fs_OnMoved(object sender, FSWX.FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        private static void Fs_OnChanged(object sender, FSWX.FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        private static void Fs_OnDeleted(object sender, FSWX.FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }

        static int ii = 0;
        private static void Fs_OnCreated(object sender, FSWX.FSWXEvent e)
        {
            ++ii;
            Console.WriteLine("...\nEvent ({3}): {0}\nPath: {1}\nOldPath: {2}", e.Event, e.FullPath, e.OldPath, ii);
        }
    }
}
