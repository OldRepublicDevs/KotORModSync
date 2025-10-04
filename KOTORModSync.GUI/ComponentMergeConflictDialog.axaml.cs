// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using Component = KOTORModSync.Core.Component;
using KOTORModSync.Core;

namespace KOTORModSync
{
	public partial class ComponentMergeConflictDialog : Window
	{
		public ComponentMergeConflictViewModel ViewModel { get; private set; }
		public bool UserConfirmed { get; private set; }
		public List<Component> MergedComponents { get; private set; }

		public ComponentMergeConflictDialog()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif
		}

		public ComponentMergeConflictDialog(
			[NotNull] List<Component> existingComponents,
			[NotNull] List<Component> incomingComponents,
			[NotNull] string existingSource,
			[NotNull] string incomingSource,
			[NotNull] Func<Component, Component, bool> matchFunc)
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif

			ViewModel = new ComponentMergeConflictViewModel(
				existingComponents,
				incomingComponents,
				existingSource,
				incomingSource,
				matchFunc);

			DataContext = ViewModel;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		private void MaximizeButton_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button maximizeButton) )
				return;

			if ( WindowState == WindowState.Maximized )
			{
				WindowState = WindowState.Normal;
				maximizeButton.Content = "▢";
			}
			else
			{
				WindowState = WindowState.Maximized;
				maximizeButton.Content = "▣";
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = false;
			Close();
		}

		private void Continue_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				MergedComponents = ViewModel.GetMergedComponents();
				UserConfirmed = true;
				Close();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error merging components");
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = false;
			Close();
		}

		private void OnItemClicked(object sender, Avalonia.Input.PointerPressedEventArgs e)
		{
			if ( !(sender is Border border) || !(border.DataContext is ComponentConflictItem item) )
				return;
			// Single click: highlight and show details
			if ( item.IsFromExisting )
				ViewModel.SelectedExistingItem = item;
			else
				ViewModel.SelectedIncomingItem = item;

			e.Handled = true;
		}

		private void OnItemContextRequested(object sender, RoutedEventArgs e)
		{
			// Select the item when context menu is requested
			if ( !(sender is Border border) || !(border.DataContext is ComponentConflictItem item) )
				return;
			if ( item.IsFromExisting )
				ViewModel.SelectedExistingItem = item;
			else
				ViewModel.SelectedIncomingItem = item;
		}

		private void LinkSelectedMenuItem_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( ViewModel?.LinkSelectedCommand?.CanExecute(null) == true )
					ViewModel.LinkSelectedCommand.Execute(null);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error linking selected components");
			}
		}

		private void UnlinkMenuItem_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( ViewModel?.UnlinkSelectedCommand?.CanExecute(null) == true )
					ViewModel.UnlinkSelectedCommand.Execute(null);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error unlinking component");
			}
		}

		private void UseThisGuid_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( !(sender is MenuItem menuItem) || !(menuItem.Parent is ContextMenu contextMenu) || !(contextMenu.Parent is Border border) )
					return;

				if ( !(border.DataContext is ComponentConflictItem item) )
					return;

				ViewModel.ChooseGuidForItem(item);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error choosing GUID");
			}
		}
	}
}

