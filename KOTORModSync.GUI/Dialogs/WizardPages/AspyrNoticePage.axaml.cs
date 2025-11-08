// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class AspyrNoticePage : WizardPageBase
    {
        private TextBlock _contentText;

        public AspyrNoticePage([NotNull] string aspyrContent)
        {
            InitializeComponent();
            SetContent(aspyrContent);
        }

        public override string Title => "Aspyr Version Notice";

        public override string Subtitle => "Important information about Aspyr-specific mods";

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _contentText = this.FindControl<TextBlock>("ContentText");
        }

        private void SetContent(string aspyrContent)
        {
            if (_contentText != null)
            {
                _contentText.Text = aspyrContent ?? string.Empty;
            }
        }

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));
    }
}


