// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JetBrains.Annotations;

namespace KOTORModSync
{
	public partial class ProgressWindow : Window
	{
		public event EventHandler CancelRequested;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public ProgressWindow()
		{
			InitializeComponent();

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) => _mouseDownForWindowMoving = false;
		public void Dispose() => Close();

		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		private void ToggleMaximizeButton_Click(object sender, RoutedEventArgs e) =>
			WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		public void UpdateMetrics(
			double percentComplete,
			int installedCount,
			int totalCount,
			DateTime installStartUtc,
			int warningCount,
			int errorCount,
			[CanBeNull] string currentComponentName
		)
		{
			PercentCompleted.Text = $"{Math.Round(percentComplete * 100)}%";
			InstalledRemaining.Text = $"{installedCount}/{totalCount} Total Installed";
			ProgressBar.Value = percentComplete;
			TimeSpan elapsed = DateTime.UtcNow - installStartUtc;
			ElapsedText.Text = elapsed.ToString("hh\\:mm\\:ss");
			int remainingCount = Math.Max(0, totalCount - installedCount);
			if ( installedCount > 0 )
			{
				var avgPerMod = TimeSpan.FromTicks(elapsed.Ticks / installedCount);
				var eta = TimeSpan.FromTicks(avgPerMod.Ticks * remainingCount);
				RemainingText.Text = eta.ToString("hh\\:mm\\:ss");
				double perMinute = installedCount / Math.Max(0.001, elapsed.TotalMinutes);
				RateText.Text = $"{perMinute:0.0} mods/min";
			}
			else
			{
				RemainingText.Text = "--:--:--";
				RateText.Text = "0.0 mods/min";
			}
			ComponentsSummaryText.Text = $"{installedCount} of {totalCount}";
			WarningsText.Text = warningCount.ToString();
			ErrorsText.Text = errorCount.ToString();
			StageText.Text = "Installing";
			CurrentOperationText.Text = string.IsNullOrWhiteSpace(currentComponentName)
				? "Preparing..."
				: $"Installing: {currentComponentName}";
			CurrentStepProgress.IsIndeterminate = true;
		}

		private void OnCancelClick([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

		public static async Task ShowProgressWindow(
			[CanBeNull] Window parentWindow,
			[CanBeNull] string message,
			decimal progress
		)
		{
			var progressWindow = new ProgressWindow
			{
				Owner = parentWindow,
				ProgressTextBlock =
				{
					Text = message,
				},
				ProgressBar =
				{
					Value = (double)progress,
				},
				Topmost = true,
			};

			if ( !(parentWindow is null) )
				_ = await progressWindow.ShowDialog<bool?>(parentWindow);
		}

		private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
		{
			if ( !_mouseDownForWindowMoving )
				return;

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed(object sender, PointerEventArgs e)
		{
			if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen )
				return;

			if ( ShouldIgnorePointerForWindowDrag(e) )
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
		{

			if ( !(e.Source is Visual source) )
				return false;

			Visual current = source;
			while ( current != null && current != this )
			{
				switch ( current )
				{

					case Button _:
					case TextBox _:
					case ComboBox _:
					case ListBox _:
					case MenuItem _:
					case Menu _:
					case Expander _:
					case Slider _:
					case TabControl _:
					case TabItem _:

					case Control control when control.ContextMenu?.IsOpen == true:
						return true;
					case Control control when control.ContextFlyout?.IsOpen == true:
						return true;
					default:
						current = current.GetVisualParent();
						break;
				}
			}

			return false;
		}
	}
}
