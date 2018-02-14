using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Deployment;
using System.Deployment.Application;
using Microsoft.Win32;
using System.Diagnostics;
using R3MUS.Devpack.Slack;
using System.Windows.Forms;
using Microsoft.AspNet.SignalR.Client;

namespace R3MUS.Devpack.IntelLogger
{
    class Program
	{
		private static string path = string.Concat(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"\EVE\logs\Chatlogs\");

        public static HubConnection HubConnection { get; set; }
        public static IHubProxy HubProxy { get; set; }

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

			if (!Directory.Exists(path) && Directory.Exists(Properties.Settings.Default.LogFolder))
			{
				path = Properties.Settings.Default.LogFolder;
			}
			while(!Directory.Exists(path))
			{
                path = GetLogFolder();
                Properties.Settings.Default.LogFolder = path;
                Properties.Settings.Default.Save();
			}

			Properties.Settings.Default.LastWriteTime = DateTime.Now.ToString();
            StartSignalR();
            SetupCronJob();

            Console.ReadLine();
        }
        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            HubConnection.Stop();
            HubConnection.Dispose();
            Properties.Settings.Default.LastWriteTime = string.Empty;
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

        static void StartSignalR()
        {
            HubConnection = new HubConnection(Properties.Settings.Default.IntelHubURL);
            HubProxy = HubConnection.CreateHubProxy("IntelHub");
            HubConnection.Start().Wait();
            try
            {
                //  Not implemented anywhere serverside right now
                HubProxy.Invoke("joinGroup", Properties.Settings.Default.BroadcastGroup);
            }
            catch { }
        }
    }
}
