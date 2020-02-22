using System.Collections.Generic;

namespace R3MUS.Devpack.IntelLogger.Models
{
    public class LogDataModel
    {
        public string LoggerName { get; set; }

        public long CorporationId { get; set; }

        public long? AllianceId { get; set; }

        public List<LogLine> LogLines { get; set; }

        public string Group { get; set; }
    }
}
