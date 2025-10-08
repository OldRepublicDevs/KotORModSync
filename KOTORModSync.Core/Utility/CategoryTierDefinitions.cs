// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	/// <summary>
	/// Provides definitions and descriptions for mod categories and tiers used in KOTORModSync.
	/// </summary>
	public static class CategoryTierDefinitions
	{
		/// <summary>
		/// Dictionary of category names to their descriptions.
		/// </summary>
		[NotNull]
		public static readonly Dictionary<string, string> CategoryDefinitions = new Dictionary<string, string>
		{
			// Core Categories
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

			// Gameplay Categories
			["Mechanics Change"] = "Mods that alter game mechanics, rules, or gameplay systems.",
			["Mechanics Change & Patch"] = "Mods that change game mechanics and include patches.",
			["Mechanics Change & Immersion"] = "Mods that change game mechanics while enhancing immersion.",
			["Mechanics Change, Bugfix & Immersion"] = "Mods that change mechanics, fix bugs, and enhance immersion.",
			["Gameplay"] = "Mods that modify gameplay elements, combat, or game systems.",

			// Visual Categories
			["Appearance Change"] = "Mods that change the appearance of characters, items, or environments.",
			["Appearance Change & Graphics Improvement"] = "Mods that change appearances and improve graphics quality.",
			["Appearance Change & Bugfix"] = "Mods that change appearances and fix related bugs.",
			["Appearance Change, Immersion & Graphics Improvement"] = "Mods that change appearances, enhance immersion, and improve graphics.",
			["Appearance Change, Bugfix & Graphics Improvement"] = "Mods that change appearances, fix bugs, and improve graphics.",

			// Content Categories
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

			// UI and Audio
			["UI"] = "Mods that modify the user interface, menus, or HUD elements.",
			["Audio"] = "Mods that change or improve audio, music, or sound effects.",

			// Combined Categories
			["Graphics Improvement & Immersion"] = "Mods that improve graphics and enhance immersion.",
			["Graphics Improvement & Appearance Change"] = "Mods that improve graphics and change appearances.",
			["Patch & Graphics Improvement"] = "Mods that provide patches and improve graphics.",
			["Bugfix & Immersion"] = "Mods that fix bugs and enhance immersion.",
			["Bugfix, Graphics Improvement & Appearance Change"] = "Mods that fix bugs, improve graphics, and change appearances.",
			["Appearance Change & Immersion"] = "Mods that change appearances and enhance immersion.",
		};

		/// <summary>
		/// Dictionary of tier names to their descriptions.
		/// </summary>
		[NotNull]
		public static readonly Dictionary<string, string> TierDefinitions = new Dictionary<string, string>
		{
			// Essential Tiers
			["Essential"] = "Critical mods that fix major bugs or provide essential improvements.",
			["1 - Essential"] = "Critical mods that fix major bugs or provide essential improvements.",

			// Recommended Tiers
			["Recommended"] = "High-quality mods that significantly improve the game experience. Strongly recommended for most players.",
			["2 - Recommended"] = "High-quality mods that significantly improve the game experience. Strongly recommended for most players.",

			// Suggested Tiers
			["Suggested"] = "Good quality mods that enhance specific aspects of the game. Recommended for players who want more content.",
			["3 - Suggested"] = "Good quality mods that enhance specific aspects of the game. Recommended for players who want more content.",

			// Optional Tiers
			["Optional"] = "Mods that are nice to have but not necessary. Install based on personal preference.",
			["4 - Optional"] = "Mods that are nice to have but not necessary. Install based on personal preference.",
			["4 - Option"] = "Mods that are nice to have but not necessary. Install based on personal preference.",
		};

		/// <summary>
		/// Gets the description for a category, returning a default message if not found.
		/// </summary>
		/// <param name="category">The category name.</param>
		/// <returns>The category description or a default message.</returns>
		[NotNull]
		public static string GetCategoryDescription([CanBeNull] string category)
		{
			if ( string.IsNullOrEmpty(category) )
				return "No category specified.";

			return CategoryDefinitions.TryGetValue(category, out string description)
				? description
				: $"Custom category: {category}";
		}

		/// <summary>
		/// Gets the description for a tier, returning a default message if not found.
		/// </summary>
		/// <param name="tier">The tier name.</param>
		/// <returns>The tier description or a default message.</returns>
		[NotNull]
		public static string GetTierDescription([CanBeNull] string tier)
		{
			if ( string.IsNullOrEmpty(tier) )
				return "No tier specified.";

			return TierDefinitions.TryGetValue(tier, out string description)
				? description
				: $"Custom tier: {tier}";
		}
	}
}
