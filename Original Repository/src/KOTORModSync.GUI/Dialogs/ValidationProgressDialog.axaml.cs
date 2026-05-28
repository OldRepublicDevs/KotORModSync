// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KOTORModSync.Dialogs
{
    public partial class ValidationProgressDialog : Window
    {
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private readonly Queue<string> _logQueue = new Queue<string>();
        private readonly object _logLock = new object();
        private readonly bool _autoScroll = true;
        private DispatcherTimer _logUpdateTimer;
        private CancellationTokenSource _cancellationSource;

        public ValidationProgressDialog()
        {
            InitializeComponent();
            InitializeLogUpdateTimer();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeLogUpdateTimer()
        {
            _logUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            _logUpdateTimer.Tick += LogUpdateTimer_Tick;
            _logUpdateTimer.Start();
        }

        private void LogUpdateTimer_Tick(object sender, EventArgs e)
        {
            FlushLogQueue();
        }

        private void FlushLogQueue()
        {
            lock (_logLock)
            {
                if (_logQueue.Count == 0)
                {
                    return;
                }

                TextBlock logText = this.FindControl<TextBlock>("LogText");
                if (logText == null)
                {
                    return;
                }

                while (_logQueue.Count > 0)
                {
                    string message = _logQueue.Dequeue();
                    _logBuilder.AppendLine(message);
                }

                logText.Text = _logBuilder.ToString();

                if (_autoScroll)
                {
                    ScrollViewer scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
                    scrollViewer?.ScrollToEnd();
                }
            }
        }

        public void AppendLog(string message)
        {
            lock (_logLock)
            {
                _logQueue.Enqueue(message);
            }
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.UIThread.Post(() =>
            {
                TextBlock statusText = this.FindControl<TextBlock>("StatusText");
                if (statusText != null)
                {
                    statusText.Text = status;
                }
            }, DispatcherPriority.Normal);
        }

        public void UpdateProgress(int current, int total)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressBar progressBar = this.FindControl<ProgressBar>("ProgressBar");
                TextBlock progressText = this.FindControl<TextBlock>("ProgressText");

                if (progressBar != null)
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Maximum = total;
                    progressBar.Value = current;
                }

                if (progressText != null)
                {
                    double percentage = total > 0 ? (current / (double)total) * 100.0 : 0;
                    progressText.Text = $"Progress: {percentage:F0}%";
                }
            }, DispatcherPriority.Normal);
        }

        public void SetIndeterminate(bool isIndeterminate)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressBar progressBar = this.FindControl<ProgressBar>("ProgressBar");
                if (progressBar != null)
                {
                    progressBar.IsIndeterminate = isIndeterminate;
                }
            }, DispatcherPriority.Normal);
        }

        public void Complete(bool success, string finalMessage)
        {
            Dispatcher.UIThread.Post(() =>
            {
                FlushLogQueue();

                TextBlock titleText = this.FindControl<TextBlock>("TitleText");
                TextBlock statusText = this.FindControl<TextBlock>("StatusText");
                ProgressBar progressBar = this.FindControl<ProgressBar>("ProgressBar");
                Button closeButton = this.FindControl<Button>("CloseButton");

                if (titleText != null)
                {
                    titleText.Text = success ? "✅ Validation Complete" : "⚠️ Validation Complete";
                }

                if (statusText != null)
                {
                    statusText.Text = finalMessage ?? (success ? "Validation completed successfully!" : "Validation completed with issues.");
                }

                if (progressBar != null)
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Value = progressBar.Maximum;
                }

                if (closeButton != null)
                {
                    closeButton.IsEnabled = true;
                    closeButton.IsVisible = true;
                }

                AppendLog(string.Empty);
                AppendLog(success ? "✅ Validation completed successfully!" : "⚠️ Validation completed with issues.");
            }, DispatcherPriority.Normal);
        }

        private void CloseButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _cancellationSource?.Cancel();
            _logUpdateTimer?.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationSource?.Cancel();
            _logUpdateTimer?.Stop();
            base.OnClosed(e);
        }

        internal void AttachCancellationSource(CancellationTokenSource cancellationSource)
        {
            _cancellationSource = cancellationSource;
        }

        public static async Task<ValidationProgressDialog> ShowValidationProgress(Window parent, CancellationTokenSource cancellationSource = null)
        {
            ValidationProgressDialog dialog = null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dialog = new ValidationProgressDialog();
                dialog.AttachCancellationSource(cancellationSource);
                dialog.Show(parent);
            }, DispatcherPriority.Normal);

            return dialog;
        }
    }
}

