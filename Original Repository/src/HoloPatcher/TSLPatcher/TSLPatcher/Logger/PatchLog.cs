using System;

namespace KOTORModSync.Logger
{

    /// <summary>
    /// Represents a single log entry in the patching process
    /// </summary>
    public class PatchLog
    {
        public string Message { get; }
        public LogType LogType { get; }
        public DateTime Timestamp { get; }

        public PatchLog(string message, LogType logType)
        {
            Message = message;
            LogType = logType;
            Timestamp = DateTime.Now;
        }

        public string FormattedMessage => $"[{LogType}] [{Timestamp:HH:mm:ss}] {Message}";

        public override string ToString() => FormattedMessage;
    }
}

