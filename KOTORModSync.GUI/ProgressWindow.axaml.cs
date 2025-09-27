// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using JetBrains.Annotations;

namespace KOTORModSync
{
	public partial class ProgressWindow : Window
	{
		public event EventHandler CancelRequested;

		public ProgressWindow() => InitializeComponent();
		public void Dispose() => Close();


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

		private void OnCancelClick([CanBeNull] object sender, [CanBeNull] Avalonia.Interactivity.RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

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
	}
}
