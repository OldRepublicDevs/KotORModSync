// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

using KOTORModSync.Models;

namespace KOTORModSync.Controls
{
    public partial class CategorySelectionControl : UserControl
    {
        public static readonly StyledProperty<List<string>> SelectedCategoriesProperty =
            AvaloniaProperty.Register<CategorySelectionControl, List<string>>(nameof(SelectedCategories));

        public static readonly StyledProperty<ObservableCollection<SelectionFilterItem>> AvailableCategoriesProperty =
            AvaloniaProperty.Register<CategorySelectionControl, ObservableCollection<SelectionFilterItem>>(nameof(AvailableCategories));

        private readonly ObservableCollection<SelectionFilterItem> _categoryItems = new ObservableCollection<SelectionFilterItem>();
        private bool _isRefreshing = false;

        public List<string> SelectedCategories
        {
            get => GetValue(SelectedCategoriesProperty);
            set => SetValue(SelectedCategoriesProperty, value);
        }

        public ObservableCollection<SelectionFilterItem> AvailableCategories
        {
            get => GetValue(AvailableCategoriesProperty);
            set => SetValue(AvailableCategoriesProperty, value);
        }

        public CategorySelectionControl()
        {
            InitializeComponent();

            AvailableCategories = _categoryItems;

            if (CategoryItemsControl != null)
            {
                CategoryItemsControl.ItemsSource = _categoryItems;
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (CategoryItemsControl != null && CategoryItemsControl.ItemsSource != _categoryItems)
            {
                CategoryItemsControl.ItemsSource = _categoryItems;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedCategoriesProperty)
            {
                UpdateCategorySelections();
            }
        }

        private void UpdateCategorySelections()
        {
            if (SelectedCategories is null)
            {
                return;
            }

            _isRefreshing = true;
            try
            {

                foreach (SelectionFilterItem item in _categoryItems)
                {
                    item.IsSelected = SelectedCategories.Contains(item.Name, StringComparer.OrdinalIgnoreCase);
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (SelectionFilterItem item in _categoryItems)
            {
                item.IsSelected = false;
            }
            UpdateSelectedCategories();
        }

        private void AddNewCategory_Click(object sender, RoutedEventArgs e)
        {
            if (NewCategoryTextBox is null)
            {
                return;
            }

            string newCategoryText = NewCategoryTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(newCategoryText))
            {
                return;
            }

            if (_categoryItems.Any(item => string.Equals(item.Name, newCategoryText, StringComparison.OrdinalIgnoreCase)))
            {

                SelectionFilterItem existingItem = _categoryItems.First(item => string.Equals(item.Name, newCategoryText, StringComparison.OrdinalIgnoreCase));
                existingItem.IsSelected = true;
            }
            else
            {

                var newItem = new SelectionFilterItem
                {
                    Name = newCategoryText,
                    Count = 0,
                    IsSelected = true,
                };
                newItem.PropertyChanged += CategoryItem_PropertyChanged;
                _categoryItems.Add(newItem);
            }

            NewCategoryTextBox.Text = string.Empty;
            UpdateSelectedCategories();
        }

        private void CategoryItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(SelectionFilterItem.IsSelected), StringComparison.Ordinal) && !_isRefreshing)
            {
                UpdateSelectedCategories();
            }
        }

        private void UpdateSelectedCategories()
        {
            var selected = _categoryItems
                .Where(item => item.IsSelected)
                .Select(item => item.Name)
                .ToList();

            if (SelectedCategories is null || !SelectedCategories.SequenceEqual(selected, StringComparer.Ordinal))
            {
                SelectedCategories = selected;
            }
        }

        public void RefreshCategories(IEnumerable<Core.ModComponent> components)
        {
            _isRefreshing = true;
            try
            {
                _categoryItems.Clear();

                var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (Core.ModComponent component in components)
                {
                    if (component.Category.Count > 0)
                    {
                        foreach (string category in component.Category)
                        {
                            if (!string.IsNullOrEmpty(category))
                            {
                                if (categoryCounts.TryGetValue(category, out int value))
                                {
                                    categoryCounts[category] = ++value;
                                }
                                else
                                {
                                    categoryCounts[category] = 1;
                                }
                            }
                        }
                    }
                }

                foreach (KeyValuePair<string, int> kvp in categoryCounts.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var item = new SelectionFilterItem
                    {
                        Name = kvp.Key,
                        Count = kvp.Value,
                        IsSelected = SelectedCategories?.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase) ?? false,
                    };
                    item.PropertyChanged += CategoryItem_PropertyChanged;
                    _categoryItems.Add(item);
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }
    }
}
