// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

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
        private bool _hasValidated;
        private bool _hasCriticalErrors;
        private int _errorCount;
        private int _warningCount;
        private int _passedCount;

        public ValidatePage([NotNull][ItemNotNull] List<ModComponent> allComponents, [NotNull] MainConfig mainConfig)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            InitializeComponent();
            CacheControls();
            HookEvents();
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
        }

        private void HookEvents()
        {
            if (_validateButton != null)
            {
                _validateButton.Click += async (_, __) => await RunValidation();
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

            _resultsPanel?.Children.Clear();
            _hasCriticalErrors = false;
            _errorCount = 0;
            _warningCount = 0;
            _passedCount = 0;

            UpdateBadges();

            await Task.Delay(100);

            try
            {
                var selectedMods = _allComponents.Where(c => c.IsSelected).ToList();

                (bool envSuccess, string envMessage) = await InstallationService.ValidateInstallationEnvironmentAsync(
                    _mainConfig,
                    async msg =>
                    {
                        await Task.CompletedTask;
                        return true;
                    }
                );

                if (!envSuccess)
                {
                    AddResult("❌ Environment Error", envMessage);
                    _errorCount++;
                    _hasCriticalErrors = true;
                }
                else
                {
                    AddResult("✅ Environment", "Installation environment is valid");
                    _passedCount++;
                }

                foreach (ModComponent component in selectedMods)
                {
                    Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(
                        component.Dependencies,
                        component.Restrictions,
                        _allComponents
                    );

                    if (conflicts.ContainsKey("Dependency"))
                    {
                        List<ModComponent> deps = conflicts["Dependency"];
                        AddResult($"⚠️ {component.Name}", $"Missing dependencies: {string.Join(", ", deps.Select(d => d.Name ?? string.Empty))}");
                        _warningCount++;
                    }

                    if (conflicts.ContainsKey("Restriction"))
                    {
                        List<ModComponent> restrictions = conflicts["Restriction"];
                        AddResult($"❌ {component.Name}", $"Incompatible with: {string.Join(", ", restrictions.Select(r => r.Name ?? string.Empty))}");
                        _errorCount++;
                        _hasCriticalErrors = true;
                    }
                }

                try
                {
                    (bool isCorrectOrder, List<ModComponent> _) = ModComponent.ConfirmComponentsInstallOrder(selectedMods);
                    if (!isCorrectOrder)
                    {
                        AddResult("⚠️ Install Order", "Mods will be automatically reordered for proper installation");
                        _warningCount++;
                    }
                    else
                    {
                        AddResult("✅ Install Order", "Mod installation order is correct");
                        _passedCount++;
                    }
                }
                catch (Exception ex)
                {
                    AddResult("❌ Install Order", $"Circular dependency detected: {ex.Message}");
                    _errorCount++;
                    _hasCriticalErrors = true;
                }

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


