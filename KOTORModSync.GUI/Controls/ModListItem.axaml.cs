// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
	public partial class ModListItem : UserControl
	{
		public ModListItem()
		{
			AvaloniaXamlLoader.Load(this);

			// Add pointer enter/leave handlers for hover effect
			PointerEntered += OnPointerEntered;
			PointerExited += OnPointerExited;

			DataContextChanged += OnDataContextChanged;

			// Wire up checkbox event
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
		}

		private void OnDoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			// Double-click toggles selection
			if ( !(DataContext is Component component) )
				return;
			component.IsSelected = !component.IsSelected;
			if ( !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			mainWindow.UpdateModCounts();
			if ( component.IsSelected )
				mainWindow.ComponentCheckboxChecked(component, new HashSet<Component>());
			else
				mainWindow.ComponentCheckboxUnchecked(component, new HashSet<Component>());
		}

		private void OnDragHandlePressed(object sender, PointerPressedEventArgs e)
		{
			if ( !(DataContext is Component component) || !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
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
			if ( DataContext is Component component && this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.SetCurrentComponent(component);
		}

		private void OnCheckBoxChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			// The two-way binding will handle updating the Component.IsSelected
			// We just need to notify the main window to update counts
			if ( this.FindAncestorOfType<Window>() is MainWindow mainWindow )
				mainWindow.OnComponentCheckBoxChanged(sender, e);
		}

		private void OnDataContextChanged(object sender, EventArgs e)
		{
			if ( !(DataContext is Component component) )
				return;
			// Set up rich tooltip
			string tooltipText = CreateRichTooltip(component);
			ToolTip.SetTip(this, tooltipText);

			// Update from mod management service
			UpdateFromModManagementService();

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
		}

		private void UpdateValidationState(Component component)
		{
			if ( !(this.FindControl<Border>("RootBorder") is Border border) )
				return;

			// Validate component and update visual state
			bool hasWarnings = false;
			bool hasErrors = string.IsNullOrWhiteSpace(component.Name);

			// Check for basic validation issues

			// Check if component is selected but not downloaded
			if ( component.IsSelected && !component.IsDownloaded )
				hasWarnings = true;

			// Check for dependency issues
			if ( component.Dependencies.Count != 0 )
			{
				// In a full implementation, we'd check if dependencies are satisfied
				// For now, just flag if there are dependencies
				hasWarnings = true;
			}

			// Check for instruction issues
			if ( component.Instructions.Count == 0 )
				hasWarnings = true;

			// Update border based on validation state
			if ( hasErrors )
			{
				// Red border for errors
				border.BorderBrush = new SolidColorBrush(Color.Parse("#FF6B6B"));
				border.BorderThickness = new Thickness(2);
			}
			else if ( hasWarnings )
			{
				// Yellow/orange border for warnings
				border.BorderBrush = new SolidColorBrush(Color.Parse("#FFA500"));
				border.BorderThickness = new Thickness(1.5);
			}
			else
			{
				// Normal theme color for valid components
				border.BorderBrush = new SolidColorBrush(Color.Parse("#062766"));
				border.BorderThickness = new Thickness(1);
			}

			// Update validation icon if it exists
			if ( this.FindControl<TextBlock>("ValidationIcon") is TextBlock validationIcon )
			{
				if ( hasErrors )
				{
					validationIcon.Text = "❌";
					validationIcon.Foreground = new SolidColorBrush(Color.Parse("#FF6B6B"));
					validationIcon.IsVisible = true;
					ToolTip.SetTip(validationIcon, "Component has validation errors");
				}
				else if ( hasWarnings )
				{
					validationIcon.Text = "⚠️";
					validationIcon.Foreground = new SolidColorBrush(Color.Parse("#FFA500"));
					validationIcon.IsVisible = true;
					ToolTip.SetTip(validationIcon, "Component has validation warnings");
				}
				else
				{
					validationIcon.IsVisible = false;
				}
			}
		}

		private void UpdateFromModManagementService()
		{
			if ( !(DataContext is Component component) || !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) )
				return;
			// Update validation state
			UpdateValidationState(component);

			// Update context menu
			ContextMenu = mainWindow.BuildContextMenuForComponent(component);
		}

		private void UpdateEditorModeVisibility(bool isEditorMode)
		{
			if ( this.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexBlock )
				indexBlock.IsVisible = isEditorMode;

			if ( this.FindControl<TextBlock>("DragHandle") is TextBlock dragHandle )
				dragHandle.IsVisible = isEditorMode;
		}

		private string CreateRichTooltip(Component component)
		{
			var sb = new System.Text.StringBuilder();

			_ = sb.AppendLine($"📦 {component.Name}");
			_ = sb.AppendLine();

			if ( !string.IsNullOrEmpty(component.Author) )
				_ = sb.AppendLine($"👤 Author: {component.Author}");

			if ( !string.IsNullOrEmpty(component.Category) )
				_ = sb.AppendLine($"📁 Category: {component.Category}");

			if ( !string.IsNullOrEmpty(component.Tier) )
				_ = sb.AppendLine($"⭐ Tier: {component.Tier}");

			if ( !string.IsNullOrEmpty(component.Description) )
			{
				_ = sb.AppendLine();
				_ = sb.AppendLine("📝 Description:");
				string desc = component.Description.Length > 200
					? string.Concat(component.Description.AsSpan(0, 200), "...")
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
			border.BorderBrush = new SolidColorBrush(Color.Parse("#A8B348"));
			border.Background = new SolidColorBrush(Color.Parse("#020228"));
		}

		private void OnPointerExited(object sender, PointerEventArgs e)
		{
			if ( !(this.FindControl<Border>("RootBorder") is Border border) )
				return;
			border.BorderBrush = new SolidColorBrush(Color.Parse("#062766"));
			border.Background = new SolidColorBrush(Color.Parse("#010116"));
		}
	}
}

