using Quartz;
using Quartz.Impl;
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
            //var worker = new Worker();

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

            sched.ScheduleJob(jobDetail, trigger);

            Console.ReadLine();
        }
        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastWriteTime = string.Empty;
        }
    }
}
