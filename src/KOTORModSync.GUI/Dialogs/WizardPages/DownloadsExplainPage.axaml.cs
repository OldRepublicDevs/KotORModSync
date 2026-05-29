// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class DownloadsExplainPage : WizardPageBase
    {
        private readonly List<ModComponent> _allComponents;
        private TextBlock _selectionSummaryText;

        public DownloadsExplainPage()
            : this(new List<ModComponent>())
        {
        }

        public DownloadsExplainPage([NotNull][ItemNotNull] List<ModComponent> allComponents)
        {
            _allComponents = allComponents ?? throw new System.ArgumentNullException(nameof(allComponents));
            InitializeComponent();
            _selectionSummaryText = this.FindControl<TextBlock>("SelectionSummaryText");
        }

        public override string Title => "Download Process";

        public override string Subtitle => "Downloading required mod files";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            int selected = _allComponents.Count(c => c.IsSelected && !c.WidescreenOnly);
            if (_selectionSummaryText != null)
            {
                _selectionSummaryText.Text = selected == 0
                    ? "No mods are selected yet — downloads will only cover mods you chose on the previous step."
                    : $"Downloads will be requested for {selected} selected mod(s). You can continue while files download in the background.";
            }

            return Task.CompletedTask;
        }

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
