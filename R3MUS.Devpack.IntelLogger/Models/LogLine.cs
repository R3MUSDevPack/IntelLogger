using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace R3MUS.Devpack.IntelLogger.Models
{
    public class LogLine
    {
        public DateTime LogDateTime { get; set; }
        public string UserName { get; set; }
        public string Message { get; set; }
        public string Group { get; set; }

        public LogLine(string line)
        {
            var split = line.Split(new string [] { " ] " }, StringSplitOptions.RemoveEmptyEntries);
            var dateTimeSplit = split[0].Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            var dateSplit = dateTimeSplit[1].Split('.');
            var timeSplit = dateTimeSplit[2].Split(':');

            LogDateTime = new DateTime(
                Convert.ToInt32(dateSplit[0]), 
                Convert.ToInt32(dateSplit[1]), 
                Convert.ToInt32(dateSplit[2]),
                Convert.ToInt32(timeSplit[0]),
                Convert.ToInt32(timeSplit[1]),
                Convert.ToInt32(timeSplit[2])
                );
            split = split[1].Split(new string[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
            UserName = split[0];
            Message = split[1];
        }
    }
}
