using R3MUS.Devpack.IntelLogger.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace R3MUS.Devpack.IntelLogger.Helpers
{
    public class LogFileHelper
    {
        // Methods
        public static DateTime GetFileCreationTime(List<string> logFileLines)
        {
            string[] separator = new string[] { ": " };
            return Convert.ToDateTime(Enumerable.FirstOrDefault<string>((IEnumerable<string>)(from line in logFileLines
                                                                                              where line.Contains("Session started:")
                                                                                              select line)).Split(separator, StringSplitOptions.RemoveEmptyEntries)[1]);
        }

        public static string GetLoggerName(List<string> logFileLines)
        {
            string[] separator = new string[] { ":        " };
            return Enumerable.FirstOrDefault<string>((IEnumerable<string>)(from line in logFileLines
                                                                           where line.Contains("Listener:")
                                                                           select line)).Split(separator, StringSplitOptions.RemoveEmptyEntries)[1];
        }

        public static LogFileModel ParseLogFile(string fileName, string groupName)
        {
            List<string> logFileLines = ReadLogFile(fileName);
            LogFileModel model1 = new LogFileModel();
            model1.LogLines = new List<LogLine>();
            LogFileModel result = model1;
            result.Logger = GetLoggerName(logFileLines);
            result.CreatedAt = GetFileCreationTime(logFileLines);
            logFileLines.ForEach(delegate (string line)
            {
                if (!line.Contains("MOTD"))
                {
                    try
                    {
                        LogLine item = new LogLine(line);
                        item.Group = groupName;
                        result.LogLines.Add(item);
                    }
                    catch (Exception)
                    {
                    }
                }
            });
            return result;
        }
        public static List<string> ReadLogFile(string fileName)
        {
            List<string> list = new List<string>();
            using (FileStream stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (true)
                    {
                        if (reader.EndOfStream)
                        {
                            break;
                        }
                        list.Add(reader.ReadLine());
                    }
                }
            }
            return list;
        }
    }
}