using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace R3MUS.Devpack.IntelLogger
{
    public class Worker
    {
        private string path = @"C:\Users\{0}\Documents\EVE\logs\Chatlogs";
        private string user = string.Empty;
        private FileSystemWatcher watcher;

        public Worker()
        {
            while (user == string.Empty)
            {
                var split = GetLoggedInUser().Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                user = split[split.Length - 1];
            }
            path = string.Format(path, user);
            watcher = new FileSystemWatcher(path);
            watcher.EnableRaisingEvents = true;
            watcher.Changed += new FileSystemEventHandler(CheckLogs);
            watcher.EnableRaisingEvents = true;
        }

        private string GetLoggedInUser()
        {
            var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
            var collection = searcher.Get();
            return (string)collection.Cast<ManagementBaseObject>().First()["UserName"];
        }

        private void CheckLogs(object source, FileSystemEventArgs args)
        {
            //var ready = false;
            if(args.Name.Contains(Properties.Settings.Default.IntelChannel))
            {
                //while(!ready)
                //{
                //    ready = IsFileReady(args.FullPath);
                //}
                ReadLog(args.FullPath);
            }
        }

        private bool IsFileReady(string fileName)
        {
            try
            {
                using (FileStream inputStream = File.Open(fileName, FileMode.Open, FileAccess.Read))
                {
                    if(inputStream.Length > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch(Exception ex)
            {
                return false;
            }
        }

        private void ReadLog(string fileName)
        {
            //var lines = File.ReadAllLines(fileName).ToList();
            var lines = new List<string>();

            using (FileStream inputStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(inputStream))
                {
                    while (!reader.EndOfStream)
                    {
                        lines.Add(reader.ReadLine());
                    }
                }
            }

            lines.Reverse();
            lines = lines.Take(10).ToList();
            var messages = new List<LogLine>();
            lines.ForEach(line =>
            {
                try
                {
                    messages.Add(new LogLine(line));
                }
                catch (Exception ex) { }
            });
            messages.Reverse();
            Poll(messages);
        }

        private void Poll(List<LogLine> messages)
        {
            var hubConnection = new HubConnection(Properties.Settings.Default.IntelHubURL);
            var hub = hubConnection.CreateHubProxy("IntelHub");
            hubConnection.Start().Wait();
            messages.ForEach(message => {
                try
                {
                    hub.Invoke<LogLine>("reportIntel", message);
                }
                catch (Exception ex)
                {

                }
            });
        }
    }
}
