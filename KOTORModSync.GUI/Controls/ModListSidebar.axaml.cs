// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;

namespace KOTORModSync.Controls
{
	public partial class ModListSidebar : UserControl
	{
		public static readonly StyledProperty<string> SearchTextProperty =
			AvaloniaProperty.Register<ModListSidebar, string>(nameof(SearchText));

		public static readonly StyledProperty<bool> IsHorizontalLayoutProperty =
			AvaloniaProperty.Register<ModListSidebar, bool>(nameof(IsHorizontalLayout), defaultValue: true);

		public string SearchText
		{
			get => GetValue(SearchTextProperty);
			set => SetValue(SearchTextProperty, value);
		}

		public bool IsHorizontalLayout
		{
			get => GetValue(IsHorizontalLayoutProperty);
			set => SetValue(IsHorizontalLayoutProperty, value);
		}

		public event EventHandler<RoutedEventArgs> SelectAllRequested;
		public event EventHandler<RoutedEventArgs> DeselectAllRequested;
		public event EventHandler<RoutedEventArgs> SelectByTierRequested;
		public event EventHandler<RoutedEventArgs> ClearCategorySelectionRequested;
		public event EventHandler<RoutedEventArgs> ApplyCategorySelectionsRequested;

		// Vertical toolbar events
		public event EventHandler<RoutedEventArgs> RefreshListRequested;
		public event EventHandler<RoutedEventArgs> ValidateAllModsRequested;
		public event EventHandler<RoutedEventArgs> AutofetchInstructionsRequested;
		public event EventHandler<RoutedEventArgs> LockInstallOrderRequested;
		public event EventHandler<RoutedEventArgs> RemoveAllDependenciesRequested;
		public event EventHandler<RoutedEventArgs> AddNewModRequested;
		public event EventHandler<RoutedEventArgs> ModManagementToolsRequested;
		public event EventHandler<RoutedEventArgs> ModStatisticsRequested;
		public event EventHandler<RoutedEventArgs> SaveConfigRequested;
		public event EventHandler<RoutedEventArgs> CloseTOMLRequested;

		public ModListSidebar()
		{
			InitializeComponent();
		}

		public ListBox ModListBox => this.FindControl<ListBox>("ModListBoxElement");
		public TextBlock ModCountTextBlock => this.FindControl<TextBlock>("ModCountText");
		public TextBlock SelectedCountTextBlock => this.FindControl<TextBlock>("SelectedCountText");

		[UsedImplicitly]
		private void SelectAll_Click(object sender, RoutedEventArgs e)
		{
			SelectAllRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void DeselectAll_Click(object sender, RoutedEventArgs e)
		{
			DeselectAllRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void SelectByTier_Click(object sender, RoutedEventArgs e)
		{
			SelectByTierRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void ClearCategorySelection_Click(object sender, RoutedEventArgs e)
		{
			ClearCategorySelectionRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void ApplyCategorySelections_Click(object sender, RoutedEventArgs e)
		{
			ApplyCategorySelectionsRequested?.Invoke(sender, e);
		}

		// Vertical toolbar event handlers
		[UsedImplicitly]
		private void RefreshList_Click(object sender, RoutedEventArgs e)
		{
			RefreshListRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void ValidateAllMods_Click(object sender, RoutedEventArgs e)
		{
			ValidateAllModsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void AutofetchInstructions_Click(object sender, RoutedEventArgs e)
		{
			AutofetchInstructionsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void LockInstallOrder_Click(object sender, RoutedEventArgs e)
		{
			LockInstallOrderRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void RemoveAllDependencies_Click(object sender, RoutedEventArgs e)
		{
			RemoveAllDependenciesRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void AddNewMod_Click(object sender, RoutedEventArgs e)
		{
			AddNewModRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void ModManagementTools_Click(object sender, RoutedEventArgs e)
		{
			ModManagementToolsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void ModStatistics_Click(object sender, RoutedEventArgs e)
		{
			ModStatisticsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void SaveConfig_Click(object sender, RoutedEventArgs e)
		{
			SaveConfigRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void CloseTOML_Click(object sender, RoutedEventArgs e)
		{
			CloseTOMLRequested?.Invoke(sender, e);
		}
	}
}

