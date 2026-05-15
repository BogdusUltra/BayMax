using System;
using System.Collections.Generic;
using System.Text;

namespace BayMax.Utils
{
    public enum LogLevel { Info, Success, Warning, Error, Debug }

    public class LogMessage
    {
        public DateTime Time { get; set; }
        public string Message { get; set; }
        public LogLevel Level { get; set; }
    }
    public class LoggerService
    {
        public static LoggerService Global { get; } = new LoggerService();
        public List<LogMessage> History { get; } = new List<LogMessage>();

        public event Action<LogMessage> OnLog;

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            var logMsg = new LogMessage
            {
                Time = DateTime.Now,
                Message = message,
                Level = level
            };

            History.Add(logMsg);
            OnLog?.Invoke(logMsg);

            System.Diagnostics.Debug.WriteLine($"[{level.ToString().ToUpper()}] {message}");
        }
    }
}
