// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class WidescreenNoticePage : WizardPageBase
    {
        private TextBlock _contentText;

        public WidescreenNoticePage()
            : this(string.Empty)
        {
        }

        public WidescreenNoticePage([NotNull] string widescreenContent)
        {
            InitializeComponent();
            SetContent(widescreenContent);
        }

        public override string Title => "Widescreen Support";

        public override string Subtitle => "Information about widescreen mod installation";

        public override bool CanNavigateBack => false;

        public override bool CanCancel => false;

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _contentText = this.FindControl<TextBlock>("ContentText");
        }

        private void SetContent(string widescreenContent)
        {
            if (_contentText != null)
            {
                _contentText.Text = widescreenContent ?? string.Empty;
            }
        }

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));
    }
}


