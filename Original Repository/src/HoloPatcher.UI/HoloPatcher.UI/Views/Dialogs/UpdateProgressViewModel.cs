using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace HoloPatcher.UI.Views.Dialogs
{
    public sealed class UpdateProgressViewModel : INotifyPropertyChanged
    {
        private string _statusText = "Preparing update...";
        private string _bytesText = string.Empty;
        private string _etaText = string.Empty;
        private double _progressMaximum = 100;
        private double _progressValue;
        private bool _isIndeterminate = true;

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string BytesText
        {
            get => _bytesText;
            private set => SetProperty(ref _bytesText, value);
        }

        public string EtaText
        {
            get => _etaText;
            private set => SetProperty(ref _etaText, value);
        }

        public double ProgressMaximum
        {
            get => _progressMaximum;
            private set => SetProperty(ref _progressMaximum, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            private set => SetProperty(ref _progressValue, value);
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set => SetProperty(ref _isIndeterminate, value);
        }

        public void Reset()
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusText = "Preparing update...";
                BytesText = string.Empty;
                EtaText = string.Empty;
                ProgressMaximum = 100;
                ProgressValue = 0;
                IsIndeterminate = true;
            });
        }

        public void ReportStatus(string status)
        {
            Dispatcher.UIThread.Post(() => StatusText = status);
        }

        public void ReportDownload(long downloadedBytes, long? totalBytes, TimeSpan? eta = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    ProgressMaximum = totalBytes.Value;
                    ProgressValue = Math.Min(downloadedBytes, totalBytes.Value);
                    IsIndeterminate = false;
                    BytesText = $"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes.Value)}";
                }
                else
                {
                    IsIndeterminate = true;
                    BytesText = $"{FormatBytes(downloadedBytes)} downloaded";
                }

                EtaText = eta.HasValue && eta.Value > TimeSpan.Zero
                    ? $"Estimated time remaining: {eta.Value:mm\\:ss}"
                    : string.Empty;
            });
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return $"{value:F1} {units[unitIndex]}";
        }
    }
}

