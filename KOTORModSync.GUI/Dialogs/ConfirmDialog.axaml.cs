// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Dialogs
{
	public partial class ConfirmDialog : Window
	{
		public ConfirmDialog()
		{
			InitializeComponent();
		}

		public ConfirmDialog( string title, string message, string confirmText = "Confirm", string cancelText = "Cancel" ) : this()
		{
			Title = title;

			var titleText = this.FindControl<TextBlock>( "TitleText" );
			var messageText = this.FindControl<TextBlock>( "MessageText" );
			var confirmButton = this.FindControl<Button>( "ConfirmButton" );
			var cancelButton = this.FindControl<Button>( "CancelButton" );

			if (titleText != null)
				titleText.Text = title;

			if (messageText != null)
				messageText.Text = message;

			if (confirmButton != null)
				confirmButton.Content = confirmText;

			if (cancelButton != null)
				cancelButton.Content = cancelText;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load( this );
		}

		private void ConfirmButton_Click( object sender, RoutedEventArgs e )
		{
			Close( true );
		}

		private void CancelButton_Click( object sender, RoutedEventArgs e )
		{
			Close( false );
		}
	}
}