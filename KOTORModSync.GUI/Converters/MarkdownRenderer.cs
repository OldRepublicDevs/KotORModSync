// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

using JetBrains.Annotations;

using KOTORModSync;

namespace KOTORModSync.Converters
{
    public static class MarkdownRenderer
    {
        private static IBrush GetThemeForegroundBrush()
        {
            // Try to get the brush from resources using ThemeResourceHelper which handles proper lookup
            IBrush brush = ThemeResourceHelper.GetBrush("ThemeForegroundBrush");
            if (brush != null && brush != Brushes.Transparent)
            {
                return brush;
            }

            // Fallback: try direct resource lookup with multiple strategies
            if (Application.Current != null)
            {
                // Try Application.Resources first
                if (Application.Current.Resources.TryGetResource("ThemeForegroundBrush", theme: null, out object resource) && resource is IBrush appBrush)
                {
                    return appBrush;
                }

                // Try each Style's resources - iterate in reverse to get the most recently added (custom theme) first
                if (Application.Current.Styles != null)
                {
                    // Iterate backwards to check custom theme (added last) before base Fluent theme
                    for (int i = Application.Current.Styles.Count - 1; i >= 0; i--)
                    {
                        Avalonia.Styling.IStyle style = Application.Current.Styles[i];
                        if (style is Avalonia.Markup.Xaml.Styling.StyleInclude styleInclude)
                        {
                            // Try Loaded resources first (most reliable)
                            if (styleInclude.Loaded is Avalonia.Styling.Styles loadedStyles
                                && loadedStyles.Resources.TryGetResource("ThemeForegroundBrush", theme: null, out object loadedRes)
                                && loadedRes is IBrush loadedBrush)
                            {
                                return loadedBrush;
                            }

                            // If Loaded is null, try to load from Source
                            if (styleInclude.Source != null && styleInclude.Loaded == null)
                            {
                                try
                                {
                                    object styleResource = Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(styleInclude.Source);
                                    if (styleResource is Avalonia.Styling.Styles tempStyles
                                        && tempStyles.Resources.TryGetResource("ThemeForegroundBrush", theme: null, out object sourceRes)
                                        && sourceRes is IBrush sourceBrush)
                                    {
                                        return sourceBrush;
                                    }
                                }
                                catch
                                {
                                    // Ignore errors in fallback
                                }
                            }
                        }
                    }
                }
            }

            // Final fallback: determine color based on current theme path
            string currentTheme = ThemeManager.GetCurrentStylePath();
            if (!string.IsNullOrEmpty(currentTheme))
            {
                if (currentTheme.Contains("FluentLightStyle", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)); // #000000 - black for light theme
                }
                else if (currentTheme.Contains("Kotor2Style", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(Color.FromRgb(0x18, 0xae, 0x88)); // #18ae88 - green for KOTOR 2
                }
                else if (currentTheme.Contains("KotorStyle", StringComparison.OrdinalIgnoreCase))
                {
                    return new SolidColorBrush(Color.FromRgb(0x3A, 0xAA, 0xFF)); // #3AAAFF - blue for KOTOR 1
                }
            }

            // Default fallback
            return new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)); // #000000
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

