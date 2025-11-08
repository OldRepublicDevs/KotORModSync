// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class BaseInstallCompletePage : WizardPageBase
    {
        private ItemsControl _summaryItems;

        public BaseInstallCompletePage()
        {
            InitializeComponent();
            InitializeSummary();
        }

        public override string Title => "Base Installation Complete";

        public override string Subtitle => "The base mod installation has finished successfully";

        public override bool CanNavigateBack => false;

        public override bool CanCancel => false;

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _summaryItems = this.FindControl<ItemsControl>("SummaryItems");
        }

        private void InitializeSummary()
        {
            if (_summaryItems is null)
            {
                return;
            }

            var summary = new List<SummaryItem>
            {
                new SummaryItem("Mods Installed", "TBD"),
                new SummaryItem("Time Elapsed", "TBD"),
                new SummaryItem("Checkpoints Created", "TBD"),
                new SummaryItem("Warnings", "TBD"),
                new SummaryItem("Errors", "TBD"),
            };

            _summaryItems.ItemsSource = summary;
        }

        private sealed class SummaryItem
        {
            public SummaryItem(string label, string value)
            {
                Label = label;
                Value = value;
            }

            public string Label { get; }

            public string Value { get; }
        }
    }
}


