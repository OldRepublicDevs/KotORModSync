// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class WidescreenInstallingPage : WizardPageBase
    {
        public override string Title => "Installing Widescreen Mods";
        public override string Subtitle => "Please wait...";
        public override bool CanNavigateBack => false;
        public override bool CanNavigateForward => _canNavigateForward;

        private readonly List<ModComponent> _widescreenMods;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private ProgressBar _progressBar;
        private TextBlock _statusText;
        private bool _installationComplete;
        private bool _canNavigateForward;

        public WidescreenInstallingPage(
            [NotNull][ItemNotNull] List<ModComponent> widescreenMods,
            [NotNull] MainConfig mainConfig,
            [NotNull] CancellationTokenSource cancellationTokenSource)
        {
            _widescreenMods = widescreenMods ?? throw new ArgumentNullException(nameof(widescreenMods));
            if (mainConfig is null)
            {
                throw new ArgumentNullException(nameof(mainConfig));
            }
            _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));

            InitializeComponent();
            CacheControls();
            InitializeDefaults();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void CacheControls()
        {
            _progressBar = this.FindControl<ProgressBar>("ProgressBar");
            _statusText = this.FindControl<TextBlock>("StatusText");
        }

        private void InitializeDefaults()
        {
            if (_statusText != null)
            {
                _statusText.Text = "Installing widescreen mods...";
            }

            if (_progressBar != null)
            {
                _progressBar.Value = 0;
            }
        }

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override async Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            if (_installationComplete)
            {
                return;
            }

            await Task.Run(async () =>
            {
                var selectedMods = _widescreenMods.Where(m => m.IsSelected).ToList();

                for (int i = 0; i < selectedMods.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested || _cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }

                    ModComponent mod = selectedMods[i];

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_statusText != null)
                        {
                            _statusText.Text = $"Installing: {mod.Name} ({i + 1}/{selectedMods.Count})";
                        }

                        if (_progressBar != null)
                        {
                            _progressBar.Value = selectedMods.Count == 0 ? 0 : (double)i / selectedMods.Count;
                        }
                    });

                    await InstallationService.InstallSingleComponentAsync(mod, _widescreenMods, cancellationToken);
                }

                _installationComplete = true;
                _canNavigateForward = true;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_statusText != null)
                    {
                        _statusText.Text = "Widescreen installation complete!";
                    }

                    if (_progressBar != null)
                    {
                        _progressBar.Value = 1;
                    }
                });
            }, cancellationToken);
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            if (!_installationComplete)
            {
                return Task.FromResult((false, "Installation in progress"));
            }

            return Task.FromResult((true, (string)null));
        }
    }
}


