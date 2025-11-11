// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia.Data.Converters;

using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public partial class SpoilerFreeContentConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is null || values.Count < 4)
            {
                return string.Empty;
            }

            // First value should be the regular property name (e.g., "Description")
            // Second value should be the ModComponent
            // Third value should be the SpoilerFreeMode boolean
            // Fourth value should be the spoiler-free property name (e.g., "DescriptionSpoilerFree")

            string regularPropertyName = values[0]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(regularPropertyName))
            {
                return string.Empty;
            }

            if (!(values[1] is ModComponent component))
            {
                return string.Empty;
            }

            if (!(values[2] is bool spoilerFreeMode))
            {
                return string.Empty;
            }

            string spoilerFreePropertyName = values[3]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(spoilerFreePropertyName))
            {
                return string.Empty;
            }

            // Use reflection to dynamically get the property value
            return GetPropertyValue(component, regularPropertyName, spoilerFreePropertyName, spoilerFreeMode);
        }

        /// <summary>
        /// Dynamically retrieves the appropriate property value based on spoiler-free mode.
        /// Includes automatic fallback generation when spoiler-free properties are empty.
        /// </summary>
        private static string GetPropertyValue(ModComponent component, string regularPropertyName, string spoilerFreePropertyName, bool spoilerFreeMode)
        {
            try
            {
                Type componentType = typeof(ModComponent);

                // If in spoiler-free mode, try to get the spoiler-free version first
                if (spoilerFreeMode)
                {
                    PropertyInfo spoilerFreeProperty = componentType.GetProperty(spoilerFreePropertyName);

                    if (spoilerFreeProperty != null)
                    {
                        object spoilerFreeValue = spoilerFreeProperty.GetValue(component);
                        if (spoilerFreeValue is string spoilerFreeString && !string.IsNullOrWhiteSpace(spoilerFreeString))
                        {
                            return spoilerFreeString;
                        }
                    }

                    // If spoiler-free property is empty, generate automatic fallback content
                    return GenerateSpoilerFreeContent(component, regularPropertyName);
                }

                // Get the regular property value
                PropertyInfo regularProperty = componentType.GetProperty(regularPropertyName);
                if (regularProperty != null)
                {
                    object regularValue = regularProperty.GetValue(component);
                    if (regularValue is string regularString)
                    {
                        return regularString ?? string.Empty;
                    }
                }

                // If property doesn't exist or is not a string, return empty
                return string.Empty;
            }
            catch (Exception)
            {
                // If anything goes wrong, return empty string
                return string.Empty;
            }
        }

        /// <summary>
        /// Generates automatic spoiler-free content when the dedicated spoiler-free property is empty.
        /// Creates generic, non-spoiler descriptions based on the content type.
        /// </summary>
        private static string GenerateSpoilerFreeContent(ModComponent component, string propertyName)
        {
            switch (propertyName)
            {
                case "Name":
                    return GenerateSpoilerFreeName(component);

                case "Description":
                    return GenerateSpoilerFreeDescription(component);

                case "Directions":
                    return "Installation instructions available. Please review carefully before proceeding.";

                case "DownloadInstructions":
                    return "Download instructions provided. Follow the steps to obtain the required files.";

                case "UsageWarning":
                    return "⚠️ Please read the full warning before using this mod.";

                case "Screenshots":
                    return string.Empty; // Don't generate fallback for screenshots

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Generates a spoiler-free name based on component metadata.
        /// Uses category, tier, and author info without revealing specific content.
        /// PUBLIC ENTRY POINT for use by other classes.
        /// </summary>
        public static string GenerateAutoName(ModComponent component)
        {
            return GenerateSpoilerFreeName(component);
        }

        /// <summary>
        /// Generates a spoiler-free name based on component metadata.
        /// Creates a case-sensitive acronym from the original name plus metadata.
        /// Example: "Sith assassins With Lightsabers" → "SaWL (Story/Characters, Tier 1, by JCarter426)"
        /// </summary>
        private static string GenerateSpoilerFreeName(ModComponent component)
        {
            if (component == null)
            {
                return "Mod Component";
            }

            var nameParts = new List<string>();

            // Generate case-sensitive acronym from the original name
            string acronym = GenerateAcronym(component.Name);
            if (!string.IsNullOrWhiteSpace(acronym))
            {
                nameParts.Add(acronym);
            }

            // Build metadata suffix
            var metadata = new List<string>();

            // Add category if available
            if (component.Category != null && component.Category.Count > 0)
            {
                metadata.Add(string.Join("/", component.Category));
            }

            // Add tier if available
            if (!string.IsNullOrWhiteSpace(component.Tier))
            {
                metadata.Add($"Tier {component.Tier}");
            }

            // Add author if available
            if (!string.IsNullOrWhiteSpace(component.Author))
            {
                metadata.Add($"by {component.Author}");
            }

            // Combine acronym with metadata in parentheses
            if (metadata.Count > 0)
            {
                if (nameParts.Count > 0)
                {
                    nameParts.Add($"({string.Join(", ", metadata)})");
                }
                else
                {
                    // No acronym, use metadata directly
                    nameParts.Add(string.Join(", ", metadata));
                }
            }
            else if (nameParts.Count == 0)
            {
                // No acronym and no metadata
                nameParts.Add("Mod");
            }

            return string.Join(" ", nameParts);
        }

        /// <summary>
        /// Generates a case-sensitive acronym from a mod name.
        /// Preserves uppercase letters and includes lowercase for significant words.
        /// Examples:
        /// - "Sith assassins With Lightsabers" → "SaWL"
        /// - "High Quality Blasters" → "HQB"
        /// - "JC's Weapons" → "JCW"
        /// - "K1 Restoration" → "K1R"
        /// </summary>
        private static string GenerateAcronym(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var acronymChars = new List<char>();

            // Common words to skip (articles, prepositions, conjunctions)
            var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "an", "the", "of", "in", "on", "at", "to", "for", "and", "or", "but",
                "is", "are", "was", "were", "be", "been", "being"
            };

            // Split into words
            string[] words = name.Split(new[] { ' ', '-', '_', ':', '\'', '"' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                if (string.IsNullOrWhiteSpace(word))
                {
                    continue;
                }

                // Skip common filler words unless it's a single-letter word or starts with uppercase
                if (skipWords.Contains(word) && word.Length > 1 && !char.IsUpper(word[0]))
                {
                    continue;
                }

                // Check if word has mixed case or starts with uppercase
                bool hasUpperCase = word.Any(c => char.IsUpper(c));

                if (hasUpperCase)
                {
                    // For mixed case or capitalized words, take the first character
                    // but preserve case of significant letters
                    foreach (char c in word)
                    {
                        if (char.IsUpper(c) || (acronymChars.Count == 0 && char.IsLetter(c)))
                        {
                            acronymChars.Add(c);
                            // Only take first uppercase or first letter for this word
                            if (char.IsUpper(c))
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // All lowercase word - take first letter as lowercase
                    if (char.IsLetter(word[0]))
                    {
                        acronymChars.Add(word[0]);
                    }
                }
            }

            // Limit acronym length to prevent overly long results
            const int maxAcronymLength = 8;
            if (acronymChars.Count > maxAcronymLength)
            {
                acronymChars = acronymChars.Take(maxAcronymLength).ToList();
            }

            return acronymChars.Count > 0 ? new string(acronymChars.ToArray()) : string.Empty;
        }

        /// <summary>
        /// Generates a spoiler-free description based on mod metadata.
        /// Provides informational content without revealing story elements.
        /// Includes content metrics (word counts, instruction complexity, etc.)
        /// PUBLIC ENTRY POINT for use by other classes.
        /// </summary>
        public static string GenerateSpoilerFreeDescription(ModComponent component)
        {
            if (component == null)
            {
                return "Mod description available.";
            }

            var descriptionParts = new List<string>();

            // Generate acronym for consistency with name
            string acronym = GenerateAcronym(component.Name);
            if (!string.IsNullOrWhiteSpace(acronym))
            {
                descriptionParts.Add($"[{acronym}]");
            }

            // Add category-based description
            if (component.Category != null && component.Category.Count > 0)
            {
                string categoryStr = string.Join(", ", component.Category);
                descriptionParts.Add($"This {categoryStr} modification enhances your gameplay.");
            }
            else
            {
                descriptionParts.Add("This modification enhances your gameplay.");
            }

            // Add tier-based description
            if (!string.IsNullOrWhiteSpace(component.Tier))
            {
                string tierDescription;
                if (string.Equals(component.Tier, "1", StringComparison.Ordinal))
                {
                    tierDescription = "Essential/highly recommended";
                }
                else if (string.Equals(component.Tier, "2", StringComparison.Ordinal))
                {
                    tierDescription = "Strongly recommended";
                }
                else if (string.Equals(component.Tier, "3", StringComparison.Ordinal))
                {
                    tierDescription = "Recommended";
                }
                else if (string.Equals(component.Tier, "4", StringComparison.Ordinal))
                {
                    tierDescription = "Suggested optional";
                }
                else if (string.Equals(component.Tier, "Optional", StringComparison.Ordinal))
                {
                    tierDescription = "Optional";
                }
                else
                {
                    tierDescription = $"Tier {component.Tier}";
                }
                descriptionParts.Add($"({tierDescription})");
            }

            // Add content metrics for transparency
            var metrics = new List<string>();

            // Installation complexity
            if (component.Instructions != null && component.Instructions.Count > 0)
            {
                metrics.Add($"{component.Instructions.Count} installation step(s)");
            }

            // Customization options
            if (component.Options != null && component.Options.Count > 0)
            {
                metrics.Add($"{component.Options.Count} customization option(s)");
            }

            // Installation method
            if (!string.IsNullOrWhiteSpace(component.InstallationMethod))
            {
                metrics.Add($"uses {component.InstallationMethod}");
            }

            if (metrics.Count > 0)
            {
                descriptionParts.Add($"Installation: {string.Join(", ", metrics)}.");
            }

            // Content availability indicators
            var contentFeatures = new List<string>();

            if (!string.IsNullOrWhiteSpace(component.Description) && component.Description.Length > 50)
            {
                int wordCount = component.Description.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                contentFeatures.Add($"detailed description ({wordCount} words)");
            }

            if (!string.IsNullOrWhiteSpace(component.Directions))
            {
                contentFeatures.Add("installation notes");
            }

            if (!string.IsNullOrWhiteSpace(component.DownloadInstructions))
            {
                contentFeatures.Add("download guide");
            }

            if (!string.IsNullOrWhiteSpace(component.UsageWarning))
            {
                contentFeatures.Add("usage warnings");
            }

            if (!string.IsNullOrWhiteSpace(component.Screenshots))
            {
                contentFeatures.Add("screenshots");
            }

            if (contentFeatures.Count > 0)
            {
                descriptionParts.Add($"Includes: {string.Join(", ", contentFeatures)}.");
            }

            // Add author info
            if (!string.IsNullOrWhiteSpace(component.Author))
            {
                descriptionParts.Add($"By {component.Author}.");
            }

            // Add language support info
            if (component.Language != null && component.Language.Count > 0)
            {
                string languageStr = string.Join(", ", component.Language);
                descriptionParts.Add($"Language support: {languageStr}.");
            }

            // Resource information
            if (component.ResourceRegistry != null && component.ResourceRegistry.Count > 0)
            {
                descriptionParts.Add($"{component.ResourceRegistry.Count} download source(s) available.");
            }

            return string.Join(" ", descriptionParts);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
