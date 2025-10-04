// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
		InstallAfter
	}

	public partial class DependencyControl : UserControl
	{
		[NotNull]
		public static readonly StyledProperty<List<Guid>> ThisGuidListProperty =
			AvaloniaProperty.Register<DependencyControl, List<Guid>>(nameof(ThisGuidList));

		[NotNull]
		public static readonly StyledProperty<Component> CurrentComponentProperty =
			AvaloniaProperty.Register<DependencyControl, Component>(nameof(CurrentComponent));

		[NotNull]
		public static readonly StyledProperty<ModManagementService> ModManagementServiceProperty =
			AvaloniaProperty.Register<DependencyControl, ModManagementService>(nameof(ModManagementService));

		[NotNull]
		public static readonly StyledProperty<DependencyType> DependencyTypeProperty =
			AvaloniaProperty.Register<DependencyControl, DependencyType>(nameof(DependencyType));

		public DependencyControl() => InitializeComponent();

		[NotNull]
		public List<Guid> ThisGuidList
		{
			get => GetValue(ThisGuidListProperty)
				?? throw new NullReferenceException("Could not retrieve property 'ThisGuidListProperty'");
			set => SetValue(ThisGuidListProperty, value);
		}

		[CanBeNull]
		public Component CurrentComponent
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

		[NotNull]
		[UsedImplicitly]
#pragma warning disable CA1822
		public List<Component> ThisComponentList => MainWindow.ComponentsList;
#pragma warning restore CA1822

		// used to fix the move window code with combo boxes.
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);

			if ( VisualRoot is MainWindow mainWindow )
				mainWindow.FindComboBoxesInWindow(mainWindow);
		}

	// ReSharper disable once UnusedParameter.Local
	private void AddToList_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
	{
		try
		{
			if ( !(sender is Button addButton) )
				throw new ArgumentException("Sender is not a Button.");
			if ( !(addButton.Tag is ComboBox comboBox) )
				throw new ArgumentException("Button doesn't have a proper ComboBox tag.");
			if ( !(comboBox.Tag is ListBox listBox) )
				throw new ArgumentException("ComboBox does not have a ListBox Tag.");

			if ( !(comboBox.SelectedItem is Component selectedComponent) )
				return; // no selection
			if ( ThisGuidList.Contains(selectedComponent.Guid) )
				return; // already in list.

			// Use service methods if available, otherwise fall back to direct manipulation
			bool added = false;
			if ( ModManagementService != null && CurrentComponent != null &&
			     (DependencyType == DependencyType.Dependency || DependencyType == DependencyType.Restriction) )
			{
				if ( DependencyType == DependencyType.Dependency )
					added = ModManagementService.AddDependency(CurrentComponent, selectedComponent);
				else if ( DependencyType == DependencyType.Restriction )
					added = ModManagementService.AddRestriction(CurrentComponent, selectedComponent);
			}
			else
			{
				// Fallback for InstallBefore/InstallAfter or when service is not available
				ThisGuidList.Add(selectedComponent.Guid);
				added = true;
			}

			if ( !added )
				return; // Addition failed or already exists

			var convertedItems = new GuidListToComponentNames().Convert(
				new object[]
				{
					ThisGuidList, MainWindow.ComponentsList,
				},
				ThisGuidList.GetType(),
				parameter: null,
				CultureInfo.CurrentCulture
			) as List<string>;

			listBox.ItemsSource = null;
			listBox.ItemsSource = new AvaloniaList<object>(convertedItems ?? throw new InvalidOperationException());

			comboBox.Tag = listBox;
			DependenciesListBox = listBox;

			listBox.InvalidateVisual();
			listBox.InvalidateArrange();
			listBox.InvalidateMeasure();
		}
		catch ( Exception exception )
		{
			Logger.LogException(exception);
		}
	}

	// ReSharper disable once UnusedParameter.Local
	private void RemoveFromList_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
	{
		try
		{
			if ( !(sender is Button removeButton) )
				throw new ArgumentException("Sender is not a Button.");
			if ( !(removeButton.Tag is ListBox listBox) )
				throw new ArgumentException("Button doesn't have a proper ListBox tag.");

			int index = listBox.SelectedIndex;
			if ( index < 0 || index >= ThisGuidList.Count )
				return; // no selection

			Guid guidToRemove = ThisGuidList[index];

			// Use service methods if available, otherwise fall back to direct manipulation
			bool removed = false;
			if ( ModManagementService != null && CurrentComponent != null &&
			     (DependencyType == DependencyType.Dependency || DependencyType == DependencyType.Restriction) )
			{
				// Find the component being removed
				Component componentToRemove = MainWindow.ComponentsList?.FirstOrDefault(c => c.Guid == guidToRemove);
				if ( componentToRemove != null )
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
				// Fallback for InstallBefore/InstallAfter or when service is not available
				ThisGuidList.RemoveAt(index);
				removed = true;
			}

			if ( !removed )
				return; // Removal failed

			var convertedItems = new GuidListToComponentNames().Convert(
				new object[]
				{
					ThisGuidList, MainWindow.ComponentsList,
				},
				ThisGuidList.GetType(),
				parameter: null,
				CultureInfo.CurrentCulture
			) as List<string>;

			listBox.ItemsSource = null;
			listBox.ItemsSource = new AvaloniaList<object>(convertedItems ?? throw new InvalidOperationException());

			listBox.InvalidateVisual();
			listBox.InvalidateArrange();
			listBox.InvalidateMeasure();
		}
		catch ( Exception exception )
		{
			Logger.LogException(exception);
		}
	} // ReSharper disable twice UnusedParameter.Local
		private void DependenciesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				object selectedItem = DependenciesComboBox.SelectedItem;
				if ( !(selectedItem is Component dropdownComponent) )
				{
					Logger.LogVerbose("selected item of this dependency component is not a Component!");
					return;
				}

				OptionsComboBox.ItemsSource = dropdownComponent.Options;
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}
	}
}
