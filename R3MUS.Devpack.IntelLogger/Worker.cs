using Microsoft.AspNet.SignalR.Client;
using Quartz;
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
    [DisallowConcurrentExecution]
    [PersistJobDataAfterExecution]
    public class Worker : IJob
    {
        private string path = @"C:\Users\{0}\Documents\EVE\logs\Chatlogs\";
        private string user = string.Empty;

        private int logLinesLength = 0;
        public DateTime LastWriteTime { get; set; }
        private bool run = true;
        
        public Worker()
        {
            try {
                while (user == string.Empty)
                {
                    var split = GetLoggedInUser().Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                    user = split[split.Length - 1];
                }
                if (Properties.Settings.Default.LastWriteTime != string.Empty)
                {
                    LastWriteTime = Convert.ToDateTime(Properties.Settings.Default.LastWriteTime);
                }
                else
                {
                    LastWriteTime = DateTime.Now;
                }
                path = string.Format(path, user);                
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

        public void Execute(IJobExecutionContext context)
        {
            CheckLogs();
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
        
        private void CheckLogs()
        {
            Console.WriteLine("Checking Log Files...");
            var info = new DirectoryInfo(path).EnumerateFiles(string.Concat(Properties.Settings.Default.IntelChannel, "*"));
            var fileInfo = info.OrderByDescending(fInfo => fInfo.CreationTimeUtc).FirstOrDefault();
            if ((fileInfo != null) && (run))
            {
                Console.WriteLine(string.Format("Found Log File {0}", fileInfo.Name));
                run = false;
                ReadLog(fileInfo.FullName);
                run = true;
            }
        }

        private void ReadLog(string fileName)
        {
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
            if (messages.Count > 0)
            {
                Poll(messages);
                LastWriteTime = messages.LastOrDefault().LogDateTime;
                Properties.Settings.Default.LastWriteTime = LastWriteTime.ToString();
            }
        }

        private void Poll(List<LogLine> messages)
        {
            var hubConnection = new HubConnection(Properties.Settings.Default.IntelHubURL);
            var hub = hubConnection.CreateHubProxy("IntelHub");
            hubConnection.Start().Wait();
            messages.Where(message => message.LogDateTime > LastWriteTime).ToList().ForEach(message => {
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
