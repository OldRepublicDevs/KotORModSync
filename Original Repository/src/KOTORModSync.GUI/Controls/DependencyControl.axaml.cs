// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;

using JetBrains.Annotations;

using KOTORModSync.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Controls
{
    public enum DependencyType
    {
        Dependency,
        Restriction,
        InstallBefore,
        InstallAfter,
    }

    public partial class DependencyControl : UserControl
    {
        [NotNull]
        public static readonly StyledProperty<List<Guid>> ThisGuidListProperty =
            AvaloniaProperty.Register<DependencyControl, List<Guid>>(nameof(ThisGuidList));

        [NotNull]
        public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
            AvaloniaProperty.Register<DependencyControl, ModComponent>(nameof(CurrentComponent));

        [NotNull]
        public static readonly StyledProperty<ModManagementService> ModManagementServiceProperty =
            AvaloniaProperty.Register<DependencyControl, ModManagementService>(nameof(ModManagementService));

        [NotNull]
        public static readonly StyledProperty<DependencyType> DependencyTypeProperty =
            AvaloniaProperty.Register<DependencyControl, DependencyType>(nameof(DependencyType));

        [NotNull]
        public static readonly StyledProperty<ModComponent> SelectedModComponentProperty =
            AvaloniaProperty.Register<DependencyControl, ModComponent>(nameof(SelectedModComponent));

        [NotNull]
        public static readonly StyledProperty<Option> SelectedOptionProperty =
            AvaloniaProperty.Register<DependencyControl, Option>(nameof(SelectedOption));

        public DependencyControl()
        {
            InitializeComponent();

            Loaded += (sender, args) =>
            {
                AutoCompleteBox autoComplete = this.FindControl<AutoCompleteBox>("DependenciesAutoComplete");
                if (autoComplete != null)
                {
                    autoComplete.SelectionChanged += DependenciesAutoComplete_SelectionChanged;
                }
            };
        }

        [NotNull]
        public List<Guid> ThisGuidList
        {
            get => GetValue(ThisGuidListProperty)
                ?? throw new InvalidOperationException("Could not retrieve property 'ThisGuidListProperty'");
            set => SetValue(ThisGuidListProperty, value);
        }

        [CanBeNull]
        public ModComponent CurrentComponent
        {
            get => GetValue(CurrentComponentProperty);
            set => SetValue(CurrentComponentProperty, value);
        }

        [CanBeNull]
        public ModManagementService ModManagementService
        {
            get => GetValue(ModManagementServiceProperty);
            set => SetValue(ModManagementServiceProperty, value);
        }

        public DependencyType DependencyType
        {
            get => GetValue(DependencyTypeProperty);
            set => SetValue(DependencyTypeProperty, value);
        }

        [CanBeNull]
        public ModComponent SelectedModComponent
        {
            get => GetValue(SelectedModComponentProperty);
            set => SetValue(SelectedModComponentProperty, value);
        }

        [CanBeNull]
        public Option SelectedOption
        {
            get => GetValue(SelectedOptionProperty);
            set => SetValue(SelectedOptionProperty, value);
        }


        [NotNull]
        [UsedImplicitly]
#pragma warning disable CA1822
        public List<ModComponent> ThisComponentList => MainWindow.ComponentsList;
#pragma warning restore CA1822

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (VisualRoot is MainWindow mainWindow)
            {
                mainWindow.FindComboBoxesInWindow(mainWindow);
            }
        }

        [UsedImplicitly]
        private void AddModToList_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            try
            {
                if (!(DependenciesAutoComplete.SelectedItem is ModComponent selectedComponent))
                {
                    return;
                }

                if (ThisGuidList.Contains(selectedComponent.Guid))
                {
                    return;
                }

                AddComponentToList(selectedComponent);

                DependenciesAutoComplete.SelectedItem = null;
                DependenciesAutoComplete.Text = string.Empty;
            }
            catch (Exception exception)
            {
                Logger.LogException(exception);
            }
        }

        [UsedImplicitly]
        private void AddOptionToList_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            try
            {
                if (!(OptionsAutoComplete.SelectedItem is Option selectedOption))
                {
                    return;
                }

                if (ThisGuidList.Contains(selectedOption.Guid))
                {
                    return;
                }

                AddComponentToList(selectedOption);

                OptionsAutoComplete.SelectedItem = null;
                OptionsAutoComplete.Text = string.Empty;
            }
            catch (Exception exception)
            {
                Logger.LogException(exception);
            }
        }

        private void AddComponentToList([NotNull] ModComponent selectedComponent)
        {

            bool added = false;
            if (ModManagementService != null && CurrentComponent != null &&
                 (DependencyType == DependencyType.Dependency || DependencyType == DependencyType.Restriction))
            {
                switch (DependencyType)
                {
                    case DependencyType.Dependency:
                        added = ModManagementService.AddDependency(CurrentComponent, selectedComponent);
                        break;
                    case DependencyType.Restriction:
                        added = ModManagementService.AddRestriction(CurrentComponent, selectedComponent);
                        break;
                }
            }
            else
            {
                ThisGuidList.Add(selectedComponent.Guid);
                added = true;
            }

            if (!added)
            {
                return;
            }

            RefreshDependenciesList();
        }

        private void RefreshDependenciesList()
        {
            var convertedItems = new GuidListToComponentNames().Convert(
                new object[]
                {
                ThisGuidList, MainWindow.ComponentsList,
                },
                ThisGuidList.GetType(),
                parameter: null,
                CultureInfo.CurrentCulture
            ) as List<string>;

            DependenciesListBox.ItemsSource = null;
            DependenciesListBox.ItemsSource = new AvaloniaList<object>(convertedItems ?? throw new InvalidOperationException());

            DependenciesListBox.InvalidateVisual();
            DependenciesListBox.InvalidateArrange();
            DependenciesListBox.InvalidateMeasure();
        }

        private void RemoveFromList_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            try
            {
                int index = DependenciesListBox.SelectedIndex;
                if (index < 0 || index >= ThisGuidList.Count)
                {
                    return;
                }

                Guid guidToRemove = ThisGuidList[index];

                bool removed = false;
                if (ModManagementService != null && CurrentComponent != null &&
                     (DependencyType == DependencyType.Dependency || DependencyType == DependencyType.Restriction))
                {

                    ModComponent componentToRemove = MainWindow.ComponentsList?.FirstOrDefault(c => c.Guid == guidToRemove);
                    if (componentToRemove != null)
                    {
                        switch (DependencyType)
                        {
                            case DependencyType.Dependency:
                                removed = ModManagementService.RemoveDependency(CurrentComponent, componentToRemove);
                                break;
                            case DependencyType.Restriction:
                                removed = ModManagementService.RemoveRestriction(CurrentComponent, componentToRemove);
                                break;
                        }
                    }
                }
                else
                {
                    ThisGuidList.RemoveAt(index);
                    removed = true;
                }

                if (!removed)
                {
                    return;
                }

                RefreshDependenciesList();
            }
            catch (Exception exception)
            {
                Logger.LogException(exception);
            }
        }

        [UsedImplicitly]
        private void DependenciesAutoComplete_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(DependenciesAutoComplete.SelectedItem is ModComponent selectedComponent))
                {
                    OptionsAutoComplete.ItemsSource = null;
                    return;
                }

                OptionsAutoComplete.ItemsSource = selectedComponent.Options;
            }
            catch (Exception exception)
            {
                Logger.LogException(exception);
            }
        }

    }
}
