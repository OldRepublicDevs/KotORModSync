// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class WelcomePage : UserControl, IWizardPage
    {
        public string Title => "Welcome";
        public string Subtitle => "Welcome to the KOTORModSync Installation Wizard";
        Control IWizardPage.Content => this;
        public bool CanNavigateBack => false;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        public WelcomePage()
        {
            InitializeComponent();
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((true, (string)null));
        }
    }
}

