// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Controls;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class GameDirectoryPage : WizardPageBase
    {
        private readonly MainConfig _mainConfig;
        private DirectoryPickerControl _destinationPathPicker;
        private TextBlock _statusText;

        public GameDirectoryPage([NotNull] MainConfig mainConfig)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            InitializeComponent();
            HookEvents();
            UpdateStatus();
        }

        public override string Title => "Set Game Directory";

        public override string Subtitle => "Point to your KOTOR installation folder";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_mainConfig.destinationPathFullName))
            {
                _destinationPathPicker.SetCurrentPath(_mainConfig.destinationPathFullName);
            }

            UpdateStatus();
            return Task.CompletedTask;
        }

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            string destPath = _destinationPathPicker.GetCurrentPath();
            if (!string.IsNullOrEmpty(destPath) && Directory.Exists(destPath))
            {
                _mainConfig.destinationPath = new DirectoryInfo(destPath);
            }

            return Task.CompletedTask;
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            string destPath = _destinationPathPicker.GetCurrentPath();
            if (string.IsNullOrEmpty(destPath) || !Directory.Exists(destPath))
            {
                return Task.FromResult((false, "Please select a valid game installation directory."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _destinationPathPicker = this.FindControl<DirectoryPickerControl>("DestinationPathPicker");
            _statusText = this.FindControl<TextBlock>("StatusText");
        }

        private void HookEvents()
        {
            if (_destinationPathPicker != null)
            {
                _destinationPathPicker.DirectoryChanged += OnDirectoryChanged;
                ToolTip.SetTip(_destinationPathPicker, "Choose the folder that contains swkotor.exe or swkotor2.exe.");
            }
        }

        private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Path) || !Directory.Exists(e.Path))
            {
                UpdateStatus();
                return;
            }

            try
            {
                if (e.PickerType == DirectoryPickerType.KotorDirectory)
                {
                    _mainConfig.destinationPath = new DirectoryInfo(e.Path);
                }
            }
            catch (Exception)
            {
                // DirectoryInfo can throw for invalid paths; ignore and fall back to validation.
            }

            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(UpdateStatus);
                return;
            }

            if (_destinationPathPicker is null || _statusText is null)
            {
                return;
            }

            string destPath = _destinationPathPicker.GetCurrentPath();
            bool destValid = !string.IsNullOrEmpty(destPath) && Directory.Exists(destPath);

            _statusText.Text = destValid
                ? $"Game directory selected: {destPath}"
                : "Select your installed KOTOR game folder.";
        }
    }
}


