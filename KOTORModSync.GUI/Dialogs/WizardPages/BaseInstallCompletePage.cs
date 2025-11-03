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
    public class BaseInstallCompletePage : IWizardPage
    {
        public string Title => "Base Installation Complete";
        public string Subtitle => "The base mod installation has finished successfully";
        public Control Content { get; }
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => true;
        public bool CanCancel => false;

        public BaseInstallCompletePage()
        {
            var panel = new StackPanel
            {
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = "✅ Base Installation Complete!",
                FontSize = 28,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center
            });

            panel.Children.Add(new TextBlock
            {
                Text = "All selected mods have been installed successfully.",
                FontSize = 16,
                TextAlignment = TextAlignment.Center
            });

            var resultsBorder = new Border
            {
                Padding = new Avalonia.Thickness(24),
                CornerRadius = new Avalonia.CornerRadius(8),
                MaxWidth = 500
            };

            var resultsPanel = new StackPanel
            {
                Spacing = 12
            };

            resultsPanel.Children.Add(new TextBlock
            {
                Text = "📊 Installation Summary",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // TODO: Show actual statistics
            resultsPanel.Children.Add(CreateStatRow("Mods Installed", "TBD"));
            resultsPanel.Children.Add(CreateStatRow("Time Elapsed", "TBD"));
            resultsPanel.Children.Add(CreateStatRow("Checkpoints Created", "TBD"));
            resultsPanel.Children.Add(CreateStatRow("Warnings", "TBD"));
            resultsPanel.Children.Add(CreateStatRow("Errors", "TBD"));

            resultsBorder.Child = resultsPanel;
            panel.Children.Add(resultsBorder);

            panel.Children.Add(new TextBlock
            {
                Text = "Click 'Next' to continue...",
                FontSize = 14,
                Opacity = 0.7,
                TextAlignment = TextAlignment.Center
            });

            Content = panel;
        }

        private Grid CreateStatRow(string label, string value)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };

            grid.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 14
            });

            var valueText = new TextBlock
            {
                Text = value,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold
            };
            Grid.SetColumn(valueText, 1);
            grid.Children.Add(valueText);

            return grid;
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

