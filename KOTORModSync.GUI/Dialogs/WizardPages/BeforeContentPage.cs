// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using JetBrains.Annotations;

namespace KOTORModSync.Dialogs.WizardPages
{
	public class BeforeContentPage : IWizardPage
	{
		public string Title => "Before You Begin";
		public string Subtitle => "Important information before starting the installation";
		public Control Content { get; }
		public bool CanNavigateBack => true;
		public bool CanNavigateForward => true;
		public bool CanCancel => true;

		public BeforeContentPage([NotNull] string beforeContent)
		{
			var scrollViewer = new ScrollViewer
			{
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};

			var textBlock = new TextBlock
			{
				Text = beforeContent ?? string.Empty,
				TextWrapping = TextWrapping.Wrap,
				FontSize = 14,
				LineHeight = 22
			};

			scrollViewer.Content = textBlock;
			Content = scrollViewer;
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

