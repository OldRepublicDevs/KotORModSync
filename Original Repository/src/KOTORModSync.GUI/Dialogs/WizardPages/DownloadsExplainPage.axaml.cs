// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class DownloadsExplainPage : WizardPageBase
    {
        public DownloadsExplainPage()
        {
            InitializeComponent();
        }

        public override string Title => "Download Process";

        public override string Subtitle => "Downloading required mod files";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}


