// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace KOTORModSync
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "Shared log UI model")]
    public sealed class OutputViewModel : INotifyPropertyChanged
    {
        public readonly Queue<string> _logBuilder = new Queue<string>();
        public string LogText { get; set; } = string.Empty;
        public ObservableCollection<LogLine> LogLines { get; } = new ObservableCollection<LogLine>();

        public event PropertyChangedEventHandler PropertyChanged;

        public void AppendLog(string message)
        {
            _logBuilder.Enqueue(message);
            LogLines.Add(LogLine.FromMessage(message));
            OnPropertyChanged(nameof(LogText));
        }

        public void RemoveOldestLog()
        {
            _ = _logBuilder.Dequeue();
            if (LogLines.Count > 0)
            {
                LogLines.RemoveAt(0);
            }

            OnPropertyChanged(nameof(LogText));
        }

        public void ClearAll()
        {
            _logBuilder.Clear();
            LogLines.Clear();
            OnPropertyChanged(nameof(LogText));
        }

        private void OnPropertyChanged(string propertyName)
        {
            LogText = string.Join(Environment.NewLine, _logBuilder);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "Shared log UI model")]
    public sealed class LogLine
    {
        public string Timestamp { get; set; }
        public string Message { get; set; }
        public string Level { get; set; }
        public string LevelColor { get; set; }
        public bool IsHighlighted { get; set; }

        public static LogLine FromMessage(string raw)
        {
            string level = "INFO";
            string color = "#00AA00";
            if (raw?.IndexOf("[Error]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                level = "Error";
                color = "#FF4444";
            }
            else if (raw?.IndexOf("[Warning]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                level = "Warning";
                color = "#FFAA00";
            }

            return new LogLine
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                Message = raw ?? string.Empty,
                Level = level,
                LevelColor = color,
            };
        }
    }
}
