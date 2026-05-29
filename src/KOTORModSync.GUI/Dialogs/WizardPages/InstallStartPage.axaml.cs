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
            int selectedCount = _allComponents.Count(c => c.IsSelected && !c.WidescreenOnly);
            if (selectedCount == 0)
            {
                return Task.FromResult((false, "No mods are selected. Go back to Mod Selection and choose mods to install."));
            }

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
                _selectedModsText.Text = selectedMods.Count == 0
                    ? "No mods selected — go back to Mod Selection before continuing."
                    : $"📦 {selectedMods.Count} mods selected for installation";
            }

            if (_modListPanel == null)
            {
                return;
            }

            _modListPanel.Children.Clear();

            if (selectedMods.Count == 0)
            {
                _modListPanel.Children.Add(new TextBlock
                {
                    Text = "Use Mod Selection to choose at least one mod, then return here to review.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.85,
                });
                return;
            }

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


