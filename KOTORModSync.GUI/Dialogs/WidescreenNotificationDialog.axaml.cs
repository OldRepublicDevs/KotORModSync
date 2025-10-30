// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

using KOTORModSync.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs
{
	public partial class WidescreenNotificationDialog : Window
	{
		public bool DontShowAgain { get; private set; }
		public bool UserCancelled { get; private set; }

		public WidescreenNotificationDialog()
		{
			InitializeComponent();
		}

		public WidescreenNotificationDialog(string widescreenContent) : this()
		{
			LoadContent(widescreenContent);
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private void LoadContent(string widescreenContent)
		{
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					var contentTextBlock = this.FindControl<TextBlock>("ContentTextBlock");
					if (contentTextBlock != null && !string.IsNullOrWhiteSpace(widescreenContent))
					{

						var renderedTextBlock = MarkdownRenderer.RenderToTextBlock(
							widescreenContent,
							url => Core.Utility.UrlUtilities.OpenUrl(url)
						);

						if (renderedTextBlock?.Inlines != null)
						{
							contentTextBlock.Inlines.Clear();
							contentTextBlock.Inlines.AddRange(renderedTextBlock.Inlines);

							// Ensure the ContentTextBlock has the proper foreground color for the current theme
							string currentTheme = ThemeManager.GetCurrentStylePath();
							if (currentTheme.Contains("FluentLightStyle"))
								contentTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)); // #212121
							else if (currentTheme.Contains("Kotor2Style"))
								contentTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0xae, 0x88)); // #18ae88
							else if (currentTheme.Contains("KotorStyle"))
								contentTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0xAA, 0xFF)); // #3AAAFF

							contentTextBlock.PointerPressed += (sender, e) =>
							{
								try
								{
									if (sender is TextBlock tb)
									{
										string fullText = GetTextBlockText(tb);
										if (!string.IsNullOrEmpty(fullText))
										{
											var linkPattern = @"🔗([^🔗]+)🔗";
											var match = System.Text.RegularExpressions.Regex.Match(
												fullText,
												linkPattern,
												System.Text.RegularExpressions.RegexOptions.None,
												TimeSpan.FromMilliseconds(250) // Limit regex execution time to reduce DoS risk
											);
											if (match.Success)
											{
												string url = match.Groups[1].Value;
												Core.Utility.UrlUtilities.OpenUrl(url);
												e.Handled = true;
											}
										}
									}
								}
								catch (Exception ex)
								{
									Logger.LogError($"Error handling link click: {ex.Message}");
								}
							};
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogError($"Error loading widescreen content: {ex.Message}");
				}
			});
		}

		private static string GetTextBlockText(TextBlock textBlock)
		{
			if (textBlock.Inlines is null || textBlock.Inlines.Count == 0)
				return textBlock.Text ?? string.Empty;

			var text = new System.Text.StringBuilder();
			foreach (var inline in textBlock.Inlines)
			{
				if (inline is Avalonia.Controls.Documents.Run run)
					text.Append(run.Text);
			}
			return text.ToString();
		}

		private void ContinueButton_Click(object sender, RoutedEventArgs e)
		{
			var dontShowCheckBox = this.FindControl<CheckBox>("DontShowAgainCheckBox");
			DontShowAgain = dontShowCheckBox?.IsChecked == true;
			UserCancelled = false;
			Close(true);
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			UserCancelled = true;
			Close(false);
		}
	}
}