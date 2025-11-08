// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace KOTORModSync.Dialogs
{
    public partial class ProgressDialog : Window
    {
        public ProgressDialog()
        {
            InitializeComponent();
            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        public ProgressDialog(string title, string message) : this()
        {
            TextBlock titleText = this.FindControl<TextBlock>("TitleText");
            TextBlock messageText = this.FindControl<TextBlock>("MessageText");

            if (titleText != null)
            {
                titleText.Text = title;
            }

            if (messageText != null)
            {
                messageText.Text = message;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void UpdateProgress(string message, int current, int total)
        {
            Dispatcher.UIThread.Post(() =>
            {
                TextBlock messageText = this.FindControl<TextBlock>("MessageText");
                ProgressBar progressBar = this.FindControl<ProgressBar>("ProgressBar");

                if (messageText != null)
                {
                    messageText.Text = message;
                }

                if (progressBar != null && total > 0)
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Maximum = total;
                    progressBar.Value = current;
                }
            });
        }

        public async Task UpdateProgressAsync(string message, int current, int total)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                TextBlock messageText = this.FindControl<TextBlock>("MessageText");
                ProgressBar progressBar = this.FindControl<ProgressBar>("ProgressBar");

                if (messageText != null)
                {
                    messageText.Text = message;
                }

                if (progressBar != null && total > 0)
                {
                    progressBar.IsIndeterminate = false;
                    progressBar.Maximum = total;
                    progressBar.Value = current;
                }
            });
        }
    }
}
