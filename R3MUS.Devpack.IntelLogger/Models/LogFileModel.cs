using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace R3MUS.Devpack.IntelLogger.Models
{
    public class LogFileModel
    {
        public string Logger { get; set; }

        public string GroupName { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<LogLine> LogLines { get; set; }
    }
}
