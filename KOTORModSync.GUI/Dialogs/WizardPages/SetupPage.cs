// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using JetBrains.Annotations;
using KOTORModSync.Controls;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class SetupPage : IWizardPage
    {
        public string Title => "Setup";
        public string Subtitle => "Configure your directories and load instruction file";
        public Control Content { get; }
        public bool CanNavigateBack => true;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        private readonly MainConfig _mainConfig;
        private readonly DirectoryPickerControl _sourcePathPicker;
        private readonly DirectoryPickerControl _destinationPathPicker;
        private readonly TextBlock _statusText;

        public SetupPage([NotNull] MainConfig mainConfig)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Padding = new Thickness(40, 20, 40, 20),
            };

            var mainPanel = new StackPanel
            {
                Spacing = 24,
                MaxWidth = 900,
            };

            // Header
            var headerPanel = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
            };

            headerPanel.Children.Add(new TextBlock
            {
                Text = "⚙️",
                FontSize = 48,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = "Directory Setup",
                FontSize = 28,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
            });

            headerPanel.Children.Add(new TextBlock
            {
                Text = "Configure your workspace and game directories",
                FontSize = 16,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
                Opacity = 0.8,
            });

            mainPanel.Children.Add(headerPanel);

            // Step 1: Mod Workspace Directory Card
            var modDirCard = new Border
            {
                Padding = new Thickness(24),
                CornerRadius = new CornerRadius(12, 12, 12, 12),
            };

            var modDirPanel = new StackPanel { Spacing = 12 };

            var modDirHeader = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
            };

            modDirHeader.Children.Add(new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16, 16, 16, 16),
                Child = new TextBlock
                {
                    Text = "1",
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                },
            });

            modDirHeader.Children.Add(new TextBlock
            {
                Text = "📁 Mod Workspace Directory",
                FontSize = 20,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });

            modDirPanel.Children.Add(modDirHeader);

            modDirPanel.Children.Add(new TextBlock
            {
                Text = "Choose a folder where mod archives will be downloaded and processed. This should be a dedicated folder with plenty of space (at least 10GB recommended).",
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.9,
                LineHeight = 20,
            });

            var tipPanel1 = new Border
            {
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(6, 6, 6, 6),
                Margin = new Thickness(0, 8, 0, 0),
            };

            var tipContent1 = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
            };

            tipContent1.Children.Add(new TextBlock
            {
                Text = "💡",
                FontSize = 16,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            });

            tipContent1.Children.Add(new TextBlock
            {
                Text = "Tip: Create a new folder like 'C:\\KOTORMods' or use your Documents folder",
                FontSize = 13,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.8,
            });

            tipPanel1.Child = tipContent1;
            modDirPanel.Children.Add(tipPanel1);

            _sourcePathPicker = new DirectoryPickerControl
            {
                Height = 44,
                PickerType = DirectoryPickerType.ModDirectory,
                Margin = new Thickness(0, 12, 0, 0),
            };
            modDirPanel.Children.Add(_sourcePathPicker);

            modDirCard.Child = modDirPanel;
            mainPanel.Children.Add(modDirCard);

            // Step 2: KOTOR Installation Directory Card
            var kotorDirCard = new Border
            {
                Padding = new Thickness(24),
                CornerRadius = new CornerRadius(12, 12, 12, 12),
            };

            var kotorDirPanel = new StackPanel { Spacing = 12 };

            var kotorDirHeader = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
            };

            kotorDirHeader.Children.Add(new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16, 16, 16, 16),
                Child = new TextBlock
                {
                    Text = "2",
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                },
            });

            kotorDirHeader.Children.Add(new TextBlock
            {
                Text = "🎮 KOTOR Installation Directory",
                FontSize = 20,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });

            kotorDirPanel.Children.Add(kotorDirHeader);

            kotorDirPanel.Children.Add(new TextBlock
            {
                Text = "Select your KOTOR game installation folder. This is where the game executable (swkotor.exe or swkotor2.exe) is located.",
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.9,
                LineHeight = 20,
            });

            var tipPanel2 = new Border
            {
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(6, 6, 6, 6),
                Margin = new Thickness(0, 8, 0, 0),
            };

            var tipContent2 = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
            };

            tipContent2.Children.Add(new TextBlock
            {
                Text = "💡",
                FontSize = 16,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            });

            var commonPathsText = new TextBlock
            {
                FontSize = 13,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.8,
            };
            commonPathsText.Inlines.AddRange(new Avalonia.Controls.Documents.Inline[]
            {
                new Avalonia.Controls.Documents.Run { Text = "Common locations:\n• Steam: C:\\Program Files (x86)\\Steam\\steamapps\\common\\swkotor\n• GOG: C:\\GOG Games\\Star Wars - KotOR" },
            });

            tipContent2.Children.Add(commonPathsText);
            tipPanel2.Child = tipContent2;
            kotorDirPanel.Children.Add(tipPanel2);

            _destinationPathPicker = new DirectoryPickerControl
            {
                Height = 44,
                PickerType = DirectoryPickerType.KotorDirectory,
                Margin = new Thickness(0, 12, 0, 0),
            };
            kotorDirPanel.Children.Add(_destinationPathPicker);

            kotorDirCard.Child = kotorDirPanel;
            mainPanel.Children.Add(kotorDirCard);

            // Step 3: Load Instruction File Card
            var loadFileCard = new Border
            {
                Padding = new Thickness(24),
                CornerRadius = new CornerRadius(12, 12, 12, 12),
            };

            var loadFilePanel = new StackPanel { Spacing = 12 };

            var loadFileHeader = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 12,
            };

            loadFileHeader.Children.Add(new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16, 16, 16, 16),
                Child = new TextBlock
                {
                    Text = "3",
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                },
            });

            loadFileHeader.Children.Add(new TextBlock
            {
                Text = "📄 Load Instruction File",
                FontSize = 20,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });

            loadFilePanel.Children.Add(loadFileHeader);

            loadFilePanel.Children.Add(new TextBlock
            {
                Text = "Load a mod build configuration file (.toml) that contains the list of mods to install and their installation instructions.",
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.9,
                LineHeight = 20,
            });

            var loadFileButton = new Button
            {
                Content = "📂 Browse for Instruction File",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(16, 12),
                FontSize = 15,
            };
            loadFileButton.Click += (s, e) =>
            {
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow as MainWindow;
                    mainWindow?.LoadFile_Click(s, e);
                    UpdateStatus();
                }
            };
            loadFilePanel.Children.Add(loadFileButton);

            loadFileCard.Child = loadFilePanel;
            mainPanel.Children.Add(loadFileCard);

            // Status indicator
            var statusCard = new Border
            {
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(8, 8, 8, 8),
            };

            _statusText = new TextBlock
            {
                FontSize = 14,
                FontWeight = Avalonia.Media.FontWeight.Medium,
                TextAlignment = Avalonia.Media.TextAlignment.Center,
            };

            statusCard.Child = _statusText;
            mainPanel.Children.Add(statusCard);

            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        public async Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            // Pre-populate from MainConfig
            if (!string.IsNullOrEmpty(_mainConfig.sourcePathFullName))
            {
                _sourcePathPicker.SetCurrentPath(_mainConfig.sourcePathFullName);
            }

            if (!string.IsNullOrEmpty(_mainConfig.destinationPathFullName))
            {
                _destinationPathPicker.SetCurrentPath(_mainConfig.destinationPathFullName);
            }

            UpdateStatus();
            await Task.CompletedTask;
        }

        public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            // Save to MainConfig
            string sourcePath = _sourcePathPicker.GetCurrentPath();
            if (!string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath))
            {
                _mainConfig.sourcePath = new DirectoryInfo(sourcePath);
            }

            string destPath = _destinationPathPicker.GetCurrentPath();
            if (!string.IsNullOrEmpty(destPath) && Directory.Exists(destPath))
            {
                _mainConfig.destinationPath = new DirectoryInfo(destPath);
            }

            return Task.CompletedTask;
        }

        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            string sourcePath = _sourcePathPicker.GetCurrentPath();
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                return Task.FromResult((false, "Please select a valid mod files directory."));
            }

            string destPath = _destinationPathPicker.GetCurrentPath();
            if (string.IsNullOrEmpty(destPath) || !Directory.Exists(destPath))
            {
                return Task.FromResult((false, "Please select a valid game installation directory."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void UpdateStatus()
        {
            string sourcePath = _sourcePathPicker.GetCurrentPath();
            bool sourceValid = !string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath);
            string destPath = _destinationPathPicker.GetCurrentPath();
            bool destValid = !string.IsNullOrEmpty(destPath) && Directory.Exists(destPath);

            if (sourceValid && destValid)
            {
                _statusText.Text = "✅ Directories configured successfully";
            }
            else if (!sourceValid && !destValid)
            {
                _statusText.Text = "⚠️ Please select both directories";
            }
            else if (!sourceValid)
            {
                _statusText.Text = "⚠️ Please select a mod files directory";
            }
            else
            {
                _statusText.Text = "⚠️ Please select a game installation directory";
            }
        }
    }
}

