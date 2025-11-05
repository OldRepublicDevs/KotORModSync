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
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };

            // Container to center content with equal margins
            var contentContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Avalonia.Thickness(40, 20, 40, 20),
            };
            contentContainer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            contentContainer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            contentContainer.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var mainPanel = new StackPanel
            {
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 1200,
                MinWidth = 600,
            };

            Grid.SetColumn(mainPanel, 1);
            contentContainer.Children.Add(mainPanel);

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

            // Content card with markdown rendering - use RenderToPanel for block-level markdown support
            var contentCard = new Border
            {
                Padding = new Avalonia.Thickness(32),
                CornerRadius = new Avalonia.CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 600,
            };

            // Use the markdown renderer to convert markdown to Panel (supports headings, warning blocks, etc.)
            var renderedContent = MarkdownRenderer.RenderToPanel(
                beforeContent ?? string.Empty,
                url => Core.Utility.UrlUtilities.OpenUrl(url)
            );

            // Ensure rendered content expands to fill available width
            renderedContent.HorizontalAlignment = HorizontalAlignment.Stretch;

            contentCard.Child = renderedContent;
            mainPanel.Children.Add(contentCard);

            // Additional tip
            var tipPanel = new Border
            {
                Padding = new Avalonia.Thickness(20),
                CornerRadius = new Avalonia.CornerRadius(8),
                Margin = new Avalonia.Thickness(0, 20, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var tipContent = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            };

            var tipIcon = new TextBlock
            {
                Text = "💡",
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Avalonia.Thickness(0, 0, 12, 0),
            };

            var tipText = new TextBlock
            {
                Text = "Take a moment to read through this information carefully. It contains important details that will help ensure a successful installation.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Opacity = 0.9,
            };

            Grid.SetColumn(tipIcon, 0);
            Grid.SetColumn(tipText, 1);
            tipContent.Children.Add(tipIcon);
            tipContent.Children.Add(tipText);

            tipPanel.Child = tipContent;
            mainPanel.Children.Add(tipPanel);

            scrollViewer.Content = contentContainer;
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

