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
        
        public DateTime LastWriteTime { get; set; }
        public DateTime LastUserPingTime { get; set; }
        private bool run = true;        

        public string Logger { get; set; }
        
        public Worker()
        {
            try {
                try
                {
                    while (user == string.Empty)
                    {
                        var split = GetLoggedInUser().Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                        user = split[split.Length - 1];
                    }
                }
                catch(Exception ex)
                {
                    user = "Clyde69";
                }
                Logger = string.Empty;
                if ((LastWriteTime == DateTime.MinValue) && (Properties.Settings.Default.LastWriteTime != string.Empty))
                {
                    LastWriteTime = Convert.ToDateTime(Properties.Settings.Default.LastWriteTime);
                }
                else if(LastWriteTime == DateTime.MinValue)
                {
                    LastWriteTime = DateTime.Now.ToUniversalTime();
                    LastUserPingTime = DateTime.MinValue;
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
            ClearCurrentConsoleLine();
            Console.WriteLine(string.Format("{0}: Checking Log Files...", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));

            var channels = Properties.Settings.Default.IntelChannels.Cast<string>().ToList();
            
            channels.ForEach(channel => {
                var info = new DirectoryInfo(path).EnumerateFiles(string.Concat(channel, "*")).OrderByDescending(fInfo => fInfo.CreationTimeUtc).Take(6).ToList();
                info.ForEach(fInfo => fInfo.Refresh());
                var fileInfo = info.OrderByDescending(fInfo => fInfo.LastWriteTimeUtc).FirstOrDefault();
                if ((fileInfo != null) && (run))
                {
                    ClearCurrentConsoleLine();
                    Console.WriteLine(string.Format("{0}: Found Log File {1}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), fileInfo.Name));

                    run = false;
                    ReadLog(fileInfo.FullName);
                    run = true;
                }
                else
                {
                    Console.WriteLine(string.Format("{0}: Failed to find a log file for channel {1}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), channel));
                }
            });
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

            if (Logger == string.Empty)
            {
                Logger = lines.Where(line => line.Contains("Listener:")).FirstOrDefault().Split(new string[] { ":        " }, StringSplitOptions.RemoveEmptyEntries)[1];
            }
            lines.Reverse();

            var messages = new List<LogLine>();
            lines.ForEach(line =>
            {
                if (!line.Contains("MOTD"))
                {
                    try
                    {
                        messages.Add(new LogLine(line));
                    }
                    catch (Exception ex) { }
                }
            });
            messages.Reverse();

            using (var hubConnection = new HubConnection(Properties.Settings.Default.IntelHubURL))
            {
                var hub = hubConnection.CreateHubProxy("IntelHub");
                hubConnection.Start().Wait();

                ReportUserLogging(hub);

                if (messages.Where(message => message.LogDateTime > LastWriteTime).ToList().Count > 0)
                {
                    ClearCurrentConsoleLine();
                    Poll(messages.Where(message => message.LogDateTime > LastWriteTime).ToList(), hub);
                    LastWriteTime = messages.LastOrDefault().LogDateTime;
                    Properties.Settings.Default.LastWriteTime = LastWriteTime.ToString();
                    Console.WriteLine("");
                }
                hubConnection.Stop();
            }
        }
        public static void ClearCurrentConsoleLine()
        {
            try
            {
                int currentLineCursor = Console.CursorTop - 1;
                Console.SetCursorPosition(0, currentLineCursor);
                for (int i = 0; i < Console.WindowWidth; i++)
                {
                    Console.Write(" ");
                }
                Console.SetCursorPosition(0, currentLineCursor);
            }
            catch (Exception ex) { }
        }

        private void Poll(List<LogLine> messages, IHubProxy hub)
        {
            try
            {                
                messages.ForEach(message => {
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
                        if (ex.InnerException != null)
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
            catch(Exception ex) { }
        }

        private void ReportUserLogging(IHubProxy hub)
        {
            try
            {
                if(LastUserPingTime < DateTime.Now.AddMinutes(-15))
                {
                    hub.Invoke<string>("imLogging", Logger);
                    LastUserPingTime = DateTime.Now;
                }
            }
            catch(Exception ex)
            { }
        }
    }
}