        /// <summary>
        /// Renders markdown content to a Panel, supporting block-level elements like headings and warning blocks.
        /// </summary>
        [NotNull]
        public static Panel RenderToPanel(
            [CanBeNull] string markdownText,
            [CanBeNull] Action<string> onLinkClick = null)
        {
            var mainPanel = new StackPanel
            {
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            if (string.IsNullOrWhiteSpace(markdownText))
            {
                return mainPanel;
            }

            var textBlocksWithLinks = new List<TextBlock>();
            var runToUrlMap = new Dictionary<Run, string>(); // Map Run to URL for click handling

            try
            {
                string[] lines = markdownText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                int i = 0;
                IBrush foregroundBrush = GetThemeForegroundBrush();

                while (i < lines.Length)
                {
                    string line = lines[i].TrimEnd();

                    // Handle warning blocks (:::warning ... :::)
                    if (line.StartsWith(":::warning", StringComparison.OrdinalIgnoreCase))
                    {
                        Border warningBlock = ParseWarningBlock(lines, ref i, onLinkClick, foregroundBrush);
                        if (warningBlock != null)
                        {
                            mainPanel.Children.Add(warningBlock);
                        }
                        continue;
                    }

                    // Handle headings (# ## ###)
                    if (line.StartsWith("#", StringComparison.Ordinal))
                    {
                        int headingLevel = 0;
                        while (headingLevel < line.Length && line[headingLevel] == '#')
                        {
                            headingLevel++;
                        }

                        string headingText = line.Substring(headingLevel).Trim();
                        if (!string.IsNullOrEmpty(headingText))
                        {
                            var headingBlock = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = foregroundBrush,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                            };

                            // Set font size based on heading level
                            switch (headingLevel)
                            {
                                case 1:
                                    headingBlock.FontSize = 28;
                                    break;
                                case 2:
                                    headingBlock.FontSize = 24;
                                    break;
                                case 3:
                                    headingBlock.FontSize = 20;
                                    break;
                                case 4:
                                    headingBlock.FontSize = 18;
                                    break;
                                default:
                                    headingBlock.FontSize = 16;
                                    break;
                            }
                            headingBlock.FontWeight = FontWeight.Bold;
                            headingBlock.Margin = new Thickness(0, headingLevel == 1 ? 0 : 8, 0, 4);

                            if (headingBlock.Inlines != null)
                            {
                                headingBlock.Inlines.AddRange(ParseMarkdownInlines(headingText, onLinkClick));
                                if (onLinkClick != null && HasLinks(headingBlock.Inlines))
                                {
                                    ProcessLinkRuns(headingBlock.Inlines, runToUrlMap);
                                    textBlocksWithLinks.Add(headingBlock);
                                }
                            }

                            mainPanel.Children.Add(headingBlock);
                        }
                        i++;
                        continue;
                    }

                    // Handle regular paragraphs (collect consecutive non-empty lines)
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var paragraphBuilder = new System.Text.StringBuilder();
                        paragraphBuilder.Append(line);
                        i++;

                        // Collect consecutive non-empty, non-heading, non-warning lines into a paragraph
                        while (i < lines.Length)
                        {
                            string nextLine = lines[i].TrimEnd();

                            // Stop if we hit a heading, warning block, or double newline
                            if (nextLine.StartsWith("#", StringComparison.Ordinal) ||
                                nextLine.StartsWith(":::", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }

                            // If we hit an empty line, check if it's a paragraph break
                            if (string.IsNullOrWhiteSpace(nextLine))
                            {
                                i++;
                                // If next non-empty line is also a paragraph, this is a paragraph break
                                int nextNonEmpty = i;
                                while (nextNonEmpty < lines.Length && string.IsNullOrWhiteSpace(lines[nextNonEmpty]))
                                {
                                    nextNonEmpty++;
                                }
                                if (nextNonEmpty < lines.Length &&
                                    !lines[nextNonEmpty].TrimEnd().StartsWith("#", StringComparison.Ordinal) &&
                                    !lines[nextNonEmpty].TrimEnd().StartsWith(":::", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Continue paragraph after empty line
                                    paragraphBuilder.Append(" ");
                                    i = nextNonEmpty;
                                    continue;
                                }
                                break;
                            }

                            paragraphBuilder.Append(" ");
                            paragraphBuilder.Append(nextLine);
                            i++;
                        }

                        string paragraphText = paragraphBuilder.ToString();
                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            var paragraphBlock = new TextBlock
                            {
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = foregroundBrush,
                                FontSize = 15,
                                LineHeight = 24,
                                Margin = new Thickness(0, 0, 0, 8),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                            };

                            paragraphBlock.Inlines?.AddRange(ParseMarkdownInlines(paragraphText, onLinkClick));

                            if (onLinkClick != null && paragraphBlock.Inlines != null && HasLinks(paragraphBlock.Inlines))
                            {
                                ProcessLinkRuns(paragraphBlock.Inlines, runToUrlMap);
                                textBlocksWithLinks.Add(paragraphBlock);
                            }

                            mainPanel.Children.Add(paragraphBlock);
                        }
                        continue;
                    }

                    i++;
                }
            }
            catch (Exception)
            {
                // Fallback to simple text block
                var fallbackBlock = new TextBlock
                {
                    Text = markdownText,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = GetThemeForegroundBrush(),
                };
                mainPanel.Children.Add(fallbackBlock);
            }

            // Attach click handlers to TextBlocks with links
            if (onLinkClick != null && runToUrlMap.Count > 0)
            {
                foreach (var textBlock in textBlocksWithLinks)
                {
                    // Capture runToUrlMap in closure
                    var urlMap = runToUrlMap;
                    textBlock.PointerPressed += (sender, e) => OnTextBlockLinkClicked(sender, e, onLinkClick, urlMap);
                    textBlock.Cursor = new Cursor(StandardCursorType.Hand);
                }
            }

            return mainPanel;
        }

