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
    public partial class ModDirectoryPage : WizardPageBase
    {
        private readonly MainConfig _mainConfig;
        private DirectoryPickerControl _sourcePathPicker;
        private TextBlock _statusText;

        public ModDirectoryPage([NotNull] MainConfig mainConfig)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            InitializeComponent();
            HookEvents();
            UpdateStatus();
        }

        public override string Title => "Mod Workspace Directory";

        public override string Subtitle => "Choose where mod archives are downloaded and processed";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_mainConfig.sourcePathFullName))
            {
                _sourcePathPicker.SetCurrentPath(_mainConfig.sourcePathFullName);
            }

            UpdateStatus();
            return Task.CompletedTask;
        }

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            string sourcePath = _sourcePathPicker.GetCurrentPath();
            if (!string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath))
            {
                _mainConfig.sourcePath = new DirectoryInfo(sourcePath);
            }

            return Task.CompletedTask;
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            string sourcePath = _sourcePathPicker.GetCurrentPath();
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                return Task.FromResult((false, "Please select a valid mod workspace directory."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _sourcePathPicker = this.FindControl<DirectoryPickerControl>("SourcePathPicker");
            _statusText = this.FindControl<TextBlock>("StatusText");
        }

        private void HookEvents()
        {
            if (_sourcePathPicker != null)
            {
                _sourcePathPicker.DirectoryChanged += OnDirectoryChanged;
                ToolTip.SetTip(_sourcePathPicker, "Select the folder where mod archives are stored and processed.");
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
                if (e.PickerType == DirectoryPickerType.ModDirectory)
                {
                    _mainConfig.sourcePath = new DirectoryInfo(e.Path);
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

            if (_sourcePathPicker is null || _statusText is null)
            {
                return;
            }

            string sourcePath = _sourcePathPicker.GetCurrentPath();
            bool sourceValid = !string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath);

            _statusText.Text = sourceValid
                ? $"Workspace directory selected: {sourcePath}"
                : "Select a folder to use as your mod workspace.";
        }
    }
}


