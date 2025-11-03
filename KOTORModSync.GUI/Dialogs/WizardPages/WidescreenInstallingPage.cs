// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class WidescreenInstallingPage : IWizardPage
    {
        public string Title => "Installing Widescreen Mods";
        public string Subtitle => "Please wait...";
        public Control Content { get; }
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => _canNavigateForward;
        public bool CanCancel => true;

        private readonly List<ModComponent> _widescreenMods;
        private readonly MainConfig _mainConfig;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ProgressBar _progressBar;
        private readonly TextBlock _statusText;
        private bool _installationComplete;
        private bool _canNavigateForward;

        public WidescreenInstallingPage(
            [NotNull][ItemNotNull] List<ModComponent> widescreenMods,
            [NotNull] MainConfig mainConfig,
            [NotNull] CancellationTokenSource cancellationTokenSource)
        {
            _widescreenMods = widescreenMods ?? throw new ArgumentNullException(nameof(widescreenMods));
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
            _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));

            var panel = new StackPanel { Spacing = 16 };
            _statusText = new TextBlock { Text = "Installing widescreen mods...", FontSize = 16 };
            _progressBar = new ProgressBar { Height = 18, Minimum = 0, Maximum = 1, Value = 0 };

            panel.Children.Add(_statusText);
            panel.Children.Add(_progressBar);
            Content = panel;
        }

        public async Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            if (_installationComplete)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                var selectedMods = _widescreenMods.Where(m => m.IsSelected).ToList();
                for (int i = 0; i < selectedMods.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var mod = selectedMods[i];
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _statusText.Text = $"Installing: {mod.Name} ({i + 1}/{selectedMods.Count})";
                        _progressBar.Value = (double)i / selectedMods.Count;
                    });

                    await InstallationService.InstallSingleComponentAsync(mod, _widescreenMods, cancellationToken);
                }

                _installationComplete = true;
                _canNavigateForward = true;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _statusText.Text = "Widescreen installation complete!";
                    _progressBar.Value = 1;
                });
            }, cancellationToken);
        }

        public Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            if (!_installationComplete)
            {
                return Task.FromResult((false, "Installation in progress"));
            }

            return Task.FromResult((true, (string)null));
        }
    }
}

