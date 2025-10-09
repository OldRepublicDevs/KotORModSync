// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using KOTORModSync.Core;
using KOTORModSync.Services;

namespace KOTORModSync.Controls
{
	public partial class ModListItem : UserControl
	{
		// Static dictionary to store error messages per component
		private static readonly Dictionary<Guid, string> s_componentErrors = new Dictionary<Guid, string>();

		// Drag visual state properties
		public static readonly StyledProperty<bool> IsBeingDraggedProperty =
			AvaloniaProperty.Register<ModListItem, bool>(nameof(IsBeingDragged));

		public static readonly StyledProperty<bool> IsDropTargetProperty =
			AvaloniaProperty.Register<ModListItem, bool>(nameof(IsDropTarget));

		public bool IsBeingDragged
		{
			get => GetValue(IsBeingDraggedProperty);
			set => SetValue(IsBeingDraggedProperty, value);
		}

		public bool IsDropTarget
		{
			get => GetValue(IsDropTargetProperty);
			set => SetValue(IsDropTargetProperty, value);
		}

		public ModListItem()
		{
			AvaloniaXamlLoader.Load(this);

			// Add pointer enter/leave handlers for hover effect
			PointerEntered += OnPointerEntered;
			PointerExited += OnPointerExited;

			DataContextChanged += OnDataContextChanged;

			// Wire up checkbox events
			CheckBox checkbox = this.FindControl<CheckBox>("ComponentCheckBox");
			if ( checkbox != null )
				checkbox.IsCheckedChanged += OnCheckBoxChanged;

			// Wire up click to select
			PointerPressed += OnPointerPressed;

			// Wire up double-click
			DoubleTapped += OnDoubleTapped;

			// Wire up drag handle
			TextBlock dragHandle = this.FindControl<TextBlock>("DragHandle");
			if ( dragHandle != null )
				dragHandle.PointerPressed += OnDragHandlePressed;

			// Wire up main mod info events for selection logic
			Grid mainModInfo = this.FindControl<Grid>("MainModInfo");
			if ( mainModInfo != null )
			{
				mainModInfo.PointerPressed += OnMainModInfoPointerPressed;
				mainModInfo.DoubleTapped += OnMainModInfoDoubleTapped;
			}
		}

