// Copyright 2021-2025 KOTORModSync
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using JetBrains.Annotations;

namespace KOTORModSync.Converters
{
	/// <summary>
	/// Renders markdown content to Avalonia UI elements with full markdown support
	/// </summary>
	public static class MarkdownRenderer
	{
		// Note: Markdig pipeline is available but not used in the simplified net462-compatible implementation
		// private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()...

		/// <summary>
		/// Gets the theme foreground color for text elements
		/// </summary>
        private static IBrush GetThemeForegroundBrush()
        {
            // Retained for compatibility; no longer used to force colors on runs.
            // Text elements should inherit theme colors from styles.
            if ( Application.Current?.Resources.TryGetResource("ThemeForegroundBrush", null, out object resource) == true && resource is IBrush brush )
                return brush;
            return Brushes.Transparent;
        }

		/// <summary>
		/// Renders markdown text to a TextBlock with inline elements
		/// </summary>
		/// <param name="markdownText">The markdown text to render</param>
		/// <param name="onLinkClick">Optional callback for handling link clicks</param>
		/// <returns>A TextBlock containing the rendered markdown</returns>
		[NotNull]
		public static TextBlock RenderToTextBlock([CanBeNull] string markdownText, [CanBeNull] Action<string> onLinkClick = null)
		{
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.WrapWithOverflow,
                TextTrimming = TextTrimming.None,
            };

			if ( string.IsNullOrWhiteSpace(markdownText) )
			{
				return textBlock;
			}

			try
			{
				// For now, use a simple regex-based approach for net462 compatibility
				// This handles the most common markdown elements: bold, italic, and links
                if ( textBlock.Inlines != null )
                    textBlock.Inlines.AddRange(ParseMarkdownInlines(markdownText, onLinkClick));
			}
			catch ( Exception )
			{
                // If markdown parsing fails, fall back to plain text
                textBlock.Text = markdownText;
			}

			return textBlock;
		}

		/// <summary>
		/// Simple regex-based markdown parser for net462 compatibility
		/// </summary>
		private static List<Inline> ParseMarkdownInlines([NotNull] string text, [CanBeNull] Action<string> onLinkClick)
		{
			var inlines = new List<Inline>();
			int currentIndex = 0;

			// Pattern for markdown links: [text](url)
			string linkPattern = @"\[([^\]]+)\]\(([^)]+)\)";
			MatchCollection linkMatches = Regex.Matches(text, linkPattern);

			foreach ( Match match in linkMatches )
			{
				// Add text before the link
				if ( match.Index > currentIndex )
				{
                    string beforeText = text.Substring(currentIndex, match.Index - currentIndex);
					AddTextWithFormatting(beforeText, inlines);
				}

				// Add the link
                string linkText = match.Groups[1].Value;
                string linkUrl = match.Groups[2].Value;
				var linkRun = new Run
				{
                    Text = linkText,
                    TextDecorations = TextDecorations.Underline,
				};

				// For net462 compatibility, we'll store the URL in the Text property
				// by prefixing it with a special marker
                if ( onLinkClick != null )
                    linkRun.Text = $"🔗{linkUrl}🔗{linkText}";

				inlines.Add(linkRun);
				currentIndex = match.Index + match.Length;
			}

			// Add remaining text
			if ( currentIndex < text.Length )
			{
				string remainingText = text.Substring(currentIndex);
				AddTextWithFormatting(remainingText, inlines);
			}

			return inlines;
		}

		/// <summary>
		/// Adds text with basic formatting (bold, italic) to the inlines collection
		/// </summary>
		private static void AddTextWithFormatting([NotNull] string text, [NotNull] List<Inline> inlines)
		{
			if ( string.IsNullOrEmpty(text) )
				return;

			// Handle bold text: **text** or __text__
			string boldPattern = @"(\*\*|__)([^*_]+)\1";
			MatchCollection boldMatches = Regex.Matches(text, boldPattern);
			int currentIndex = 0;

			foreach ( Match match in boldMatches )
			{
				// Add text before the bold
				if ( match.Index > currentIndex )
				{
                    string beforeText = text.Substring(currentIndex, match.Index - currentIndex);
                    AddTextWithItalic(beforeText, inlines);
				}

				// Add bold text
				string boldText = match.Groups[2].Value;
                var boldRun = new Run
                {
                    Text = boldText,
                    FontWeight = FontWeight.Bold,
                };
				inlines.Add(boldRun);

				currentIndex = match.Index + match.Length;
			}

			// Add remaining text
			if ( currentIndex < text.Length )
			{
                string remainingText = text.Substring(currentIndex);
				AddTextWithItalic(remainingText, inlines);
			}
		}

		/// <summary>
		/// Adds text with italic formatting to the inlines collection
		/// </summary>
		private static void AddTextWithItalic([NotNull] string text, [NotNull] List<Inline> inlines)
		{
			if ( string.IsNullOrEmpty(text) )
				return;

			// Handle italic text: *text* or _text_
			string italicPattern = @"(\*|_)([^*_]+)\1";
			MatchCollection italicMatches = Regex.Matches(text, italicPattern);
			int currentIndex = 0;

			foreach ( Match match in italicMatches )
			{
				// Add text before the italic
                if ( match.Index > currentIndex )
                {
                    string beforeText = text.Substring(currentIndex, match.Index - currentIndex);
                    inlines.Add(new Run { Text = beforeText });
                }

				// Add italic text
				string italicText = match.Groups[2].Value;
                var italicRun = new Run
                {
                    Text = italicText,
                    FontStyle = FontStyle.Italic,
                };
				inlines.Add(italicRun);

				currentIndex = match.Index + match.Length;
			}

			// Add remaining text
			if ( currentIndex < text.Length )
			{
                string remainingText = text.Substring(currentIndex);
                inlines.Add(new Run { Text = remainingText });
			}
		}
	}
}