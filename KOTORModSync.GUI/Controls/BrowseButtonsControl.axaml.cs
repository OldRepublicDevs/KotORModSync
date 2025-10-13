



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

		private void BrowseSourceFiles_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => BrowseSourceFiles?.Invoke(sender, e);

		private void BrowseSourceFromFolders_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => BrowseSourceFromFolders?.Invoke(sender, e);
	}
}
