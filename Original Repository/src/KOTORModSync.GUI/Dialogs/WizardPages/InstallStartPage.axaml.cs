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
using Avalonia.Media;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class InstallStartPage : WizardPageBase
    {
        private readonly List<ModComponent> _allComponents;
        private TextBlock _selectedModsText;
        private StackPanel _modListPanel;

        public InstallStartPage()
            : this(new List<ModComponent>())
        {
        }

        public InstallStartPage([NotNull][ItemNotNull] List<ModComponent> allComponents)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));

            InitializeComponent();
            RefreshSummary();
        }

        public override string Title => "Ready to Install";

        public override string Subtitle => "Review your selections and begin installation";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            RefreshSummary();
            return Task.CompletedTask;
        }

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((true, (string)null));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _selectedModsText = this.FindControl<TextBlock>("SelectedModsText");
            _modListPanel = this.FindControl<StackPanel>("ModListPanel");
        }

        private void RefreshSummary()
        {
            var selectedMods = _allComponents.Where(c => c.IsSelected && !c.WidescreenOnly).ToList();

            if (_selectedModsText != null)
            {
                _selectedModsText.Text = $"📦 {selectedMods.Count} mods selected for installation";
            }

            if (_modListPanel == null)
            {
                return;
            }

            _modListPanel.Children.Clear();

            foreach (ModComponent mod in selectedMods)
            {
                _modListPanel.Children.Add(new TextBlock
                {
                    Text = $"• {mod.Name}",
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }
    }
}


