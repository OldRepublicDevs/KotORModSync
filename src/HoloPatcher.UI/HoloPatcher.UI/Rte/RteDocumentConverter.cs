using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvRichTextBox;
using AvaloniaTextElement = Avalonia.Controls.Documents.TextElement;

namespace HoloPatcher.UI.Rte
{
    internal static class RteDocumentConverter
    {
        public static RteDocument FromFlowDocument(FlowDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string normalizedContent = NormalizeToTkContent(document.Text);
            var result = new RteDocument
            {
                Content = normalizedContent
            };

            var tagLookup = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<Paragraph, int> paragraphPositions = CalculateParagraphPositions(document, normalizedContent);

            foreach (Paragraph paragraph in document.Blocks.OfType<Paragraph>())
            {
                int paragraphStart = paragraphPositions[paragraph];
                int paragraphTextLength = GetParagraphTextLength(paragraph);
                int paragraphEnd = paragraphStart + paragraphTextLength;

                foreach (EditableRun inline in paragraph.Inlines.OfType<EditableRun>())
                {
                    Dictionary<string, string> config = BuildInlineConfig(paragraph, inline);
                    if (config.Count == 0 || inline.InlineLength == 0)
                    {
                        continue;
                    }

                    string configKey = string.Join("|", config.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    string tagName;
                    if (!tagLookup.TryGetValue(configKey, out tagName))
                    {
                        tagName = $"tag_{tagLookup.Count + 1}";
                        tagLookup[configKey] = tagName;
                        result.TagConfigs[tagName] = config;
                        result.Tags[tagName] = new List<RteRange>();
                    }

                    // Calculate inline position within paragraph
                    int inlineOffset = GetInlineOffsetInParagraph(paragraph, inline);
                    int start = paragraphStart + inlineOffset;
                    int end = start + inline.InlineLength;
                    result.Tags[tagName].Add(new RteRange
                    {
                        Start = OffsetToTkIndex(normalizedContent, start),
                        End = OffsetToTkIndex(normalizedContent, end)
                    });
                }

                Dictionary<string, string> paragraphConfig = BuildParagraphConfig(paragraph);
                if (paragraphConfig.Count > 0)
                {
                    string configKey = string.Join("|", paragraphConfig.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));
                    string tagName;
                    if (!tagLookup.TryGetValue(configKey, out tagName))
                    {
                        tagName = $"tag_{tagLookup.Count + 1}";
                        tagLookup[configKey] = tagName;
                        result.TagConfigs[tagName] = paragraphConfig;
                        result.Tags[tagName] = new List<RteRange>();
                    }

                    result.Tags[tagName].Add(new RteRange
                    {
                        Start = OffsetToTkIndex(normalizedContent, paragraphStart),
                        End = OffsetToTkIndex(normalizedContent, paragraphEnd)
                    });
                }
            }

            return result;
        }

        private static Dictionary<Paragraph, int> CalculateParagraphPositions(FlowDocument document, string normalizedContent)
        {
            var positions = new Dictionary<Paragraph, int>();
            int currentOffset = 0;

            foreach (Block block in document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    positions[paragraph] = currentOffset;
                    currentOffset += GetParagraphTextLength(paragraph);
                }
            }

            return positions;
        }

        private static int GetParagraphTextLength(Paragraph paragraph)
        {
            int length = 0;
            foreach (IEditable inline in paragraph.Inlines)
            {
                if (inline is EditableRun run)
                {
                    length += run.InlineLength;
                }
                else if (inline is Avalonia.Controls.Documents.Run avRun)
                {
                    length += avRun.Text?.Length ?? 0;
                }
            }
            return length;
        }

        private static int GetInlineOffsetInParagraph(Paragraph paragraph, EditableRun targetInline)
        {
            int offset = 0;
            foreach (IEditable inline in paragraph.Inlines)
            {
                if (inline == targetInline)
                {
                    break;
                }
                if (inline is EditableRun run)
                {
                    offset += run.InlineLength;
                }
                else if (inline is Avalonia.Controls.Documents.Run avRun)
                {
                    offset += avRun.Text?.Length ?? 0;
                }
            }
            return offset;
        }

