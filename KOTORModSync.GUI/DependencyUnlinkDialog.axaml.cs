// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using KOTORModSync.Core;

namespace KOTORModSync
{
	public partial class DependencyUnlinkDialog : Window
	{
		public DependencyUnlinkViewModel ViewModel { get; }
		public bool UserConfirmed { get; private set; }
		public List<Component> ComponentsToUnlink => ViewModel?.DependentComponents
			.Where(c => c.IsSelected)
			.Select(c => c.Component)
			.ToList();

		public DependencyUnlinkDialog()
		{
			InitializeComponent();
		}

		public DependencyUnlinkDialog(Component componentToDelete, List<Component> dependentComponents)
		{
			InitializeComponent();
			ViewModel = new DependencyUnlinkViewModel(componentToDelete, dependentComponents);
			DataContext = ViewModel;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void UnlinkAndDelete_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = false;
			Close();
		}

		public static async System.Threading.Tasks.Task<(bool confirmed, List<Component> componentsToUnlink)> ShowUnlinkDialog(
			Window owner,
			Component componentToDelete,
			List<Component> dependentComponents)
		{
			// Don't show dialog if there are no dependent components
			if (dependentComponents == null || !dependentComponents.Any())
				return (true, new List<Component>());

			var dialog = new DependencyUnlinkDialog(componentToDelete, dependentComponents);
			await dialog.ShowDialog(owner);
			return (dialog.UserConfirmed, dialog.ComponentsToUnlink);
		}
	}
}
