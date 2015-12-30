using Microsoft.AspNet.SignalR.Client;
using R3MUS.Devpack.Slack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace R3MUS.Devpack.IntelLogger
{
    public class Worker
    {
        private string path = @"C:\Users\{0}\Documents\EVE\logs\Chatlogs";
        private string user = string.Empty;
        private FileSystemWatcher watcher;
        private int logLinesLength = 0;
        private DateTime lastWriteTime = DateTime.Now;
        private bool run = true;

        public Worker()
        {
            //Console.WriteLine(1.ToString());
            //Console.ReadLine();
            try {
                while (user == string.Empty)
                {
                    var split = GetLoggedInUser().Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                    user = split[split.Length - 1];
                }
                path = string.Format(path, user);
                watcher = new FileSystemWatcher(path);
                watcher.Changed += new FileSystemEventHandler(CheckLogs);
                watcher.EnableRaisingEvents = true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                var payload = new MessagePayload()
                {
                    Text = "R3MUS Intel Logger Error",
                    Username = "IntelLoggerBot",
                    Channel = "it_testing"
                };
                payload.Attachments.Add(new MessagePayloadAttachment()
                {
                    AuthorName = user,
                    Title = ex.Message,
                    Colour = "#ff0000"
                });
                if (ex.InnerException != null)
                {
                    payload.Attachments.FirstOrDefault().Text = ex.InnerException.Message;
                }
                Slack.Plugin.SendToRoom(payload, "it_testing", Properties.Settings.Default.SlackWebHook, "IntelLoggerBot");
            }
        }

        private string GetLoggedInUser()
        {
            ManagementObjectSearcher searcher;
            ManagementObjectCollection collection;
            if (Properties.Settings.Default.Debug)
            {
                return "Clyde69";
            }
            else
            {
                try
                {
                    searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
                    collection = searcher.Get();
                    return (string)collection.Cast<ManagementBaseObject>().First()["UserName"];
                }
                catch (Exception ex)
                {
                    return string.Empty;
                }
            }
        }

        private void CheckLogs(object source, FileSystemEventArgs args)
        {
            if((args.Name.Contains(Properties.Settings.Default.IntelChannel)) && (File.GetLastWriteTime(args.FullPath) > lastWriteTime) && (run))
            {
                run = false;
                ReadLog(args.FullPath);
                lastWriteTime = File.GetLastWriteTime(args.FullPath);
                run = true;
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
            //var take = 1;
            //if (logLinesLength > 0)
            //{
            //    take = lines.Count - logLinesLength;
            //}
            lines.Reverse();
            //lines = lines.Take(take).ToList();
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
            logLinesLength = lines.Count;
        }

        private void Poll(List<LogLine> messages)
        {
            var hubConnection = new HubConnection(Properties.Settings.Default.IntelHubURL);
            var hub = hubConnection.CreateHubProxy("IntelHub");
            hubConnection.Start().Wait();
            messages.Where(message => message.LogDateTime > lastWriteTime).ToList().ForEach(message => {
                try
                {
                    Console.WriteLine(string.Format("{0}: {1} > {2}", message.LogDateTime.ToString("yyyy-MM-dd HH:mm:ss"), message.UserName, message.Message));
                    hub.Invoke<LogLine>("reportIntel", message);
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    var payload = new MessagePayload()
                    {
                        Text = "R3MUS Intel Logger Error",
                        Username = "IntelLoggerBot",
                        Channel = "it_testing"
                    };
                    payload.Attachments.Add(new MessagePayloadAttachment()
                    {
                        AuthorName = message.UserName, Title = message.LogDateTime.ToString("yyyy-MM-dd HH:mm:ss"), Text = message.Message,
                        Colour = "#ff0000"
                    });
                    payload.Attachments.Add(new MessagePayloadAttachment()
                    {
                        AuthorName = message.UserName,
                        Title = message.Message,
                        Text = ex.Message,
                        Colour = "#ff0000"
                    });
                    if(ex.InnerException != null)
                    {
                        payload.Attachments.Add(new MessagePayloadAttachment()
                        {
                            AuthorName = message.UserName,
                            Title = message.Message,
                            Text = ex.InnerException.Message,
                            Colour = "#ff0000"
                        });
                    }
                    Slack.Plugin.SendToRoom(payload, "it_testing", Properties.Settings.Default.SlackWebHook, "IntelLoggerBot");
                }
            });
        }
    }
}