        public static void ApplyToRichTextBox(RichTextBox richTextBox, RteDocument document)
        {
            if (richTextBox is null)
            {
                throw new ArgumentNullException(nameof(richTextBox));
            }

            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string avaloniaContent = NormalizeToAvaloniaContent(document.Content);
            FlowDocument flowDocument = new FlowDocument();
            richTextBox.FlowDocument = flowDocument;
            flowDocument.Selection.Text = avaloniaContent;
            flowDocument.Select(0, 0);

            if (document.Tags.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<string, List<RteRange>> kvp in document.Tags)
            {
                List<RteRange> ranges = kvp.Value;
                Dictionary<string, string> config;
                if (!document.TagConfigs.TryGetValue(kvp.Key, out config) || config.Count == 0)
                {
                    continue;
                }

                foreach (RteRange rteRange in ranges)
                {
                    int start = TkIndexToOffset(rteRange.Start, document.Content);
                    int end = TkIndexToOffset(rteRange.End, document.Content);
                    if (start >= end)
                    {
                        continue;
                    }

                    var textRange = new TextRange(flowDocument, start, end);
                    ApplyInlineFormatting(textRange, config);
                    ApplyParagraphFormatting(flowDocument, textRange, config);
                }
            }
        }

        private static void ApplyInlineFormatting(TextRange range, IReadOnlyDictionary<string, string> config)
        {
            string fontDescriptor;
            if (config.TryGetValue("font", out fontDescriptor) && !string.IsNullOrWhiteSpace(fontDescriptor))
            {
                (FontFamily Family, double? Size, FontWeight? Weight, FontStyle? Style) descriptor = ParseFontDescriptor(fontDescriptor);
                if (descriptor.Family != null)
                {
                    range.ApplyFormatting(AvaloniaTextElement.FontFamilyProperty, descriptor.Family);
                }
                if (descriptor.Size.HasValue)
                {
                    range.ApplyFormatting(AvaloniaTextElement.FontSizeProperty, descriptor.Size.Value);
                }
                if (descriptor.Weight.HasValue)
                {
                    range.ApplyFormatting(AvaloniaTextElement.FontWeightProperty, descriptor.Weight.Value);
                }
                if (descriptor.Style.HasValue)
                {
                    range.ApplyFormatting(AvaloniaTextElement.FontStyleProperty, descriptor.Style.Value);
                }
            }

            string foreground;
            if (config.TryGetValue("foreground", out foreground))
            {
                SolidColorBrush brush = ParseBrush(foreground);
                if (brush != null)
                {
                    range.ApplyFormatting(AvaloniaTextElement.ForegroundProperty, brush);
                }
            }

            string background;
            if (config.TryGetValue("background", out background))
            {
                SolidColorBrush brush = ParseBrush(background);
                if (brush != null)
                {
                    range.ApplyFormatting(AvaloniaTextElement.BackgroundProperty, brush);
                }
            }

            string underlineValue;
            bool underline = config.TryGetValue("underline", out underlineValue) &&
                             (underlineValue == "1" || (bool.TryParse(underlineValue, out bool u) && u));
            string strikeValue;
            bool strike = config.TryGetValue("overstrike", out strikeValue) &&
                          (strikeValue == "1" || (bool.TryParse(strikeValue, out bool s) && s));

            if (underline || strike)
            {
                var decorations = new TextDecorationCollection();
                if (underline)
                {
                    decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
                }
                if (strike)
                {
                    decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
                }
                // TextDecorationsProperty may not be available in all Avalonia versions
                // Try to get it via reflection
                try
                {
                    Type textElementType = typeof(AvaloniaTextElement);
                    System.Reflection.PropertyInfo prop = textElementType.GetProperty("TextDecorationsProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null)
                    {
                        var textDecorationsProp = prop.GetValue(null) as Avalonia.AvaloniaProperty;
                        if (textDecorationsProp != null)
                        {
                            range.ApplyFormatting(textDecorationsProp, decorations);
                        }
                    }
                }
                catch
                {
                    // Fallback: property doesn't exist - skip text decorations
                    // This is a workaround for API differences
                }
            }
        }

        private static void ApplyParagraphFormatting(FlowDocument document, TextRange range, IReadOnlyDictionary<string, string> config)
        {
            // Calculate paragraph positions to find which paragraphs intersect with the range
            string normalizedContent = NormalizeToTkContent(document.Text);
            Dictionary<Paragraph, int> paragraphPositions = CalculateParagraphPositions(document, normalizedContent);
            var paragraphs = new List<Paragraph>();

            foreach (Block block in document.Blocks)
            {
                if (block is Paragraph paragraph && paragraphPositions.ContainsKey(paragraph))
                {
                    int paragraphStart = paragraphPositions[paragraph];
                    int paragraphTextLength = GetParagraphTextLength(paragraph);
                    int paragraphEnd = paragraphStart + paragraphTextLength;

                    if (paragraphStart < range.End && paragraphEnd > range.Start)
                    {
                        paragraphs.Add(paragraph);
                    }
                }
            }

            if (paragraphs.Count == 0)
            {
                return;
            }

            string justify;
            if (config.TryGetValue("justify", out justify))
            {
                TextAlignment alignment;
                switch (justify.ToLowerInvariant())
                {
                    case "center": alignment = TextAlignment.Center; break;
                    case "right": alignment = TextAlignment.Right; break;
                    case "justify": alignment = TextAlignment.Justify; break;
                    default: alignment = TextAlignment.Left; break;
                }

                foreach (Paragraph paragraph in paragraphs)
                {
                    paragraph.TextAlignment = alignment;
                }
            }

            string spacingBefore;
            double spacingBeforeValue;
            if (config.TryGetValue("spacing1", out spacingBefore) && double.TryParse(spacingBefore, NumberStyles.Float, CultureInfo.InvariantCulture, out spacingBeforeValue))
            {
                foreach (Paragraph paragraph in paragraphs)
                {
                    Thickness margin = paragraph.Margin;
                    paragraph.Margin = new Thickness(margin.Left, spacingBeforeValue, margin.Right, margin.Bottom);
                }
            }

            string spacingAfter;
            double spacingAfterValue;
            if (config.TryGetValue("spacing3", out spacingAfter) && double.TryParse(spacingAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out spacingAfterValue))
            {
                foreach (Paragraph paragraph in paragraphs)
                {
                    Thickness margin = paragraph.Margin;
                    paragraph.Margin = new Thickness(margin.Left, margin.Top, margin.Right, spacingAfterValue);
                }
            }

            string firstMargin;
            double firstMarginValue;
            if (config.TryGetValue("lmargin1", out firstMargin) && double.TryParse(firstMargin, NumberStyles.Float, CultureInfo.InvariantCulture, out firstMarginValue))
            {
                foreach (Paragraph paragraph in paragraphs)
                {
                    Thickness margin = paragraph.Margin;
                    paragraph.Margin = new Thickness(firstMarginValue, margin.Top, margin.Right, margin.Bottom);
                }
            }
        }

        private static Dictionary<string, string> BuildInlineConfig(Paragraph paragraph, EditableRun inline)
        {
            var config = new Dictionary<string, string>(StringComparer.Ordinal);

            string fontDescriptor = $"{inline.FontFamily?.Name ?? paragraph.FontFamily.Name} {Math.Round(inline.FontSize)}";
            if (inline.FontWeight == FontWeight.Bold)
            {
                fontDescriptor += " bold";
            }
            if (inline.FontStyle == FontStyle.Italic)
            {
                fontDescriptor += " italic";
            }

            config["font"] = fontDescriptor.Trim();

            if (inline.Foreground is SolidColorBrush fg)
            {
                config["foreground"] = fg.Color.ToString();
            }

            if (inline.Background is SolidColorBrush bg && bg.Color != Colors.Transparent)
            {
                config["background"] = bg.Color.ToString();
            }

            if (inline.TextDecorations != null && inline.TextDecorations.Count > 0)
            {
                if (inline.TextDecorations.Any(td => td.Location == TextDecorationLocation.Underline))
                {
                    config["underline"] = "1";
                }

                if (inline.TextDecorations.Any(td => td.Location == TextDecorationLocation.Strikethrough))
                {
                    config["overstrike"] = "1";
                }
            }

            return config;
        }

        private static Dictionary<string, string> BuildParagraphConfig(Paragraph paragraph)
        {
            var config = new Dictionary<string, string>(StringComparer.Ordinal);
            if (paragraph.TextAlignment != TextAlignment.Left)
            {
                config["justify"] = paragraph.TextAlignment.ToString().ToLowerInvariant();
            }

            if (paragraph.Margin.Top > 0)
            {
                config["spacing1"] = paragraph.Margin.Top.ToString(CultureInfo.InvariantCulture);
            }

            if (paragraph.Margin.Bottom > 0)
            {
                config["spacing3"] = paragraph.Margin.Bottom.ToString(CultureInfo.InvariantCulture);
            }

            if (paragraph.Margin.Left > 0)
            {
                config["lmargin1"] = paragraph.Margin.Left.ToString(CultureInfo.InvariantCulture);
            }

            return config;
        }

        private static (FontFamily Family, double? Size, FontWeight? Weight, FontStyle? Style) ParseFontDescriptor(string descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor))
            {
                return (null, null, null, null);
            }

