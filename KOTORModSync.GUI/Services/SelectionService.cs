// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{

	public class SelectionService
	{
		private readonly MainConfig _mainConfig;

		public SelectionService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

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

		public void DeselectAll(Action<ModComponent, HashSet<ModComponent>> componentCheckboxUnchecked)
		{
			try
			{

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

		public void SelectByTier(string selectedTier, int selectedPriority, List<string> allTierNames, List<int> allTierPriorities, Action<ModComponent, HashSet<ModComponent>> componentCheckboxChecked)
		{
			try
			{
				var visitedComponents = new HashSet<ModComponent>();
				var tiersToInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				for ( int i = 0; i < allTierNames.Count; i++ )
				{
					if ( allTierPriorities[i] <= selectedPriority )
					{
						_ = tiersToInclude.Add(allTierNames[i]);
					}
				}

				Logger.LogVerbose($"Selecting tier '{selectedTier}' (Priority: {selectedPriority})");
				Logger.LogVerbose($"Including tiers: {string.Join(", ", tiersToInclude)}");

				var matchingMods = _mainConfig.allComponents.Where(c =>
					!string.IsNullOrEmpty(c.Tier) && tiersToInclude.Contains(c.Tier)
				).ToList();

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

				var matchingMods = _mainConfig.allComponents.Where(c =>
					c.Category.Count > 0 && c.Category.Any(cat => selectedCategories.Contains(cat))
				).ToList();

				Logger.LogVerbose($"Categories selected: {string.Join(", ", selectedCategories)}");
				Logger.LogVerbose($"Matched {matchingMods.Count} components by category");

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

