// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using KOTORModSync.Core;

namespace KOTORModSync.Core.CLI
{
    public class ConsoleProgressDisplay : IDisposable
    {
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<string, ProgressItem> _activeItems = new ConcurrentDictionary<string, ProgressItem>(StringComparer.Ordinal);
        private readonly Dictionary<string, FailedItem> _failedItems = new Dictionary<string, FailedItem>(StringComparer.Ordinal);
        private readonly Queue<string> _scrollingLogs = new Queue<string>();
        private readonly Timer _refreshTimer;
        private bool _disposed;
        private readonly bool _isEnabled;
        private readonly bool _usePlainText;
        private bool _needsRender;
        private string _lastRenderedContent = string.Empty;
        private int _consoleWidth;
        private readonly int _maxActiveItems = 5;
        private readonly int _maxFailedItems = 10;
        private readonly int _maxScrollingLogs = 100;
        private const string CLEAR_LINE = "\x1b[2K";
        private const string HIDE_CURSOR = "\x1b[?25l";
        private const string SHOW_CURSOR = "\x1b[?25h";

        public ConsoleProgressDisplay(bool usePlainText = false)
        {
            _usePlainText = usePlainText;

            try
            {
                _consoleWidth = Console.WindowWidth;
                _isEnabled = !Console.IsOutputRedirected && Environment.UserInteractive;
            }
            catch
            {
                _isEnabled = false;
            }

            if (_usePlainText)
            {
                _isEnabled = false;
            }

            if (_isEnabled)
            {
                Console.Write(HIDE_CURSOR);
                _refreshTimer = new Timer(_ => Render(), state: null, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16));
            }
        }

        private sealed class ProgressItem
        {
            public string Key { get; set; }
            public string DisplayText { get; set; }
            public double Progress { get; set; }
            public DateTime LastUpdate { get; set; }
            public string Status { get; set; }
        }

        private sealed class FailedItem
        {
            public string Url { get; set; }
            public string Error { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public void UpdateProgress(string key, string displayText, double progress, string status = "processing")
        {
            if (_usePlainText)
            {
                Console.WriteLine($"[{status.ToUpper(System.Globalization.CultureInfo.CurrentCulture)}] {displayText} - {progress:F1}%");
                return;
            }

            if (!_isEnabled)
            {
                return;
            }

            bool isNewItem = !_activeItems.ContainsKey(key);
            bool shouldRender = isNewItem;

            _activeItems.AddOrUpdate(key,
                new ProgressItem
                {
                    Key = key,
                    DisplayText = displayText,
                    Progress = progress,
                    LastUpdate = DateTime.Now,
                    Status = status,
                },
            (k, existing) =>
            {
                if (Math.Abs(existing.Progress - progress) >= 0.5 || !string.Equals(existing.Status, status, StringComparison.Ordinal))
                {
                    shouldRender = true;
                }
                existing.DisplayText = displayText;
                existing.Progress = progress;
                existing.LastUpdate = DateTime.Now;
                existing.Status = status;
                return existing;
            });

            if (shouldRender)
            {
                _needsRender = true;
            }
        }

        public void RemoveProgress(string key)
        {
            if (_usePlainText)
            {
                if (_activeItems.TryGetValue(key, out ProgressItem item))
                {
                    Console.WriteLine($"[COMPLETED] {item.DisplayText}");
                }
                return;
            }

            if (!_isEnabled)
            {
                return;
            }

            _activeItems.TryRemove(key, out _);
            _needsRender = true;
        }

        public void AddFailedItem(string url, string error)
        {
            if (_usePlainText)
            {
                Console.WriteLine($"[FAILED] {url}");
                Console.WriteLine($"  Error: {error}");
                return;
            }

            if (!_isEnabled)
            {
                return;
            }

            lock (_lock)
            {
                _failedItems[url] = new FailedItem
                {
                    Url = url,
                    Error = error,
                    Timestamp = DateTime.Now,
                };

                if (_failedItems.Count > _maxFailedItems * 2)
                {
                    var oldestKeys = _failedItems
                        .OrderBy(kvp => kvp.Value.Timestamp)
                        .Take(_failedItems.Count - _maxFailedItems)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (string key in oldestKeys)
                    {
                        _failedItems.Remove(key);
                    }
                }

                _needsRender = true;
            }
        }

        public void AddLog(string message)
        {
            if (_usePlainText)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                return;
            }

            if (!_isEnabled)
            {
                Console.WriteLine(message);
                return;
            }

            lock (_lock)
            {
                _scrollingLogs.Enqueue(message);

                while (_scrollingLogs.Count > _maxScrollingLogs)
                {
                    _scrollingLogs.Dequeue();
                }

                _needsRender = true;
            }
        }

