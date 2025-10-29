// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
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

	public class ModListService
	{
		private readonly MainConfig _mainConfig;

		public ModListService( MainConfig mainConfig )
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException( nameof( mainConfig ) );
		}

		public List<ModComponent> FilterModList( string searchText, ModSearchOptions options = null )
		{
			try
			{
				if (string.IsNullOrWhiteSpace( searchText ))
					return _mainConfig.allComponents.ToList();

				options = options ?? new ModSearchOptions
				{
					SearchInName = true,
					SearchInAuthor = true,
					SearchInCategory = true,
					SearchInDescription = true
				};

				return _mainConfig.allComponents.Where( component =>
				{
					if (options.SearchInName && component.Name?.IndexOf( searchText, StringComparison.OrdinalIgnoreCase ) >= 0)
						return true;
					if (options.SearchInAuthor && component.Author?.IndexOf( searchText, StringComparison.OrdinalIgnoreCase ) >= 0)
						return true;
					if (options.SearchInDescription && component.Description?.IndexOf( searchText, StringComparison.OrdinalIgnoreCase ) >= 0)
						return true;
					if (options.SearchInCategory && component.Category.Any( cat => cat?.IndexOf( searchText, StringComparison.OrdinalIgnoreCase ) >= 0 ))
						return true;

					return false;
				} ).ToList();
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error filtering mod list" );
				return _mainConfig.allComponents.ToList();
			}
		}

		public static void PopulateModList( ListBox modListBox, List<ModComponent> components, Action updateModCounts )
		{
			try
			{
				if (modListBox == null)
					return;

				modListBox.Items.Clear();

				foreach (ModComponent component in components)
				{
					_ = modListBox.Items.Add( component );
				}

				updateModCounts?.Invoke();
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error populating mod list" );
			}
		}

		public static void RefreshModListVisuals( ListBox modListBox, Action updateStepProgress )
		{
			try
			{
				if (modListBox?.ItemsSource == null)
					return;

				var currentItems = modListBox.ItemsSource;
				modListBox.ItemsSource = null;
				modListBox.ItemsSource = currentItems;

				updateStepProgress?.Invoke();
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error refreshing mod list visuals" );
			}
		}

		public static void RefreshSingleComponentVisuals( ListBox modListBox, ModComponent component )
		{
			try
			{
				if (modListBox == null || component == null)
					return;

				Dispatcher.UIThread.Post( () =>
				{
					try
					{

						if (!(modListBox.ContainerFromItem( component ) is ListBoxItem container))
							return;

						if (container.GetVisualDescendants().OfType<ModListItem>().FirstOrDefault() is ModListItem modListItem)
						{

							modListItem.UpdateValidationState( component );

							var optionsContainer = modListItem.FindControl<ItemsControl>( "OptionsContainer" );
							if (optionsContainer != null)
							{

								var currentItems = optionsContainer.ItemsSource;
								optionsContainer.ItemsSource = null;
								optionsContainer.ItemsSource = currentItems;

								var currentVisibility = optionsContainer.IsVisible;
								optionsContainer.IsVisible = !currentVisibility;
								optionsContainer.IsVisible = currentVisibility;
							}
						}
					}
					catch (Exception ex)
					{
						Logger.LogException( ex, "Error refreshing component visuals on UI thread" );
					}
				}, DispatcherPriority.Normal );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error posting visual refresh to UI thread" );
			}
		}

		public void RefreshModListItems( ListBox modListBox, bool editorMode, Func<ModComponent, ContextMenu> buildContextMenu )
		{
			try
			{
				if (modListBox == null)
					return;

				foreach (object item in modListBox.Items)
				{
#pragma warning disable IDE0078
					if (!(item is ModComponent component))
						continue;
#pragma warning restore IDE0078

#pragma warning disable IDE0078
					if (!(modListBox.ContainerFromItem( item ) is ListBoxItem container))
						continue;
#pragma warning restore IDE0078

					ModListItem modListItem = container.GetVisualDescendants().OfType<ModListItem>().FirstOrDefault();
					if (modListItem == null)
						continue;

					modListItem.ContextMenu = buildContextMenu( component );

					if (modListItem.FindControl<TextBlock>( "IndexTextBlock" ) is TextBlock indexBlock)
						indexBlock.IsVisible = editorMode;

					if (modListItem.FindControl<TextBlock>( "DragHandle" ) is TextBlock dragHandle)
						dragHandle.IsVisible = editorMode;

					if (!editorMode)
						continue;
					int index = _mainConfig.allComponents.IndexOf( component );
					if (index >= 0 && modListItem.FindControl<TextBlock>( "IndexTextBlock" ) is TextBlock indexTextBlock)
						indexTextBlock.Text = $"#{index + 1}";
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error refreshing mod list items" );
			}
		}

		public void UpdateModCounts(
			TextBlock modCountText,
			TextBlock selectedCountText,
			CheckBox selectAllCheckBox,
			Action<bool> setSuppressSelectAllEvents )
		{
			try
			{
				if (modCountText != null)
				{
					int totalCount = _mainConfig.allComponents.Count;
					modCountText.Text = totalCount == 1 ? "1 mod" : $"{totalCount} mods";
				}

				if (selectedCountText != null)
				{
					int selectedCount = _mainConfig.allComponents.Count( c => c.IsSelected );
					selectedCountText.Text = selectedCount == 1 ? "1 selected" : $"{selectedCount} selected";
				}

				if (selectAllCheckBox != null)
				{
					setSuppressSelectAllEvents?.Invoke( true );
					try
					{
						int totalCount = _mainConfig.allComponents.Count;
						int selectedCount = _mainConfig.allComponents.Count( c => c.IsSelected );

						if (selectedCount == 0)
							selectAllCheckBox.IsChecked = false;
						else if (selectedCount == totalCount)
							selectAllCheckBox.IsChecked = true;
						else
							selectAllCheckBox.IsChecked = null;
					}
					finally
					{
						setSuppressSelectAllEvents?.Invoke( false );
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error updating mod counts" );
			}
		}
	}
}