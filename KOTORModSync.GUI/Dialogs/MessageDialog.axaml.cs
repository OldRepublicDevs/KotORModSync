// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Dialogs
{
	public partial class MessageDialog : Window
	{
		public MessageDialog()
		{
			InitializeComponent();
		}

		public MessageDialog(string title, string message, string buttonText = "OK") : this()
		{
			Title = title;

			var titleText = this.FindControl<TextBlock>("TitleText");
			var messageText = this.FindControl<TextBox>("MessageText");
			var okButton = this.FindControl<Button>("OkButton");

			if ( titleText != null )
				titleText.Text = title;

			if ( messageText != null )
				messageText.Text = message;

			if ( okButton != null )
				okButton.Content = buttonText;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}