		private void OnMainModInfoPointerPressed(object sender, PointerPressedEventArgs e)
		{
			// Don't interfere with checkbox clicks within the main mod info
			if ( e.Source is CheckBox )
				return;

			// Set this component as current in MainWindow
			if ( DataContext is ModComponent component && this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.SetCurrentComponent(component);
		}

		private void OnMainModInfoDoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			// Don't interfere with checkbox clicks
			if ( e.Source is CheckBox )
				return;

			// Double-click toggles selection
			if ( !(DataContext is ModComponent component) )
				return;
			component.IsSelected = !component.IsSelected;
			if ( !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			mainWindow.UpdateModCounts();
			if ( component.IsSelected )
				mainWindow.ComponentCheckboxChecked(component, new HashSet<ModComponent>());
			else
				mainWindow.ComponentCheckboxUnchecked(component, new HashSet<ModComponent>());

			e.Handled = true; // Prevent the event from bubbling up
		}

		private void OnDoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			// Double-click toggles selection
			if ( !(DataContext is ModComponent component) )
				return;
			component.IsSelected = !component.IsSelected;
			if ( !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			mainWindow.UpdateModCounts();
			if ( component.IsSelected )
				mainWindow.ComponentCheckboxChecked(component, new HashSet<ModComponent>());
			else
				mainWindow.ComponentCheckboxUnchecked(component, new HashSet<ModComponent>());
		}

		private void OnDragHandlePressed(object sender, PointerPressedEventArgs e)
		{
			if ( !(DataContext is ModComponent component) || !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			mainWindow.StartDragComponent(component, e);
			e.Handled = true;
		}


		private void OnPointerPressed(object sender, PointerPressedEventArgs e)
		{
			// Don't interfere with drag handle or checkbox clicks
			if ( e.Source is TextBlock textBlock && textBlock.Name == "DragHandle" )
				return;
			if ( e.Source is CheckBox )
				return;

			// Set this component as current in MainWindow
			if ( DataContext is ModComponent component && this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.SetCurrentComponent(component);
		}

		private void OnCheckBoxChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			// The two-way binding will handle updating the ModComponent.IsSelected
			// We just need to notify the main window to update counts
			if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.OnComponentCheckBoxChanged(sender, e);
		}

		private void OnDataContextChanged(object sender, EventArgs e)
		{
			if ( !(DataContext is ModComponent component) )
				return;

			// Update from mod management service first to determine status
			UpdateFromModManagementService();

			// Set up rich tooltip based on current status
			UpdateTooltip(component);

			// Set up option selection change handlers
			SetupOptionSelectionHandlers(component);
		}

		/// <summary>
		/// Sets up property change handlers for option selections to update background colors
		/// </summary>
		private void SetupOptionSelectionHandlers(ModComponent component)
		{
			foreach ( Option option in component.Options )
			{
				// Remove existing handler if any
				option.PropertyChanged -= OnOptionSelectionChanged;
				// Add new handler
				option.PropertyChanged += OnOptionSelectionChanged;
			}
		}

		/// <summary>
		/// Handles property changes on options, specifically IsSelected changes
		/// </summary>
		private void OnOptionSelectionChanged(object sender, PropertyChangedEventArgs e)
		{
			if ( e.PropertyName == nameof(Option.IsSelected) && sender is Option option )
			{
				// Find the border for this option and update its background
				var optionsContainer = this.FindControl<ItemsControl>("OptionsContainer");
				if ( optionsContainer != null )
				{
					// Find the container for this specific option
					var container = optionsContainer.ContainerFromItem(option);
					if ( container != null )
					{
						var border = container.GetVisualDescendants().OfType<Border>().FirstOrDefault();
						if ( border != null )
						{
							UpdateOptionBackground(border, option.IsSelected);
						}
					}
				}
			}
		}

		/// <summary>
		/// Updates the tooltip for this ModListItem
		/// </summary>
		public void UpdateTooltip(ModComponent component)
		{
			// Find the name TextBlock
			TextBlock nameTextBlock = this.FindControl<TextBlock>("NameTextBlock");
			if ( nameTextBlock == null )
				return;

			// Set a basic tooltip immediately to avoid UI lag
			string basicTooltip = CreateBasicTooltip(component);
			ToolTip.SetTip(nameTextBlock, basicTooltip);

			// Update editor mode visibility
			if ( !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			UpdateEditorModeVisibility(mainWindow.EditorMode);

			// Update index if in editor mode
			if ( !mainWindow.EditorMode )
				return;
			int index = mainWindow.MainConfigInstance?.allComponents.IndexOf(component) ?? -1;
			if ( index >= 0 && this.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexBlock )
				indexBlock.Text = $"#{index + 1}";

			// Generate detailed tooltip and update when ready
			try
			{
				string detailedTooltip = CreateRichTooltipAsync(component);
				ToolTip.SetTip(nameTextBlock, detailedTooltip);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error generating detailed tooltip for {component?.Name}");
				// Keep the basic tooltip if detailed generation fails
			}
		}

		public void UpdateValidationState(ModComponent component)
		{
			if ( !(this.FindControl<Border>("RootBorder") is Border border) )
				return;

			// If component is not selected, clear all validation styling
			if ( !component.IsSelected )
			{
				// Clear border brush to use default theme color
				border.ClearValue(Border.BorderBrushProperty);
				border.ClearValue(Border.BorderThicknessProperty);

				// Hide validation icon
				if ( this.FindControl<TextBlock>("ValidationIcon") is TextBlock validationIcon )
					validationIcon.IsVisible = false;
				return;
			}

			// Determine validation state for selected components
			bool isMissingDownload = !component.IsDownloaded;
			bool hasErrors = false;
			var errorReasons = new List<string>();

			// Check for critical errors
			if ( string.IsNullOrWhiteSpace(component.Name) )
			{
				hasErrors = true;
				errorReasons.Add("Missing mod name");
			}

			// Check for dependency violations
			if ( component.Dependencies.Count > 0 )
			{
				if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				{
					List<ModComponent> allComponents = mainWindow.MainConfigInstance?.allComponents;
					if ( allComponents != null )
					{
						List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(component.Dependencies, allComponents);
						foreach ( ModComponent dep in dependencyComponents )
						{
							if ( dep == null || dep.IsSelected )
								continue;
							hasErrors = true;
							errorReasons.Add($"Requires '{dep.Name}' to be selected");
						}
					}
				}
			}

			// Check for restriction violations
			if ( component.Restrictions.Count > 0 )
			{
				if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				{
					List<ModComponent> allComponents = mainWindow.MainConfigInstance?.allComponents;
					if ( allComponents != null )
					{
						List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(component.Restrictions, allComponents);
						foreach ( ModComponent restriction in restrictionComponents )
						{
							if ( restriction == null || !restriction.IsSelected )
								continue;
							hasErrors = true;
							errorReasons.Add($"Conflicts with '{restriction.Name}' which is selected");
						}
					}
				}
			}

			// Check for instruction issues
			if ( component.Instructions.Count == 0 )
			{
				hasErrors = true;
				errorReasons.Add("No installation instructions defined");
			}

			// Check for invalid ModLinks/URLs only when in EditorMode
			if ( component.ModLink.Count > 0 )
			{
				if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow && mainWindow.EditorMode )
				{
					var invalidUrls = new List<string>();
					foreach ( string link in component.ModLink )
					{
						if ( string.IsNullOrWhiteSpace(link) )
							continue; // Skip empty links

						if ( !IsValidUrl(link) )
							invalidUrls.Add(link);
					}

					if ( invalidUrls.Count > 0 )
					{
						hasErrors = true;
						errorReasons.Add($"Invalid download URLs: {string.Join(", ", invalidUrls)}");
					}
				}
			}

			// Store error reasons in a static dictionary for tooltip lookup
			if ( errorReasons.Count > 0 )
				s_componentErrors[component.Guid] = string.Join("\n", errorReasons);
			else
				_ = s_componentErrors.Remove(component.Guid);

			// Update border - don't set any border when there are no issues (let default style handle it)
			if ( hasErrors )
			{
				// Red border for errors
				border.BorderBrush = ThemeResourceHelper.ModListItemErrorBrush;
				border.BorderThickness = new Thickness(2);
			}
			else if ( isMissingDownload )
			{
				// Orange border for missing downloads
				border.BorderBrush = ThemeResourceHelper.ModListItemWarningBrush;
				border.BorderThickness = new Thickness(1.5);
			}
			else
			{
				// Clear border brush to use default theme color
				border.ClearValue(Border.BorderBrushProperty);
				border.ClearValue(Border.BorderThicknessProperty);
			}

			// Update validation icon
			UpdateValidationIcon(this.FindControl<TextBlock>("ValidationIcon"), hasErrors, isMissingDownload);
		}

		private void UpdateValidationIcon(TextBlock validationIconControl, bool hasErrors, bool isMissingDownload)
		{
			if ( validationIconControl == null )
				return;

			if ( hasErrors )
			{
				validationIconControl.Text = "❌";
				validationIconControl.Foreground = ThemeResourceHelper.ModListItemErrorBrush;
				validationIconControl.IsVisible = true;
				ToolTip.SetTip(validationIconControl, "ModComponent has validation errors");
			}
			else if ( isMissingDownload )
			{
				validationIconControl.Text = "⚠️";
				validationIconControl.Foreground = ThemeResourceHelper.ModListItemWarningBrush;
				validationIconControl.IsVisible = true;
				ToolTip.SetTip(validationIconControl, "Mod archive not downloaded");
			}
			else
			{
				validationIconControl.IsVisible = false;
			}
		}

		private void UpdateFromModManagementService()
		{
			if ( !(DataContext is ModComponent component) || !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			// Update validation state
			UpdateValidationState(component);

			// Update context menu
			ContextMenu = mainWindow.BuildContextMenuForComponent(component);
		}

		private void UpdateEditorModeVisibility(bool isEditorMode)
		{
			// Update controls
			if ( this.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexBlock )
				indexBlock.IsVisible = isEditorMode;

			if ( this.FindControl<TextBlock>("DragHandle") is TextBlock dragHandle )
				dragHandle.IsVisible = isEditorMode;
		}

		/// <summary>
		/// Creates a basic tooltip without heavy validation operations
		/// </summary>
		private static string CreateBasicTooltip(ModComponent component)
		{
			var sb = new System.Text.StringBuilder();

			// Only show issues for selected components
			if ( !component.IsSelected )
			{
				// Show basic info for unselected components
				_ = sb.AppendLine($"📦 {component.Name}");
				if ( !string.IsNullOrWhiteSpace(component.Author) )
					_ = sb.AppendLine($"👤 Author: {component.Author}");
				if ( component.Category.Count > 0 )
					_ = sb.AppendLine($"🏷️ Category: {string.Join(", ", component.Category)}");
				if ( !string.IsNullOrWhiteSpace(component.Tier) )
					_ = sb.AppendLine($"⭐ Tier: {component.Tier}");
				if ( !string.IsNullOrWhiteSpace(component.Description) )
				{
					string desc = component.Description.Length > 200 ? component.Description.Substring(0, 200) + "..." : component.Description;
					_ = sb.AppendLine($"📝 {desc}");
				}
				return sb.ToString();
			}

			// Show basic info for selected components
			_ = sb.AppendLine($"📦 {component.Name}");
			if ( !string.IsNullOrWhiteSpace(component.Author) )
				_ = sb.AppendLine($"👤 Author: {component.Author}");
			if ( component.Category.Count > 0 )
				_ = sb.AppendLine($"🏷️ Category: {string.Join(", ", component.Category)}");
			if ( !string.IsNullOrWhiteSpace(component.Tier) )
				_ = sb.AppendLine($"⭐ Tier: {component.Tier}");

			// Check for basic issues without heavy validation
			bool isMissingDownload = !component.IsDownloaded;
			_ = s_componentErrors.TryGetValue(component.Guid, out string errorReasons);
			bool hasErrors = !string.IsNullOrEmpty(errorReasons);

			// Show issue banner if there are problems
			if ( hasErrors || isMissingDownload )
			{
				_ = sb.AppendLine("⚠️ ISSUES DETECTED ⚠️");
				_ = sb.AppendLine(new string('─', 40));

				if ( isMissingDownload )
				{
					_ = sb.AppendLine("❗ Missing Download");
					_ = sb.AppendLine("This mod is selected but the archive file is not");
					_ = sb.AppendLine("in your mod directory. Please:");
					_ = sb.AppendLine("  1. Click 'Fetch Downloads' to auto-download");
					_ = sb.AppendLine("  2. Or manually download from the mod links");
					if ( component.ModLink.Count > 0 )
						_ = sb.AppendLine($"  3. Download Link: {component.ModLink[0]}");
					_ = sb.AppendLine();
				}

				if ( hasErrors )
				{
					_ = sb.AppendLine("❌ Configuration Errors:");
					string[] errors = errorReasons.Split('\n');
					foreach ( string error in errors )
					{
						_ = sb.AppendLine($"  • {error}");
					}
					_ = sb.AppendLine();
					_ = sb.AppendLine("How to fix:");
					if ( errorReasons.Contains("Requires") )
						_ = sb.AppendLine("  • Enable required dependency mods");
					if ( errorReasons.Contains("Conflicts") )
						_ = sb.AppendLine("  • Disable conflicting mods");
					_ = sb.AppendLine();
				}
			}

			return sb.ToString();
		}

		/// <summary>
		/// Creates a rich tooltip with detailed validation information
		/// </summary>
		private static string CreateRichTooltipAsync(ModComponent component)
		{
			var sb = new System.Text.StringBuilder();

			// Only show issues for selected components
			if ( !component.IsSelected )
			{
				// Show basic info for unselected components
				_ = sb.AppendLine($"📦 {component.Name}");
				if ( !string.IsNullOrWhiteSpace(component.Author) )
					_ = sb.AppendLine($"👤 Author: {component.Author}");
				if ( component.Category.Count > 0 )
					_ = sb.AppendLine($"🏷️ Category: {string.Join(", ", component.Category)}");
				if ( !string.IsNullOrWhiteSpace(component.Tier) )
					_ = sb.AppendLine($"⭐ Tier: {component.Tier}");
				if ( !string.IsNullOrWhiteSpace(component.Description) )
				{
					string desc = component.Description.Length > 200 ? component.Description.Substring(0, 200) + "..." : component.Description;
					_ = sb.AppendLine($"📝 {desc}");
				}
				return sb.ToString();
			}

			// Show basic info for selected components
			_ = sb.AppendLine($"📦 {component.Name}");
			if ( !string.IsNullOrWhiteSpace(component.Author) )
				_ = sb.AppendLine($"👤 Author: {component.Author}");
			if ( component.Category.Count > 0 )
				_ = sb.AppendLine($"🏷️ Category: {string.Join(", ", component.Category)}");
			if ( !string.IsNullOrWhiteSpace(component.Tier) )
				_ = sb.AppendLine($"⭐ Tier: {component.Tier}");

			// Check for issues first
			bool isMissingDownload = !component.IsDownloaded;
			_ = s_componentErrors.TryGetValue(component.Guid, out string errorReasons);
			bool hasErrors = !string.IsNullOrEmpty(errorReasons);

			// Show issue banner if there are problems
			if ( hasErrors || isMissingDownload )
			{
				_ = sb.AppendLine("⚠️ ISSUES DETECTED ⚠️");
				_ = sb.AppendLine(new string('─', 40));

				if ( isMissingDownload )
				{
					_ = sb.AppendLine("❗ Missing Download");

					// Check DownloadCacheService for cached downloads
					var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
						? desktop.MainWindow as MainWindow
						: null;
					var downloadCacheService = mainWindow?.DownloadCacheService;
					if ( downloadCacheService != null && component.ModLink.Count > 0 )
					{
						var missingUrls = new List<string>();
						foreach ( string url in component.ModLink )
						{
							if ( !downloadCacheService.IsCached(component.Guid, url) )
							{
								missingUrls.Add(url);
							}
						}

						if ( missingUrls.Count > 0 )
						{
							_ = sb.AppendLine("Missing cached downloads:");
							foreach ( string url in missingUrls )
							{
								_ = sb.AppendLine($"  • {url}");
							}
							_ = sb.AppendLine();
						}
					}

					_ = sb.AppendLine("This mod is selected but the download is not cached.");
					_ = sb.AppendLine("Please:");
					_ = sb.AppendLine("  1. Click 'Fetch Downloads' to auto-download");
					_ = sb.AppendLine("  2. Or manually download from the mod links");
					if ( component.ModLink.Count > 0 )
						_ = sb.AppendLine($"  3. Download Link: {component.ModLink[0]}");
					_ = sb.AppendLine();
				}

				if ( hasErrors )
				{
					_ = sb.AppendLine("❌ Configuration Errors:");
					string[] errors = errorReasons.Split('\n');
					foreach ( string error in errors )
					{
						_ = sb.AppendLine($"  • {error}");
					}
					_ = sb.AppendLine();
					_ = sb.AppendLine("How to fix:");
					if ( errorReasons.Contains("Requires") )
						_ = sb.AppendLine("  • Enable required dependency mods");
					if ( errorReasons.Contains("Conflicts") )
						_ = sb.AppendLine("  • Disable conflicting mods");
					_ = sb.AppendLine();
				}
			}

			// Show additional details
			if ( !string.IsNullOrWhiteSpace(component.Description) )
			{
				_ = sb.AppendLine($"📝 Description:");
				string desc = component.Description.Length > 300 ? component.Description.Substring(0, 300) + "..." : component.Description;
				_ = sb.AppendLine(desc);
				_ = sb.AppendLine();
			}

			if ( component.ModLink.Count > 0 )
			{
				_ = sb.AppendLine($"🔗 Download Links ({component.ModLink.Count}):");
				for ( int i = 0; i < Math.Min(component.ModLink.Count, 3); i++ )
				{
					_ = sb.AppendLine($"  {i + 1}. {component.ModLink[i]}");
				}
				if ( component.ModLink.Count > 3 )
					_ = sb.AppendLine($"  ... and {component.ModLink.Count - 3} more");
				_ = sb.AppendLine();
			}

			if ( component.Dependencies.Count > 0 )
			{
				_ = sb.AppendLine($"🔗 Dependencies ({component.Dependencies.Count}):");
				foreach ( Guid depGuid in component.Dependencies )
				{
					ModComponent depComponent = MainConfig.AllComponents.FirstOrDefault(c => c.Guid == depGuid);
					if ( depComponent != null )
					{
						string status = depComponent.IsSelected ? "✅" : "❌";
						_ = sb.AppendLine($"  {status} {depComponent.Name}");
					}
					else
					{
						_ = sb.AppendLine($"  ❓ Unknown dependency ({depGuid})");
					}
				}
				_ = sb.AppendLine();
			}

			if ( component.Restrictions.Count > 0 )
			{
				_ = sb.AppendLine($"⚠️ Conflicts ({component.Restrictions.Count}):");
				foreach ( Guid restrictGuid in component.Restrictions )
				{
					ModComponent restrictComponent = MainConfig.AllComponents.FirstOrDefault(c => c.Guid == restrictGuid);
					if ( restrictComponent != null )
					{
						string status = restrictComponent.IsSelected ? "❌" : "✅";
						_ = sb.AppendLine($"  {status} {restrictComponent.Name}");
					}
					else
					{
						_ = sb.AppendLine($"  ❓ Unknown conflict ({restrictGuid})");
					}
				}
				_ = sb.AppendLine();
			}

			return sb.ToString();
		}

		private static string CreateRichTooltip(ModComponent component)
		{
			var sb = new System.Text.StringBuilder();

			// Check for issues first
			bool isMissingDownload = !component.IsDownloaded;
			_ = s_componentErrors.TryGetValue(component.Guid, out string errorReasons);
			bool hasErrors = !string.IsNullOrEmpty(errorReasons);

			// Show issue banner if there are problems
			if ( hasErrors || isMissingDownload )
			{
				_ = sb.AppendLine("⚠️ ISSUES DETECTED ⚠️");
				_ = sb.AppendLine(new string('─', 40));

				if ( isMissingDownload )
				{
					_ = sb.AppendLine("❗ Missing Download");

					// Get specific missing files using ValidationService
					var missingFiles = ValidationService.GetMissingFilesForComponentStatic(component);
					if ( missingFiles.Count > 0 )
					{
						_ = sb.AppendLine("Missing file(s):");
						foreach ( string fileName in missingFiles )
						{
							_ = sb.AppendLine($"  • {fileName}");
						}
						_ = sb.AppendLine();
					}

					_ = sb.AppendLine("This mod is selected but the archive file is not");
					_ = sb.AppendLine("in your mod directory. Please:");
					_ = sb.AppendLine("  1. Click 'Fetch Downloads' to auto-download");
					_ = sb.AppendLine("  2. Or manually download from the mod links");
					if ( component.ModLink.Count > 0 )
						_ = sb.AppendLine($"  3. Download Link: {component.ModLink[0]}");
					_ = sb.AppendLine();
				}

				if ( hasErrors )
				{
					_ = sb.AppendLine("❌ Configuration Errors:");
					string[] errors = errorReasons.Split('\n');
					foreach ( string error in errors )
					{
						_ = sb.AppendLine($"  • {error}");
					}
					_ = sb.AppendLine();
					_ = sb.AppendLine("How to fix:");
					if ( errorReasons.Contains("Requires") )
						_ = sb.AppendLine("  • Enable required dependency mods");
					if ( errorReasons.Contains("Conflicts") )
						_ = sb.AppendLine("  • Deselect conflicting mods");
					if ( errorReasons.Contains("No installation instructions") )
						_ = sb.AppendLine("  • This mod needs instructions (contact mod author)");
					if ( errorReasons.Contains("Invalid download URLs") )
					{
						_ = sb.AppendLine("  • Fix invalid download URLs:");
						_ = sb.AppendLine("    1. Click 'Edit' to open the mod editor");
						_ = sb.AppendLine("    2. Go to the 'Download Links' section");
						_ = sb.AppendLine("    3. Replace invalid URLs with working ones");
						_ = sb.AppendLine("    4. URLs must start with 'http://' or 'https://'");
						_ = sb.AppendLine("    5. Save your changes");
					}
					_ = sb.AppendLine();
				}
				_ = sb.AppendLine(new string('─', 40));
				_ = sb.AppendLine();
			}
			_ = sb.AppendLine($"📦 {component.Name}");
			_ = sb.AppendLine();

			if ( !string.IsNullOrEmpty(component.Author) )
				_ = sb.AppendLine($"👤 Author: {component.Author}");

			if ( component.Category.Count > 0 )
				_ = sb.AppendLine($"📁 Category: {string.Join(", ", component.Category)}");

			if ( !string.IsNullOrEmpty(component.Tier) )
				_ = sb.AppendLine($"⭐ Tier: {component.Tier}");

			if ( !string.IsNullOrEmpty(component.Description) )
			{
				_ = sb.AppendLine();
				_ = sb.AppendLine("📝 Description:");
				string desc = component.Description.Length > 200
					? component.Description.Substring(0, 200) + "..."
					: component.Description;
				_ = sb.AppendLine(desc);
			}

			// Show dependency info
			if ( component.Dependencies.Count > 0 )
			{
				_ = sb.AppendLine();
				_ = sb.AppendLine($"✓ Requires: {component.Dependencies.Count} mod(s)");
			}

			if ( component.Restrictions.Count > 0 )
				_ = sb.AppendLine($"✗ Conflicts with: {component.Restrictions.Count} mod(s)");

			if ( component.Options.Count > 0 )
				_ = sb.AppendLine($"⚙️ Has {component.Options.Count} optional component(s)");

			return sb.ToString();
		}

		private void OnPointerEntered(object sender, PointerEventArgs e)
		{
			if ( !(this.FindControl<Border>("RootBorder") is Border border) )
				return;

			// Store current border for restoration
			IBrush currentBrush = border.BorderBrush;
			border.Tag = currentBrush;

			// Yellow border on hover (unless there's an error/warning, then keep that color but brighten it)
			if ( currentBrush is SolidColorBrush solidBrush )
			{
				Color color = solidBrush.Color;
				// If red or orange, brighten it
				if ( color.R > 200 && color.G < 150 ) // Reddish
					border.BorderBrush = ThemeResourceHelper.ModListItemHoverErrorBrush; // Lighter red
				else if ( color.R > 200 && color.G > 100 && color.G < 200 ) // Orange
					border.BorderBrush = ThemeResourceHelper.ModListItemHoverWarningBrush; // Lighter orange
				else
					border.BorderBrush = ThemeResourceHelper.ModListItemHoverDefaultBrush; // Yellow
			}
			else
			{
				border.BorderBrush = ThemeResourceHelper.ModListItemHoverDefaultBrush; // Yellow
			}

			border.Background = ThemeResourceHelper.ModListItemHoverBackgroundBrush;
		}

		private void OnPointerExited(object sender, PointerEventArgs e)
		{
			if ( !(this.FindControl<Border>("RootBorder") is Border border) )
				return;

			// Restore original border
			if ( border.Tag is IBrush originalBrush )
				border.BorderBrush = originalBrush;
			else
			{
				// Revalidate to restore correct state
				if ( DataContext is ModComponent component )
					UpdateValidationState(component);
			}

			border.Background = ThemeResourceHelper.ModListItemDefaultBackgroundBrush;
		}

		/// <summary>
		/// Sets the visual state for when this item is being dragged
		/// </summary>
		public void SetDraggedState(bool isDragged)
		{
			IsBeingDragged = isDragged;
			if ( this.FindControl<Border>("RootBorder") is Border border )
			{
				border.Opacity = isDragged ? 0.5 : 1.0;
			}
		}

		/// <summary>
		/// Sets the visual state for when this item is a drop target
		/// </summary>
		public void SetDropTargetState(bool isDropTarget)
		{
			IsDropTarget = isDropTarget;
			if ( this.FindControl<Border>("DropIndicator") is Border indicator )
			{
				indicator.IsVisible = isDropTarget;
			}
		}

		/// <summary>
		/// Checks if a string is a valid URL
		/// </summary>
		private static bool IsValidUrl(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			// Basic URL validation
			if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) )
				return false;

			// Check if it's HTTP or HTTPS
			if ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps )
				return false;

			// Check if it has a valid host
			if ( string.IsNullOrWhiteSpace(uri.Host) )
				return false;

			return true;
		}


