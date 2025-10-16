// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using KOTORModSync.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

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
						OpenUrl
					);

					descriptionTextBlock.Inlines?.Clear();
					descriptionTextBlock.Inlines?.AddRange(renderedDescription.Inlines ?? throw new NullReferenceException("renderedDescription.Inlines is null: " + descriptionContent));

					descriptionTextBlock.PointerPressed -= OnTextBlockPointerPressed;
					descriptionTextBlock.PointerPressed += OnTextBlockPointerPressed;
				}

				if ( directionsTextBlock != null )
				{
					string directionsContent = spoilerFreeMode
						? component.DirectionsSpoilerFree
						: component.Directions;

					TextBlock renderedDirections = MarkdownRenderer.RenderToTextBlock(
						directionsContent,
						OpenUrl
					);

					directionsTextBlock.Inlines?.Clear();
					directionsTextBlock.Inlines?.AddRange(
						renderedDirections.Inlines
						?? throw new NullReferenceException("renderedDirections.Inlines is null: " + directionsContent)
					);

					directionsTextBlock.PointerPressed -= OnTextBlockPointerPressed;
					directionsTextBlock.PointerPressed += OnTextBlockPointerPressed;
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
				if ( !(sender is TextBlock textBlock) )
					return;
				string fullText = GetTextBlockText(textBlock);
				if ( string.IsNullOrEmpty(fullText) )
					return;
				string linkPattern = @"🔗([^🔗]+)🔗";
				Match match = Regex.Match(fullText, linkPattern);
				if ( !match.Success )
					return;
				string url = match.Groups[1].Value;
				OpenUrl(url);
				e.Handled = true;
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

