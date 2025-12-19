// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Services.Validation;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class ValidatePage : WizardPageBase
    {
        private readonly List<ModComponent> _allComponents;
        private readonly MainConfig _mainConfig;
        private StackPanel _resultsPanel;
        private ProgressBar _validationProgress;
        private TextBlock _statusText;
        private TextBlock _summaryText;
        private TextBlock _summaryDetails;
        private TextBlock _errorCountBadge;
        private TextBlock _warningCountBadge;
        private TextBlock _passedCountBadge;
        private Button _validateButton;
        private Expander _logExpander;
        private ScrollViewer _logScrollViewer;
        private TextBlock _logText;
        private TextBlock _logProgressText;
        private bool _hasValidated;
        private bool _hasCriticalErrors;
        private int _errorCount;
        private int _warningCount;
        private int _passedCount;
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private readonly Queue<string> _logQueue = new Queue<string>();
        private readonly object _logLock = new object();
        private DispatcherTimer _logUpdateTimer;
        private DateTime _validationStartTime;
        private int _currentStep;
        private int _totalSteps;
        private string _currentOperation = string.Empty;

        public ValidatePage()
            : this(new List<ModComponent>(), new MainConfig())
        {
        }

        public ValidatePage([NotNull][ItemNotNull] List<ModComponent> allComponents, [NotNull] MainConfig mainConfig)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            InitializeComponent();
            CacheControls();
            HookEvents();
            InitializeLogUpdateTimer();
        }

        public override string Title => "Validation";

        public override string Subtitle => "Validating your mod selection and installation environment";

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            if (!_hasValidated)
            {
                return Task.FromResult((false, "Please run validation before continuing."));
            }

            if (_hasCriticalErrors)
            {
                return Task.FromResult((false, "Critical errors detected. Please resolve them before continuing."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void CacheControls()
        {
            _resultsPanel = this.FindControl<StackPanel>("ResultsPanel");
            _validationProgress = this.FindControl<ProgressBar>("ValidationProgress");
            _statusText = this.FindControl<TextBlock>("StatusText");
            _summaryText = this.FindControl<TextBlock>("SummaryText");
            _summaryDetails = this.FindControl<TextBlock>("SummaryDetails");
            _errorCountBadge = this.FindControl<TextBlock>("ErrorCountBadge");
            _warningCountBadge = this.FindControl<TextBlock>("WarningCountBadge");
            _passedCountBadge = this.FindControl<TextBlock>("PassedCountBadge");
            _validateButton = this.FindControl<Button>("ValidateButton");
            _logExpander = this.FindControl<Expander>("LogExpander");
            _logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
            _logText = this.FindControl<TextBlock>("LogText");
            _logProgressText = this.FindControl<TextBlock>("LogProgressText");
        }

        private void HookEvents()
        {
            if (_validateButton != null)
            {
                _validateButton.Click += async (_, __) => await RunValidation();
            }
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
            UpdateLogHeader();
        }

        private void FlushLogQueue()
        {
            lock (_logLock)
            {
                if (_logQueue.Count == 0 || _logText == null)
                {
                    return;
                }

                while (_logQueue.Count > 0)
                {
                    string message = _logQueue.Dequeue();
                    _logBuilder.AppendLine(message);
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (_logText != null)
                    {
                        _logText.Text = _logBuilder.ToString();
                    }

                    if (_logScrollViewer != null)
                    {
                        _logScrollViewer.ScrollToEnd();
                    }
                }, DispatcherPriority.Normal);
            }
        }

        private void UpdateLogHeader()
        {
            if (_logProgressText == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (_logProgressText != null)
                {
                    var parts = new List<string>();

                    if (_totalSteps > 0 && _currentStep > 0)
                    {
                        double percentage = (_currentStep / (double)_totalSteps) * 100.0;
                        parts.Add($"{percentage:F0}%");
                    }

                    if (!string.IsNullOrEmpty(_currentOperation))
                    {
                        parts.Add(_currentOperation);
                    }

                    if (_validationStartTime != default && _currentStep > 0 && _totalSteps > 0)
                    {
                        TimeSpan elapsed = DateTime.UtcNow - _validationStartTime;
                        if (elapsed.TotalSeconds > 0 && _currentStep > 0)
                        {
                            double avgTimePerStep = elapsed.TotalSeconds / _currentStep;
                            int remainingSteps = _totalSteps - _currentStep;
                            if (remainingSteps > 0)
                            {
                                TimeSpan eta = TimeSpan.FromSeconds(avgTimePerStep * remainingSteps);
                                string etaText = eta.TotalSeconds < 60 ? $"{eta.TotalSeconds:F0}s" : $"{eta.TotalMinutes:F1}m";
                                parts.Add($"ETA: {etaText}");
                            }
                        }
                    }

                    _logProgressText.Text = parts.Count > 0 ? string.Join(" • ", parts) : string.Empty;
                }
            }, DispatcherPriority.Normal);
        }

        private void AppendLog(string message)
        {
            lock (_logLock)
            {
                _logQueue.Enqueue($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
            }
        }

        private void ClearLog()
        {
            lock (_logLock)
            {
                _logBuilder.Clear();
                _logQueue.Clear();
                Dispatcher.UIThread.Post(() =>
                {
                    if (_logText != null)
                    {
                        _logText.Text = string.Empty;
                    }
                }, DispatcherPriority.Normal);
            }
        }

        private async Task RunValidation()
        {
            if (_validateButton != null)
            {
                _validateButton.IsEnabled = false;
            }

            if (_validationProgress != null)
            {
                _validationProgress.IsVisible = true;
            }

            if (_statusText != null)
            {
                _statusText.Text = "Running validation...";
            }

            if (_logExpander != null)
            {
                _logExpander.IsVisible = true;
            }

            _resultsPanel?.Children.Clear();
            _hasCriticalErrors = false;
            _errorCount = 0;
            _warningCount = 0;
            _passedCount = 0;
            _currentStep = 0;
            _totalSteps = 0;
            _currentOperation = string.Empty;
            _validationStartTime = DateTime.UtcNow;

            ClearLog();
            AppendLog("Starting validation...");
            UpdateBadges();

            await Task.Delay(100);

            try
            {
                var selectedMods = _allComponents.Where(c => c.IsSelected).ToList();
                _totalSteps = 3 + selectedMods.Count + 1; // Environment + each mod + install order + dry-run

                _currentStep = 1;
                _currentOperation = "Validating environment...";
                AppendLog("Step 1: Validating installation environment");
                UpdateLogHeader();

                (bool envSuccess, string envMessage) = await InstallationService.ValidateInstallationEnvironmentAsync(
                    _mainConfig,
                    async msg =>
                    {
                        await Task.CompletedTask;
                        AppendLog($"  {msg}");
                        return true;
                    }
                );

                if (!envSuccess)
                {
                    AppendLog($"  ❌ Environment validation failed: {envMessage}");
                    AddResult("❌ Environment Error", envMessage);
                    _errorCount++;
                    _hasCriticalErrors = true;
                }
                else
                {
                    AppendLog("  ✅ Environment validation passed");
                    AddResult("✅ Environment", "Installation environment is valid");
                    _passedCount++;
                }

                _currentStep++;
                _currentOperation = "Checking mod conflicts...";
                AppendLog($"Step 2: Checking conflicts for {selectedMods.Count} selected mod(s)");
                UpdateLogHeader();

                foreach (ModComponent component in selectedMods)
                {
                    AppendLog($"  Checking {component.Name}...");
                    Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(
                        component.Dependencies,
                        component.Restrictions,
                        _allComponents
                    );

                    if (conflicts.ContainsKey("Dependency"))
                    {
                        List<ModComponent> deps = conflicts["Dependency"];
                        string depNames = string.Join(", ", deps.Select(d => d.Name ?? string.Empty));
                        AppendLog($"    ⚠️ Missing dependencies: {depNames}");
                        AddResult($"⚠️ {component.Name}", $"Missing dependencies: {depNames}");
                        _warningCount++;
                    }

                    if (conflicts.ContainsKey("Restriction"))
                    {
                        List<ModComponent> restrictions = conflicts["Restriction"];
                        string restrictionNames = string.Join(", ", restrictions.Select(r => r.Name ?? string.Empty));
                        AppendLog($"    ❌ Incompatible with: {restrictionNames}");
                        AddResult($"❌ {component.Name}", $"Incompatible with: {restrictionNames}");
                        _errorCount++;
                        _hasCriticalErrors = true;
                    }
                    else if (!conflicts.ContainsKey("Dependency"))
                    {
                        AppendLog($"    ✅ No conflicts");
                    }
                }

                _currentStep++;
                _currentOperation = "Validating install order...";
                AppendLog("Step 3: Validating mod installation order");
                UpdateLogHeader();

                try
                {
                    (bool isCorrectOrder, List<ModComponent> _) = ModComponent.ConfirmComponentsInstallOrder(selectedMods);
                    if (!isCorrectOrder)
                    {
                        AppendLog("  ⚠️ Mods will be automatically reordered");
                        AddResult("⚠️ Install Order", "Mods will be automatically reordered for proper installation");
                        _warningCount++;
                    }
                    else
                    {
                        AppendLog("  ✅ Install order is correct");
                        AddResult("✅ Install Order", "Mod installation order is correct");
                        _passedCount++;
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"  ❌ Circular dependency detected: {ex.Message}");
                    AddResult("❌ Install Order", $"Circular dependency detected: {ex.Message}");
                    _errorCount++;
                    _hasCriticalErrors = true;
                }

                // Perform dry-run validation using VFS and ExecuteInstructionsAsync
                _currentStep++;
                _currentOperation = "Running dry-run validation...";
                AppendLog("Step 4: Running instruction execution validation (dry-run)");
                UpdateLogHeader();

                if (_statusText != null)
                {
                    _statusText.Text = "Running instruction execution validation...";
                }

                try
                {
                    DryRunValidationResult dryRunResult = await DryRunValidator.ValidateInstallationAsync(
                        _allComponents,
                        skipDependencyCheck: false,
                        CancellationToken.None
                    ).ConfigureAwait(false);

                    if (dryRunResult.IsValid && !dryRunResult.HasWarnings)
                    {
                        AppendLog("  ✅ All instructions validated successfully");
                        AddResult("✅ Instruction Execution", "All instructions validated successfully. Dry-run completed without errors.");
                        _passedCount++;
                    }
                    else
                    {
                        int dryRunErrors = dryRunResult.Issues.Count(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);
                        int dryRunWarnings = dryRunResult.Issues.Count(i => i.Severity == ValidationSeverity.Warning);

                        AppendLog($"  Found {dryRunErrors} error(s) and {dryRunWarnings} warning(s)");

                        if (dryRunErrors > 0)
                        {
                            var errorIssues = dryRunResult.Issues
                                .Where(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical)
                                .Take(5)
                                .ToList();

                            foreach (var issue in errorIssues)
                            {
                                AppendLog($"    ❌ [{issue.Category}] {issue.Message}");
                            }

                            if (dryRunErrors > 5)
                            {
                                AppendLog($"    ... and {dryRunErrors - 5} more error(s)");
                            }

                            string errorSummary = errorIssues.Any()
                                ? string.Join("; ", errorIssues.Select(i => $"{i.Category}: {i.Message}"))
                                : "Unknown errors occurred";

                            if (dryRunErrors > 5)
                            {
                                errorSummary += $" (and {dryRunErrors - 5} more)";
                            }

                            AddResult("❌ Instruction Execution", $"Dry-run validation failed with {dryRunErrors} error(s). {errorSummary}");
                            _errorCount += dryRunErrors;
                            _hasCriticalErrors = true;
                        }
                        else if (dryRunWarnings > 0)
                        {
                            var warningIssues = dryRunResult.Issues
                                .Where(i => i.Severity == ValidationSeverity.Warning)
                                .Take(3)
                                .ToList();

                            foreach (var issue in warningIssues)
                            {
                                AppendLog($"    ⚠️ [{issue.Category}] {issue.Message}");
                            }

                            if (dryRunWarnings > 3)
                            {
                                AppendLog($"    ... and {dryRunWarnings - 3} more warning(s)");
                            }

                            AddResult("⚠️ Instruction Execution", $"Dry-run validation passed with {dryRunWarnings} warning(s). Review details before proceeding.");
                            _warningCount += dryRunWarnings;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"  ❌ Exception: {ex.Message}");
                    AppendLog($"  Stack trace: {ex.StackTrace}");
                    AddResult("❌ Instruction Execution", $"Dry-run validation failed with exception: {ex.Message}");
                    _errorCount++;
                    _hasCriticalErrors = true;
                }

                _currentStep = _totalSteps;
                _currentOperation = string.Empty;
                TimeSpan totalTime = DateTime.UtcNow - _validationStartTime;
                AppendLog($"Validation completed in {totalTime.TotalSeconds:F1} seconds");
                AppendLog(_hasCriticalErrors ? "❌ Validation failed" : "✅ Validation passed");
                UpdateLogHeader();

                UpdateBadges();
                UpdateSummary();

                _hasValidated = true;
            }
            finally
            {
                if (_validationProgress != null)
                {
                    _validationProgress.IsVisible = false;
                }

                if (_validateButton != null)
                {
                    _validateButton.IsEnabled = true;
                }

                _currentOperation = string.Empty;
                UpdateLogHeader();
            }
        }

        private void AddResult(string title, string message)
        {
            if (_resultsPanel is null)
            {
                return;
            }

            var border = new Border
            {
                Padding = new Avalonia.Thickness(16, 12),
                CornerRadius = new Avalonia.CornerRadius(8),
                Margin = new Avalonia.Thickness(0, 0, 0, 8),
            };

            var panel = new StackPanel
            {
                Spacing = 6,
            };

            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
            });

            panel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap,
            });

            border.Child = panel;
            _resultsPanel.Children.Add(border);
        }

        private void UpdateBadges()
        {
            if (_errorCountBadge != null)
            {
                _errorCountBadge.Text = _errorCount.ToString();
            }

            if (_warningCountBadge != null)
            {
                _warningCountBadge.Text = _warningCount.ToString();
            }

            if (_passedCountBadge != null)
            {
                _passedCountBadge.Text = _passedCount.ToString();
            }
        }

        private void UpdateSummary()
        {
            if (_summaryText != null)
            {
                _summaryText.Text = _hasCriticalErrors
                    ? "❌ Validation Failed"
                    : "✅ Validation Passed";
            }

            if (_summaryDetails != null)
            {
                if (_hasCriticalErrors)
                {
                    _summaryDetails.Text = "Please resolve all critical errors before continuing. Check the results above for details.";
                }
                else if (_warningCount > 0)
                {
                    _summaryDetails.Text = $"Validation passed with {_warningCount} warning(s). These will be handled automatically during installation.";
                }
                else
                {
                    _summaryDetails.Text = "Everything looks good! You're ready to proceed with the installation.";
                }
            }

            if (_statusText != null)
            {
                _statusText.Text = _hasCriticalErrors
                    ? "Validation failed"
                    : "Validation complete";
            }
        }
    }
}