		/// <summary>
		/// Resolves a path by replacing placeholders like <<modDirectory>>
		/// </summary>
		private static string ResolvePath(string path)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return path;

			// Replace <<modDirectory>> with actual mod directory path
			if ( path.Contains("<<modDirectory>>") )
			{
				string modDir = MainConfig.SourcePath?.FullName ?? "";
				path = path.Replace("<<modDirectory>>", modDir);
			}

			return path;
		}

		/// <summary>
		/// Handles clicks on option borders to toggle the option selection
		/// </summary>
		private void OptionBorder_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			// Prevent the event from bubbling up to the main ModListItem
			e.Handled = true;

			if ( sender is Border border && border.Tag is Option option )
			{
				// Toggle the option selection
				option.IsSelected = !option.IsSelected;

				// Update the background color based on selection state
				UpdateOptionBackground(border, option.IsSelected);
			}
		}

		/// <summary>
		/// Updates the background color of an option border based on its selection state
		/// </summary>
		private void UpdateOptionBackground(Border border, bool isSelected)
		{
			if ( border == null )
				return;

			if ( isSelected )
			{
				// Use the hover background color for selected options
				border.Background = ThemeResourceHelper.ModListItemHoverBackgroundBrush;
			}
			else
			{
				// Use transparent background for unselected options
				border.Background = Brushes.Transparent;
			}
		}
	}
}

