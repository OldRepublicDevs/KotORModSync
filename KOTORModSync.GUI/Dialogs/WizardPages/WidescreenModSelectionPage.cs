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
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class WidescreenModSelectionPage : IWizardPage
    {
        public string Title => "Widescreen Mod Selection";
        public string Subtitle => "Select widescreen mods to install";
        public Control Content { get; }
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => true;
        public bool CanCancel => false;

        private readonly List<ModComponent> _widescreenMods;

        public WidescreenModSelectionPage([NotNull][ItemNotNull] List<ModComponent> widescreenMods)
        {
            _widescreenMods = widescreenMods ?? throw new ArgumentNullException(nameof(widescreenMods));
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = "Select widescreen mods:", FontSize = 16 });

            foreach (var mod in _widescreenMods)
            {
                var checkBox = new CheckBox { Content = mod.Name, IsChecked = mod.IsSelected, Tag = mod };
                checkBox.IsCheckedChanged += (s, e) =>
                {
                    if (checkBox.Tag is ModComponent comp)
                    {
                        comp.IsSelected = checkBox.IsChecked == true;
                    }
                };
                panel.Children.Add(checkBox);
            }

            Content = panel;
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken) =>
            Task.FromResult((true, (string)null));
    }
}

