// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KOTORModSync.Dialogs
{
    public partial class MessageDialog : Window
    {
        private readonly TextBlock _titleText;
        private readonly TextBlock _messageText;
        private readonly Button _okButton;

        public MessageDialog()
        {
            InitializeComponent();
        }

        public MessageDialog(string title, string message, string buttonText = "OK")
            : this()
        {
            // Assign named controls after the XAML is loaded
            _titleText = this.FindControl<TextBlock>("titleText");
            _messageText = this.FindControl<TextBlock>("messageText");
            _okButton = this.FindControl<Button>("okButton");

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Title = title;

                    if (_titleText != null)
                        _titleText.Text = title;

                    if (_messageText != null)
                        _messageText.Text = message;

                    if (_okButton != null)
                        _okButton.Content = buttonText;
                });
                return;
            }
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
