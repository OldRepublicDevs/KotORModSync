// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class ValidatePage : IWizardPage
    {
        public string Title => "Validation";
        public string Subtitle => "Validating your mod selection and installation environment";
        public Control Content { get; }
        public bool CanNavigateBack => true;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        private readonly List<ModComponent> _allComponents;
        private readonly MainConfig _mainConfig;
        private readonly StackPanel _resultsPanel;
        private readonly ProgressBar _validationProgress;
        private readonly TextBlock _statusText;
        private readonly Button _validateButton;
        private bool _hasValidated;
        private bool _hasCriticalErrors;

        public ValidatePage([NotNull][ItemNotNull] List<ModComponent> allComponents, [NotNull] MainConfig mainConfig)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            var panel = new StackPanel
            {
                Spacing = 16,
            };

            panel.Children.Add(new TextBlock
            {
                Text = "Click 'Run Validation' to check your installation for potential issues.",
                FontSize = 14,
            });

            _validateButton = new Button
            {
                Content = "🔍 Run Validation",
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Avalonia.Thickness(16, 8),
            };
            _validateButton.Click += async (s, e) => await RunValidation();
            panel.Children.Add(_validateButton);

            _validationProgress = new ProgressBar
            {
                IsIndeterminate = true,
                IsVisible = false,
                Height = 4,
            };
            panel.Children.Add(_validationProgress);

            _statusText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
            };
            panel.Children.Add(_statusText);

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 300,
            };

            _resultsPanel = new StackPanel
            {
                Spacing = 8,
            };

            scrollViewer.Content = _resultsPanel;
            panel.Children.Add(scrollViewer);

            Content = panel;
        }

        private async Task RunValidation()
        {
            _validateButton.IsEnabled = false;
            _validationProgress.IsVisible = true;
            _statusText.Text = "Running validation...";
            _resultsPanel.Children.Clear();
            _hasCriticalErrors = false;

            await Task.Delay(100); // Allow UI to update

            try
            {
                var selectedMods = _allComponents.Where(c => c.IsSelected).ToList();
                int errorCount = 0;
                int warningCount = 0;

                // Validate environment
                var (envSuccess, envMessage) = await InstallationService.ValidateInstallationEnvironmentAsync(
                    _mainConfig,
                    async msg => await Task.FromResult(true)
                );

                if (!envSuccess)
                {
                    AddResult("❌ Environment Error", envMessage, true);
                    errorCount++;
                    _hasCriticalErrors = true;
                }
                else
                {
                    AddResult("✅ Environment", "Installation environment is valid", false);
                }

                // Validate mod dependencies
                foreach (var component in selectedMods)
                {
                    var conflicts = ModComponent.GetConflictingComponents(
                        component.Dependencies,
                        component.Restrictions,
                        _allComponents
                    );

                    if (conflicts.ContainsKey("Dependency"))
                    {
                        var deps = conflicts["Dependency"];
                        AddResult($"⚠️ {component.Name}", $"Missing dependencies: {string.Join(", ", deps.Select(d => d.Name ?? string.Empty))}", false);
                        warningCount++;
                    }

                    if (conflicts.ContainsKey("Restriction"))
                    {
                        var restrictions = conflicts["Restriction"];
                        AddResult($"❌ {component.Name}", $"Incompatible with: {string.Join(", ", restrictions.Select(r => r.Name ?? string.Empty))}", true);
                        errorCount++;
                        _hasCriticalErrors = true;
                    }
                }

                // Validate mod order
                try
                {
                    var (isCorrectOrder, _) = ModComponent.ConfirmComponentsInstallOrder(selectedMods);
                    if (!isCorrectOrder)
                    {
                        AddResult("⚠️ Install Order", "Mods will be automatically reordered for proper installation", false);
                        warningCount++;
                    }
                    else
                    {
                        AddResult("✅ Install Order", "Mod installation order is correct", false);
                    }
                }
                catch (Exception ex)
                {
                    AddResult("❌ Install Order", $"Circular dependency detected: {ex.Message}", true);
                    errorCount++;
                    _hasCriticalErrors = true;
                }

                // Summary
                _statusText.Text = _hasCriticalErrors
                    ? $"❌ Validation failed: {errorCount} error(s), {warningCount} warning(s)"
                    : $"✅ Validation passed: {errorCount} error(s), {warningCount} warning(s)";

                _hasValidated = true;
            }
            finally
            {
                _validationProgress.IsVisible = false;
                _validateButton.IsEnabled = true;
            }
        }

        private void AddResult(string title, string message, bool isError = false)
        {
            var border = new Border
            {
                Padding = new Avalonia.Thickness(12, 8),
                CornerRadius = new Avalonia.CornerRadius(4),
                BorderBrush = new SolidColorBrush(Colors.Red),
                BorderThickness = new Avalonia.Thickness(1),
            };
            if (isError)
            {
                border.Background = new SolidColorBrush(Colors.Red);
            }

            var panel = new StackPanel
            {
                Spacing = 4,
            };

            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
            });

            panel.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                Opacity = 0.9,
                TextWrapping = TextWrapping.Wrap,
            });

            border.Child = panel;
            _resultsPanel.Children.Add(border);
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
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
    }
}

