// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync.Dialogs
{
	public partial class AutoGenerationResultsDialog : Window
	{
		public static readonly AvaloniaProperty<AutoGenerationResults> ResultsProperty =
			AvaloniaProperty.Register<AutoGenerationResultsDialog, AutoGenerationResults>( nameof( Results ) );

		public static readonly AvaloniaProperty<Action<Guid>> JumpToComponentActionProperty =
			AvaloniaProperty.Register<AutoGenerationResultsDialog, Action<Guid>>( nameof( JumpToComponentAction ) );

		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public AutoGenerationResults Results
		{
			get => (AutoGenerationResults)GetValue( ResultsProperty );
			set => SetValue( ResultsProperty, value );
		}

		public Action<Guid> JumpToComponentAction
		{
			get => (Action<Guid>)GetValue( JumpToComponentActionProperty );
			set => SetValue( JumpToComponentActionProperty, value );
		}

		public ObservableCollection<ComponentResult> GeneratedComponents { get; } = new ObservableCollection<ComponentResult>();
		public ObservableCollection<ComponentResult> SkippedComponents { get; } = new ObservableCollection<ComponentResult>();

		public AutoGenerationResultsDialog()
		{
			InitializeComponent();
			ThemeManager.ApplyCurrentToWindow( this );

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;

			// Bind the collections to the ListBoxes
			GeneratedListBox.ItemsSource = GeneratedComponents;
			SkippedListBox.ItemsSource = SkippedComponents;
		}

		protected override void OnOpened( EventArgs e )
		{
			base.OnOpened( e );
			UpdateDialogContent();
		}

		private void UpdateDialogContent()
		{
			if (Results is null) return;

			// Update summary
			SummaryTextBlock.Text = $"Auto-generation complete! Processed {Results.TotalProcessed} components.";

			// Update counts
			ProcessedCountText.Text = Results.TotalProcessed.ToString();
			GeneratedCountText.Text = Results.SuccessfullyGenerated.ToString();
			SkippedCountText.Text = Results.Skipped.ToString();

			// Update expander headers
			GeneratedExpander.Header = $"Successfully Generated Instructions ({Results.SuccessfullyGenerated})";
			SkippedExpander.Header = $"Skipped Components ({Results.Skipped})";

			// Populate collections
			GeneratedComponents.Clear();
			foreach (var result in Results.ComponentResults.Where( r => r.Success ))
			{
				GeneratedComponents.Add( result );
			}

			SkippedComponents.Clear();
			foreach (var result in Results.ComponentResults.Where( r => !r.Success ))
			{
				SkippedComponents.Add( result );
			}

			// Hide expanders if they have no content
			GeneratedExpander.IsVisible = GeneratedComponents.Count > 0;
			SkippedExpander.IsVisible = SkippedComponents.Count > 0;
		}

		private void JumpToComponent_Click( object sender, RoutedEventArgs e )
		{
			if (sender is Button button && button.Tag is Guid componentGuid)
			{
				JumpToComponentAction?.Invoke( componentGuid );
				// Don't close the dialog - let the user continue reviewing results
				// Close();
			}
		}

		private void OKButton_Click( object sender, RoutedEventArgs e ) => Close();
		private void CloseButton_Click( object sender, RoutedEventArgs e ) => Close();

		private void InputElement_OnPointerMoved( object sender, PointerEventArgs e )
		{
			if (!_mouseDownForWindowMoving)
				return;

			PointerPoint currentPoint = e.GetCurrentPoint( this );
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed( object sender, PointerEventArgs e )
		{
			if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint( this );
		}

		private void InputElement_OnPointerReleased( object sender, PointerEventArgs e ) =>
			_mouseDownForWindowMoving = false;

		public static void ShowResultsDialog(
			[NotNull] Window parentWindow,
			[NotNull] AutoGenerationResults results,
			[NotNull] Action<Guid> jumpToComponentAction
		)
		{
			var dialog = new AutoGenerationResultsDialog
			{
				Results = results,
				JumpToComponentAction = jumpToComponentAction,
				Topmost = true,
			};
			dialog.Show( parentWindow );
		}

	}

	/// <summary>
	/// Represents the results of an auto-generation operation
	/// </summary>
	public class AutoGenerationResults
	{
		public int TotalProcessed { get; set; }
		public int SuccessfullyGenerated { get; set; }
		public int Skipped => TotalProcessed - SuccessfullyGenerated;
		public List<ComponentResult> ComponentResults { get; set; } = new List<ComponentResult>();
	}

	/// <summary>
	/// Represents the result of processing a single component
	/// </summary>
	public class ComponentResult
	{
		public Guid ComponentGuid { get; set; }
		public string ComponentName { get; set; }
		public bool Success { get; set; }
		public int InstructionsGenerated { get; set; }
		public string SkipReason { get; set; }
	}
}