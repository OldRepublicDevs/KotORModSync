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
	public class WelcomePage : IWizardPage
	{
		public string Title => "Welcome";
		public string Subtitle => "Welcome to the KOTORModSync Installation Wizard";
		public Control Content { get; }
		public bool CanNavigateBack => false;
		public bool CanNavigateForward => true;
		public bool CanCancel => true;

		public WelcomePage()
		{
			var panel = new StackPanel
			{
				Spacing = 20,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};

			// Welcome message
			panel.Children.Add(new TextBlock
			{
				Text = "🎮 Welcome to KOTORModSync",
				FontSize = 32,
				FontWeight = FontWeight.Bold,
				TextAlignment = TextAlignment.Center
			});

			panel.Children.Add(new TextBlock
			{
				Text = "This wizard will guide you through the process of installing mods for Knights of the Old Republic.",
				FontSize = 16,
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
							Text = "What you'll do:",
							FontSize = 18,
							FontWeight = FontWeight.SemiBold
						},
						CreateBulletPoint("📁 Configure your game and mod directories"),
						CreateBulletPoint("🎯 Select the mods you want to install"),
						CreateBulletPoint("⬇️ Download required mod files"),
						CreateBulletPoint("✅ Validate your installation"),
						CreateBulletPoint("🚀 Install all mods automatically"),
					}
				}
			});

			panel.Children.Add(new TextBlock
			{
				Text = "⚠️ Important: Make sure you have a fresh installation of the game before proceeding.",
				FontSize = 14,
				TextAlignment = TextAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				MaxWidth = 600,
				FontWeight = FontWeight.SemiBold
			});

			panel.Children.Add(new TextBlock
			{
				Text = "Click 'Next' to begin the installation process.",
				FontSize = 14,
				TextAlignment = TextAlignment.Center,
				Opacity = 0.7
			});

			Content = panel;
		}

		private StackPanel CreateBulletPoint(string text)
		{
			return new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 8,
				Children =
				{
					new TextBlock
					{
						Text = text,
						FontSize = 14,
						TextWrapping = TextWrapping.Wrap
					}
				}
			};
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

