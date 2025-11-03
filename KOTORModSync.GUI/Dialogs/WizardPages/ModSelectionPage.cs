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

namespace KOTORModSync.Dialogs.WizardPages
{
    public class ModSelectionPage : IWizardPage
    {
        public string Title => "Mod Selection";
        public string Subtitle => "Select the mods you want to install";
        public Control Content { get; }
        public bool CanNavigateBack => true;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        private readonly List<ModComponent> _allComponents;
        private readonly List<CheckBox> _modCheckBoxes = new List<CheckBox>();
        private readonly TextBlock _selectionCountText;

        public ModSelectionPage([NotNull][ItemNotNull] List<ModComponent> allComponents)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));

            var mainPanel = new StackPanel
            {
                Spacing = 12
            };

            // Header with quick actions
            var headerPanel = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto")
            };

            _selectionCountText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_selectionCountText, 0);
            headerPanel.Children.Add(_selectionCountText);

            var selectAllButton = new Button
            {
                Content = "Select All",
                Margin = new Avalonia.Thickness(0, 0, 8, 0)
            };
            selectAllButton.Click += (s, e) => SetAllMods(true);
            Grid.SetColumn(selectAllButton, 1);
            headerPanel.Children.Add(selectAllButton);

            var deselectAllButton = new Button
            {
                Content = "Deselect All"
            };
            deselectAllButton.Click += (s, e) => SetAllMods(false);
            Grid.SetColumn(deselectAllButton, 2);
            headerPanel.Children.Add(deselectAllButton);

            mainPanel.Children.Add(headerPanel);

            // Mod list in scrollable area
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400
            };

            var modListPanel = new StackPanel
            {
                Spacing = 4
            };

            // Group mods by category if available
            var categorizedMods = _allComponents
                .Where(c => !c.WidescreenOnly) // Exclude widescreen mods from base selection
                .GroupBy(c => c.Category?.FirstOrDefault() ?? "Other", StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (var categoryGroup in categorizedMods)
            {
                // Category header
                modListPanel.Children.Add(new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                    Margin = new Avalonia.Thickness(0, 12, 0, 4)
                });

                // Mods in this category
                foreach (var component in categoryGroup.OrderBy(c => c.Name, StringComparer.Ordinal))
                {
                    var checkBox = new CheckBox
                    {
                        Content = component.Name,
                        IsChecked = component.IsSelected,
                        Tag = component,
                        Margin = new Avalonia.Thickness(16, 2, 0, 2)
                    };

                    checkBox.IsCheckedChanged += (s, e) =>
                    {
                        if (checkBox.Tag is ModComponent comp)
                        {
                            comp.IsSelected = checkBox.IsChecked == true;
                            UpdateSelectionCount();
                        }
                    };

                    _modCheckBoxes.Add(checkBox);
                    modListPanel.Children.Add(checkBox);

                    // Add description if available
                    if (!string.IsNullOrWhiteSpace(component.Description))
                    {
                        var descText = new TextBlock
                        {
                            Text = component.Description,
                            FontSize = 12,
                            Opacity = 0.7,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Avalonia.Thickness(36, 0, 0, 4)
                        };
                        modListPanel.Children.Add(descText);
                    }
                }
            }

            scrollViewer.Content = modListPanel;
            mainPanel.Children.Add(scrollViewer);

            Content = mainPanel;
        }

        private void SetAllMods(bool selected)
        {
            foreach (var checkBox in _modCheckBoxes)
            {
                checkBox.IsChecked = selected;
            }
            UpdateSelectionCount();
        }

        private void UpdateSelectionCount()
        {
            int selectedCount = _allComponents.Count(c => c.IsSelected && !c.WidescreenOnly);
            int totalCount = _allComponents.Count(c => !c.WidescreenOnly);
            _selectionCountText.Text = $"{selectedCount} of {totalCount} mods selected";
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            UpdateSelectionCount();
            return Task.CompletedTask;
        }

        public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            int selectedCount = _allComponents.Count(c => c.IsSelected && !c.WidescreenOnly);
            if (selectedCount == 0)
            {
                return Task.FromResult((false, "Please select at least one mod to install."));
            }

            return Task.FromResult((true, (string)null));
        }
    }
}

