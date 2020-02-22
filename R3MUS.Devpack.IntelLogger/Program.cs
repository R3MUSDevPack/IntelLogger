using Microsoft.AspNet.SignalR.Client;
using Quartz;
using Quartz.Impl;
using R3MUS.Devpack.ESI.Models.Character;
using R3MUS.Devpack.IntelLogger;
using R3MUS.Devpack.IntelLogger.Helpers;
using R3MUS.Devpack.IntelLogger.Models;
using R3MUS.Devpack.IntelLogger.Properties;
using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace R3MUS.Devpack.IntelLogger
{
    class Program
	{
		public static string Path = string.Concat(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"\EVE\logs\Chatlogs\");

        public static HubConnection HubConnection { get; set; }
        public static IHubProxy HubProxy { get; set; }
        public static Dictionary<string, DateTime> ReadFromTimes { get; set; }
        public static List<R3MUS.Devpack.SSO.IntelMap.Models.GroupChannelName> LogFileNames { get; set; }
        public static Dictionary<string, Detail> LoggingCharacters { get; set; }

        [STAThread]
		static void Main(string[] args)
        {
            if(ApplicationDeployment.IsNetworkDeployed)
            {
                Console.Title = string.Format("R3MUS Intel Logger - v{0}", ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString());
            }
            else
            {
                Console.Title = string.Format("R3MUS Intel Logger - v{0}", Assembly.GetCallingAssembly().GetName().Version.ToString());
            }

			if (!Directory.Exists(Path) && Directory.Exists(Properties.Settings.Default.LogFolder))
			{
                Path = Properties.Settings.Default.LogFolder;
			}
			while(!Directory.Exists(Path))
			{
                Path = GetLogFolder();
                Settings.Default.LogFolder = Path;
                Settings.Default.Save();
			}
            Console.WriteLine(string.Concat("Log file folder path set to ", Path));
            Console.WriteLine();

            LoggingCharacters = new Dictionary<string, Detail>();
            ReadFromTimes = new Dictionary<string, DateTime>();
            LogFileNames = new List<SSO.IntelMap.Models.GroupChannelName>();

            Settings.Default.LastWriteTime = DateTime.Now.ToString();
            StartSignalR();
            SetupCronJob();

            Console.ReadLine();
        }
        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            HubProxy.Invoke("leaveGroups", new object[1]
            {
                LogFileNames.Select(s => s.Group)
            });
            HubConnection.Stop();
            HubConnection.Dispose();
            Settings.Default.LastWriteTime = string.Empty;
        }

        static void SetupCronJob()
        {
            var sched = new StdSchedulerFactory().GetScheduler();
            sched.Start();

            var jobDetail = JobBuilder.Create(Type.GetType("R3MUS.Devpack.IntelLogger.Worker"))
                .WithIdentity(string.Format("{0}Instance", "IntelLogCheck"), string.Format("{0}Group", "IntelLogCheck"))
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity(string.Format("{0}Trigger", "IntelLogCheck"), string.Format("{0}TriggerGroup", "IntelLogCheck"))
                .StartNow()
                .WithSimpleSchedule(x => x.WithIntervalInSeconds(10).RepeatForever())
                .Build();

            Properties.Settings.Default.LastWriteTime = DateTime.Now.ToUniversalTime().ToString();

            sched.ScheduleJob(jobDetail, trigger);
        }

        static string GetLogFolder()
        {
            var dialog = new FolderBrowserDialog();
            dialog.Description = "Your log folder appears to be in a non-standard location. Please provide the folder location to proceed.";
            if(dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.SelectedPath;
            }
            else
            {
                Console.WriteLine("Unable to continue at this time...");
                Console.ReadLine();
                Environment.Exit(0);
            }
            return string.Empty;
        }

        public static Detail GetLoggingToon(string name)
        {
            if (!LoggingCharacters.ContainsKey(name))
            {
                var detail = new Detail();
                detail.LoadCharacterByName(name);
                LoggingCharacters.Add(name, detail);
            }
            return LoggingCharacters[name];
        }

        public static void StartSignalR()
        {
            try
            {
                HubConnection = new HubConnection(Settings.Default.IntelHubURL);
                HubProxy = HubConnection.CreateHubProxy("IntelHub");
                HubProxyExtensions.On<IEnumerable<SSO.IntelMap.Models.GroupChannelName>>(HubProxy, "sendLogFileNames", data => 
                    GetLogFileNames(data.ToList()));
                HubConnection.Start().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static void GetLogFileNames(List<SSO.IntelMap.Models.GroupChannelName> data)
        {
            var catcher = new List<SSO.IntelMap.Models.GroupChannelName>();
            var dInfo = new DirectoryInfo(Path);
            data.ForEach(group =>
            {
                group.Channels.ForEach(channel =>
                {
                    var fileInfo = dInfo.EnumerateFiles(string.Concat(channel, "*"))
                        .OrderByDescending(o => o.LastWriteTimeUtc).FirstOrDefault();

                    if (fileInfo != null)
                    {
                        var flag1 = false;

                        Console.WriteLine("Listening to channel " + channel);
                        catcher.Add(group);
                        LogFileModel model = LogFileHelper.ParseLogFile(fileInfo.FullName, group.Group);
                        DateTime createdAt = model.CreatedAt;

                        if (model.LogLines.Count > 0)
                        {
                            createdAt = Enumerable.Last<LogLine>(model.LogLines).LogDateTime;
                        }
                        if (!ReadFromTimes.ContainsKey(group.Group))
                        {
                            flag1 = false;
                        }
                        else
                        {
                            flag1 = ReadFromTimes.FirstOrDefault().Value < createdAt;
                        }
                        if (flag1)
                        {
                            ReadFromTimes.Remove(group.Group);
                        }
                        if (!ReadFromTimes.ContainsKey(group.Group))
                        {
                            ReadFromTimes.Add(group.Group, createdAt);
                        }
                    }
                });
            });

            catcher = catcher.Distinct().ToList();
            catcher.ForEach(group => {
                HubProxy.Invoke("joinGroup", group.Group);
            });
            Console.WriteLine();
            LogFileNames = catcher;
        }
    }
}
