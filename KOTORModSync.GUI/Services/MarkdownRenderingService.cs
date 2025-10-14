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

	public class MarkdownRenderingService
	{

		public void RenderComponentMarkdown(
			ModComponent component,
			TextBlock descriptionTextBlock,
			TextBlock directionsTextBlock,
			bool spoilerFreeMode = false)
		{
			try
			{
				if ( component == null )
					return;

				if ( descriptionTextBlock != null )
				{
					string descriptionContent = spoilerFreeMode
						? component.DescriptionSpoilerFree
						: component.Description;

					TextBlock renderedDescription = MarkdownRenderer.RenderToTextBlock(
						descriptionContent,
						url => MarkdownRenderingService.OpenUrl(url)
					);

					if ( renderedDescription?.Inlines != null )
					{
						descriptionTextBlock.Inlines.Clear();
						descriptionTextBlock.Inlines.AddRange(renderedDescription.Inlines);

						descriptionTextBlock.PointerPressed -= OnTextBlockPointerPressed;
						descriptionTextBlock.PointerPressed += OnTextBlockPointerPressed;
					}
				}

				if ( directionsTextBlock != null )
				{
					string directionsContent = spoilerFreeMode
						? component.DirectionsSpoilerFree
						: component.Directions;

					TextBlock renderedDirections = MarkdownRenderer.RenderToTextBlock(
						directionsContent,
						url => MarkdownRenderingService.OpenUrl(url)
					);

					if ( renderedDirections?.Inlines != null )
					{
						directionsTextBlock.Inlines.Clear();
						directionsTextBlock.Inlines.AddRange(renderedDirections.Inlines);

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

		private void OnTextBlockPointerPressed(object sender, PointerPressedEventArgs e)
		{
			try
			{
				if ( sender is TextBlock textBlock )
				{
					string fullText = GetTextBlockText(textBlock);
					if ( !string.IsNullOrEmpty(fullText) )
					{

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

