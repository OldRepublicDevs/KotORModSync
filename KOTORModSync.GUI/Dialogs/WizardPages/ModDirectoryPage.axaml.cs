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
        private Border _validationFeedback;
        private TextBlock _validationTitle;
        private TextBlock _validationMessage;

        public ModDirectoryPage()
            : this(new MainConfig())
        {
        }

        public ModDirectoryPage([NotNull] MainConfig mainConfig)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            InitializeComponent();
            CacheControls();
            HookEvents();
            UpdateValidation();
        }

        public override string Title => "Mod Workspace Directory";
        public override string Subtitle => "Choose where mod archives are downloaded and processed";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_mainConfig.sourcePathFullName))
            {
                _sourcePathPicker?.SetCurrentPath(_mainConfig.sourcePathFullName);
            }

            UpdateValidation();
            return Task.CompletedTask;
        }

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            string sourcePath = _sourcePathPicker?.GetCurrentPath();
            if (!string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath))
            {
                _mainConfig.sourcePath = new DirectoryInfo(sourcePath);
            }

            return Task.CompletedTask;
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            string sourcePath = _sourcePathPicker?.GetCurrentPath();
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                return Task.FromResult((false, "Please select a valid mod workspace directory."));
            }

            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void CacheControls()
        {
            _sourcePathPicker = this.FindControl<DirectoryPickerControl>("SourcePathPicker");
            _validationFeedback = this.FindControl<Border>("ValidationFeedback");
            _validationTitle = this.FindControl<TextBlock>("ValidationTitle");
            _validationMessage = this.FindControl<TextBlock>("ValidationMessage");
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
            if (string.IsNullOrWhiteSpace(e.Path))
            {
                UpdateValidation();
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

            UpdateValidation();
        }

        private void UpdateValidation()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(UpdateValidation);
                return;
            }

            if (_sourcePathPicker is null || _validationFeedback is null)
            {
                return;
            }

            string sourcePath = _sourcePathPicker.GetCurrentPath();
            bool isValid = !string.IsNullOrEmpty(sourcePath) && Directory.Exists(sourcePath);

            if (string.IsNullOrEmpty(sourcePath))
            {
                _validationFeedback.IsVisible = false;
                return;
            }

            _validationFeedback.IsVisible = true;

            if (isValid)
            {
                _validationTitle.Text = "✅ Valid Directory";
                _validationMessage.Text = $"Workspace directory set to: {sourcePath}";
            }
            else
            {
                _validationTitle.Text = "❌ Invalid Directory";
                _validationMessage.Text = "The specified directory does not exist. Please select an existing folder.";
            }
        }
    }
}


