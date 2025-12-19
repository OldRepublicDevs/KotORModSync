// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Dialogs
{
    public partial class ModDownloadDetailsDialog : Window
    {
        private readonly DownloadProgress _downloadProgress;
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;
        private readonly Action<DownloadProgress> _retryAction;

        public ModDownloadDetailsDialog() => InitializeComponent();

        public ModDownloadDetailsDialog(DownloadProgress progress, Action<DownloadProgress> retryAction = null) : this()
        {
            _downloadProgress = progress;
            _retryAction = retryAction;
            LoadDetails();
            WireUpEvents();

            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;

            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        private void LoadDetails()
        {
            if (_downloadProgress is null)
            {
                return;
            }

            TextBlock statusIconText = this.FindControl<TextBlock>("StatusIconText");
            if (statusIconText != null)
            {
                statusIconText.Text = _downloadProgress.StatusIcon;
            }

            TextBlock modNameText = this.FindControl<TextBlock>("ModNameText");
            if (modNameText != null)
            {
                modNameText.Text = _downloadProgress.ModName;
            }

            TextBlock statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = $"Status: {_downloadProgress.Status}";
            }


            TextBlock urlText = this.FindControl<TextBlock>("UrlText");
            if (urlText != null)
            {
                urlText.Text = _downloadProgress.Url;
            }

            TextBlock filePathText = this.FindControl<TextBlock>("FilePathText");
            if (filePathText != null)
            {
                filePathText.Text = string.IsNullOrEmpty(_downloadProgress.FilePath) ? "N/A" : _downloadProgress.FilePath;
            }

            TextBlock fileSizeText = this.FindControl<TextBlock>("FileSizeText");
            if (fileSizeText != null)
            {
                fileSizeText.Text = _downloadProgress.TotalBytes > 0
                    ? $"{_downloadProgress.DownloadedSize} / {_downloadProgress.TotalSize} ({_downloadProgress.ProgressPercentage:F1}%)"
                    : "Unknown";
            }

            TextBlock downloadSpeedText = this.FindControl<TextBlock>("DownloadSpeedText");
            if (downloadSpeedText != null)
            {
                downloadSpeedText.Text = _downloadProgress.DownloadSpeed;
            }

            TextBlock durationText = this.FindControl<TextBlock>("DurationText");
            if (durationText != null)
            {
                durationText.Text = FormatDuration(_downloadProgress.Duration);
            }


            Border statusMessageSection = this.FindControl<Border>("StatusMessageSection");
            TextBlock statusMessageText = this.FindControl<TextBlock>("StatusMessageText");
            if (statusMessageSection != null && statusMessageText != null)
            {
                if (!string.IsNullOrWhiteSpace(_downloadProgress.StatusMessage))
                {
                    statusMessageText.Text = _downloadProgress.StatusMessage;
                    statusMessageSection.IsVisible = true;
                }
                else
                {
                    statusMessageSection.IsVisible = false;
                }
            }


            Border errorSection = this.FindControl<Border>("ErrorSection");
            TextBlock errorMessageText = this.FindControl<TextBlock>("ErrorMessageText");
            TextBlock exceptionHeader = this.FindControl<TextBlock>("ExceptionHeader");
            TextBox exceptionDetailsText = this.FindControl<TextBox>("ExceptionDetailsText");
            Button copyErrorButton = this.FindControl<Button>("CopyErrorButton");

            if (errorSection != null && errorMessageText != null)
            {
                if (_downloadProgress.IsFailed && !string.IsNullOrWhiteSpace(_downloadProgress.ErrorMessage))
                {
                    errorMessageText.Text = _downloadProgress.ErrorMessage;
                    errorSection.IsVisible = true;

                    if (_downloadProgress.Exception != null)
                    {
                        if (exceptionHeader != null)
                        {
                            exceptionHeader.IsVisible = true;
                        }

                        if (exceptionDetailsText != null)
                        {
                            exceptionDetailsText.Text = _downloadProgress.Exception.ToString();
                            exceptionDetailsText.IsVisible = true;
                        }
                    }

                    if (copyErrorButton != null)
                    {
                        copyErrorButton.IsVisible = true;
                    }
                }
                else
                {
                    errorSection.IsVisible = false;
                }
            }


            TextBlock startTimeText = this.FindControl<TextBlock>("StartTimeText");
            if (startTimeText != null)
            {
                startTimeText.Text = _downloadProgress.StartTime != default
                    ? _downloadProgress.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
                    : "N/A";
            }

            TextBlock endTimeText = this.FindControl<TextBlock>("EndTimeText");
            if (endTimeText != null)
            {
                endTimeText.Text = _downloadProgress.EndTime.HasValue
                    ? _downloadProgress.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
                    : "N/A";
            }


            TextBox logsTextBox = this.FindControl<TextBox>("LogsTextBox");
            if (logsTextBox != null)
            {
                logsTextBox.Text = string.Join(Environment.NewLine, CollectDetailedLogs());
            }

            PopulateFilesSection();

            Button openFolderButton = this.FindControl<Button>("OpenFolderButton");
            if (openFolderButton != null)
            {
                openFolderButton.IsEnabled = !string.IsNullOrEmpty(_downloadProgress.FilePath) && File.Exists(_downloadProgress.FilePath);
            }

            Button retryButton = this.FindControl<Button>("RetryButton");
            if (retryButton != null)
            {
                retryButton.IsVisible = _downloadProgress.IsFailed;
            }
        }

        private void WireUpEvents()
        {
            Button openFolderButton = this.FindControl<Button>("OpenFolderButton");
            if (openFolderButton != null)
            {
                openFolderButton.Click += OpenFolderButton_Click;
            }

            Button copyErrorButton = this.FindControl<Button>("CopyErrorButton");
            if (copyErrorButton != null)
            {
                copyErrorButton.Click += CopyErrorButton_Click;
            }

            Button retryButton = this.FindControl<Button>("RetryButton");
            if (retryButton != null)
            {
                retryButton.Click += RetryButton_Click;
            }

            Button closeButton = this.FindControl<Button>("CloseButton");
            if (closeButton != null)
            {
                closeButton.Click += CloseButton_Click;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_downloadProgress?.FilePath) || !File.Exists(_downloadProgress.FilePath))
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(_downloadProgress.FilePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                if (UtilityHelper.GetOperatingSystem() == OSPlatform.Windows)
                {
                    _ = System.Diagnostics.Process.Start("explorer.exe", directory);
                }
                else if (UtilityHelper.GetOperatingSystem() == OSPlatform.OSX)
                {
                    _ = System.Diagnostics.Process.Start("open", directory);
                }
                else
                {
                    _ = System.Diagnostics.Process.Start("xdg-open", directory);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open download folder: {ex.Message}");
            }
        }

        private async void CopyErrorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string errorDetails = $"Mod: {_downloadProgress.ModName}\n";
                errorDetails += $"URL: {_downloadProgress.Url}\n";
                errorDetails += $"Error: {_downloadProgress.ErrorMessage}\n";

                if (_downloadProgress.Exception != null)
                {
                    errorDetails += $"\nException:\n{_downloadProgress.Exception}";
                }

                errorDetails += "\n\nDownload Logs:\n";
                errorDetails += string.Join("\n", _downloadProgress.GetLogs());

                if (Clipboard is null)
                {
                    await Logger.LogErrorAsync("Clipboard is null");
                    return;
                }

                await Clipboard.SetTextAsync(errorDetails);

                if (!(sender is Button button))
                {
                    return;
                }

                button.Content = "Copied!";

                // Use ConfigureAwait(true) to ensure continuation remains on UI thread
                await System.Threading.Tasks.Task.Delay(2000);
                button.Content = "Copy Error Details";
            }
            catch (Exception ex)


            {
                await Logger.LogErrorAsync($"Failed to copy error details: {ex.Message}");
            }
        }

        private async void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_retryAction is null)


                {
                    await Logger.LogErrorAsync("Retry action not provided to dialog");
                    await InformationDialog.ShowInformationDialogAsync(
                        this,
                        message: "Retry action not provided to dialog"
                    );
                    return;
                }

                if (_downloadProgress is null)


                {
                    await Logger.LogErrorAsync("Download progress is null");
                    return;


                }

                await Logger.LogAsync($"Retrying download for: {_downloadProgress.ModName} ({_downloadProgress.Url})");


                _downloadProgress.Status = DownloadStatus.Pending;
                _downloadProgress.StatusMessage = "Retrying download...";
                _downloadProgress.ErrorMessage = string.Empty;
                _downloadProgress.Exception = null;
                _downloadProgress.ProgressPercentage = 0;
                _downloadProgress.BytesDownloaded = 0;
                _downloadProgress.AddLog("Retry requested by user");


                _retryAction(_downloadProgress);


                Close();
            }
            catch (Exception ex)


            {
                await Logger.LogErrorAsync($"Failed to retry download: {ex.Message}");
                await InformationDialog.ShowInformationDialogAsync(this, $"Failed to retry download: {ex.Message}");
            }
        }

        [UsedImplicitly]
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        [UsedImplicitly]
        private void ToggleMaximizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (!(sender is Button maximizeButton))
            {
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                maximizeButton.Content = "▢";
            }
            else
            {
                WindowState = WindowState.Maximized;
                maximizeButton.Content = "▣";
            }
        }

        [UsedImplicitly]
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
            {
                return "< 1 second";
            }

            if (duration.TotalMinutes < 1)
            {
                return $"{(int)duration.TotalSeconds} seconds";
            }

            if (duration.TotalHours < 1)
            {
                return $"{(int)duration.TotalMinutes} minutes, {duration.Seconds} seconds";
            }

            return $"{(int)duration.TotalHours} hours, {duration.Minutes} minutes";
        }

        private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_mouseDownForWindowMoving)
            {
                return;
            }

            PointerPoint currentPoint = e.GetCurrentPoint(this);
            Position = new PixelPoint(
                Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
                Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
            );
        }

        private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
            {
                return;
            }

            if (ShouldIgnorePointerForWindowDrag(e))
            {
                return;
            }

            _mouseDownForWindowMoving = true;
            _originalPoint = e.GetCurrentPoint(this);
        }

        private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) =>
            _mouseDownForWindowMoving = false;

        private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
        {

            if (!(e.Source is Visual source))
            {
                return false;
            }

            Visual current = source;
            while (current != null && current != this)
            {
                switch (current)
                {

                    case Button _:
                    case TextBox _:
                    case ComboBox _:
                    case ListBox _:
                    case MenuItem _:
                    case Menu _:
                    case Expander _:
                    case Slider _:
                    case TabControl _:
                    case TabItem _:
                    case ProgressBar _:
                    case ScrollViewer _:

                    case Control control when control.ContextMenu?.IsOpen == true:
                        return true;
                    case Control control when control.ContextFlyout?.IsOpen == true:
                        return true;
                    default:
                        current = current.GetVisualParent();
                        break;
                }
            }

            return false;
        }

        private void PopulateFilesSection()
        {
            Border filesSection = this.FindControl<Border>("FilesSection");
            ItemsControl filesItemsControl = this.FindControl<ItemsControl>("FilesItemsControl");

            if (filesSection is null || filesItemsControl is null)
            {
                return;
            }

            List<string> fileDetails = CollectFileDetails();

            if (fileDetails.Count == 0)
            {
                filesSection.IsVisible = false;
                filesItemsControl.ItemsSource = null;
                return;
            }

            filesItemsControl.ItemsSource = fileDetails;
            filesSection.IsVisible = true;
        }

        private List<string> CollectFileDetails()
        {
            var details = new List<string>();

            if (_downloadProgress is null)
            {
                return details;
            }

            void AppendDetails(DownloadProgress progress, int? index = null, int? total = null)
            {
                if (progress is null)
                {
                    return;
                }

                string status = progress.Status.ToString();
                string url = string.IsNullOrWhiteSpace(progress.Url) ? "Unknown URL" : progress.Url;

                List<string> filenames = ExtractFilenames(progress);
                if (filenames.Count == 0)
                {
                    filenames.Add("Unknown file");
                }

                foreach (string filename in filenames)
                {
                    string prefix = index.HasValue && total.HasValue
                        ? $"[{index.Value}/{total.Value}] "
                        : string.Empty;

                    string detail = $"{prefix}{status}: {filename}";
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        detail += $" ← {url}";
                    }

                    details.Add(detail);
                }
            }

            if (_downloadProgress.IsGrouped && _downloadProgress.ChildDownloads.Count > 0)
            {
                int total = _downloadProgress.ChildDownloads.Count;
                for (int i = 0; i < total; i++)
                {
                    AppendDetails(_downloadProgress.ChildDownloads[i], i + 1, total);
                }
            }
            else
            {
                AppendDetails(_downloadProgress);
            }

            return details;
        }

        private static List<string> ExtractFilenames(DownloadProgress progress)
        {
            var result = new List<string>();

            if (progress is null)
            {
                return result;
            }

            if (progress.TargetFilenames != null && progress.TargetFilenames.Count > 0)
            {
                result.AddRange(progress.TargetFilenames.Where(f => !string.IsNullOrWhiteSpace(f)));
            }

            if (!string.IsNullOrWhiteSpace(progress.FilePath))
            {
                string name = Path.GetFileName(progress.FilePath);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    result.Add(name);
                }
            }

            if (result.Count == 0 && !string.IsNullOrWhiteSpace(progress.Url))
            {
                try
                {
                    string name = Path.GetFileName(new Uri(progress.Url).AbsolutePath);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        result.Add(name);
                    }
                }
                catch (UriFormatException)
                {
                    string fallback = Path.GetFileName(progress.Url);
                    if (!string.IsNullOrWhiteSpace(fallback))
                    {
                        result.Add(fallback);
                    }
                }
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private List<string> CollectDetailedLogs()
        {
            var collected = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddLine(string line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                string trimmed = line.TrimEnd();
                if (seen.Add(trimmed))
                {
                    collected.Add(trimmed);
                }
            }

            foreach (string log in _downloadProgress.GetLogs())
            {
                AddLine(log);
            }

            if (_downloadProgress.IsGrouped)
            {
                foreach (DownloadProgress child in _downloadProgress.ChildDownloads)
                {
                    foreach (string log in child.GetLogs())
                    {
                        AddLine(log);
                    }
                }
            }

            Guid? componentGuid = _downloadProgress.ComponentGuid;
            if (!componentGuid.HasValue && _downloadProgress.IsGrouped)
            {
                DownloadProgress firstChildWithGuid = _downloadProgress.ChildDownloads.FirstOrDefault(child => child.ComponentGuid.HasValue);
                if (firstChildWithGuid != null)
                {
                    componentGuid = firstChildWithGuid.ComponentGuid;
                }
            }

            IReadOnlyList<string> capturedLogs = DownloadLogCaptureManager.GetCapturedLogs(componentGuid);
            foreach (string log in capturedLogs)
            {
                AddLine(log);
            }

            if (collected.Count == 0)
            {
                collected.Add("No logs were recorded for this download.");
            }

            return collected;
        }

        // Pattern-based log matching removed; logs are now captured via DownloadLogCaptureManager scopes.
    }
}
