// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class InstallingPage : WizardPageBase
    {
        public override string Title => "Installing Mods";
        public override string Subtitle => "Please wait while mods are being installed...";
        public override bool CanNavigateBack => false;
        public override bool CanNavigateForward => _canNavigateForward;

        private readonly List<ModComponent> _allComponents;

        private ProgressBar _mainProgressBar;
        private ProgressBar _currentModProgress;
        private TextBlock _percentText;
        private TextBlock _countText;
        private TextBlock _currentModText;
        private TextBlock _currentOperationText;
        private TextBlock _elapsedTimeText;
        private TextBlock _remainingTimeText;
        private TextBlock _rateText;
        private TextBlock _warningsText;
        private TextBlock _errorsText;
        private TextBlock _directionsText;

        private bool _isInstalling;
        private bool _installationComplete;
        private bool _canNavigateForward;
        private int _installedCount;
        private int _warningCount;
        private int _errorCount;
        private Stopwatch _stopwatch;

        public InstallingPage(
            [NotNull][ItemNotNull] List<ModComponent> allComponents,
            [NotNull] MainConfig mainConfig,
            [NotNull] CancellationTokenSource cancellationTokenSource)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));
            if (mainConfig is null)
            {
                throw new ArgumentNullException(nameof(mainConfig));
            }

            if (cancellationTokenSource is null)
            {
                throw new ArgumentNullException(nameof(cancellationTokenSource));
            }

            InitializeComponent();
            CacheControls();
            InitializeDefaults();
        }

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            if (_isInstalling || _installationComplete)
            {
                return Task.CompletedTask;
            }

            _isInstalling = true;
            _stopwatch = Stopwatch.StartNew();
            Logger.Logged += OnLogMessage;
            Logger.ExceptionLogged += OnException;

            _ = Task.Run(async () => await RunInstallation(cancellationToken).ConfigureAwait(false), cancellationToken);
            return Task.CompletedTask;
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            if (!_installationComplete)
            {
                return Task.FromResult((false, "Installation is still in progress. Please wait for it to complete."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void CacheControls()
        {
            _mainProgressBar = this.FindControl<ProgressBar>("MainProgressBar");
            _currentModProgress = this.FindControl<ProgressBar>("CurrentModProgress");
            _percentText = this.FindControl<TextBlock>("PercentText");
            _countText = this.FindControl<TextBlock>("CountText");
            _currentModText = this.FindControl<TextBlock>("CurrentModText");
            _currentOperationText = this.FindControl<TextBlock>("CurrentOperationText");
            _elapsedTimeText = this.FindControl<TextBlock>("ElapsedTimeText");
            _remainingTimeText = this.FindControl<TextBlock>("RemainingTimeText");
            _rateText = this.FindControl<TextBlock>("RateText");
            _warningsText = this.FindControl<TextBlock>("WarningsText");
            _errorsText = this.FindControl<TextBlock>("ErrorsText");
            _directionsText = this.FindControl<TextBlock>("DirectionsText");
        }

        private void InitializeDefaults()
        {
            if (_percentText != null)
            {
                _percentText.Text = "0%";
            }

            if (_countText != null)
            {
                _countText.Text = "0/0 mods installed";
            }

            if (_currentModText != null)
            {
                _currentModText.Text = "Preparing installation...";
            }

            if (_currentOperationText != null)
            {
                _currentOperationText.Text = "Initializing...";
            }

            if (_elapsedTimeText != null)
            {
                _elapsedTimeText.Text = "--:--:--";
            }

            if (_remainingTimeText != null)
            {
                _remainingTimeText.Text = "--:--:--";
            }

            if (_rateText != null)
            {
                _rateText.Text = "0.0 mods/min";
            }

            if (_warningsText != null)
            {
                _warningsText.Text = "0";
            }

            if (_errorsText != null)
            {
                _errorsText.Text = "0";
            }

            if (_mainProgressBar != null)
            {
                _mainProgressBar.Value = 0;
            }

            if (_currentModProgress != null)
            {
                _currentModProgress.IsIndeterminate = true;
                _currentModProgress.Value = 0;
            }
        }

        private async Task RunInstallation(CancellationToken cancellationToken)
        {
            try
            {
                var selectedMods = _allComponents.Where(c => c.IsSelected && !c.WidescreenOnly).ToList();
                int totalMods = selectedMods.Count;

                for (int i = 0; i < selectedMods.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        await UpdateUIAsync(() =>
                        {
                            if (_currentModText != null)
                            {
                                _currentModText.Text = "Installation cancelled by user";
                            }

                            if (_currentOperationText != null)
                            {
                                _currentOperationText.Text = "Cancelled";
                            }
                        });
                        break;
                    }

                    ModComponent component = selectedMods[i];

                    await UpdateUIAsync(() =>
                    {
                        double progress = totalMods == 0 ? 0 : (double)i / totalMods;
                        if (_mainProgressBar != null)
                        {
                            _mainProgressBar.Value = progress;
                        }

                        if (_percentText != null)
                        {
                            _percentText.Text = $"{Math.Round(progress * 100)}%";
                        }

                        if (_countText != null)
                        {
                            _countText.Text = $"{i}/{totalMods} mods installed";
                        }

                        if (_currentModText != null)
                        {
                            _currentModText.Text = $"Installing: {component.Name}";
                        }

                        if (_directionsText != null)
                        {
                            _directionsText.Text = component.Directions ?? string.Empty;
                        }

                        _installedCount = i;
                        UpdateMetrics(totalMods);
                    });

                    await Logger.LogAsync($"Starting installation of '{component.Name}'");

                    ModComponent.InstallExitCode exitCode = await InstallationService.InstallSingleComponentAsync(
                        component,
                        _allComponents,
                        cancellationToken
                    );

                    await Logger.LogAsync($"Finished installation of '{component.Name}' with exit code: {UtilityHelper.GetEnumDescription(exitCode)}");

                    if (exitCode != ModComponent.InstallExitCode.Success)
                    {
                        await Logger.LogErrorAsync($"Failed to install '{component.Name}'");
                    }
                }

                _installedCount = selectedMods.Count;
                _installationComplete = true;

                await UpdateUIAsync(() =>
                {
                    if (_mainProgressBar != null)
                    {
                        _mainProgressBar.Value = 1;
                    }

                    if (_percentText != null)
                    {
                        _percentText.Text = "100%";
                    }

                    if (_countText != null)
                    {
                        _countText.Text = $"{selectedMods.Count}/{selectedMods.Count} mods installed";
                    }

                    if (_currentModText != null)
                    {
                        _currentModText.Text = "✅ Installation complete!";
                    }

                    if (_currentOperationText != null)
                    {
                        _currentOperationText.Text = "Complete";
                    }

                    if (_currentModProgress != null)
                    {
                        _currentModProgress.IsIndeterminate = false;
                        _currentModProgress.Value = 1;
                    }

                    UpdateMetrics(selectedMods.Count);
                });

                _canNavigateForward = true;
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Error during installation");
            }
            finally
            {
                _isInstalling = false;
                _stopwatch?.Stop();
                Logger.Logged -= OnLogMessage;
                Logger.ExceptionLogged -= OnException;
            }
        }

        private void OnLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (message.IndexOf("[Warning]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _warningCount++;
            }

            if (message.IndexOf("[Error]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _errorCount++;
            }

            _ = UpdateUIAsync(() =>
            {
                if (_warningsText != null)
                {
                    _warningsText.Text = _warningCount.ToString(CultureInfo.InvariantCulture);
                }

                if (_errorsText != null)
                {
                    _errorsText.Text = _errorCount.ToString(CultureInfo.InvariantCulture);
                }
            });
        }

        private void OnException(Exception ex)
        {
            _errorCount++;
            _ = UpdateUIAsync(() =>
            {
                if (_errorsText != null)
                {
                    _errorsText.Text = _errorCount.ToString(CultureInfo.InvariantCulture);
                }
            });
        }

        private void UpdateMetrics(int totalMods)
        {
            TimeSpan elapsed = _stopwatch?.Elapsed ?? TimeSpan.Zero;

            if (_elapsedTimeText != null)
            {
                _elapsedTimeText.Text = elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            }

            if (_installedCount > 0)
            {
                var avgPerMod = TimeSpan.FromTicks(elapsed.Ticks / Math.Max(1, _installedCount));
                int remaining = Math.Max(0, totalMods - _installedCount);
                var eta = TimeSpan.FromTicks(avgPerMod.Ticks * remaining);

                if (_remainingTimeText != null)
                {
                    _remainingTimeText.Text = eta.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                }

                double perMinute = _installedCount / Math.Max(0.001, elapsed.TotalMinutes);
                if (_rateText != null)
                {
                    _rateText.Text = $"{perMinute:0.0} mods/min";
                }
            }
            else
            {
                if (_remainingTimeText != null)
                {
                    _remainingTimeText.Text = "--:--:--";
                }

                if (_rateText != null)
                {
                    _rateText.Text = "0.0 mods/min";
                }
            }
        }

        private Task UpdateUIAsync(Action action)
        {
            return Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal).GetTask();
        }
    }
}


