// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KOTORModSync.Controls;
using KOTORModSync.Core;
using static KOTORModSync.Core.Services.ModManagementService;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for managing the mod list display and interactions
	/// </summary>
	public class ModListService
	{
		private readonly MainConfig _mainConfig;

		public ModListService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

		/// <summary>
		/// Filters the mod list based on search criteria
		/// </summary>
		public List<ModComponent> FilterModList(string searchText, ModSearchOptions options = null)
		{
			try
			{
				if ( string.IsNullOrWhiteSpace(searchText) )
					return _mainConfig.allComponents.ToList();

				options = options ?? new ModSearchOptions
				{
					SearchInName = true,
					SearchInAuthor = true,
					SearchInCategory = true,
					SearchInDescription = true
				};

				return _mainConfig.allComponents.Where(component =>
				{
					if ( options.SearchInName && component.Name?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 )
						return true;
					if ( options.SearchInAuthor && component.Author?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 )
						return true;
					if ( options.SearchInDescription && component.Description?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 )
						return true;
					if ( options.SearchInCategory && component.Category.Any(cat => cat?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) )
						return true;

					return false;
				}).ToList();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error filtering mod list");
				return _mainConfig.allComponents.ToList();
			}
		}

		/// <summary>
		/// Populates the mod list UI with components
		/// </summary>
		public static void PopulateModList(ListBox modListBox, List<ModComponent> components, Action updateModCounts)
		{
			try
			{
				if ( modListBox == null )
					return;

				modListBox.Items.Clear();

				foreach ( ModComponent component in components )
				{
					_ = modListBox.Items.Add(component);
				}

				updateModCounts?.Invoke();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error populating mod list");
			}
		}

		/// <summary>
		/// Refreshes mod list visuals without rebuilding the entire list
		/// </summary>
		public static void RefreshModListVisuals(ListBox modListBox, Action updateStepProgress)
		{
			try
			{
				if ( modListBox?.ItemsSource == null )
					return;

				// Force re-evaluation of all mod list items by refreshing the ItemsSource
				var currentItems = modListBox.ItemsSource;
				modListBox.ItemsSource = null;
				modListBox.ItemsSource = currentItems;

				// Update step progress after refreshing visuals
				updateStepProgress?.Invoke();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error refreshing mod list visuals");
			}
		}

		/// <summary>
		/// Refreshes a single component's visual state
		/// </summary>
		public static void RefreshSingleComponentVisuals(ListBox modListBox, ModComponent component)
		{
			try
			{
				if ( modListBox == null || component == null )
					return;

				// Ensure UI updates happen on the UI thread
				Dispatcher.UIThread.Post(() =>
				{
					try
					{
						// Find the container for this specific component
#pragma warning disable IDE0078 // Use pattern matching
						if ( !(modListBox.ContainerFromItem(component) is ListBoxItem container) )
							return;
#pragma warning restore IDE0078 // Use pattern matching

						// Find the ModListItem control
						if ( container.GetVisualDescendants().OfType<ModListItem>().FirstOrDefault() is ModListItem modListItem )
							// Directly call UpdateValidationState to refresh border colors
							modListItem.UpdateValidationState(component);
					}
					catch ( Exception ex )
					{
						Logger.LogException(ex, "Error refreshing component visuals on UI thread");
					}
				}, DispatcherPriority.Normal);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error posting visual refresh to UI thread");
			}
		}

		/// <summary>
		/// Refreshes all mod list item controls (context menus, visibility, etc.)
		/// </summary>
		public void RefreshModListItems(ListBox modListBox, bool editorMode, Func<ModComponent, ContextMenu> buildContextMenu)
		{
			try
			{
				if ( modListBox == null )
					return;

				// Force all ModListItem controls to refresh their context menus and visibility
				foreach ( object item in modListBox.Items )
				{
#pragma warning disable IDE0078 // Use pattern matching
					if ( !(item is ModComponent component) )
						continue;
#pragma warning restore IDE0078 // Use pattern matching

					// Find the container for this item
#pragma warning disable IDE0078 // Use pattern matching
					if ( !(modListBox.ContainerFromItem(item) is ListBoxItem container) )
						continue;
#pragma warning restore IDE0078 // Use pattern matching

					// Find the ModListItem control
					ModListItem modListItem = container.GetVisualDescendants().OfType<ModListItem>().FirstOrDefault();
					if ( modListItem == null )
						continue;

					// Trigger context menu rebuild
					modListItem.ContextMenu = buildContextMenu(component);

					// Update editor mode visibility for child elements
					if ( modListItem.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexBlock )
						indexBlock.IsVisible = editorMode;

					if ( modListItem.FindControl<TextBlock>("DragHandle") is TextBlock dragHandle )
						dragHandle.IsVisible = editorMode;

					// Update index if in editor mode
					if ( !editorMode )
						continue;
					int index = _mainConfig.allComponents.IndexOf(component);
					if ( index >= 0 && modListItem.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexTextBlock )
						indexTextBlock.Text = $"#{index + 1}";
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error refreshing mod list items");
			}
		}

		/// <summary>
		/// Updates mod count displays
		/// </summary>
		public void UpdateModCounts(
			TextBlock modCountText,
			TextBlock selectedCountText,
			CheckBox selectAllCheckBox,
			Action<bool> setSuppressSelectAllEvents)
		{
			try
			{
				if ( modCountText != null )
				{
					int totalCount = _mainConfig.allComponents.Count;
					modCountText.Text = totalCount == 1 ? "1 mod" : $"{totalCount} mods";
				}

				if ( selectedCountText != null )
				{
					int selectedCount = _mainConfig.allComponents.Count(c => c.IsSelected);
					selectedCountText.Text = selectedCount == 1 ? "1 selected" : $"{selectedCount} selected";
				}

				// Update SelectAllCheckBox state
				if ( selectAllCheckBox != null )
				{
					setSuppressSelectAllEvents?.Invoke(true);
					try
					{
						int totalCount = _mainConfig.allComponents.Count;
						int selectedCount = _mainConfig.allComponents.Count(c => c.IsSelected);

						if ( selectedCount == 0 )
							selectAllCheckBox.IsChecked = false;
						else if ( selectedCount == totalCount )
							selectAllCheckBox.IsChecked = true;
						else
							selectAllCheckBox.IsChecked = null;
					}
					finally
					{
						setSuppressSelectAllEvents?.Invoke(false);
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating mod counts");
			}
		}
	}
}

