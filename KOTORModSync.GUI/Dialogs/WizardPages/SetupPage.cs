// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

            var panel = new StackPanel
            {
                Spacing = 16
            };

            // Instructions
            panel.Children.Add(new TextBlock
            {
                Text = "Configure your mod installation directories:",
                FontSize = 16,
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            });

            // Source directory
            panel.Children.Add(new TextBlock
            {
                Text = "Mod Files Directory (where downloaded mods are stored):",
                FontSize = 14
            });

            _sourcePathPicker = new DirectoryPickerControl
            {
                Height = 40,
                PickerType = DirectoryPickerType.ModDirectory
            };
            panel.Children.Add(_sourcePathPicker);

            // Destination directory
            panel.Children.Add(new TextBlock
            {
                Text = "Game Installation Directory (where KOTOR is installed):",
                FontSize = 14,
                Margin = new Avalonia.Thickness(0, 16, 0, 0)
            });

            _destinationPathPicker = new DirectoryPickerControl
            {
                Height = 40,
                PickerType = DirectoryPickerType.KotorDirectory
            };
            panel.Children.Add(_destinationPathPicker);

            // Status
            _statusText = new TextBlock
            {
                FontSize = 12,
                Opacity = 0.7,
                Margin = new Avalonia.Thickness(0, 16, 0, 0)
            };
            panel.Children.Add(_statusText);

            Content = panel;
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

