// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
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
		/// <summary>
		/// Renders markdown content to a TextBlock's Inlines collection.
		/// </summary>
		public void RenderMarkdownToTextBlock( TextBlock targetTextBlock, string markdownContent )
		{
			try
			{
				if (targetTextBlock == null)
					return;

				if (string.IsNullOrWhiteSpace( markdownContent ))
				{
					targetTextBlock.Inlines?.Clear();
					return;
				}

				TextBlock renderedContent = MarkdownRenderer.RenderToTextBlock(
					markdownContent,
					OpenUrl
				);

				targetTextBlock.Inlines?.Clear();
				targetTextBlock.Inlines?.AddRange(
					renderedContent.Inlines
					?? throw new NullReferenceException( "renderedContent.Inlines is null: " + markdownContent )
				);

				targetTextBlock.PointerPressed -= OnTextBlockPointerPressed;
				targetTextBlock.PointerPressed += OnTextBlockPointerPressed;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error rendering markdown content" );
			}
		}

		/// <summary>
		/// Renders markdown content and returns the Inlines collection.
		/// Useful for converters that need to return rendered content.
		/// </summary>
		public static List<Inline> RenderMarkdownToInlines( string markdownContent )
		{
			try
			{
				if (string.IsNullOrWhiteSpace( markdownContent ))
					return new List<Inline>();

				TextBlock renderedContent = MarkdownRenderer.RenderToTextBlock( markdownContent, null );
				return renderedContent.Inlines?.Count > 0
					? new List<Inline>( renderedContent.Inlines )
					: new List<Inline>();
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error rendering markdown to inlines" );
				return new List<Inline> { new Run { Text = markdownContent } };
			}
		}

		/// <summary>
		/// Renders markdown content and returns a plain string (for converters that need string output).
		/// This strips formatting but preserves the text content.
		/// </summary>
		public static string RenderMarkdownToString( string markdownContent )
		{
			try
			{
				if (string.IsNullOrWhiteSpace( markdownContent ))
					return string.Empty;

				// For now, just return the markdown content as-is since TextBlock can handle it
				// The actual rendering happens when it's assigned to TextBlock.Inlines
				return markdownContent;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error rendering markdown to string" );
				return markdownContent ?? string.Empty;
			}
		}

		/// <summary>
		/// Legacy method for backward compatibility. Use RenderMarkdownToTextBlock instead.
		/// </summary>
		public void RenderComponentMarkdown(
			ModComponent component,
			TextBlock descriptionTextBlock,
			TextBlock directionsTextBlock,
			bool spoilerFreeMode = false )
		{
			try
			{
				if (component == null)
					return;

				if (descriptionTextBlock != null)
				{
					string descriptionContent = spoilerFreeMode
						? component.DescriptionSpoilerFree
						: component.Description;

					RenderMarkdownToTextBlock( descriptionTextBlock, descriptionContent );
				}

				if (directionsTextBlock != null)
				{
					string directionsContent = spoilerFreeMode
						? component.DirectionsSpoilerFree
						: component.Directions;

					RenderMarkdownToTextBlock( directionsTextBlock, directionsContent );
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error rendering component markdown" );
			}
		}

		private static void OpenUrl( string url )
		{
			try
			{
				UrlUtilities.OpenUrl( url );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, $"Error opening URL: {url}" );
			}
		}

		private void OnTextBlockPointerPressed( object sender, PointerPressedEventArgs e )
		{
			try
			{
				if (!(sender is TextBlock textBlock))
					return;
				string fullText = GetTextBlockText( textBlock );
				if (string.IsNullOrEmpty( fullText ))
					return;
				string linkPattern = @"🔗([^🔗]+)🔗";
				Match match = Regex.Match( fullText, linkPattern );
				if (!match.Success)
					return;
				string url = match.Groups[1].Value;
				OpenUrl( url );
				e.Handled = true;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error handling text block click" );
			}
		}

		private static string GetTextBlockText( TextBlock textBlock )
		{
			if (textBlock.Inlines == null || textBlock.Inlines.Count == 0)
				return textBlock.Text ?? string.Empty;

			var text = new System.Text.StringBuilder();
			foreach (Inline inline in textBlock.Inlines)
			{
				if (inline is Run run)
					_ = text.Append( run.Text );
			}
			return text.ToString();
		}
	}
}