        private static bool HasLinks(InlineCollection inlines)
        {
            if (inlines == null)
            {
                return false;
            }

            foreach (var inline in inlines)
            {
                if (inline is Run run && run.Text != null && run.Text.Contains("🔗"))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ProcessLinkRuns(InlineCollection inlines, Dictionary<Run, string> runToUrlMap)
        {
            if (inlines == null)
            {
                return;
            }

            // Process each Run to extract just the display text from encoded format
            for (int i = 0; i < inlines.Count; i++)
            {
                if (inlines[i] is Run run && run.Text != null && run.Text.Contains("🔗"))
                {
                    // Extract URL and display text from format: 🔗{url}🔗{text}
                    string linkPattern = @"🔗([^🔗]+)🔗(.+)";
                    Match match = Regex.Match(run.Text, linkPattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                    if (match.Success && match.Groups.Count >= 3)
                    {
                        string url = match.Groups[1].Value;
                        string displayText = match.Groups[2].Value;

                        // Store URL mapping
                        runToUrlMap[run] = url;

                        // Replace the Run's text with just the display text
                        run.Text = displayText;
                    }
                }
            }
        }

        private static void OnTextBlockLinkClicked(object sender, PointerPressedEventArgs e, Action<string> onLinkClick, Dictionary<Run, string> runToUrlMap)
        {
            try
            {
                if (!(sender is TextBlock textBlock))
                {
                    return;
                }

                // Find which Run was clicked by checking if pointer is over a link Run
                if (textBlock.Inlines != null)
                {
                    // Get the pointer position relative to the TextBlock
                    var point = e.GetPosition(textBlock);

                    // Check each Run in the Inlines collection
                    foreach (var inline in textBlock.Inlines)
                    {
                        if (inline is Run run && runToUrlMap.ContainsKey(run))
                        {
                            // Check if click is within this Run's bounds (simplified - check if it's in the text block)
                            // For now, if any link is clicked, use the first one found
                            // A more precise implementation would check the actual character position
                            string url = runToUrlMap[run];
                            onLinkClick?.Invoke(url);
                            e.Handled = true;
                            return;
                        }
                    }

                    // Fallback: try to find any link Run (less precise but works)
                    foreach (var inline in textBlock.Inlines)
                    {
                        if (inline is Run run && runToUrlMap.ContainsKey(run))
                        {
                            string url = runToUrlMap[run];
                            onLinkClick?.Invoke(url);
                            e.Handled = true;
                            return;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail on link click errors
            }
        }


        private static Border ParseWarningBlock(
            string[] lines,
            ref int currentIndex,
            Action<string> onLinkClick,
            IBrush foregroundBrush)
        {
            var warningPanel = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            };

            var iconBlock = new TextBlock
            {
                Text = "⚠️",
                FontSize = 22,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 12, 0),
            };

            var textPanel = new StackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Parse title - check if it's on the same line as :::warning or next line
            string line = currentIndex < lines.Length ? lines[currentIndex] : string.Empty;
            string titleLine = string.Empty;

            if (line.StartsWith(":::warning", StringComparison.OrdinalIgnoreCase))
            {
                titleLine = line.Substring(":::warning".Length).Trim();
                currentIndex++;
            }

            if (!string.IsNullOrWhiteSpace(titleLine))
            {
                var titleBlock = new TextBlock
                {
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = foregroundBrush,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                if (titleBlock.Inlines != null)
                {
                    titleBlock.Inlines.AddRange(ParseMarkdownInlines(titleLine, onLinkClick));
                    if (onLinkClick != null && HasLinks(titleBlock.Inlines))
                    {
                        // Note: Warning block title blocks would need to be tracked if we want click handlers
                        // For now, links in warning titles won't be clickable
                    }
                }

                textPanel.Children.Add(titleBlock);
            }

            // Parse content until closing :::
            var contentBuilder = new System.Text.StringBuilder();
            while (currentIndex < lines.Length)
            {
                string contentLine = lines[currentIndex];
                if (string.Equals(contentLine.Trim(), ":::", StringComparison.Ordinal))
                {
                    currentIndex++;
                    break;
                }

                if (contentBuilder.Length > 0)
                {
                    contentBuilder.AppendLine();
                }
                contentBuilder.Append(contentLine);
                currentIndex++;
            }

            string contentText = contentBuilder.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(contentText))
            {
                var contentBlock = new TextBlock
                {
                    FontSize = 11,
                    Opacity = 0.9,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = foregroundBrush,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                contentBlock.Inlines?.AddRange(ParseMarkdownInlines(contentText, onLinkClick));

                // Note: Warning block content blocks would need to be tracked separately if we want click handlers
                // For now, links in warning blocks won't be clickable

                textPanel.Children.Add(contentBlock);
            }

            Grid.SetColumn(iconBlock, 0);
            Grid.SetColumn(textPanel, 1);
            warningPanel.Children.Add(iconBlock);
            warningPanel.Children.Add(textPanel);

            var warningBorder = new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(8),
                Child = warningPanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            // Try to get warning background color from theme, fallback to yellow
            try
            {
                if (Application.Current?.Resources.TryGetResource("WarningBackgroundBrush", theme: null, out object resource) == true
                    && resource is IBrush warningBrush)
                {
                    warningBorder.Background = warningBrush;
                }
                else
                {
                    // Default yellow background
                    warningBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B));
                }
            }
            catch
            {
                warningBorder.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B));
            }

            return warningBorder;
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

                // Create a Run with the link text
                var linkRun = new Run
                {
                    // Display only the link text, but store URL in encoded format for click detection
                    // Format: 🔗{url}🔗{text} - the click handler will extract the URL
                    Text = onLinkClick != null ? $"🔗{linkUrl}🔗{linkText}" : linkText,
                    TextDecorations = TextDecorations.Underline,
                };

                // Set link color - try to get link color from theme, fallback to blue
                try
                {
                    if (Application.Current?.Resources.TryGetResource("LinkForegroundBrush", theme: null, out object linkResource) == true
                        && linkResource is IBrush linkBrush)
                    {
                        linkRun.Foreground = linkBrush;
                    }
                    else
                    {
                        // Default link color - blue
                        linkRun.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
                    }
                }
                catch
                {
                    linkRun.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
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
