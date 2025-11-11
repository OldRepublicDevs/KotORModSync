// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class WidescreenModSelectionPage : WizardPageBase
    {
        private readonly List<ModComponent> _widescreenMods;
        private StackPanel _modListPanel;

        public WidescreenModSelectionPage()
            : this(new List<ModComponent>())
        {
        }

        public WidescreenModSelectionPage([NotNull][ItemNotNull] List<ModComponent> widescreenMods)
        {
            _widescreenMods = widescreenMods ?? throw new ArgumentNullException(nameof(widescreenMods));

            InitializeComponent();
            CacheControls();
            BuildModList();
        }

        public override string Title => "Widescreen Mod Selection";

        public override string Subtitle => "Select widescreen mods to install";

        public override bool CanNavigateBack => false;

        public override bool CanCancel => false;

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void CacheControls()
        {
            _modListPanel = this.FindControl<StackPanel>("ModListPanel");
        }

        private void BuildModList()
        {
            if (_modListPanel is null)
            {
                return;
            }

            _modListPanel.Children.Clear();

            foreach (ModComponent mod in _widescreenMods)
            {
                var checkBox = new CheckBox
                {
                    Content = mod.Name,
                    IsChecked = mod.IsSelected,
                    Tag = mod,
                };

                checkBox.IsCheckedChanged += (_, __) =>
                {
                    if (checkBox.Tag is ModComponent comp)
                    {
                        comp.IsSelected = checkBox.IsChecked == true;
                    }
                };

                _modListPanel.Children.Add(checkBox);
            }
        }

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));
    }
}


