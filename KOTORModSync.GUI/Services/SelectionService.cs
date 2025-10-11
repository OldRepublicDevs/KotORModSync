// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for bulk selection operations on components
	/// </summary>
	public class SelectionService
	{
		private readonly MainConfig _mainConfig;

		public SelectionService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

		/// <summary>
		/// Selects all components
		/// </summary>
		public void SelectAll(Action<ModComponent, HashSet<ModComponent>> componentCheckboxChecked)
		{
			try
			{
				var visitedComponents = new HashSet<ModComponent>();

				foreach ( ModComponent component in _mainConfig.allComponents )
				{
					if ( component.IsSelected )
						continue;
					component.IsSelected = true;
					componentCheckboxChecked?.Invoke(component, visitedComponents);
				}

				Logger.LogVerbose($"Selected all {_mainConfig.allComponents.Count} mods");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error selecting all mods");
			}
		}

		/// <summary>
		/// Deselects all components
		/// </summary>
		public void DeselectAll(Action<ModComponent, HashSet<ModComponent>> componentCheckboxUnchecked)
		{
			try
			{
				// Deselect all components without triggering dependency resolution
				// (Deselect all should be an absolute operation - no dependencies matter)
				foreach ( ModComponent component in _mainConfig.allComponents )
				{
					component.IsSelected = false;
				}

				Logger.LogVerbose($"Deselected all {_mainConfig.allComponents.Count} mods");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error deselecting all mods");
			}
		}

		/// <summary>
		/// Selects components by tier (including higher priority tiers)
		/// </summary>
		public void SelectByTier(string selectedTier, int selectedPriority, List<string> allTierNames, List<int> allTierPriorities, Action<ModComponent, HashSet<ModComponent>> componentCheckboxChecked)
		{
			try
			{
				var visitedComponents = new HashSet<ModComponent>();
				var tiersToInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				// Build list of tiers to include (selected tier + all higher priority tiers)
				for ( int i = 0; i < allTierNames.Count; i++ )
				{
					if ( allTierPriorities[i] <= selectedPriority )
					{
						_ = tiersToInclude.Add(allTierNames[i]);
					}
				}

				Logger.LogVerbose($"Selecting tier '{selectedTier}' (Priority: {selectedPriority})");
				Logger.LogVerbose($"Including tiers: {string.Join(", ", tiersToInclude)}");

				// Get matching components
				var matchingMods = _mainConfig.allComponents.Where(c =>
					!string.IsNullOrEmpty(c.Tier) && tiersToInclude.Contains(c.Tier)
				).ToList();

				// Select all matching mods
				foreach ( ModComponent component in matchingMods )
				{
					if ( component.IsSelected )
						continue;
					component.IsSelected = true;
					componentCheckboxChecked?.Invoke(component, visitedComponents);
				}

				Logger.Log($"Selected {matchingMods.Count} mods in tier '{selectedTier}' and higher priority tiers");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error selecting by tier");
			}
		}

		/// <summary>
		/// Selects components by categories
		/// </summary>
		public void SelectByCategories(List<string> selectedCategories, Action<ModComponent, HashSet<ModComponent>> componentCheckboxChecked)
		{
			try
			{
				if ( selectedCategories == null || selectedCategories.Count == 0 )
				{
					Logger.LogWarning("No categories selected");
					return;
				}

				var visitedComponents = new HashSet<ModComponent>();

				// Get mods matching the selected categories
				var matchingMods = _mainConfig.allComponents.Where(c =>
					c.Category.Count > 0 && c.Category.Any(cat => selectedCategories.Contains(cat))
				).ToList();

				Logger.LogVerbose($"Categories selected: {string.Join(", ", selectedCategories)}");
				Logger.LogVerbose($"Matched {matchingMods.Count} components by category");

				// Select all matching mods
				foreach ( ModComponent component in matchingMods )
				{
					if ( component.IsSelected )
						continue;
					component.IsSelected = true;
					componentCheckboxChecked?.Invoke(component, visitedComponents);
				}

				Logger.Log($"Selected {matchingMods.Count} mods in categories: {string.Join(", ", selectedCategories)}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error selecting by categories");
			}
		}
	}
}

