// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
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

		private static readonly Dictionary<Guid, string> s_componentErrors = new Dictionary<Guid, string>();

		private ModComponent _previousComponent;

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

			PointerEntered += OnPointerEntered;
			PointerExited += OnPointerExited;

			DataContextChanged += OnDataContextChanged;

			CheckBox checkbox = this.FindControl<CheckBox>("ComponentCheckBox");
			if ( checkbox != null )
				checkbox.IsCheckedChanged += OnCheckBoxChanged;

			PointerPressed += OnPointerPressed;

			DoubleTapped += OnDoubleTapped;

			TextBlock dragHandle = this.FindControl<TextBlock>("DragHandle");
			if ( dragHandle != null )
				dragHandle.PointerPressed += OnDragHandlePressed;

			Grid mainModInfo = this.FindControl<Grid>("MainModInfo");
			if ( mainModInfo != null )
			{
				mainModInfo.PointerPressed += OnMainModInfoPointerPressed;
				mainModInfo.DoubleTapped += OnMainModInfoDoubleTapped;
			}
		}

		private void OnMainModInfoPointerPressed(object sender, PointerPressedEventArgs e)
		{

			if ( e.Source is CheckBox )
				return;

			if ( DataContext is ModComponent component && this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.SetCurrentModComponent(component);
		}

		private void OnMainModInfoDoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{

			if ( e.Source is CheckBox )
				return;

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

			e.Handled = true;
		}

		private void OnDoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{

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

			if ( e.Source is TextBlock textBlock && textBlock.Name == "DragHandle" )
				return;
			if ( e.Source is CheckBox )
				return;

			if ( DataContext is ModComponent component && this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.SetCurrentModComponent(component);
		}

		private void OnCheckBoxChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{

			if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.OnComponentCheckBoxChanged(sender, e);
		}

		private void OnOptionCheckBoxChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{

			if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.OnComponentCheckBoxChanged(sender, e);
		}

		private void OnDataContextChanged(object sender, EventArgs e)
		{
			if ( !(DataContext is ModComponent component) )
				return;

			if ( _previousComponent != null )
				_previousComponent.PropertyChanged -= OnComponentPropertyChanged;

			_previousComponent = component;

			component.PropertyChanged += OnComponentPropertyChanged;

			UpdateFromModManagementService();

			UpdateTooltip(component);

			SetupOptionSelectionHandlers(component);
		}

		private void OnComponentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if ( e.PropertyName == nameof(ModComponent.IsValidating) && sender is ModComponent component )
			{
				UpdateValidationState(component);
			}
		}

		private void SetupOptionSelectionHandlers(ModComponent component)
		{
			foreach ( Option option in component.Options )
			{

				option.PropertyChanged -= OnOptionSelectionChanged;

				option.PropertyChanged += OnOptionSelectionChanged;
			}
		}

		private void OnOptionSelectionChanged(object sender, PropertyChangedEventArgs e)
		{
			if ( e.PropertyName == nameof(Option.IsSelected) && sender is Option option )
			{

				var optionsContainer = this.FindControl<ItemsControl>("OptionsContainer");
				if ( optionsContainer != null )
				{

					var container = optionsContainer.ContainerFromItem(option);
					if ( container != null )
					{
						var border = container.GetVisualDescendants().OfType<Border>().FirstOrDefault();
						if ( border != null )
						{
							ModListItem.UpdateOptionBackground(border, option.IsSelected);
						}
					}
				}
			}
		}

		public void UpdateTooltip(ModComponent component)
		{

			TextBlock nameTextBlock = this.FindControl<TextBlock>("NameTextBlock");
			if ( nameTextBlock == null )
				return;

			string basicTooltip = CreateBasicTooltip(component);
			ToolTip.SetTip(nameTextBlock, basicTooltip);

			if ( !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			UpdateEditorModeVisibility(mainWindow.EditorMode);

			if ( !mainWindow.EditorMode )
				return;
			int index = mainWindow.MainConfigInstance?.allComponents.IndexOf(component) ?? -1;
			if ( index >= 0 && this.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexBlock )
				indexBlock.Text = $"#{index + 1}";

			try
			{
				string detailedTooltip = CreateRichTooltipAsync(component);
				ToolTip.SetTip(nameTextBlock, detailedTooltip);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error generating detailed tooltip for {component?.Name}");

			}
		}

		public void UpdateValidationState(ModComponent component)
		{
			if ( !(this.FindControl<Border>("RootBorder") is Border border) )
				return;

			if ( component.IsValidating )
			{
				border.Opacity = 0.5;

				return;
			}
			else
			{

				border.Opacity = 1.0;
			}

			if ( !component.IsSelected )
			{

				border.ClearValue(Border.BorderBrushProperty);
				border.ClearValue(Border.BorderThicknessProperty);

				if ( this.FindControl<TextBlock>("ValidationIcon") is TextBlock validationIcon )
					validationIcon.IsVisible = false;
				return;
			}

			bool isMissingDownload = !component.IsDownloaded;
			bool hasErrors = false;
			var errorReasons = new List<string>();

			if ( string.IsNullOrWhiteSpace(component.Name) )
			{
				hasErrors = true;
				errorReasons.Add("Missing mod name");
			}

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

			if ( component.Instructions.Count == 0 )
			{
				hasErrors = true;
				errorReasons.Add("No installation instructions defined");
			}

			if ( component.ModLink.Count > 0 )
			{
				if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow && mainWindow.EditorMode )
				{
					var invalidUrls = new List<string>();
					foreach ( string link in component.ModLink )
					{
						if ( string.IsNullOrWhiteSpace(link) )
							continue;

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

			if ( errorReasons.Count > 0 )
				s_componentErrors[component.Guid] = string.Join("\n", errorReasons);
			else
				_ = s_componentErrors.Remove(component.Guid);

			bool shouldShowDownloadWarning = isMissingDownload && MainWindow.HasFetchedDownloads && hasErrors;

			if ( hasErrors )
			{

				border.BorderBrush = ThemeResourceHelper.ModListItemErrorBrush;
				border.BorderThickness = new Thickness(2);
			}
			else if ( shouldShowDownloadWarning )
			{

				border.BorderBrush = ThemeResourceHelper.ModListItemWarningBrush;
				border.BorderThickness = new Thickness(1.5);
			}
			else
			{

				border.ClearValue(Border.BorderBrushProperty);
				border.ClearValue(Border.BorderThicknessProperty);
			}

			ModListItem.UpdateValidationIcon(this.FindControl<TextBlock>("ValidationIcon"), hasErrors, shouldShowDownloadWarning);
		}

		private static void UpdateValidationIcon(TextBlock validationIconControl, bool hasErrors, bool shouldShowDownloadWarning)
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
			else if ( shouldShowDownloadWarning )
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

			UpdateValidationState(component);

			ContextMenu = mainWindow.BuildContextMenuForComponent(component);
		}

		private void UpdateEditorModeVisibility(bool isEditorMode)
		{

			if ( this.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexBlock )
				indexBlock.IsVisible = isEditorMode;

			if ( this.FindControl<TextBlock>("DragHandle") is TextBlock dragHandle )
				dragHandle.IsVisible = isEditorMode;
		}

		private static string CreateBasicTooltip(ModComponent component)
		{
			var sb = new System.Text.StringBuilder();

			if ( !component.IsSelected )
			{

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

			_ = sb.AppendLine($"📦 {component.Name}");
			if ( !string.IsNullOrWhiteSpace(component.Author) )
				_ = sb.AppendLine($"👤 Author: {component.Author}");
			if ( component.Category.Count > 0 )
				_ = sb.AppendLine($"🏷️ Category: {string.Join(", ", component.Category)}");
			if ( !string.IsNullOrWhiteSpace(component.Tier) )
				_ = sb.AppendLine($"⭐ Tier: {component.Tier}");

			bool isMissingDownload = !component.IsDownloaded;
			_ = s_componentErrors.TryGetValue(component.Guid, out string errorReasons);
			bool hasErrors = !string.IsNullOrEmpty(errorReasons);

			bool shouldShowDownloadWarning = isMissingDownload && MainWindow.HasFetchedDownloads && hasErrors;

			if ( hasErrors || shouldShowDownloadWarning )
			{
				_ = sb.AppendLine("⚠️ ISSUES DETECTED ⚠️");
				_ = sb.AppendLine(new string('─', 40));

				if ( shouldShowDownloadWarning )
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

		private static string CreateRichTooltipAsync(ModComponent component)
		{
			var sb = new System.Text.StringBuilder();

			if ( !component.IsSelected )
			{

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

			_ = sb.AppendLine($"📦 {component.Name}");
			if ( !string.IsNullOrWhiteSpace(component.Author) )
				_ = sb.AppendLine($"👤 Author: {component.Author}");
			if ( component.Category.Count > 0 )
				_ = sb.AppendLine($"🏷️ Category: {string.Join(", ", component.Category)}");
			if ( !string.IsNullOrWhiteSpace(component.Tier) )
				_ = sb.AppendLine($"⭐ Tier: {component.Tier}");

			bool isMissingDownload = !component.IsDownloaded;
			_ = s_componentErrors.TryGetValue(component.Guid, out string errorReasons);
			bool hasErrors = !string.IsNullOrEmpty(errorReasons);

			bool shouldShowDownloadWarning = isMissingDownload && MainWindow.HasFetchedDownloads && hasErrors;

			if ( hasErrors || shouldShowDownloadWarning )
			{
				_ = sb.AppendLine("⚠️ ISSUES DETECTED ⚠️");
				_ = sb.AppendLine(new string('─', 40));

				if ( shouldShowDownloadWarning )
				{
					_ = sb.AppendLine("❗ Missing Download");

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

			bool isMissingDownload = !component.IsDownloaded;
			_ = s_componentErrors.TryGetValue(component.Guid, out string errorReasons);
			bool hasErrors = !string.IsNullOrEmpty(errorReasons);

			if ( hasErrors || isMissingDownload )
			{
				_ = sb.AppendLine("⚠️ ISSUES DETECTED ⚠️");
				_ = sb.AppendLine(new string('─', 40));

				if ( isMissingDownload )
				{
					_ = sb.AppendLine("❗ Missing Download");

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

			IBrush currentBrush = border.BorderBrush;
			border.Tag = currentBrush;

			if ( currentBrush is SolidColorBrush solidBrush )
			{
				Color color = solidBrush.Color;

				if ( color.R > 200 && color.G < 150 )
					border.BorderBrush = ThemeResourceHelper.ModListItemHoverErrorBrush;
				else if ( color.R > 200 && color.G > 100 && color.G < 200 )
					border.BorderBrush = ThemeResourceHelper.ModListItemHoverWarningBrush;
				else
					border.BorderBrush = ThemeResourceHelper.ModListItemHoverDefaultBrush;
			}
			else
			{
				border.BorderBrush = ThemeResourceHelper.ModListItemHoverDefaultBrush;
			}

			border.Background = ThemeResourceHelper.ModListItemHoverBackgroundBrush;
		}

		private void OnPointerExited(object sender, PointerEventArgs e)
		{
			if ( !(this.FindControl<Border>("RootBorder") is Border border) )
				return;

			if ( border.Tag is IBrush originalBrush )
				border.BorderBrush = originalBrush;
			else
			{

				if ( DataContext is ModComponent component )
					UpdateValidationState(component);
			}

			border.Background = ThemeResourceHelper.ModListItemDefaultBackgroundBrush;
		}

		public void SetDraggedState(bool isDragged)
		{
			IsBeingDragged = isDragged;
			if ( this.FindControl<Border>("RootBorder") is Border border )
			{
				border.Opacity = isDragged ? 0.5 : 1.0;
			}
		}

		public void SetDropTargetState(bool isDropTarget)
		{
			IsDropTarget = isDropTarget;
			if ( this.FindControl<Border>("DropIndicator") is Border indicator )
			{
				indicator.IsVisible = isDropTarget;
			}
		}

		private static bool IsValidUrl(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) )
				return false;

			if ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps )
				return false;

			if ( string.IsNullOrWhiteSpace(uri.Host) )
				return false;

			return true;
		}


		private static string ResolvePath(string path)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return path;

			if ( path.Contains("<<modDirectory>>") )
			{
				string modDir = MainConfig.SourcePath?.FullName ?? "";
				path = path.Replace("<<modDirectory>>", modDir);
			}

			return path;
		}

		private void OptionBorder_PointerPressed(object sender, PointerPressedEventArgs e)
		{

			e.Handled = true;

			if ( sender is Border border && border.Tag is Option option )
			{

				option.IsSelected = !option.IsSelected;

				ModListItem.UpdateOptionBackground(border, option.IsSelected);
			}
		}

		private static void UpdateOptionBackground(Border border, bool isSelected)
		{
			if ( border == null )
				return;

			if ( isSelected )
			{

				border.Background = ThemeResourceHelper.ModListItemHoverBackgroundBrush;
			}
			else
			{

				border.Background = Brushes.Transparent;
			}
		}
	}
}

