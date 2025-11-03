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
    public class FinishedPage : IWizardPage
    {
        public string Title => "Installation Complete";
        public string Subtitle => "Thank you for using KOTORModSync!";
        public Control Content { get; }
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => false;
        public bool CanCancel => false;

        public FinishedPage()
        {
            var panel = new StackPanel
            {
                Spacing = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            panel.Children.Add(new TextBlock
            {
                Text = "🎉 Installation Complete!",
                FontSize = 32,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
            });

            panel.Children.Add(new TextBlock
            {
                Text = "All mods have been successfully installed.",
                FontSize = 18,
                TextAlignment = TextAlignment.Center,
            });

            var tipsBorder = new Border
            {
                Padding = new Avalonia.Thickness(24),
                CornerRadius = new Avalonia.CornerRadius(8),
                MaxWidth = 600,
            };

            var tipsPanel = new StackPanel
            {
                Spacing = 12,
            };

            tipsPanel.Children.Add(new TextBlock
            {
                Text = "🎮 Next Steps:",
                FontSize = 20,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            tipsPanel.Children.Add(new TextBlock
            {
                Text = "• Launch the game and enjoy your modded experience!",
                FontSize = 14,
            });

            tipsPanel.Children.Add(new TextBlock
            {
                Text = "• If you encounter issues, you can rollback to previous checkpoints",
                FontSize = 14,
            });

            tipsPanel.Children.Add(new TextBlock
            {
                Text = "• Check the output log for detailed installation information",
                FontSize = 14,
            });

            tipsBorder.Child = tipsPanel;
            panel.Children.Add(tipsBorder);

            panel.Children.Add(new Border
            {
                Padding = new Avalonia.Thickness(20),
                CornerRadius = new Avalonia.CornerRadius(8),
                MaxWidth = 500,
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "💝 Support the Project",
                            FontSize = 18,
                            FontWeight = FontWeight.SemiBold,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "If you found KOTORModSync helpful, consider supporting the project!",
                            FontSize = 14,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = "Visit our sponsor page to learn more.",
                            FontSize= 12,
                            Opacity = 0.8,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                },
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Click 'Finish' to close the wizard.",
                FontSize = 14,
                Opacity = 0.7,
                TextAlignment = TextAlignment.Center,
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

