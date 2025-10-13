// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using KOTORModSync.Converters;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for rendering markdown content in the UI
	/// </summary>
	public class MarkdownRenderingService
	{
		/// <summary>
		/// Renders markdown content for a component's Description and Directions
		/// </summary>
		public void RenderComponentMarkdown(
			ModComponent component,
			TextBlock descriptionTextBlock,
			TextBlock directionsTextBlock)
		{
			try
			{
				if ( component == null )
					return;

				// Render Description
				if ( descriptionTextBlock != null )
				{
					TextBlock renderedDescription = MarkdownRenderer.RenderToTextBlock(
						component.Description,
						url => MarkdownRenderingService.OpenUrl(url)
					);

					if ( renderedDescription?.Inlines != null )
					{
						descriptionTextBlock.Inlines.Clear();
						descriptionTextBlock.Inlines.AddRange(renderedDescription.Inlines);

						// Don't override TextWrapping/TextTrimming - let XAML handle it
						descriptionTextBlock.PointerPressed -= OnTextBlockPointerPressed;
						descriptionTextBlock.PointerPressed += OnTextBlockPointerPressed;
					}
				}

				// Render Directions
				if ( directionsTextBlock != null )
				{
					TextBlock renderedDirections = MarkdownRenderer.RenderToTextBlock(
						component.Directions,
						url => MarkdownRenderingService.OpenUrl(url)
					);

					if ( renderedDirections?.Inlines != null )
					{
						directionsTextBlock.Inlines.Clear();
						directionsTextBlock.Inlines.AddRange(renderedDirections.Inlines);

						// Don't override TextWrapping/TextTrimming - let XAML handle it
						directionsTextBlock.PointerPressed -= OnTextBlockPointerPressed;
						directionsTextBlock.PointerPressed += OnTextBlockPointerPressed;
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error rendering markdown content");
			}
		}

		/// <summary>
		/// Opens a URL in the default browser
		/// </summary>
		private static void OpenUrl(string url)
		{
			try
			{
				UrlUtilities.OpenUrl(url);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error opening URL: {url}");
			}
		}

		/// <summary>
		/// Handles pointer pressed events on TextBlocks to detect link clicks
		/// </summary>
		private void OnTextBlockPointerPressed(object sender, PointerPressedEventArgs e)
		{
			try
			{
				if ( sender is TextBlock textBlock )
				{
					string fullText = GetTextBlockText(textBlock);
					if ( !string.IsNullOrEmpty(fullText) )
					{
						// Look for link markers
						string linkPattern = @"🔗([^🔗]+)🔗";
						Match match = Regex.Match(fullText, linkPattern);
						if ( match.Success )
						{
							string url = match.Groups[1].Value;
							MarkdownRenderingService.OpenUrl(url);
							e.Handled = true;
						}
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error handling text block click");
			}
		}

		/// <summary>
		/// Gets the full text content from a TextBlock including all inlines
		/// </summary>
		private static string GetTextBlockText(TextBlock textBlock)
		{
			if ( textBlock.Inlines == null || textBlock.Inlines.Count == 0 )
				return textBlock.Text ?? string.Empty;

			var text = new System.Text.StringBuilder();
			foreach ( Inline inline in textBlock.Inlines )
			{
				if ( inline is Run run )
					_ = text.Append(run.Text);
			}
			return text.ToString();
		}
	}
}

