// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{

    public static class CategoryTierDefinitions
    {

        [NotNull]
        public static readonly Dictionary<string, string> CategoryDefinitions = new Dictionary<string, string>


(System.StringComparer.Ordinal)
        {

            ["Patch"] = "Official or community patches that fix bugs and issues in the base game.",
            ["Bugfix"] = "Mods that fix specific bugs, glitches, or technical issues.",
            ["Bug Fix"] = "Mods that fix specific bugs, glitches, or technical issues.",
            ["Graphics Improvement"] = "Mods that enhance visual quality, textures, models, or lighting.",
            ["Graphical Improvement"] = "Mods that enhance visual quality, textures, models, or lighting.",
            ["Graphics Improvement & Bugfix"] = "Mods that both fix bugs and improve visual quality.",
            ["Bugfix & Graphics Improvement"] = "Mods that both fix bugs and improve visual quality.",
            ["Bugfix & Graphics Improvement & Immersion"] = "Mods that fix bugs, improve graphics, and enhance immersion.",
            ["Bugfix, Graphics Improvement & Immersion"] = "Mods that fix bugs, improve graphics, and enhance immersion.",
            ["Bugfix, Immersion, Mechanics Change & Restored Content"] = "Comprehensive mods that fix bugs, enhance immersion, change mechanics, and restore content.",

            ["Mechanics Change"] = "Mods that alter game mechanics, rules, or gameplay systems.",
            ["Mechanics Change & Patch"] = "Mods that change game mechanics and include patches.",
            ["Mechanics Change & Immersion"] = "Mods that change game mechanics while enhancing immersion.",
            ["Mechanics Change, Bugfix & Immersion"] = "Mods that change mechanics, fix bugs, and enhance immersion.",
            ["Gameplay"] = "Mods that modify gameplay elements, combat, or game systems.",

            ["Appearance Change"] = "Mods that change the appearance of characters, items, or environments.",
            ["Appearance Change & Graphics Improvement"] = "Mods that change appearances and improve graphics quality.",
            ["Appearance Change & Bugfix"] = "Mods that change appearances and fix related bugs.",
            ["Appearance Change, Immersion & Graphics Improvement"] = "Mods that change appearances, enhance immersion, and improve graphics.",
            ["Appearance Change, Bugfix & Graphics Improvement"] = "Mods that change appearances, fix bugs, and improve graphics.",

            ["Immersion"] = "Mods that enhance role-playing immersion, dialogue, or story elements.",
            ["Immersion & Appearance Change"] = "Mods that enhance immersion and change appearances.",
            ["Immersion & Mechanics Change"] = "Mods that enhance immersion and change game mechanics.",
            ["Immersion & Graphics Improvement"] = "Mods that enhance immersion and improve graphics.",
            ["Story"] = "Mods that add, modify, or enhance story content and narrative elements.",
            ["Restored Content"] = "Mods that restore content that was cut or unused in the original game.",
            ["Restored Content & Immersion"] = "Mods that restore content and enhance immersion.",
            ["Added Content"] = "Mods that add new content, areas, characters, or features to the game.",
            ["Added Content, Appearance Change & Immersion"] = "Mods that add content, change appearances, and enhance immersion.",
            ["Added Content & Immersion"] = "Mods that add new content and enhance immersion.",

            ["UI"] = "Mods that modify the user interface, menus, or HUD elements.",
            ["Audio"] = "Mods that change or improve audio, music, or sound effects.",

            ["Graphics Improvement & Immersion"] = "Mods that improve graphics and enhance immersion.",
            ["Graphics Improvement & Appearance Change"] = "Mods that improve graphics and change appearances.",
            ["Patch & Graphics Improvement"] = "Mods that provide patches and improve graphics.",
            ["Bugfix & Immersion"] = "Mods that fix bugs and enhance immersion.",
            ["Bugfix, Graphics Improvement & Appearance Change"] = "Mods that fix bugs, improve graphics, and change appearances.",
            ["Appearance Change & Immersion"] = "Mods that change appearances and enhance immersion.",
        };


        [NotNull]
        public static readonly Dictionary<string, string> TierDefinitions = new Dictionary<string, string>


(System.StringComparer.Ordinal)
        {
            ["1 - Essential"] = "Critical mods that fix major bugs or provide essential improvements.",
            ["2 - Recommended"] = "High-quality mods that significantly improve the game experience. Strongly recommended for most players.",
            ["3 - Suggested"] = "Good quality mods that enhance specific aspects of the game. Recommended for players who want more content.",
            ["4 - Optional"] = "Mods that are nice to have but not necessary. Install based on personal preference.",
        };



        [NotNull]
        public static string GetCategoryDescription([CanBeNull] string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return "No category specified.";
            }

            return CategoryDefinitions.TryGetValue(category, out string description)
                ? description
                : $"Custom category: {category}";
        }



        [NotNull]
        public static string GetTierDescription([CanBeNull] string tier)
        {
            if (string.IsNullOrEmpty(tier))
            {
                return "No tier specified.";
            }

            string normalizedTier = NormalizeTier(tier);

            return TierDefinitions.TryGetValue(normalizedTier, out string description)
                ? description
                : $"Custom tier: {tier}";
        }


        [NotNull]
        public static string NormalizeTier([CanBeNull] string tier)
        {
            if (string.IsNullOrWhiteSpace(tier))
            {
                return string.Empty;
            }

            string trimmedTier = tier.Trim();

            if (TierDefinitions.ContainsKey(trimmedTier))
            {
                return trimmedTier;
            }

            string lowerTier = trimmedTier.ToLowerInvariant();


            string tierName = System.Text.RegularExpressions.Regex.Replace(
                lowerTier,
                @"^\s*\d+\s*[-\s]*|[-\s]*\d+\s*$",
                string.Empty
            ).Trim();


            if (tierName.Contains("essential"))
            {
                return "1 - Essential";
            }

            if (tierName.Contains("recommend"))
            {
                return "2 - Recommended";
            }

            if (tierName.Contains("suggest"))
            {
                return "3 - Suggested";
            }

            if (tierName.Contains("option"))
            {
                return "4 - Optional";
            }

            return trimmedTier;
        }
    }
}
