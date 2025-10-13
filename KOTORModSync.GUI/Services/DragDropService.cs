// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KOTORModSync.Controls;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{

	public class DragDropService
	{
		private ModComponent _draggedComponent;
		private ModListItem _draggedItem;
		private ModListItem _currentDropTarget;
		private Panel _dragVisualContainer;
		private readonly Window _parentWindow;
		private readonly Func<List<ModComponent>> _getComponents;
		private readonly Func<Task> _onComponentsReordered;

		public DragDropService(Window parentWindow, Func<List<ModComponent>> getComponents, Func<Task> onComponentsReordered)
		{
			_parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
			_getComponents = getComponents ?? throw new ArgumentNullException(nameof(getComponents));
			_onComponentsReordered = onComponentsReordered ?? throw new ArgumentNullException(nameof(onComponentsReordered));
		}

		public void HandlePointerPressed(PointerPressedEventArgs e, ListBox modListBox, bool editorMode)
		{
			try
			{
				if ( !editorMode || !e.GetCurrentPoint(modListBox).Properties.IsLeftButtonPressed )
					return;

				if ( !(e.Source is Visual visual) )
					return;

				var textBlock = FindDragHandle(visual);
				if ( textBlock?.Text != "⋮⋮" )
					return;

				ListBoxItem listBoxItem = visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
				if ( !(listBoxItem?.DataContext is ModComponent component) )
					return;

				_draggedComponent = component;

				_draggedItem = visual.GetVisualAncestors().OfType<ModListItem>().FirstOrDefault();
				if ( _draggedItem != null )
				{
					_draggedItem.SetDraggedState(true);
					CreateDragVisual();
				}

				var data = new DataObject();
				data.Set("ModComponent", component);
				_ = DragDrop.DoDragDrop(e, data, DragDropEffects.Move);

				CleanupDragVisuals();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		public void HandleDragOver(DragEventArgs e, bool editorMode, Window window)
		{
			try
			{
				if ( !editorMode || _draggedComponent == null || !e.Data.Contains("ModComponent") )
				{
					e.DragEffects = DragDropEffects.None;
					e.Handled = true;
					return;
				}

				UpdateDragVisualPosition(e.GetPosition(window));

				if ( _currentDropTarget != null )
				{
					_currentDropTarget.SetDropTargetState(false);
					_currentDropTarget = null;
				}

				if ( e.Source is Visual visual )
				{
					ListBoxItem listBoxItem = visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
					if ( listBoxItem?.DataContext is ModComponent targetComponent && targetComponent != _draggedComponent )
					{
						ModListItem modListItem = visual.GetVisualAncestors().OfType<ModListItem>().FirstOrDefault();
						if ( modListItem != null )
						{
							modListItem.SetDropTargetState(true);
							_currentDropTarget = modListItem;
						}

						e.DragEffects = DragDropEffects.Move;
						e.Handled = true;
						return;
					}
				}

				e.DragEffects = DragDropEffects.Move;
				e.Handled = true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		public void HandleDrop(DragEventArgs e, bool editorMode)
		{
			try
			{
				if ( !editorMode || _draggedComponent == null )
				{
					CleanupDragVisuals();
					return;
				}

				if ( e.Source is Visual visual )
				{
					ListBoxItem listBoxItem = visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
					if ( listBoxItem?.DataContext is ModComponent targetComponent )
					{
						List<ModComponent> allComponents = _getComponents();
						int targetIndex = allComponents.IndexOf(targetComponent);
						int currentIndex = allComponents.IndexOf(_draggedComponent);

						if ( targetIndex != currentIndex && targetIndex >= 0 && currentIndex >= 0 )
						{

							allComponents.RemoveAt(currentIndex);
							allComponents.Insert(targetIndex, _draggedComponent);

							_ = Dispatcher.UIThread.InvokeAsync(async () =>
							{
								await _onComponentsReordered();
								await Logger.LogVerboseAsync($"Moved '{_draggedComponent.Name}' from index #{currentIndex + 1} to #{targetIndex + 1}");
							});
						}
					}
				}

				CleanupDragVisuals();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		public void StartDragComponent(ModComponent component, PointerPressedEventArgs e, bool editorMode)
		{
			if ( !editorMode )
				return;

			_draggedComponent = component;

			if ( e.Source is Visual visual )
			{
				_draggedItem = visual.GetVisualAncestors().OfType<ModListItem>().FirstOrDefault();
				if ( _draggedItem != null )
				{
					_draggedItem.SetDraggedState(true);
					CreateDragVisual();
				}
			}

			var data = new DataObject();
			data.Set("ModComponent", component);
			_ = DragDrop.DoDragDrop(e, data, DragDropEffects.Move);

			CleanupDragVisuals();
		}

		#region Private Helper Methods

		private static TextBlock FindDragHandle(Visual visual)
		{
			var textBlock = visual as TextBlock;
			if ( textBlock == null && visual is Control control )
			{
				IEnumerable<TextBlock> descendants = control.GetVisualDescendants().OfType<TextBlock>();
				textBlock = descendants.FirstOrDefault(tb => tb.Text == "⋮⋮");
			}
			return textBlock;
		}

		private void CreateDragVisual()
		{
			if ( _draggedItem == null || _dragVisualContainer != null )
				return;

			try
			{

				_dragVisualContainer = new Canvas
				{
					IsHitTestVisible = false,
					ZIndex = 1000,
					Background = Brushes.Transparent
				};

				var dragVisual = new ModListItem
				{
					DataContext = _draggedItem.DataContext,
					Opacity = 0.5,
					IsHitTestVisible = false
				};

				Size originalSize = _draggedItem.Bounds.Size;
				dragVisual.Width = originalSize.Width;
				dragVisual.Height = originalSize.Height;

				_dragVisualContainer.Children.Add(dragVisual);

				if ( _parentWindow.FindControl<Grid>("MainGrid") is Grid mainGrid )
				{
					mainGrid.Children.Add(_dragVisualContainer);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error creating drag visual");
			}
		}

		private void UpdateDragVisualPosition(Point position)
		{
			try
			{
				if ( _dragVisualContainer is Canvas canvas && canvas.Children.Count > 0 )
				{
					Control dragVisual = canvas.Children[0];
					if ( dragVisual != null )
					{

						Canvas.SetLeft(dragVisual, position.X - 10);
						Canvas.SetTop(dragVisual, position.Y - 10);
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogVerbose($"Error updating drag visual position: {ex.Message}");
			}
		}

		private void CleanupDragVisuals()
		{
			try
			{

				if ( _draggedItem != null )
				{
					_draggedItem.SetDraggedState(false);
					_draggedItem = null;
				}

				if ( _dragVisualContainer != null )
				{
					if ( _dragVisualContainer.Parent is Panel parent )
						_ = parent.Children.Remove(_dragVisualContainer);
					_dragVisualContainer = null;
				}

				if ( _currentDropTarget != null )
				{
					_currentDropTarget.SetDropTargetState(false);
					_currentDropTarget = null;
				}

				_draggedComponent = null;
			}
			catch ( Exception ex )
			{
				Logger.LogVerbose($"Error cleaning up drag visuals: {ex.Message}");
			}
		}

		#endregion
	}
}

