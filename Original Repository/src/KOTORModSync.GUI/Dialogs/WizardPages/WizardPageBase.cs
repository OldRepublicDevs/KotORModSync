// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace KOTORModSync.Dialogs.WizardPages
{
    /// <summary>
    /// Base class for wizard pages that leverages Avalonia XAML partial views.
    /// Provides sensible defaults for navigation behaviour and lifecycle hooks.
    /// </summary>
    public abstract class WizardPageBase : UserControl, IWizardPage
    {
        public abstract string Title { get; }

        public abstract string Subtitle { get; }

        public virtual bool CanNavigateBack => true;

        public virtual bool CanNavigateForward => true;

        public virtual bool CanCancel => true;

        Control IWizardPage.Content => this;

        public virtual Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public virtual Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public virtual Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult<(bool, string)>((true, null));
    }
}


