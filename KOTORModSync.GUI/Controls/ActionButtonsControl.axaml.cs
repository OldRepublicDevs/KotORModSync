



using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;

namespace KOTORModSync.Controls
{
	public partial class ActionButtonsControl : UserControl
	{
		public ActionButtonsControl() => InitializeComponent();

		public event EventHandler<RoutedEventArgs> DeleteItem;
		public event EventHandler<RoutedEventArgs> MoveItemUp;
		public event EventHandler<RoutedEventArgs> MoveItemDown;

		private void DeleteItem_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => DeleteItem?.Invoke(sender, e);

		private void MoveItemUp_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => MoveItemUp?.Invoke(sender, e);

		private void MoveItemDown_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => MoveItemDown?.Invoke(sender, e);
	}
}
