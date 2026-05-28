// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class BaseInstallCompletePage : WizardPageBase
    {
        private TextBlock _modsInstalledText;
        private TextBlock _checkpointsCreatedText;
        private TextBlock _timeElapsedText;
        private TextBlock _filesModifiedText;
        private Button _viewLogsButton;
        private Button _manageCheckpointsButton;
        private Button _openGameFolderButton;
        private Button _openModFolderButton;

        public BaseInstallCompletePage()
            : this(0, TimeSpan.Zero)
        {
        }

        public BaseInstallCompletePage(
            int modsInstalled,
            TimeSpan timeElapsed,
            int filesModified = 0,
            int checkpointsCreated = 0)
        {

            InitializeComponent();
            CacheControls();
            InitializeStatistics(modsInstalled, timeElapsed, filesModified, checkpointsCreated);
            HookEvents();
        }

        public override string Title => "Base Installation Complete";
        public override string Subtitle => "The base mod installation has finished successfully";
        public override bool CanNavigateBack => false;
        public override bool CanCancel => false;

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void CacheControls()
        {
            _modsInstalledText = this.FindControl<TextBlock>("ModsInstalledText");
            _checkpointsCreatedText = this.FindControl<TextBlock>("CheckpointsCreatedText");
            _timeElapsedText = this.FindControl<TextBlock>("TimeElapsedText");
            _filesModifiedText = this.FindControl<TextBlock>("FilesModifiedText");
            _viewLogsButton = this.FindControl<Button>("ViewLogsButton");
            _manageCheckpointsButton = this.FindControl<Button>("ManageCheckpointsButton");
            _openGameFolderButton = this.FindControl<Button>("OpenGameFolderButton");
            _openModFolderButton = this.FindControl<Button>("OpenModFolderButton");
        }

        private void InitializeStatistics(int modsInstalled, TimeSpan timeElapsed, int filesModified, int checkpointsCreated)
        {
            if (_modsInstalledText != null)
            {
                _modsInstalledText.Text = modsInstalled.ToString();
            }

            if (_checkpointsCreatedText != null)
            {
                _checkpointsCreatedText.Text = checkpointsCreated.ToString();
            }

            if (_timeElapsedText != null)
            {
                _timeElapsedText.Text = timeElapsed.TotalMinutes >= 1
                    ? $"{(int)timeElapsed.TotalMinutes}:{timeElapsed.Seconds:D2}"
                    : $"{timeElapsed.Seconds}s";
            }

            if (_filesModifiedText != null)
            {
                _filesModifiedText.Text = filesModified > 0 ? filesModified.ToString() : "N/A";
            }
        }

        private void HookEvents()
        {
            if (_viewLogsButton != null)
            {
                _viewLogsButton.Click += OnViewLogsClicked;
            }

            if (_manageCheckpointsButton != null)
            {
                _manageCheckpointsButton.Click += OnManageCheckpointsClicked;
            }

            if (_openGameFolderButton != null)
            {
                _openGameFolderButton.Click += OnOpenGameFolderClicked;
            }

            if (_openModFolderButton != null)
            {
                _openModFolderButton.Click += OnOpenModFolderClicked;
            }
        }

        private async void OnViewLogsClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (Directory.Exists(logPath))
                {
                    _ = Process.Start(new ProcessStartInfo
                    {
                        FileName = logPath,
                        UseShellExecute = true,
                    });
                }
                else
                {
                    await Logger.LogWarningAsync($"Log directory not found: {logPath}");
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to open logs folder");
            }
        }

        private async void OnOpenGameFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(MainConfig.DestinationPath.FullName))
                {
                    _ = Process.Start(new ProcessStartInfo
                    {
                        FileName = MainConfig.DestinationPath.FullName,
                        UseShellExecute = true,
                    });
                }
                else
                {
                    await Logger.LogWarningAsync($"Game directory not found: {MainConfig.DestinationPath.FullName}");
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to open game folder");
            }
        }

        private async void OnOpenModFolderClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(MainConfig.SourcePath.FullName))
                {
                    _ = Process.Start(new ProcessStartInfo
                    {
                        FileName = MainConfig.SourcePath.FullName,
                        UseShellExecute = true,
                    });
                }
                else
                {
                    await Logger.LogWarningAsync($"Mod directory not found: {MainConfig.SourcePath}");
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to open mod folder");
            }
        }

        private async void OnManageCheckpointsClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainConfig.DestinationPath?.FullName == null)
                {
                    await Logger.LogWarningAsync("Game directory not configured");
                    return;
                }

                var checkpointBrowser = new GitCheckpointBrowserDialog(MainConfig.DestinationPath.FullName);

                // Find the parent window
                if (TopLevel.GetTopLevel(this) is Window parentWindow)
                {
                    await checkpointBrowser.ShowDialog(parentWindow);
                }
                else
                {
                    checkpointBrowser.Show();
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to open checkpoint browser");
            }
        }
    }
}


