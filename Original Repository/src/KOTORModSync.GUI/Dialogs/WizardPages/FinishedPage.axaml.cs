// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class FinishedPage : WizardPageBase
    {
        public FinishedPage()
        {
            InitializeComponent();
        }

        public override string Title => "Installation Complete";

        public override string Subtitle => "Thank you for using KOTORModSync!";

        public override bool CanNavigateBack => false;

        public override bool CanNavigateForward => false;

        public override bool CanCancel => false;

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}


