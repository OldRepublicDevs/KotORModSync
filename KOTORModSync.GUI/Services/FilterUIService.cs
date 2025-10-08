// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using KOTORModSync.Core;
using KOTORModSync.Models;
using ModComponent = KOTORModSync.Core.ModComponent;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for tier and category filter UI logic
	/// </summary>
	public class FilterUIService
	{
		private readonly MainConfig _mainConfig;
		private readonly ObservableCollection<TierFilterItem> _tierItems = new ObservableCollection<TierFilterItem>();
		private readonly ObservableCollection<SelectionFilterItem> _categoryItems = new ObservableCollection<SelectionFilterItem>();

		public FilterUIService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

		/// <summary>
		/// Initializes filter UI with components
		/// </summary>
		public void InitializeFilters(
			List<ModComponent> components,
			ComboBox tierComboBox,
			ItemsControl categoryItemsControl)
		{
			try
			{
				// Initialize Tier Selection
				_tierItems.Clear();
				var tierCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				var tierPriorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

				int priority = 1;

				// Sort by numeric prefix if present
				IOrderedEnumerable<ModComponent> sortedComponents = components.OrderBy(x =>
				{
					if ( string.IsNullOrEmpty(x.Tier) )
						return (int.MaxValue, string.Empty);

					string tier = x.Tier.Trim();
					int dashIndex = tier.IndexOf('-');
					if ( dashIndex > 0 )
					{
						string numPart = tier.Substring(0, dashIndex).Trim();
						if ( int.TryParse(numPart, out int num) )
							return (num, tier);
					}

					return (int.MaxValue, tier);
				}).ThenBy(x => x.Tier, StringComparer.OrdinalIgnoreCase);

				foreach ( ModComponent c in sortedComponents )
				{
					if ( string.IsNullOrEmpty(c.Tier) )
						continue;

					if ( !tierCounts.TryGetValue(c.Tier, out int value) )
					{
						value = 0;
						tierCounts[c.Tier] = value;
						tierPriorities[c.Tier] = priority;
						Logger.LogVerbose($"Assigning tier '{c.Tier}' priority {priority}");
						priority++;
					}
					tierCounts[c.Tier] = ++value;
				}

				foreach ( KeyValuePair<string, int> kvp in tierCounts.OrderBy(x => tierPriorities[x.Key]) )
				{
					var item = new TierFilterItem
					{
						Name = kvp.Key,
						Count = kvp.Value,
						Priority = tierPriorities[kvp.Key],
						IsSelected = false
					};
					_tierItems.Add(item);
				}

				// Initialize Category Selection
				_categoryItems.Clear();
				var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

				foreach ( ModComponent c in components )
				{
					if ( c.Category.Count == 0 )
						continue;

					foreach ( string cat in c.Category )
					{
						if ( string.IsNullOrEmpty(cat) )
							continue;

						if ( !categoryCounts.ContainsKey(cat) )
							categoryCounts[cat] = 0;
						categoryCounts[cat]++;
					}
				}

				foreach ( KeyValuePair<string, int> kvp in categoryCounts.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase) )
				{
					var item = new SelectionFilterItem
					{
						Name = kvp.Key,
						Count = kvp.Value,
						IsSelected = false
					};
					_categoryItems.Add(item);
				}

				// Bind to UI
				if ( tierComboBox != null )
					tierComboBox.ItemsSource = _tierItems;

				if ( categoryItemsControl != null )
					categoryItemsControl.ItemsSource = _categoryItems;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error initializing filter UI");
			}
		}

		/// <summary>
		/// Selects components by tier (including higher priority tiers)
		/// </summary>
		public void SelectByTier(
			TierFilterItem selectedTierItem,
			Action<ModComponent, HashSet<ModComponent>> onComponentChecked,
			Action onUIRefresh)
		{
			try
			{
				if ( selectedTierItem == null )
				{
					Logger.LogWarning("No tier selected");
					return;
				}

				var visitedComponents = new HashSet<ModComponent>();
				var tiersToInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				// Build list of tiers to include
				foreach ( TierFilterItem tierItem in _tierItems )
				{
					if ( tierItem.Priority <= selectedTierItem.Priority )
					{
						_ = tiersToInclude.Add(tierItem.Name);
						Logger.LogVerbose($"Including tier: '{tierItem.Name}' (Priority: {tierItem.Priority})");
					}
				}

				Logger.LogVerbose($"Selected tier: '{selectedTierItem.Name}' (Priority: {selectedTierItem.Priority})");

				// Get matching components
				var matchingMods = _mainConfig.allComponents.Where(c =>
					!string.IsNullOrEmpty(c.Tier) && tiersToInclude.Contains(c.Tier)
				).ToList();

				Logger.LogVerbose($"Matched {matchingMods.Count} components by tier");

				// Select all matching mods
				Dispatcher.UIThread.Post(() =>
				{
					foreach ( ModComponent component in matchingMods )
					{
						if ( !component.IsSelected )
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, visitedComponents);
						}
					}

					onUIRefresh?.Invoke();
					Logger.Log($"Selected {matchingMods.Count} mods in tier '{selectedTierItem.Name}' and higher priority tiers");
				}, DispatcherPriority.Normal);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error selecting by tier");
			}
		}

		/// <summary>
		/// Applies category selections to select matching components
		/// </summary>
		public void ApplyCategorySelections(
			Action<ModComponent, HashSet<ModComponent>> onComponentChecked,
			Action onUIRefresh)
		{
			try
			{
				var selectedCategories = _categoryItems.Where(c => c.IsSelected).Select(c => c.Name).ToList();

				if ( selectedCategories.Count == 0 )
				{
					Logger.LogWarning("No categories selected");
					return;
				}

				var visitedComponents = new HashSet<ModComponent>();

				// Get matching components
				var matchingMods = _mainConfig.allComponents.Where(c =>
					c.Category.Count > 0 && c.Category.Any(cat => selectedCategories.Contains(cat))
				).ToList();

				Logger.LogVerbose($"Categories selected: {string.Join(", ", selectedCategories)}");
				Logger.LogVerbose($"Matched {matchingMods.Count} components by category");

				// Select all matching mods
				Dispatcher.UIThread.Post(() =>
				{
					foreach ( ModComponent component in matchingMods )
					{
						if ( !component.IsSelected )
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, visitedComponents);
						}
					}

					onUIRefresh?.Invoke();
					Logger.Log($"Selected {matchingMods.Count} mods in categories: {string.Join(", ", selectedCategories)}");
				}, DispatcherPriority.Normal);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error applying category selections");
			}
		}

		/// <summary>
		/// Clears all category selections
		/// </summary>
		public void ClearCategorySelections(Action<SelectionFilterItem, PropertyChangedEventHandler> onPropertyChangedHandler)
		{
			try
			{
				foreach ( SelectionFilterItem item in _categoryItems )
				{
					if ( onPropertyChangedHandler != null )
						onPropertyChangedHandler(item, (s, e) => { }); // Unsubscribe

					item.IsSelected = false;

					if ( onPropertyChangedHandler != null )
						onPropertyChangedHandler(item, (s, e) => { }); // Re-subscribe
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error clearing category selections");
			}
		}

		/// <summary>
		/// Gets the tier items collection
		/// </summary>
		public ObservableCollection<TierFilterItem> TierItems => _tierItems;

		/// <summary>
		/// Gets the category items collection
		/// </summary>
		public ObservableCollection<SelectionFilterItem> CategoryItems => _categoryItems;
	}
}

