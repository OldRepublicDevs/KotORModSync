// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using KOTORModSync.Converters;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class PreamblePage : WizardPageBase
    {
        private ScrollViewer _contentScrollViewer;

        public PreamblePage([NotNull] string beforeContent)
        {
            InitializeComponent();
            RenderContent(beforeContent);
        }

        public override string Title => "Before You Begin";

        public override string Subtitle => "Important information before starting the installation";

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _contentScrollViewer = this.FindControl<ScrollViewer>("ContentScrollViewer");
        }

        private void RenderContent(string beforeContent)
        {
            if (_contentScrollViewer is null)
            {
                return;
            }

            Panel renderedContent = MarkdownRenderer.RenderToPanel(
                beforeContent ?? string.Empty,
                url => Core.Utility.UrlUtilities.OpenUrl(url)
            );

            renderedContent.HorizontalAlignment = HorizontalAlignment.Stretch;
            _contentScrollViewer.Content = renderedContent;
        }

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task OnNavigatingFromAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
            => Task.FromResult((true, (string)null));
    }
}


