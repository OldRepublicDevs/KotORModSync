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
    public class WelcomePage : IWizardPage
    {
        public string Title => "Welcome";
        public string Subtitle => "Welcome to the KOTORModSync Installation Wizard";
        public Control Content { get; }
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        public WelcomePage()
        {
            // Main container
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            };

            var mainPanel = new StackPanel
            {
                Spacing = 24,
                Margin = new Avalonia.Thickness(20, 20, 20, 20),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Hero Section
            var heroPanel = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            heroPanel.Children.Add(new TextBlock
            {
                Text = "🎮",
                FontSize = 48,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            heroPanel.Children.Add(new TextBlock
            {
                Text = "Welcome to KOTORModSync",
                FontSize = 32,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            heroPanel.Children.Add(new TextBlock
            {
                Text = "The Complete Mod Installation Solution for Knights of the Old Republic",
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap,
            });

            mainPanel.Children.Add(heroPanel);

            // Use DockPanel for side-by-side layout
            var contentPanel = new DockPanel
            {
                LastChildFill = false,
                Margin = new Avalonia.Thickness(0, 0, 0, 0),
            };

            // Left: Installation Overview
            var overviewCard = new Border
            {
                Padding = new Avalonia.Thickness(24),
                CornerRadius = new Avalonia.CornerRadius(12),
                Width = 400,
            };

            var overviewContent = new StackPanel { Spacing = 16 };
            overviewContent.Children.Add(new TextBlock
            {
                Text = "🚀 What This Wizard Will Do",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            var stepsPanel = new StackPanel { Spacing = 8 };
            stepsPanel.Children.Add(CreateStep("1", "📁", "Configure Directories", "Set up your game and mod workspace folders"));
            stepsPanel.Children.Add(CreateStep("2", "📄", "Load Instruction File", "Choose your mod build configuration"));
            stepsPanel.Children.Add(CreateStep("3", "🎯", "Select Mods", "Pick which mods you want to install"));
            stepsPanel.Children.Add(CreateStep("4", "⬇️", "Download Files", "Automatically fetch required mod archives"));
            stepsPanel.Children.Add(CreateStep("5", "✅", "Validate Setup", "Ensure everything is ready for installation"));
            stepsPanel.Children.Add(CreateStep("6", "⚡", "Install Mods", "Sit back while we install everything automatically"));

            overviewContent.Children.Add(stepsPanel);
            overviewCard.Child = overviewContent;
            DockPanel.SetDock(overviewCard, Dock.Left);
            contentPanel.Children.Add(overviewCard);

            // Right: Features and Notice
            var rightColumn = new StackPanel { Spacing = 16, Width = 400 };
            
            var noticeCard = new Border
            {
                Padding = new Avalonia.Thickness(20),
                CornerRadius = new Avalonia.CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var noticeContent = new StackPanel
            {
                Spacing = 8,
                Orientation = Orientation.Horizontal,
            };

            noticeContent.Children.Add(new TextBlock
            {
                Text = "⚠️",
                FontSize = 28,
                VerticalAlignment = VerticalAlignment.Top,
            });

            var noticeTextPanel = new StackPanel { Spacing = 6 };
            noticeTextPanel.Children.Add(new TextBlock
            {
                Text = "Important: Fresh Installation Required",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
            });
            noticeTextPanel.Children.Add(new TextBlock
            {
                Text = "For best results, start with a clean installation of KOTOR. If you've previously installed mods, uninstall the game completely and reinstall before proceeding.",
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.9,
            });

            noticeContent.Children.Add(noticeTextPanel);
            noticeCard.Child = noticeContent;
            rightColumn.Children.Add(noticeCard);

            var featuresPanel = new StackPanel { Spacing = 10 };
            featuresPanel.Children.Add(new TextBlock
            {
                Text = "✨ Key Features",
                FontSize = 18,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            var featuresGrid = new StackPanel { Spacing = 6 };
            featuresGrid.Children.Add(CreateFeature("🔄", "Automatic Downloads", "We'll fetch mod files for you"));
            featuresGrid.Children.Add(CreateFeature("🔍", "Smart Validation", "Catch issues before installation"));
            featuresGrid.Children.Add(CreateFeature("💾", "Checkpoint System", "Resume from where you left off"));
            featuresGrid.Children.Add(CreateFeature("🎨", "Theme Support", "Choose your preferred visual style"));

            featuresPanel.Children.Add(featuresGrid);
            rightColumn.Children.Add(featuresPanel);

            DockPanel.SetDock(rightColumn, Dock.Right);
            contentPanel.Children.Add(rightColumn);

            mainPanel.Children.Add(contentPanel);

            // CTA spanning bottom
            var ctaPanel = new StackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 8, 0, 0),
            };

            ctaPanel.Children.Add(new TextBlock
            {
                Text = "Ready to begin your modding adventure?",
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
            });

            ctaPanel.Children.Add(new TextBlock
            {
                Text = "Click 'Next' to get started with the installation wizard",
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                Opacity = 0.7,
            });

            mainPanel.Children.Add(ctaPanel);

            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        private Border CreateStep(string number, string icon, string title, string description)
        {
            var stepPanel = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
                Margin = new Avalonia.Thickness(0, 4, 0, 4),
            };

            // Step number
            var numberBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new Avalonia.CornerRadius(16),
                Margin = new Avalonia.Thickness(0, 0, 12, 0),
                Child = new TextBlock
                {
                    Text = number,
                    FontWeight = FontWeight.Bold,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
            };
            Grid.SetColumn(numberBorder, 0);
            stepPanel.Children.Add(numberBorder);

            // Icon
            var iconBlock = new TextBlock
            {
                Text = icon,
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 0, 12, 0),
            };
            Grid.SetColumn(iconBlock, 1);
            stepPanel.Children.Add(iconBlock);

            // Title and description
            var textPanel = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
            };

            textPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeight.SemiBold,
            });

            textPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 13,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });

            Grid.SetColumn(textPanel, 2);
            stepPanel.Children.Add(textPanel);

            return new Border
            {
                Padding = new Avalonia.Thickness(16, 12, 16, 12),
                CornerRadius = new Avalonia.CornerRadius(8, 8, 8, 8),
                Child = stepPanel,
            };
        }

        private StackPanel CreateFeature(string icon, string title, string description)
        {
            var featurePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
            };

            featurePanel.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var textPanel = new StackPanel { Spacing = 2 };
            textPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Opacity = 0.7,
            });

            featurePanel.Children.Add(textPanel);

            return featurePanel;
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

