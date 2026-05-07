using System;
using System.Collections.Generic;
using System.Text;

namespace BayMax.Services
{
    public enum LogLevel { Info, Success, Warning, Error, Debug }

    public static class LoggerService
    {
        public static event Action<string, LogLevel> OnLog;

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            OnLog?.Invoke(message, level);
            System.Diagnostics.Debug.WriteLine($"[{level.ToString().ToUpper()}] {message}");
        }
    }
}
