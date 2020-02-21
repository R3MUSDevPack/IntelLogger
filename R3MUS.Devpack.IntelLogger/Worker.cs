using Microsoft.AspNet.SignalR.Client;
using Quartz;
using R3MUS.Devpack.IntelLogger.Helpers;
using R3MUS.Devpack.IntelLogger.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace R3MUS.Devpack.IntelLogger
{
    [DisallowConcurrentExecution]
    [PersistJobDataAfterExecution]
    public class Worker : IJob
    {
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
                    user = Properties.Settings.Default.BroadcastGroup;
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
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                //var payload = new MessagePayload()
                //{
                //    Text = "R3MUS Intel Logger Error",
                //    Username = "IntelLoggerBot",
                //    Channel = "it_testing"
                //};
                //payload.Attachments.Add(new MessagePayloadAttachment()
                //{
                //    AuthorName = user,
                //    Title = ex.Message,
                //    Colour = "#ff0000"
                //});
                //if (ex.InnerException != null)
                //{
                //    payload.Attachments.FirstOrDefault().Text = ex.InnerException.Message;
                //}
                //Slack.Plugin.SendToRoom(payload, "it_testing", Properties.Settings.Default.SlackWebHook, "IntelLoggerBot");
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
            if (run)
            {
                try
                {
                    run = false;
                    ClearCurrentConsoleLine();
                    Console.WriteLine(string.Format("{0}: Checking Log Files...", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));

                    Program.LogFileNames.ForEach(group =>
                    {
                        Program.HubProxy.Invoke("joinGroup", group.Group);

                        group.Channels.ForEach(channel =>
                        {
                            try
                            {
                                var filePath = GetFilePath(channel);
                                if (!string.IsNullOrEmpty(filePath))
                                {
                                    ClearCurrentConsoleLine();
                                    Console.WriteLine(string.Format("{0}: Found Log File {1}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), filePath));
                                    ReadLog(filePath, group.Group);
                                }
                            }
                            catch
                            {

                            }
                        });
                    });
                }
                catch
                {
                }
                finally
                {
                    run = true;
                }
            }            
        }

        private string GetFilePath(string channel)
        {
            var dInfo = new DirectoryInfo(Program.Path);
            var todaysFiles = dInfo.EnumerateFiles().Where(w => w.CreationTimeUtc > DateTime.UtcNow.Date)
                .OrderBy(o => o.Name);
            var list = todaysFiles.Where(w => w.Name.StartsWith(channel))
                .OrderByDescending(o => o.LastWriteTimeUtc)
                .Take(5).ToList();

            var lookup = new Dictionary<string, DateTime>();
            list.ForEach(fileInfo => 
            {
                try
                {
                    lookup.Add(fileInfo.FullName, GetLastUpdateTimeFromFile(fileInfo.FullName));
                }
                catch
                {
                }
            });
            return lookup.OrderByDescending((KeyValuePair<string, DateTime> info) => info.Value).First().Key;
        }

        private DateTime GetLastUpdateTimeFromFile(string fileName)
        {
            var lines = new List<string>();
            using (var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var streamReader = new StreamReader(stream))
                {
                    while (!streamReader.EndOfStream)
                    {
                        lines.Add(streamReader.ReadLine());
                    }
                }
            }

            var empty = string.Empty;

            empty = lines.FirstOrDefault((string line) => line.Contains("Listener:")).Split(new string[1]
                    {
                    ":        "
                    }, StringSplitOptions.RemoveEmptyEntries)[1];
            lines = lines.Distinct().ToList();
            lines.Reverse();
            return new LogLine(lines.First()).LogDateTime;
        }

        private void ReadLog(string fileName, string groupName)
        {
            var logFileModel = LogFileHelper.ParseLogFile(fileName, groupName);
            var readFromTime = Program.ReadFromTimes.FirstOrDefault((KeyValuePair<string, DateTime> group) => group.Key == groupName).Value;
            logFileModel.LogLines = logFileModel.LogLines.Where(w => w.LogDateTime > readFromTime).ToList();

            if (logFileModel.LogLines.Count() > 0)
            {
                Program.ReadFromTimes.Remove(groupName);
                Program.ReadFromTimes.Add(groupName, logFileModel.LogLines.LastOrDefault().LogDateTime);
                ClearCurrentConsoleLine();
                OutputToConsole(logFileModel.LogLines);
                var loggingToon = Program.GetLoggingToon(logFileModel.Logger);
                var request = new LogDataModel()
                {
                    LoggerName = loggingToon.Name,
                    CorporationId = loggingToon.CorporationId,
                    AllianceId = loggingToon.AllianceId,
                    LogLines = logFileModel.LogLines,
                    Group = groupName
                };
                Poll(request);
                Console.WriteLine(string.Empty);
            }
        }

        private void OutputToConsole(List<Models.LogLine> messages)
        {
            messages.ForEach(message => 
            {
                Console.WriteLine(string.Format("{0}: {1} > {2}", 
                    message.LogDateTime.ToString("yyyy-MM-dd HH:mm:ss"), message.UserName, message.Message));
            });
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

        private void Poll(LogDataModel request)
        {
            while (Program.HubConnection.State != ConnectionState.Connected)
            {
                Program.StartSignalR();
            }

            Program.HubProxy.Invoke<LogDataModel>("reportIntel", request);
        }
    }
}
