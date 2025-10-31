// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace KOTORModSync.Dialogs.WizardPages
{
	public class DownloadsExplainPage : IWizardPage
	{
		public string Title => "Download Process";
		public string Subtitle => "Downloading required mod files";
		public Control Content { get; }
		public bool CanNavigateBack => true;
		public bool CanNavigateForward => true;
		public bool CanCancel => true;

		public DownloadsExplainPage()
		{
			var panel = new StackPanel
			{
				Spacing = 16,
				HorizontalAlignment = HorizontalAlignment.Center
			};

			panel.Children.Add(new TextBlock
			{
				Text = "⬇️ Downloading Mod Files",
				FontSize = 24,
				FontWeight = FontWeight.Bold,
				TextAlignment = TextAlignment.Center
			});

			panel.Children.Add(new TextBlock
			{
				Text = "KOTORModSync will now begin downloading the required mod files in the background.",
				FontSize = 14,
				TextAlignment = TextAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				MaxWidth = 600
			});

			panel.Children.Add(new Border
			{
				Padding = new Avalonia.Thickness(16),
				CornerRadius = new Avalonia.CornerRadius(8),
				Child = new StackPanel
				{
					Spacing = 12,
					Children =
					{
						new TextBlock
						{
							Text = "What's happening:",
							FontSize = 16,
							FontWeight = FontWeight.SemiBold
						},
						new TextBlock
						{
							Text = "• Mod files are being downloaded from their respective sources",
							FontSize = 14
						},
						new TextBlock
						{
							Text = "• Downloads will continue while you proceed through the wizard",
							FontSize = 14
						},
						new TextBlock
						{
							Text = "• You can view download progress using the 'Show Downloads' button",
							FontSize = 14
						},
						new TextBlock
						{
							Text = "• Installation will wait for downloads to complete if needed",
							FontSize = 14
						}
					}
				}
			});

			panel.Children.Add(new TextBlock
			{
				Text = "💡 Tip: You can continue to the next steps while downloads are in progress.",
				FontSize = 14,
				TextAlignment = TextAlignment.Center,
				FontWeight = FontWeight.SemiBold,
				Opacity = 0.8
			});

			Content = panel;
		}

		public async Task OnNavigatedToAsync(CancellationToken cancellationToken)
		{
			// TODO: Kick off background downloads here
			await Task.CompletedTask;
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

