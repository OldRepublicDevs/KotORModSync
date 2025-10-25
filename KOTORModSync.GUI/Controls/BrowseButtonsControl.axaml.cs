// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;

namespace KOTORModSync.Controls
{
	public partial class BrowseButtonsControl : UserControl
	{
		public BrowseButtonsControl() => InitializeComponent();

		public event EventHandler<RoutedEventArgs> BrowseSourceFiles;
		public event EventHandler<RoutedEventArgs> BrowseSourceFromFolders;

		private void BrowseSourceFiles_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			Core.Logger.LogVerbose("BrowseButtonsControl.BrowseSourceFiles_Click: Event triggered");
			BrowseSourceFiles?.Invoke(sender, e);
		}

		private void BrowseSourceFromFolders_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			Core.Logger.LogVerbose("BrowseButtonsControl.BrowseSourceFromFolders_Click: Event triggered");
			BrowseSourceFromFolders?.Invoke(sender, e);
		}
	}
}
