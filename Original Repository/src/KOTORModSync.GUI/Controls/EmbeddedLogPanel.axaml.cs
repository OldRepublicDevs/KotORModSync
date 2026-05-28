// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
    public partial class EmbeddedLogPanel : UserControl
    {
        private readonly object _logLock = new object();
        private readonly int _maxLinesShown = 150;
        public readonly OutputViewModel ViewModel = new OutputViewModel();
        private bool _loggerAttached;
        private bool _isExpanded = true;

        public EmbeddedLogPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ApplyExpandedState();
            UpdateToggleButtonText();
            AttachedToVisualTree += (_, __) => EnsureLoggerAttached();
            DetachedFromVisualTree += (_, __) => DetachLogger();
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                ApplyExpandedState();
                UpdateToggleButtonText();
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void ApplyExpandedState()
        {
            if (LogBodyBorder != null)
            {
                LogBodyBorder.IsVisible = _isExpanded;
            }

            if (LogScrollViewer != null)
            {
                LogScrollViewer.IsVisible = _isExpanded;
            }
        }

        private void UpdateToggleButtonText()
        {
            if (ToggleExpandButton == null)
            {
                return;
            }

            ToggleExpandButton.Content = _isExpanded ? "▼ Hide" : "▲ Show log";
        }

        private void ToggleExpandButton_Click(object sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

        public void ExpandAndFocus()
        {
            IsExpanded = true;
            _ = Dispatcher.UIThread.InvokeAsync(() => LogScrollViewer?.ScrollToEnd());
        }

        private void EnsureLoggerAttached()
        {
            if (_loggerAttached)
            {
                return;
            }

            _loggerAttached = true;
            Logger.Logged += OnLoggerMessage;
            Logger.ExceptionLogged += OnExceptionLogged;

            try
            {
                List<string> recentLogs = Logger.GetRecentLogMessages(_maxLinesShown);
                foreach (string logMessage in recentLogs)
                {
                    AppendLogLine(logMessage);
                }
            }
            catch (Exception ex)
            {
                AppendLogLine($"[Warning] Could not load existing logs from memory: {ex.Message}");
            }

            UpdateLineCount();
            _ = Dispatcher.UIThread.InvokeAsync(() => LogScrollViewer?.ScrollToEnd());
        }

        private void DetachLogger()
        {
            if (!_loggerAttached)
            {
                return;
            }

            Logger.Logged -= OnLoggerMessage;
            Logger.ExceptionLogged -= OnExceptionLogged;
            _loggerAttached = false;
        }

        private void OnLoggerMessage(string message) => AppendLogLine(message);

        private void OnExceptionLogged(Exception ex)
        {
            string exceptionLog = $"Exception: {ex.GetType().Name}: {ex.Message}\nStack trace: {ex.StackTrace}";
            AppendLogLine(exceptionLog);
        }

        private void AppendLogLine(string message)
        {
            try
            {
                lock (_logLock)
                {
                    if (ViewModel._logBuilder.Count >= _maxLinesShown)
                    {
                        ViewModel.RemoveOldestLog();
                    }

                    ViewModel.AppendLog(message);
                    LogLine last = ViewModel.LogLines.LastOrDefault();
                    if (last != null)
                    {
                        last.IsHighlighted = string.Equals(last.Level, "Error", StringComparison.Ordinal)
                            || string.Equals(last.Level, "Warning", StringComparison.Ordinal);
                    }
                }

                _ = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateLineCount();
                    if (AutoScrollCheckBox?.IsChecked == true)
                    {
                        LogScrollViewer?.ScrollToEnd();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred appending the log: '{ex.Message}'");
            }
        }

        private void UpdateLineCount()
        {
            if (LineCountText != null)
            {
                int n = ViewModel.LogLines.Count;
                LineCountText.Text = n == 1 ? "1 line" : $"{n} lines";
            }
        }

        private async void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TopLevel.GetTopLevel(this)?.Clipboard is null || LogListBox is null)
                {
                    return;
                }

                var selected = LogListBox.SelectedItems?.OfType<LogLine>().ToList();
                if (selected?.Count == 0)
                {
                    return;
                }

                string text = string.Join(Environment.NewLine, selected.Select(l => $"[{l.Timestamp}] {l.Message}"));
                await TopLevel.GetTopLevel(this).Clipboard.SetTextAsync(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to copy logs: {ex.Message}");
            }
        }

        private void LogListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => LogListBox_KeyDown(sender, e), DispatcherPriority.Normal);
                return;
            }

            try
            {
                if (!(sender is ListBox list))
                {
                    return;
                }

                if (e.Key == Key.H)
                {
                    foreach (LogLine line in list.SelectedItems?.OfType<LogLine>() ?? Enumerable.Empty<LogLine>())
                    {
                        line.IsHighlighted = !line.IsHighlighted;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "An error occurred in LogListBox_KeyDown");
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                lock (_logLock)
                {
                    ViewModel.ClearAll();
                }

                _ = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (LogScrollViewer != null)
                    {
                        LogScrollViewer.Offset = new Vector(0, 0);
                    }

                    UpdateLineCount();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear logs: {ex.Message}");
            }
        }

        private async void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var top = TopLevel.GetTopLevel(this) as Window;
                if (top?.StorageProvider is null)
                {
                    return;
                }

                var saveOptions = new FilePickerSaveOptions
                {
                    SuggestedFileName = $"KOTORModSync_Log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log",
                    ShowOverwritePrompt = true,
                    FileTypeChoices = new[] { FilePickerFileTypes.All, FilePickerFileTypes.TextPlain },
                };

                IStorageFile file = await top.StorageProvider.SaveFilePickerAsync(saveOptions);
                string filePath = file?.TryGetLocalPath();
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                string text;
                lock (_logLock)
                {
                    text = ViewModel.LogText ?? string.Empty;
                }

                await Task.Run(() => File.WriteAllText(filePath, text));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save logs: {ex.Message}");
            }
        }
    }
}
