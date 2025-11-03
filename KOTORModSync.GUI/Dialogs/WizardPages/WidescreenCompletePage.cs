// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class WidescreenCompletePage : IWizardPage
    {
        public string Title => "Widescreen Installation Complete";
        public string Subtitle => "All widescreen mods have been installed";
        public Control Content { get; }
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => true;
        public bool CanCancel => false;

        public WidescreenCompletePage()
        {
            var panel = new StackPanel { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = "✅ Widescreen Installation Complete!",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
            });
            panel.Children.Add(new TextBlock
            {
                Text = "All widescreen mods have been successfully installed.",
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
            });
            Content = panel;
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken) =>
            Task.FromResult((true, (string)null));
    }
}

