// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using JetBrains.Annotations;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class WidescreenNoticePage : IWizardPage
    {
        public string Title => "Widescreen Support";
        public string Subtitle => "Information about widescreen mod installation";
        public Control Content { get; }
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => true;
        public bool CanCancel => false;

        public WidescreenNoticePage([NotNull] string widescreenContent)
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(new TextBlock { Text = "🖥️ Widescreen Mods", FontSize = 22, FontWeight = FontWeight.Bold });
            panel.Children.Add(new ScrollViewer
            {
                MaxHeight = 350,
                Content = new TextBlock { Text = widescreenContent ?? string.Empty, TextWrapping = TextWrapping.Wrap, FontSize = 14 },
            });
            Content = panel;
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken) =>
            Task.FromResult((true, (string)null));
    }
}

