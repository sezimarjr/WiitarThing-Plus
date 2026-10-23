using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace WiitarThing
{
    class WiitarDebug
    {
        public enum LogLevel
        {
            TooMuch = 0,
            Warning = 1,
            Error = 2,
        }

        public static void Log(string message, LogLevel level = LogLevel.TooMuch)
        {
#if !DEBUG
            if (level < LogLevel.Warning)
                return;
#endif
            var utcDate = DateTime.Now.ToUniversalTime();
            string date = $"{utcDate.Hour:D2}:{utcDate.Minute:D2}:{utcDate.Second:D2}.{utcDate.Millisecond:D3}";

            // Called from input threads and error handlers; failing to log must never become a new error
            try
            {
                string logFilePath = Frankenpath(new FileInfo(Application.ResourceAssembly.Location).DirectoryName, "WiitarLog.log");

                lock (logLock)
                using (var logFile = File.Open(logFilePath, FileMode.Append, FileAccess.Write))
                {
                    using (var logFileWriter = new StreamWriter(logFile))
                    {
                        logFileWriter.WriteLine($"[{date}] {message}");
                    }
                }
            }
            catch (Exception) { }
        }

        private static readonly object logLock = new object();

        public static string Frankenpath(params string[] pathParts)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < pathParts.Length; i++)
            {
                sb.Append(pathParts[i].Trim('\\'));
                if (i < pathParts.Length - 1)
                    sb.Append('\\');
            }

            return sb.ToString();
        }

    }
}
