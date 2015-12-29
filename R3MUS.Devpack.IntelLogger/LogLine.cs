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
            LogDateTime = DateTime.ParseExact(
                split[0].Replace("[ ", "").Replace(".", "-"),
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture);
            split = split[1].Split(new string[] { " > " }, StringSplitOptions.RemoveEmptyEntries);
            UserName = split[0];
        }
    }
}
