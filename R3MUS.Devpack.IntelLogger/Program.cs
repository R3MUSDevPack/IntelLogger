using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace R3MUS.Devpack.IntelLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            var line = new LogLine("[2015.11.26 00:21:53 ] GoodKenny > basil is there a loaner miner available there ?");
            Console.Write(line.LogDateTime);
            Console.ReadLine();
            //var worker = new Worker();
            //Console.ReadLine();
            //var path = string.Format(@"{0}\EVE\logs\Chatlogs", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            //var logFileInfo = new DirectoryInfo(path).EnumerateFiles().Where(file =>
            //    file.Name.Contains(Properties.Settings.Default.IntelChannel)
            //    ).OrderBy(file => file.LastWriteTimeUtc).FirstOrDefault();

            //var fsw = new FileSystemWatcher(path);
            //fsw.EnableRaisingEvents = true;
            //fsw.Changed += CheckLogs;
        }
    }
}
