// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

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