            string[] parts = descriptor.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return (null, null, null, null);
            }

            var familyParts = new List<string>();
            double? fontSize = null;
            FontWeight? weight = null;
            FontStyle? style = null;

            foreach (string part in parts)
            {
                double size;
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out size))
                {
                    fontSize = size;
                    continue;
                }

                switch (part.ToLowerInvariant())
                {
                    case "bold":
                        weight = FontWeight.Bold;
                        break;
                    case "italic":
                        style = FontStyle.Italic;
                        break;
                    default:
                        familyParts.Add(part.Trim('{', '}'));
                        break;
                }
            }

            FontFamily family = familyParts.Count > 0 ? new FontFamily(string.Join(" ", familyParts)) : null;
            return (family, fontSize, weight, style);
        }

        private static SolidColorBrush ParseBrush(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return new SolidColorBrush(Color.Parse(value));
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeToTkContent(string text)
        {
            return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                       .Replace("\r", "\n", StringComparison.Ordinal);
        }

        private static string NormalizeToAvaloniaContent(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                       .Replace("\r", "\n", StringComparison.Ordinal)
                       .Replace("\n", "\r", StringComparison.Ordinal);
        }

        private static string OffsetToTkIndex(string content, int offset)
        {
            offset = Clamp(offset, 0, content.Length);
            int line = 1;
            int column = 0;
            for (int i = 0; i < offset && i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    line++;
                    column = 0;
                }
                else
                {
                    column++;
                }
            }
            return $"{line}.{column}";
        }

        private static int TkIndexToOffset(string index, string content)
        {
            if (string.IsNullOrWhiteSpace(index))
            {
                return 0;
            }

            string[] parts = index.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            int line, column;
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out line) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out column))
            {
                return 0;
            }

            line = Math.Max(line, 1);
            column = Math.Max(column, 0);

            int currentLine = 1;
            int offset = 0;
            while (offset < content.Length && currentLine < line)
            {
                if (content[offset] == '\n')
                {
                    currentLine++;
                }
                offset++;
            }

            offset += column;
            return Clamp(offset, 0, content.Length);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}

