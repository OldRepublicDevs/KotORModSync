// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
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

	public class FilterUIService
	{
		private readonly MainConfig _mainConfig;
		private readonly ObservableCollection<TierFilterItem> _tierItems = new ObservableCollection<TierFilterItem>();
		private readonly ObservableCollection<SelectionFilterItem> _categoryItems = new ObservableCollection<SelectionFilterItem>();

		public FilterUIService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

		public void InitializeFilters(
			List<ModComponent> components,
			ComboBox tierComboBox,
			ItemsControl categoryItemsControl)
		{
			if (!Dispatcher.UIThread.CheckAccess())
			{
				Dispatcher.UIThread.Post(() => InitializeFilters(components, tierComboBox, categoryItemsControl), DispatcherPriority.Normal);
				return;
			}
			try
			{
				if (!Dispatcher.UIThread.CheckAccess())
				{
					Dispatcher.UIThread.Post(() => InitializeFilters(components, tierComboBox, categoryItemsControl), DispatcherPriority.Normal);
					return;
				}

				_tierItems.Clear();
				var tierCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				var tierPriorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

				int priority = 1;

				IOrderedEnumerable<ModComponent> sortedComponents = components.OrderBy(x =>
				{
					if (string.IsNullOrEmpty(x.Tier))
						return (int.MaxValue, string.Empty);

					string tier = x.Tier.Trim();
					int dashIndex = tier.IndexOf('-');
					if (dashIndex > 0)
					{
						string numPart = tier.Substring(0, dashIndex).Trim();
						if (int.TryParse(numPart, out int num))
							return (num, tier);
					}

					return (int.MaxValue, tier);
				}).ThenBy(x => x.Tier, StringComparer.OrdinalIgnoreCase);

				foreach (ModComponent c in sortedComponents)
				{
					if (string.IsNullOrEmpty(c.Tier))
						continue;

					if (!tierCounts.TryGetValue(c.Tier, out int value))
					{
						value = 0;
						tierCounts[c.Tier] = value;
						tierPriorities[c.Tier] = priority;
						Logger.LogVerbose($"Assigning tier '{c.Tier}' priority {priority}");
						priority++;
					}
					tierCounts[c.Tier] = ++value;
				}

				foreach (KeyValuePair<string, int> kvp in tierCounts.OrderBy(x => tierPriorities[x.Key]))
				{
					var item = new TierFilterItem
					{
						Name = kvp.Key,
						Count = kvp.Value,
						Priority = tierPriorities[kvp.Key],
						IsSelected = false,
					};
					_tierItems.Add(item);
				}

				_categoryItems.Clear();
				var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

				foreach (ModComponent c in components)
				{
					if (c.Category.Count == 0)
						continue;

					foreach (string cat in c.Category)
					{
						if (string.IsNullOrEmpty(cat))
							continue;

						if (!categoryCounts.ContainsKey(cat))
							categoryCounts[cat] = 0;
						categoryCounts[cat]++;
					}
				}

				foreach (KeyValuePair<string, int> kvp in categoryCounts.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
				{
					var item = new SelectionFilterItem
					{
						Name = kvp.Key,
						Count = kvp.Value,
						IsSelected = false,
					};
					_categoryItems.Add(item);
				}

				if (tierComboBox != null)
					tierComboBox.ItemsSource = _tierItems;

				if (categoryItemsControl != null)
					categoryItemsControl.ItemsSource = _categoryItems;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error initializing filter UI");
			}
		}

		public void SelectByTier(
			TierFilterItem selectedTierItem,
			Action<ModComponent, HashSet<ModComponent>> onComponentChecked,
			Action onUIRefresh)
		{
			try
			{
				if (selectedTierItem is null)
				{
					Logger.LogWarning("No tier selected");
					return;
				}

				var visitedComponents = new HashSet<ModComponent>();
				var tiersToInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (TierFilterItem tierItem in _tierItems)
				{
					if (tierItem.Priority <= selectedTierItem.Priority)
					{
						_ = tiersToInclude.Add(tierItem.Name);
						Logger.LogVerbose($"Including tier: '{tierItem.Name}' (Priority: {tierItem.Priority})");
					}
				}

				Logger.LogVerbose($"Selected tier: '{selectedTierItem.Name}' (Priority: {selectedTierItem.Priority})");

				var matchingMods = _mainConfig.allComponents.Where(c =>
					!string.IsNullOrEmpty(c.Tier) && tiersToInclude.Contains(c.Tier)
				).ToList();

				Logger.LogVerbose($"Matched {matchingMods.Count} components by tier");

				Dispatcher.UIThread.Post(() =>
				{
					foreach (ModComponent component in matchingMods)
					{
						if (!component.IsSelected)
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, visitedComponents);
						}
					}

					onUIRefresh?.Invoke();
					Logger.Log($"Selected {matchingMods.Count} mods in tier '{selectedTierItem.Name}' and higher priority tiers");
				}, DispatcherPriority.Normal);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error selecting by tier");
			}
		}

		public void ApplyCategorySelections(
			Action<ModComponent, HashSet<ModComponent>> onComponentChecked,
			Action onUIRefresh)
		{
			try
			{
				var selectedCategories = new HashSet<string>(
					_categoryItems.Where(c => c.IsSelected).Select(c => c.Name),
					StringComparer.OrdinalIgnoreCase
				);

				if (selectedCategories.Count == 0)
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

				Dispatcher.UIThread.Post(() =>
				{
					foreach (ModComponent component in matchingMods)
					{
						if (!component.IsSelected)
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, visitedComponents);
						}
					}

					onUIRefresh?.Invoke();
					Logger.Log($"Selected {matchingMods.Count} mods in categories: {string.Join(", ", selectedCategories)}");
				}, DispatcherPriority.Normal);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error applying category selections");
			}
		}

		public void ClearCategorySelections(Action<SelectionFilterItem, PropertyChangedEventHandler> onPropertyChangedHandler)
		{
			if (!Dispatcher.UIThread.CheckAccess())
			{
				Dispatcher.UIThread.Post(() => ClearCategorySelections(onPropertyChangedHandler), DispatcherPriority.Normal);
				return;
			}
			try
			{
				foreach (SelectionFilterItem item in _categoryItems)
				{
					if (onPropertyChangedHandler != null)
						onPropertyChangedHandler(item, (s, e) => { });

					item.IsSelected = false;

					if (onPropertyChangedHandler != null)
						onPropertyChangedHandler(item, (s, e) => { });
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error clearing category selections");
			}
		}

		public ObservableCollection<TierFilterItem> TierItems => _tierItems;

		public ObservableCollection<SelectionFilterItem> CategoryItems => _categoryItems;
	}
}