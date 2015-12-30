using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace R3MUS.Devpack.IntelLogger
{
    public class LogLine
    {
        public DateTime LogDateTime { get; set; }
        public string UserName { get; set; }
        public string Message { get; set; }

        public LogLine(string line)
        {
            var split = line.Split(new string [] { " ] " }, StringSplitOptions.RemoveEmptyEntries);
            LogDateTime = Convert.ToDateTime(split[0].TrimStart('['));
            split = split[1].Split(new string[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
            UserName = split[0];
            Message = split[1];
        }
    }
}
