// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using JetBrains.Annotations;
using KOTORModSync.Converters;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class BeforeContentPage : IWizardPage
    {
        public string Title => "Before You Begin";
        public string Subtitle => "Important information before starting the installation";
        public Control Content { get; }
        public bool CanNavigateBack => true;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        public BeforeContentPage([NotNull] string beforeContent)
        {
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Padding = new Avalonia.Thickness(40, 20, 40, 20),
            };

            var mainPanel = new StackPanel
            {
                Spacing = 20,
                MaxWidth = 900,
            };

            // Icon and header
            var headerPanel = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 0, 20),
            };

            headerPanel.Children.Add(new TextBlock
            {
                Text = "📖",
                FontSize = 48,
                TextAlignment = TextAlignment.Center,
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = "Before You Begin",
                FontSize = 28,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
            });

            mainPanel.Children.Add(headerPanel);

            // Content card with markdown rendering
            var contentCard = new Border
            {
                Padding = new Avalonia.Thickness(32),
                CornerRadius = new Avalonia.CornerRadius(12),
            };

            // Use the markdown renderer to convert markdown to TextBlock
            var renderedContent = MarkdownRenderer.RenderToTextBlock(beforeContent ?? string.Empty);
            renderedContent.TextWrapping = TextWrapping.Wrap;
            renderedContent.LineHeight = 24;
            renderedContent.FontSize = 15;

            contentCard.Child = renderedContent;
            mainPanel.Children.Add(contentCard);

            // Additional tip
            var tipPanel = new Border
            {
                Padding = new Avalonia.Thickness(20),
                CornerRadius = new Avalonia.CornerRadius(8),
                Margin = new Avalonia.Thickness(0, 20, 0, 0),
            };

            var tipContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
            };

            tipContent.Children.Add(new TextBlock
            {
                Text = "💡",
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Top,
            });

            tipContent.Children.Add(new TextBlock
            {
                Text = "Take a moment to read through this information carefully. It contains important details that will help ensure a successful installation.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Opacity = 0.9,
            });

            tipPanel.Child = tipContent;
            mainPanel.Children.Add(tipPanel);

            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
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

