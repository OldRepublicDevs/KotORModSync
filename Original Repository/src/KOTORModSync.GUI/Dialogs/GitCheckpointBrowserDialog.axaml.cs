// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
    public partial class GitCheckpointBrowserDialog : Window
    {
        private readonly GitCheckpointService _checkpointService;
        private readonly ObservableCollection<GitCheckpointViewModel> _checkpoints = new ObservableCollection<GitCheckpointViewModel>();
        private GitCheckpointViewModel _selectedCheckpoint;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public GitCheckpointBrowserDialog()
        {
            InitializeComponent();
            ThemeManager.ApplyCurrentToWindow(this);
        }

        public GitCheckpointBrowserDialog([NotNull] string gameDirectory) : this()
        {
            if (string.IsNullOrEmpty(gameDirectory))
            {
                throw new ArgumentNullException(nameof(gameDirectory));
            }

            _checkpointService = new GitCheckpointService(gameDirectory);
            _cancellationTokenSource = new CancellationTokenSource();

            CheckpointList.ItemsSource = _checkpoints;

            _ = LoadCheckpointsAsync();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task LoadCheckpointsAsync()
        {
            try
            {
                SetBusy(isBusy: true, "Loading checkpoints...");

                List<CheckpointInfo> checkpoints = await _checkpointService.ListCheckpointsAsync().ConfigureAwait(true);
                CheckpointInfo currentCheckpoint = await _checkpointService.GetCurrentCheckpointAsync().ConfigureAwait(true);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _checkpoints.Clear();

                    foreach (CheckpointInfo checkpoint in checkpoints)
                    {
                        _checkpoints.Add(new GitCheckpointViewModel(checkpoint)
                        {
                            IsCurrent = string.Equals(currentCheckpoint?.CommitId, checkpoint.CommitId
, StringComparison.Ordinal),
                        });
                    }

                    CheckpointCountText.Text = $"{checkpoints.Count} checkpoint{(checkpoints.Count != 1 ? "s" : "")} available";

                    if (currentCheckpoint != null)
                    {
                        CurrentCheckpointText.Text = $"{currentCheckpoint.ShortCommitId} - {currentCheckpoint.ComponentName}";
                    }
                    else
                    {
                        CurrentCheckpointText.Text = "No checkpoints";
                    }
                });

                await Logger.LogAsync($"Loaded {checkpoints.Count} checkpoints").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to load checkpoints").ConfigureAwait(false);
                await ShowErrorAsync("Failed to load checkpoints. See logs for details.").ConfigureAwait(false);
            }
            finally
            {
                SetBusy(isBusy: false);
            }
        }

        private async void CheckpointItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is GitCheckpointViewModel viewModel))
            {
                return;
            }

            _selectedCheckpoint = viewModel;
            await DisplayCheckpointDetailsAsync(viewModel).ConfigureAwait(false);
        }

        private async Task DisplayCheckpointDetailsAsync(GitCheckpointViewModel viewModel)
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SelectedComponentNameText.Text = viewModel.ComponentName;
                    SelectedTimestampText.Text = viewModel.FormattedTimestamp;
                    SelectedCommitIdText.Text = viewModel.CommitId;
                    SelectedProgressText.Text = viewModel.ProgressText;

                    RestoreButton.IsEnabled = !viewModel.IsCurrent;
                    RestoreButton.Content = viewModel.IsCurrent
                        ? "✓ This is the current checkpoint"
                        : "🔄 Restore to Selected Checkpoint";

                    FileChangesPanel.Children.Clear();
                });

                // Load file changes
                SetBusy(isBusy: true, "Loading file changes...");

                CheckpointInfo currentCheckpoint = await _checkpointService.GetCurrentCheckpointAsync().ConfigureAwait(true);
                List<FileChangeInfo> changes = await _checkpointService.GetDiffAsync(
                    viewModel.CommitId,
                    currentCheckpoint?.CommitId
                ).ConfigureAwait(true);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    FileChangesPanel.Children.Clear();

                    if (changes.Count == 0)
                    {
                        FileChangesPanel.Children.Add(new TextBlock
                        {
                            FontSize = 12,
                            Opacity = 0.6,
                            Text = "No changes",
                        });
                        return;
                    }

                    var addedFiles = changes.Where(c => c.Status == FileChangeStatus.Added).ToList();
                    var modifiedFiles = changes.Where(c => c.Status == FileChangeStatus.Modified).ToList();
                    var deletedFiles = changes.Where(c => c.Status == FileChangeStatus.Deleted).ToList();

                    if (addedFiles.Count > 0)
                    {
                        FileChangesPanel.Children.Add(new TextBlock
                        {
                            FontSize = 12,
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            Text = $"➕ {addedFiles.Count} Added Files",
                            Margin = new Avalonia.Thickness(0, 8, 0, 4),
                        });

                        foreach (FileChangeInfo file in addedFiles.Take(10))
                        {
                            FileChangesPanel.Children.Add(new TextBlock
                            {
                                FontSize = 11,
                                Opacity = 0.8,
                                Text = $"  + {file.Path}",
                                FontFamily = "Consolas,Courier New,monospace",
                            });
                        }

                        if (addedFiles.Count > 10)
                        {
                            FileChangesPanel.Children.Add(new TextBlock
                            {
                                FontSize = 11,
                                Opacity = 0.6,
                                Text = $"  ... and {addedFiles.Count - 10} more",
                            });
                        }
                    }

                    if (modifiedFiles.Count > 0)
                    {
                        FileChangesPanel.Children.Add(new TextBlock
                        {
                            FontSize = 12,
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            Text = $"📝 {modifiedFiles.Count} Modified Files",
                            Margin = new Avalonia.Thickness(0, 8, 0, 4),
                        });

                        foreach (FileChangeInfo file in modifiedFiles.Take(10))
                        {
                            FileChangesPanel.Children.Add(new TextBlock
                            {
                                FontSize = 11,
                                Opacity = 0.8,
                                Text = $"  ~ {file.Path}",
                                FontFamily = "Consolas,Courier New,monospace",
                            });
                        }

                        if (modifiedFiles.Count > 10)
                        {
                            FileChangesPanel.Children.Add(new TextBlock
                            {
                                FontSize = 11,
                                Opacity = 0.6,
                                Text = $"  ... and {modifiedFiles.Count - 10} more",
                            });
                        }
                    }

                    if (deletedFiles.Count > 0)
                    {
                        FileChangesPanel.Children.Add(new TextBlock
                        {
                            FontSize = 12,
                            FontWeight = Avalonia.Media.FontWeight.SemiBold,
                            Text = $"➖ {deletedFiles.Count} Deleted Files",
                            Margin = new Avalonia.Thickness(0, 8, 0, 4),
                        });

                        foreach (FileChangeInfo file in deletedFiles.Take(10))
                        {
                            FileChangesPanel.Children.Add(new TextBlock
                            {
                                FontSize = 11,
                                Opacity = 0.8,
                                Text = $"  - {file.Path}",
                                FontFamily = "Consolas,Courier New,monospace",
                            });
                        }

                        if (deletedFiles.Count > 10)
                        {
                            FileChangesPanel.Children.Add(new TextBlock
                            {
                                FontSize = 11,
                                Opacity = 0.6,
                                Text = $"  ... and {deletedFiles.Count - 10} more",
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to display checkpoint details").ConfigureAwait(false);
            }
            finally
            {
                SetBusy(isBusy: false);
            }
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCheckpoint is null)
            {
                return;
            }

            try
            {
                bool? confirmed = await ConfirmationDialog.ShowConfirmationDialogAsync(
                    this,
                    $"Are you sure you want to restore to checkpoint:\n\n" +
                    $"{_selectedCheckpoint.ComponentName}\n" +
                    $"Timestamp: {_selectedCheckpoint.FormattedTimestamp}\n\n" +
                    $"This will replace all game files with the state at this checkpoint. " +
                    $"Your current changes will be lost.\n\n" +
                    $"Continue?"
                ).ConfigureAwait(true);

                if (confirmed != true)
                {
                    return;
                }

                SetBusy(isBusy: true, $"Restoring checkpoint {_selectedCheckpoint.ShortCommitId}...");

                await _checkpointService.RestoreCheckpointAsync(
                    _selectedCheckpoint.CommitId,
                    _cancellationTokenSource.Token
                ).ConfigureAwait(true);

                await ShowSuccessAsync($"Successfully restored to checkpoint: {_selectedCheckpoint.ComponentName}").ConfigureAwait(false);

                // Reload checkpoints
                await LoadCheckpointsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to restore checkpoint").ConfigureAwait(false);
                await ShowErrorAsync($"Failed to restore checkpoint: {ex.Message}").ConfigureAwait(false);
            }
            finally
            {
                SetBusy(isBusy: false);
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadCheckpointsAsync().ConfigureAwait(false);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetBusy(bool isBusy, string message = null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressBar.IsVisible = isBusy;
                StatusText.Text = message ?? string.Empty;
                RestoreButton.IsEnabled = !isBusy && _selectedCheckpoint != null && !_selectedCheckpoint.IsCurrent;
                RefreshButton.IsEnabled = !isBusy;
            });
        }

        private async Task ShowSuccessAsync(string message)
        {
            await InformationDialog.ShowInformationDialogAsync(this, message).ConfigureAwait(false);
        }

        private async Task ShowErrorAsync(string message)
        {
            await InformationDialog.ShowInformationDialogAsync(this, message).ConfigureAwait(false);
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _checkpointService?.Dispose();
            base.OnClosed(e);
        }
    }

    public class GitCheckpointViewModel
    {
        private readonly CheckpointInfo _checkpoint;

        public GitCheckpointViewModel(CheckpointInfo checkpoint)
        {
            _checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
        }

        public string CommitId => _checkpoint.CommitId;
        public string ShortCommitId => _checkpoint.ShortCommitId;
        public string ComponentName => _checkpoint.ComponentName;
        public string FormattedTimestamp => _checkpoint.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string ProgressText => _checkpoint.TotalComponents > 0
            ? $"[{_checkpoint.ComponentIndex}/{_checkpoint.TotalComponents}]"
            : string.Empty;
        public bool IsCurrent { get; set; }
    }
}

