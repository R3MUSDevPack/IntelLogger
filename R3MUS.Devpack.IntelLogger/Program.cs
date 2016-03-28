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

namespace R3MUS.Devpack.IntelLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            //var worker = new Worker();

            if(ApplicationDeployment.IsNetworkDeployed)
            {
                Console.Title = string.Format("R3MUS Intel Logger - v{0}", ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString());
            }
            else
            {
                Console.Title = string.Format("R3MUS Intel Logger - v{0}", Assembly.GetCallingAssembly().GetName().Version.ToString());
            }

            Properties.Settings.Default.LastWriteTime = DateTime.Now.ToString();

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

            Console.ReadLine();
        }
        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastWriteTime = string.Empty;
        }
    }
}
