// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using JetBrains.Annotations;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class AspyrNoticePage : IWizardPage
    {
        public string Title => "Aspyr Version Notice";
        public string Subtitle => "Important information about Aspyr-specific mods";
        public Control Content { get; }
        public bool CanNavigateBack => true;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        public AspyrNoticePage([NotNull] string aspyrContent)
        {
            var panel = new StackPanel
            {
                Spacing = 16
            };

            panel.Children.Add(new TextBlock
            {
                Text = "⚠️ Aspyr Version Notice",
                FontSize = 20,
                FontWeight = FontWeight.Bold
            });

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400
            };

            scrollViewer.Content = new TextBlock
            {
                Text = aspyrContent ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                LineHeight = 22
            };

            panel.Children.Add(scrollViewer);

            panel.Children.Add(new TextBlock
            {
                Text = "Please ensure you are using the correct game version before proceeding.",
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Opacity = 0.8
            });

            Content = panel;
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((true, (string)null));
        }
    }
}