        private void Render()
        {
            if (!_isEnabled || _disposed)
            {
                return;
            }

            if (!_needsRender)
            {
                return;
            }

            lock (_lock)
            {
                try
                {
                    _consoleWidth = Console.WindowWidth;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Failed to get console width");
                }

                var sb = new StringBuilder();

                int statusLines = CalculateStatusLines();

                try
                {
                    int consoleHeight = Console.WindowHeight;
                    int statusStartLine = Math.Max(1, consoleHeight - statusLines + 1);

                    _ = sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\x1b[{0};1H", statusStartLine);
                    _ = sb.Append("\x1b[0J");

                    var failedItems = _failedItems.Values
                        .OrderByDescending(f => f.Timestamp)
                        .Take(_maxFailedItems)
                        .ToList();

                    if (failedItems.Count > 0)
                    {
                        _ = sb.AppendLine("╔═══ FAILED DOWNLOADS ═══");
                        foreach (FailedItem failed in failedItems)
                        {
                            string truncatedUrl = TruncateString(failed.Url, _consoleWidth - 35);
                            string shortError = failed.Error.Length > 25 ? failed.Error.Substring(0, 22) + "..." : failed.Error;
                            _ = sb.Append("║ ✗ ").Append(truncatedUrl).Append(" (").Append(shortError).Append(')').AppendLine();
                        }
                        _ = sb.Append("╚").AppendLine(new string('═', Math.Min(_consoleWidth - 2, 50)));
                    }

                    var activeItems = _activeItems.Values
                        .OrderBy(x => x.LastUpdate)
                        .Take(_maxActiveItems)
                        .ToList();

                    if (activeItems.Count > 0)
                    {
                        _ = sb.AppendLine("╔═══ ACTIVE DOWNLOADS ═══");
                        foreach (ProgressItem item in activeItems)
                        {
                            string progressBar = RenderProgressBar(item.Progress, 30);
                            string statusIcon = GetStatusIcon(item.Status);
                            string displayText = TruncateString(item.DisplayText, _consoleWidth - 45);
                            _ = sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2} {3:F1}%", string.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "{0} {1} {2} {3:F1}%",
                                    statusIcon, displayText, progressBar, item.Progress
                                )
).AppendLine();
                        }
                        _ = sb.Append('╚').AppendLine(new string('═', Math.Min(_consoleWidth - 2, 50)));
                    }

                    string newContent = sb.ToString();

                    if (!string.Equals(newContent, _lastRenderedContent, StringComparison.Ordinal))
                    {
                        Console.Write(newContent);
                        _lastRenderedContent = newContent;
                    }

                    _needsRender = false;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Failed to render console progress display");
                }
            }
        }

        private int CalculateStatusLines()
        {
            int lines = 2;

            if (_failedItems.Count > 0)
            {
                lines += 2;
                lines += Math.Min(_failedItems.Count, _maxFailedItems);
            }

            if (!_activeItems.IsEmpty)
            {
                lines += 2;
                lines += Math.Min(_activeItems.Count, _maxActiveItems);
            }

            return Math.Min(lines, 20);
        }

        private static string RenderProgressBar(double progress, int width)
        {
            int filled = (int)((progress / 100.0) * width);
            int empty = width - filled;

            return $"[{new string('█', filled)}{new string('░', empty)}]";
        }

        private static string GetStatusIcon(string status)
        {
            switch (status.ToLowerInvariant())
            {
                case "downloading":
                    return "⬇";
                case "processing":
                    return "⚙";
                case "extracting":
                    return "📦";
                case "resolving":
                    return "🔍";
                case "completed":
                    return "✓";
                case "failed":
                    return "✗";
                default:
                    return "●";
            }
        }

        private static string TruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength - 3) + "...";
        }

        public void WriteScrollingLog(string message)
        {
            if (_usePlainText || !_isEnabled)
            {
                Console.WriteLine(message);
                return;
            }

            lock (_lock)
            {
                try
                {
                    int statusLines = CalculateStatusLines();
                    int consoleHeight = Console.WindowHeight;
                    int statusStartLine = Math.Max(1, consoleHeight - statusLines);

                    Console.Write($"\x1b[{statusStartLine};1H");
                    Console.Write("\x1b[0J");
                    Console.Write($"\x1b[{statusStartLine - 1};1H");
                    Console.WriteLine(message);

                    _needsRender = true;
                    Render();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Failed to write scrolling log");
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _refreshTimer?.Dispose();

                if (_isEnabled)
                {
                    lock (_lock)
                    {
                        try
                        {
                            int statusLines = CalculateStatusLines();
                            int consoleHeight = Console.WindowHeight;
                            int statusStartLine = Math.Max(1, consoleHeight - statusLines);

                            Console.Write($"\x1b[{statusStartLine};1H");
                            for (int i = 0; i < statusLines; i++)
                            {
                                Console.WriteLine(CLEAR_LINE);
                            }

                            Console.Write(SHOW_CURSOR);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex, "Failed to dispose console progress display");
                        }
                    }
                }
            }

            _disposed = true;
        }
    }
}
