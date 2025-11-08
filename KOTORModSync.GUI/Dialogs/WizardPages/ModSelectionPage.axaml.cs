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

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class ModSelectionPage : WizardPageBase
    {
        private readonly List<ModComponent> _allComponents;
        private readonly List<CheckBox> _modCheckBoxes = new List<CheckBox>();
        private TextBlock _selectionCountText;
        private TextBlock _selectionDetailsText;
        private TextBlock _filterSummaryText;
        private Button _selectAllButton;
        private Button _deselectAllButton;
        private Button _clearFiltersButton;
        private TextBox _searchTextBox;
        private ComboBox _categoryFilterComboBox;
        private ComboBox _tierFilterComboBox;
        private ToggleSwitch _spoilerFreeToggle;
        private StackPanel _modListPanel;
        private TextBlock _selectedCountBadge;
        private TextBlock _requiredCountBadge;
        private TextBlock _optionalCountBadge;

        private string _currentSearchText = string.Empty;
        private string _currentCategoryFilter = null;
        private string _currentTierFilter = null;
        private bool _spoilerFreeMode;

        public ModSelectionPage([NotNull][ItemNotNull] List<ModComponent> allComponents)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));

            InitializeComponent();
            InitializeControls();
            PopulateFilters();
            BuildModList();
            UpdateSelectionCount();
        }

        public override string Title => "Mod Selection";

        public override string Subtitle => "Choose the mods you want to install";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            UpdateSelectionCount();
            UpdateFilterSummary();
            return Task.CompletedTask;
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            int selectedCount = _allComponents.Count(c => c.IsSelected && !c.WidescreenOnly);
            if (selectedCount == 0)
            {
                return Task.FromResult((false, "Please select at least one mod to install."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _selectionCountText = this.FindControl<TextBlock>("SelectionCountText");
            _selectionDetailsText = this.FindControl<TextBlock>("SelectionDetailsText");
            _filterSummaryText = this.FindControl<TextBlock>("FilterSummaryText");
            _selectAllButton = this.FindControl<Button>("SelectAllButton");
            _deselectAllButton = this.FindControl<Button>("DeselectAllButton");
            _clearFiltersButton = this.FindControl<Button>("ClearFiltersButton");
            _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            _categoryFilterComboBox = this.FindControl<ComboBox>("CategoryFilterComboBox");
            _tierFilterComboBox = this.FindControl<ComboBox>("TierFilterComboBox");
            _spoilerFreeToggle = this.FindControl<ToggleSwitch>("SpoilerFreeToggle");
            _modListPanel = this.FindControl<StackPanel>("ModListPanel");
            _selectedCountBadge = this.FindControl<TextBlock>("SelectedCountBadge");
            _requiredCountBadge = this.FindControl<TextBlock>("RequiredCountBadge");
            _optionalCountBadge = this.FindControl<TextBlock>("OptionalCountBadge");
        }

        private void InitializeControls()
        {
            if (_selectAllButton != null)
            {
                _selectAllButton.Click += (_, __) => SelectAllVisibleMods();
            }

            if (_deselectAllButton != null)
            {
                _deselectAllButton.Click += (_, __) => DeselectAllMods();
            }

            if (_clearFiltersButton != null)
            {
                _clearFiltersButton.Click += (_, __) => ClearAllFilters();
            }

            if (_searchTextBox != null)
            {
                _searchTextBox.TextChanged += (_, __) =>
                {
                    _currentSearchText = _searchTextBox.Text ?? string.Empty;
                    RebuildModList();
                };
            }

            if (_categoryFilterComboBox != null)
            {
                _categoryFilterComboBox.SelectionChanged += (_, __) =>
                {
                    _currentCategoryFilter = _categoryFilterComboBox.SelectedItem as string;
                    RebuildModList();
                };
            }

            if (_tierFilterComboBox != null)
            {
                _tierFilterComboBox.SelectionChanged += (_, __) =>
                {
                    _currentTierFilter = _tierFilterComboBox.SelectedItem as string;
                    RebuildModList();
                };
            }

            if (_spoilerFreeToggle != null)
            {
                _spoilerFreeToggle.IsCheckedChanged += (_, __) =>
                {
                    _spoilerFreeMode = _spoilerFreeToggle.IsChecked == true;
                    RebuildModList();
                };
            }
        }

        private void PopulateFilters()
        {
            // Populate category filter
            if (_categoryFilterComboBox != null)
            {
                var categories = _allComponents
                    .Where(c => !c.WidescreenOnly)
                    .SelectMany(c => c.Category ?? new List<string>())
                    .Distinct()
                    .OrderBy(cat => cat, StringComparer.Ordinal)
                    .ToList();

                _categoryFilterComboBox.Items.Clear();
                _categoryFilterComboBox.Items.Add("All Categories");
                foreach (string category in categories)
                {
                    _categoryFilterComboBox.Items.Add(category);
                }
                _categoryFilterComboBox.SelectedIndex = 0;
            }

            // Populate tier filter
            if (_tierFilterComboBox != null)
            {
                var tiers = _allComponents
                    .Where(c => !c.WidescreenOnly && !string.IsNullOrEmpty(c.Tier))
                    .Select(c => c.Tier)
                    .Distinct()
                    .OrderBy(tier => tier, StringComparer.Ordinal)
                    .ToList();

                _tierFilterComboBox.Items.Clear();
                _tierFilterComboBox.Items.Add("All Tiers");
                foreach (string tier in tiers)
                {
                    _tierFilterComboBox.Items.Add(tier);
                }
                _tierFilterComboBox.SelectedIndex = 0;
            }
        }

        private void BuildModList()
        {
            if (_modListPanel is null)
            {
                return;
            }

            _modListPanel.Children.Clear();
            _modCheckBoxes.Clear();

            var filteredMods = GetFilteredMods();
            IOrderedEnumerable<IGrouping<string, ModComponent>> categorizedMods = filteredMods
                .GroupBy(c => c.Category?.FirstOrDefault() ?? "Other", StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (IGrouping<string, ModComponent> categoryGroup in categorizedMods)
            {
                // Category header with count
                var categoryHeader = new Border
                {
                    Padding = new Avalonia.Thickness(12, 10),
                    Margin = new Avalonia.Thickness(0, 8, 0, 4),
                    CornerRadius = new Avalonia.CornerRadius(6),
                };

                var headerStack = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
                headerStack.Children.Add(new TextBlock
                {
                    Text = categoryGroup.Key,
                    FontSize = 16,
                    FontWeight = FontWeight.Bold,
                });
                headerStack.Children.Add(new TextBlock
                {
                    Text = $"({categoryGroup.Count()})",
                    FontSize = 14,
                    Opacity = 0.6,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                });

                categoryHeader.Child = headerStack;
                _modListPanel.Children.Add(categoryHeader);

                // Mods in this category
                foreach (ModComponent component in categoryGroup.OrderBy(c => c.Name, StringComparer.Ordinal))
                {
                    var modCard = CreateModCard(component);
                    _modListPanel.Children.Add(modCard);
                }
            }

            if (!filteredMods.Any())
            {
                _modListPanel.Children.Add(new Border
                {
                    Padding = new Avalonia.Thickness(24),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = "🔍", FontSize = 32, TextAlignment = TextAlignment.Center },
                            new TextBlock
                            {
                                Text = "No mods match your filters",
                                FontSize = 16,
                                TextAlignment = TextAlignment.Center,
                            },
                            new TextBlock
                            {
                                Text = "Try adjusting your search or filter criteria",
                                FontSize = 13,
                                Opacity = 0.7,
                                TextAlignment = TextAlignment.Center,
                            }
                        }
                    }
                });
            }
        }

        private Border CreateModCard(ModComponent component)
        {
            var card = new Border
            {
                Padding = new Avalonia.Thickness(14, 12),
                Margin = new Avalonia.Thickness(8, 4),
                CornerRadius = new Avalonia.CornerRadius(8),
            };

            var mainGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,12,*,Auto"),
                RowDefinitions = new RowDefinitions("Auto,4,Auto"),
            };

            // Checkbox
            var checkBox = new CheckBox
            {
                IsChecked = component.IsSelected,
                Tag = component,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            };

            checkBox.IsCheckedChanged += (_, __) =>
            {
                if (checkBox.Tag is ModComponent comp)
                {
                    comp.IsSelected = checkBox.IsChecked == true;
                    UpdateSelectionCount();
                }
            };

            _modCheckBoxes.Add(checkBox);
            Grid.SetColumn(checkBox, 0);
            Grid.SetRow(checkBox, 0);
            Grid.SetRowSpan(checkBox, 3);
            mainGrid.Children.Add(checkBox);

            // Mod name (with spoiler-free handling)
            string displayName = _spoilerFreeMode && !string.IsNullOrEmpty(component.SpoilerFreeName)
                ? component.SpoilerFreeName
                : component.Name;

            var nameText = new TextBlock
            {
                Text = displayName,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(nameText, 2);
            Grid.SetRow(nameText, 0);
            mainGrid.Children.Add(nameText);

            // Tier badge (if exists)
            if (!string.IsNullOrEmpty(component.Tier))
            {
                var tierBadge = new Border
                {
                    Padding = new Avalonia.Thickness(8, 3),
                    CornerRadius = new Avalonia.CornerRadius(4),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    Child = new TextBlock
                    {
                        Text = component.Tier,
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                    }
                };
                Grid.SetColumn(tierBadge, 3);
                Grid.SetRow(tierBadge, 0);
                mainGrid.Children.Add(tierBadge);
            }

            // Description (with spoiler-free handling)
            if (!string.IsNullOrWhiteSpace(component.Description) || !string.IsNullOrWhiteSpace(component.SpoilerFreeDescription))
            {
                string displayDesc = _spoilerFreeMode && !string.IsNullOrEmpty(component.SpoilerFreeDescription)
                    ? component.SpoilerFreeDescription
                    : component.Description;

                if (!string.IsNullOrWhiteSpace(displayDesc))
                {
                    var descText = new TextBlock
                    {
                        Text = displayDesc,
                        FontSize = 13,
                        Opacity = 0.75,
                        TextWrapping = TextWrapping.Wrap,
                    };
                    Grid.SetColumn(descText, 2);
                    Grid.SetRow(descText, 2);
                    Grid.SetColumnSpan(descText, 2);
                    mainGrid.Children.Add(descText);
                }
            }

            card.Child = mainGrid;
            return card;
        }

        private List<ModComponent> GetFilteredMods()
        {
            var filtered = _allComponents.Where(c => !c.WidescreenOnly);

            // Apply category filter
            if (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "All Categories")
            {
                filtered = filtered.Where(c => c.Category?.Contains(_currentCategoryFilter, StringComparer.OrdinalIgnoreCase) == true);
            }

            // Apply tier filter
            if (!string.IsNullOrEmpty(_currentTierFilter) && _currentTierFilter != "All Tiers")
            {
                filtered = filtered.Where(c => string.Equals(c.Tier, _currentTierFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(_currentSearchText))
            {
                filtered = filtered.Where(c =>
                {
                    string searchTarget = _spoilerFreeMode && !string.IsNullOrEmpty(c.SpoilerFreeName)
                        ? c.SpoilerFreeName
                        : c.Name;

                    string descTarget = _spoilerFreeMode && !string.IsNullOrEmpty(c.SpoilerFreeDescription)
                        ? c.SpoilerFreeDescription
                        : c.Description;

                    return (searchTarget?.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                           (descTarget?.IndexOf(_currentSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
                });
            }

            return filtered.ToList();
        }

        private void RebuildModList()
        {
            BuildModList();
            UpdateSelectionCount();
            UpdateFilterSummary();
            UpdateClearFiltersButton();
        }

        private void SelectAllVisibleMods()
        {
            foreach (CheckBox checkBox in _modCheckBoxes)
            {
                checkBox.IsChecked = true;
            }
            UpdateSelectionCount();
        }

        private void DeselectAllMods()
        {
            foreach (ModComponent component in _allComponents.Where(c => !c.WidescreenOnly))
            {
                component.IsSelected = false;
            }
            BuildModList();
            UpdateSelectionCount();
        }

        private void ClearAllFilters()
        {
            _currentSearchText = string.Empty;
            _currentCategoryFilter = null;
            _currentTierFilter = null;

            if (_searchTextBox != null)
            {
                _searchTextBox.Text = string.Empty;
            }

            if (_categoryFilterComboBox != null)
            {
                _categoryFilterComboBox.SelectedIndex = 0;
            }

            if (_tierFilterComboBox != null)
            {
                _tierFilterComboBox.SelectedIndex = 0;
            }

            RebuildModList();
        }

        private void UpdateSelectionCount()
        {
            var nonWidescreenMods = _allComponents.Where(c => !c.WidescreenOnly).ToList();
            int selectedCount = nonWidescreenMods.Count(c => c.IsSelected);
            int totalCount = nonWidescreenMods.Count;
            int requiredCount = nonWidescreenMods.Count(c => c.IsSelected && c.Dependencies?.Any() == true);
            int optionalCount = selectedCount - requiredCount;

            if (_selectionCountText != null)
            {
                _selectionCountText.Text = $"{selectedCount} of {totalCount} mods selected";
            }

            if (_selectionDetailsText != null)
            {
                if (selectedCount == 0)
                {
                    _selectionDetailsText.Text = "Select the mods you want to install";
                }
                else if (selectedCount == totalCount)
                {
                    _selectionDetailsText.Text = "All mods selected for installation";
                }
                else
                {
                    _selectionDetailsText.Text = $"{totalCount - selectedCount} mods not selected";
                }
            }

            if (_selectedCountBadge != null)
            {
                _selectedCountBadge.Text = selectedCount.ToString();
            }

            if (_requiredCountBadge != null)
            {
                _requiredCountBadge.Text = requiredCount.ToString();
            }

            if (_optionalCountBadge != null)
            {
                _optionalCountBadge.Text = optionalCount.ToString();
            }
        }

        private void UpdateFilterSummary()
        {
            if (_filterSummaryText is null)
            {
                return;
            }

            var activeFilters = new List<string>();

            if (!string.IsNullOrEmpty(_currentSearchText))
            {
                activeFilters.Add($"Search: \"{_currentSearchText}\"");
            }

            if (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "All Categories")
            {
                activeFilters.Add($"Category: {_currentCategoryFilter}");
            }

            if (!string.IsNullOrEmpty(_currentTierFilter) && _currentTierFilter != "All Tiers")
            {
                activeFilters.Add($"Tier: {_currentTierFilter}");
            }

            if (_spoilerFreeMode)
            {
                activeFilters.Add("Spoiler-Free Mode");
            }

            var filteredMods = GetFilteredMods();
            int visibleCount = filteredMods.Count;
            int totalCount = _allComponents.Count(c => !c.WidescreenOnly);

            if (activeFilters.Any())
            {
                _filterSummaryText.Text = $"Showing {visibleCount} of {totalCount} mods • Filters: {string.Join(", ", activeFilters)}";
            }
            else
            {
                _filterSummaryText.Text = $"Showing all {totalCount} mods";
            }
        }

        private void UpdateClearFiltersButton()
        {
            if (_clearFiltersButton is null)
            {
                return;
            }

            bool hasActiveFilters = !string.IsNullOrEmpty(_currentSearchText) ||
                                   (!string.IsNullOrEmpty(_currentCategoryFilter) && _currentCategoryFilter != "All Categories") ||
                                   (!string.IsNullOrEmpty(_currentTierFilter) && _currentTierFilter != "All Tiers");

            _clearFiltersButton.IsVisible = hasActiveFilters;
        }
    }
}
