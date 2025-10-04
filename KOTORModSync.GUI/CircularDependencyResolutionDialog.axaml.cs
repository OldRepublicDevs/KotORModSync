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
	public partial class CircularDependencyResolutionDialog : Window
	{
		public CircularDependencyResolutionViewModel ViewModel { get; }
		public bool UserRetried { get; private set; }
		public List<Component> ResolvedComponents => ViewModel?.Components
			.Where(c => c.IsSelected)
			.Select(c => c.Component)
			.ToList();

		public CircularDependencyResolutionDialog()
		{
			InitializeComponent();
		}

		public CircularDependencyResolutionDialog(List<Component> components, CircularDependencyDetector.CircularDependencyResult cycleInfo)
		{
			InitializeComponent();
			ViewModel = new CircularDependencyResolutionViewModel(components, cycleInfo);
			DataContext = ViewModel;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void Retry_Click(object sender, RoutedEventArgs e)
		{
			UserRetried = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			UserRetried = false;
			Close();
		}

		public static async System.Threading.Tasks.Task<(bool retry, List<Component> components)> ShowResolutionDialog(
			Window owner,
			List<Component> components,
			CircularDependencyDetector.CircularDependencyResult cycleInfo)
		{
			// Don't show dialog if there are no circular dependencies
			if (!cycleInfo.HasCircularDependencies || cycleInfo.Cycles.Count == 0)
			    return (false, components);

			var dialog = new CircularDependencyResolutionDialog(components, cycleInfo);
			await dialog.ShowDialog(owner);
			return (dialog.UserRetried, dialog.ResolvedComponents);
		}
	}
}

