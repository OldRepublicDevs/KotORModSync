// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

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
    public static class MarkdownRenderer
    {
        private static IBrush GetThemeForegroundBrush()
        {
            if (Application.Current?.Resources.TryGetResource("ThemeForegroundBrush", theme: null, out object resource) == true
                && resource is IBrush brush
            )
            {
                return brush;
            }

            if (Application.Current?.Styles != null)
            {
                foreach (Avalonia.Styling.IStyle style in Application.Current.Styles)
                {
                    if (style is Avalonia.Markup.Xaml.Styling.StyleInclude styleInclude
                        && styleInclude.Loaded is Avalonia.Styling.Styles styles
                        && styles.Resources.TryGetResource("ThemeForegroundBrush", theme: null, out object res)
                        && res is IBrush b)
                    {
                        return b;
                    }
                }
            }

            // Fallback: determine color based on current theme
            string currentTheme = ThemeManager.GetCurrentStylePath();
            if (currentTheme.Contains("FluentLightStyle"))
            {
                return new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)); // #212121
            }
            else if (currentTheme.Contains("Kotor2Style"))
            {
                return new SolidColorBrush(Color.FromRgb(0x18, 0xae, 0x88)); // #18ae88
            }
            else if (currentTheme.Contains("KotorStyle"))
            {
                return new SolidColorBrush(Color.FromRgb(0x3A, 0xAA, 0xFF)); // #3AAAFF
            }

            // Default fallback
            return new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)); // #212121
        }

        [NotNull]
        public static TextBlock RenderToTextBlock(
            [CanBeNull] string markdownText,
            [CanBeNull] Action<string> onLinkClick = null)
        {
            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.WrapWithOverflow,
                TextTrimming = TextTrimming.None,
            };

            if (string.IsNullOrWhiteSpace(markdownText))
            {
                return textBlock;
            }

            try
            {
                if (textBlock.Inlines != null)
                {
                    textBlock.Inlines.AddRange(ParseMarkdownInlines(markdownText, onLinkClick));
                }
            }
            catch (Exception)
            {

                textBlock.Text = markdownText;
            }

            return textBlock;
        }

        private static List<Inline> ParseMarkdownInlines(
            [NotNull] string text,
            [CanBeNull] Action<string> onLinkClick = null)
        {
            var inlines = new List<Inline>();
            int currentIndex = 0;


            // Use Regex.CompileToAssembly or timeout-parameter overload to avoid ReDoS
            MatchCollection linkMatches = new Regex(
                @"\[([^\]]+)\]\(([^)]+)\)",
                RegexOptions.Compiled,
                TimeSpan.FromSeconds(5)
            ).Matches(text);

            foreach (Match match in linkMatches)
            {

                if (match.Index > currentIndex)
                {
                    string beforeText = text.Substring(currentIndex, match.Index - currentIndex);
                    AddTextWithFormatting(beforeText, inlines);
                }


                string linkText = match.Groups[1].Value;
                string linkUrl = match.Groups[2].Value;
                var linkRun = new Run
                {
                    Text = linkText,
                    TextDecorations = TextDecorations.Underline,
                    Foreground = GetThemeForegroundBrush(),
                };

                if (onLinkClick != null)
                {
                    linkRun.Text = $"🔗{linkUrl}🔗{linkText}";
                }

                inlines.Add(linkRun);
                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
            {
                string remainingText = text.Substring(currentIndex);
                AddTextWithFormatting(remainingText, inlines);
            }

            return inlines;
        }

        private static void AddTextWithFormatting(
            [NotNull] string text,
            [NotNull] List<Inline> inlines)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            MatchCollection boldMatches = new Regex(
                @"(\*\*|__)([^*_]+)\1",
                RegexOptions.Compiled,
                TimeSpan.FromSeconds(5)
            ).Matches(text);
            int currentIndex = 0;

            foreach (Match match in boldMatches)
            {

                if (match.Index > currentIndex)
                {
                    string beforeText = text.Substring(currentIndex, match.Index - currentIndex);
                    AddTextWithItalic(beforeText, inlines);
                }


                string boldText = match.Groups[2].Value;
                var boldRun = new Run
                {
                    Text = boldText,
                    FontWeight = FontWeight.Bold,
                    Foreground = GetThemeForegroundBrush(),
                };
                inlines.Add(boldRun);

                currentIndex = match.Index + match.Length;
            }


            if (currentIndex < text.Length)
            {
                string remainingText = text.Substring(currentIndex);
                AddTextWithItalic(remainingText, inlines);
            }
        }

        private static void AddTextWithItalic(
            [NotNull] string text,
            [NotNull] List<Inline> inlines)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string italicPattern = @"(\*|_)([^*_]+)\1";
            MatchCollection italicMatches = new Regex(
                italicPattern,
                RegexOptions.Compiled,
                TimeSpan.FromSeconds(5)
            ).Matches(text);
            int currentIndex = 0;

            foreach (Match match in italicMatches)
            {

                if (match.Index > currentIndex)
                {
                    string beforeText = text.Substring(currentIndex, match.Index - currentIndex);
                    inlines.Add(new Run { Text = beforeText, Foreground = GetThemeForegroundBrush() });
                }


                string italicText = match.Groups[2].Value;
                var italicRun = new Run
                {
                    Text = italicText,
                    FontStyle = FontStyle.Italic,
                    Foreground = GetThemeForegroundBrush(),
                };
                inlines.Add(italicRun);

                currentIndex = match.Index + match.Length;
            }


            if (currentIndex < text.Length)
            {
                string remainingText = text.Substring(currentIndex);
                inlines.Add(new Run { Text = remainingText, Foreground = GetThemeForegroundBrush() });
            }
        }
    }
}
