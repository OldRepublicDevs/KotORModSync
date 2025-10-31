// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Dialogs.WizardPages
{
	public class InstallingPage : IWizardPage
	{
		public string Title => "Installing Mods";
		public string Subtitle => "Please wait while mods are being installed...";
		public Control Content { get; }
		public bool CanNavigateBack => false;
		public bool CanNavigateForward => _canNavigateForward; // Will be enabled when installation completes
		public bool CanCancel => true;

		private readonly List<ModComponent> _allComponents;
		private readonly MainConfig _mainConfig;
		private readonly CancellationTokenSource _cancellationTokenSource;

		// UI Elements - Progress tracking
		private readonly ProgressBar _mainProgressBar;
		private readonly ProgressBar _currentModProgress;
		private readonly TextBlock _percentText;
		private readonly TextBlock _countText;
		private readonly TextBlock _currentModText;
		private readonly TextBlock _currentOperationText;
		private readonly TextBlock _elapsedTimeText;
		private readonly TextBlock _remainingTimeText;
		private readonly TextBlock _rateText;
		private readonly TextBlock _warningsText;
		private readonly TextBlock _errorsText;
		private readonly TextBlock _directionsText;

		// Installation state
		private bool _isInstalling;
		private bool _installationComplete;
		private bool _canNavigateForward;
		private DateTime _installStartTime;
		private int _installedCount;
		private int _warningCount;
		private int _errorCount;
		private Stopwatch _stopwatch;

		public InstallingPage(
			[NotNull][ItemNotNull] List<ModComponent> allComponents,
			[NotNull] MainConfig mainConfig,
			[NotNull] CancellationTokenSource cancellationTokenSource)
		{
			_allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));

			var mainPanel = new Grid
			{
				RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto")
			};

			// Progress section
			var progressPanel = new StackPanel
			{
				Spacing = 8
			};
			Grid.SetRow(progressPanel, 0);

			var progressHeader = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,Auto")
			};

			_percentText = new TextBlock
			{
				Text = "0%",
				FontSize = 18,
				FontWeight = FontWeight.Bold
			};
			Grid.SetColumn(_percentText, 0);
			progressHeader.Children.Add(_percentText);

			_countText = new TextBlock
			{
				Text = "0/0 mods installed",
				FontSize = 14,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			Grid.SetColumn(_countText, 1);
			progressHeader.Children.Add(_countText);

			progressPanel.Children.Add(progressHeader);

			_mainProgressBar = new ProgressBar
			{
				Height = 18,
				Minimum = 0,
				Maximum = 1,
				Value = 0
			};
			progressPanel.Children.Add(_mainProgressBar);

			_currentModText = new TextBlock
			{
				Text = "Preparing installation...",
				FontSize = 14,
				FontWeight = FontWeight.SemiBold
			};
			progressPanel.Children.Add(_currentModText);

			_currentModProgress = new ProgressBar
			{
				Height = 6,
				IsIndeterminate = true
			};
			progressPanel.Children.Add(_currentModProgress);

			mainPanel.Children.Add(progressPanel);

			// Directions/current mod description
			Grid.SetRow(_directionsText = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				FontSize = 12,
				Opacity = 0.8,
				Margin = new Avalonia.Thickness(0, 16, 0, 0),
				MaxHeight = 100
			}, 1);
			mainPanel.Children.Add(_directionsText);

			// Statistics grid
			var statsGrid = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
				RowDefinitions = new RowDefinitions("Auto,Auto"),
				Margin = new Avalonia.Thickness(0, 16, 0, 0)
			};
			Grid.SetRow(statsGrid, 2);

			// Elapsed time
			var elapsedPanel = CreateStatPanel("⏱️ Elapsed", out _elapsedTimeText);
			Grid.SetRow(elapsedPanel, 0);
			Grid.SetColumn(elapsedPanel, 0);
			statsGrid.Children.Add(elapsedPanel);

			// Remaining time
			var remainingPanel = CreateStatPanel("⏳ Remaining", out _remainingTimeText);
			Grid.SetRow(remainingPanel, 0);
			Grid.SetColumn(remainingPanel, 1);
			statsGrid.Children.Add(remainingPanel);

			// Rate
			var ratePanel = CreateStatPanel("📊 Rate", out _rateText);
			Grid.SetRow(ratePanel, 0);
			Grid.SetColumn(ratePanel, 2);
			statsGrid.Children.Add(ratePanel);

			// Current operation
			_currentOperationText = new TextBlock
			{
				Text = "Initializing...",
				FontSize = 12,
				Opacity = 0.7
			};
			Grid.SetRow(_currentOperationText, 0);
			Grid.SetColumn(_currentOperationText, 3);
			statsGrid.Children.Add(_currentOperationText);

			// Warnings
			var warningsPanel = CreateStatPanel("⚠️ Warnings", out _warningsText);
			Grid.SetRow(warningsPanel, 1);
			Grid.SetColumn(warningsPanel, 0);
			statsGrid.Children.Add(warningsPanel);

			// Errors
			var errorsPanel = CreateStatPanel("❌ Errors", out _errorsText);
			Grid.SetRow(errorsPanel, 1);
			Grid.SetColumn(errorsPanel, 1);
			statsGrid.Children.Add(errorsPanel);

			mainPanel.Children.Add(statsGrid);

			// TODO: Mod image slideshow area
			Grid.SetRow(new Border
			{
				Margin = new Avalonia.Thickness(0, 16, 0, 0),
				Child = new TextBlock
				{
					Text = "📸 Mod showcase slideshow area (coming soon)",
					FontSize = 12,
					Opacity = 0.5,
					HorizontalAlignment = HorizontalAlignment.Center
				}
			}, 3);

			Content = mainPanel;
		}

		private StackPanel CreateStatPanel(string label, out TextBlock valueText)
		{
			var panel = new StackPanel
			{
				Spacing = 4
			};

			panel.Children.Add(new TextBlock
			{
				Text = label,
				FontSize = 12,
				FontWeight = FontWeight.SemiBold
			});

			valueText = new TextBlock
			{
				Text = "---",
				FontSize = 14
			};
			panel.Children.Add(valueText);

			return panel;
		}

		public async Task OnNavigatedToAsync(CancellationToken cancellationToken)
		{
			if (_isInstalling || _installationComplete)
				return;

			_isInstalling = true;
			_stopwatch = Stopwatch.StartNew();
			_installStartTime = DateTime.UtcNow;

			// Subscribe to logger events
			Logger.Logged += OnLogMessage;
			Logger.ExceptionLogged += OnException;

			// Start installation
			_ = Task.Run(async () => await RunInstallation(cancellationToken).ConfigureAwait(false), cancellationToken);
		}

		private async Task RunInstallation(CancellationToken cancellationToken)
		{
			try
			{
				var selectedMods = _allComponents.Where(c => c.IsSelected && !c.WidescreenOnly).ToList();
				int totalMods = selectedMods.Count;

				for (int i = 0; i < selectedMods.Count; i++)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						await UpdateUIAsync(() =>
						{
							_currentModText.Text = "Installation cancelled by user";
							_currentOperationText.Text = "Cancelled";
						}).ConfigureAwait(false);
						break;
					}

					var component = selectedMods[i];

					// Update UI
					await UpdateUIAsync(() =>
					{
						double progress = (double)i / totalMods;
						_mainProgressBar.Value = progress;
						_percentText.Text = $"{Math.Round(progress * 100)}%";
						_countText.Text = $"{i}/{totalMods} mods installed";
						_currentModText.Text = $"Installing: {component.Name}";
						_directionsText.Text = component.Directions ?? string.Empty;
						_installedCount = i;

						UpdateMetrics(totalMods);
					}).ConfigureAwait(false);

					// Install the mod
					await Logger.LogAsync($"Starting installation of '{component.Name}'").ConfigureAwait(false);

					var exitCode = await InstallationService.InstallSingleComponentAsync(
						component,
						_allComponents,
						cancellationToken
					).ConfigureAwait(false);

					await Logger.LogAsync($"Finished installation of '{component.Name}' with exit code: {UtilityHelper.GetEnumDescription(exitCode)}").ConfigureAwait(false);

					if (exitCode != ModComponent.InstallExitCode.Success)
					{
						// Handle installation failure
						await Logger.LogErrorAsync($"Failed to install '{component.Name}'").ConfigureAwait(false);
						// TODO: Show error dialog and allow user to skip or cancel
					}
				}

				// Installation complete
				_installedCount = selectedMods.Count;
				_installationComplete = true;

				await UpdateUIAsync(() =>
				{
					_mainProgressBar.Value = 1;
					_percentText.Text = "100%";
					_countText.Text = $"{selectedMods.Count}/{selectedMods.Count} mods installed";
					_currentModText.Text = "✅ Installation complete!";
					_currentOperationText.Text = "Complete";
					_currentModProgress.IsIndeterminate = false;
					_currentModProgress.Value = 1;
					UpdateMetrics(selectedMods.Count);
				}).ConfigureAwait(false);

				// Enable next button
				_canNavigateForward = true;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, "Error during installation").ConfigureAwait(false);
			}
			finally
			{
				_isInstalling = false;
				_stopwatch?.Stop();
				Logger.Logged -= OnLogMessage;
				Logger.ExceptionLogged -= OnException;
			}
		}

		private void OnLogMessage(string message)
		{
			if (string.IsNullOrEmpty(message))
				return;

			if (message.IndexOf("[Warning]", StringComparison.OrdinalIgnoreCase) >= 0)
				_warningCount++;
			if (message.IndexOf("[Error]", StringComparison.OrdinalIgnoreCase) >= 0)
				_errorCount++;

			_ = UpdateUIAsync(() =>
			{
				_warningsText.Text = _warningCount.ToString(CultureInfo.InvariantCulture);
				_errorsText.Text = _errorCount.ToString(CultureInfo.InvariantCulture);
			});
		}

		private void OnException(Exception ex)
		{
			_errorCount++;
			_ = UpdateUIAsync(() =>
			{
				_errorsText.Text = _errorCount.ToString(CultureInfo.InvariantCulture);
			});
		}

		private void UpdateMetrics(int totalMods)
		{
			TimeSpan elapsed = _stopwatch.Elapsed;
			_elapsedTimeText.Text = elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

			if (_installedCount > 0)
			{
				var avgPerMod = TimeSpan.FromTicks(elapsed.Ticks / _installedCount);
				int remaining = Math.Max(0, totalMods - _installedCount);
				var eta = TimeSpan.FromTicks(avgPerMod.Ticks * remaining);
				_remainingTimeText.Text = eta.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

				double perMinute = _installedCount / Math.Max(0.001, elapsed.TotalMinutes);
				_rateText.Text = $"{perMinute:0.0} mods/min";
			}
			else
			{
				_remainingTimeText.Text = "--:--:--";
				_rateText.Text = "0.0 mods/min";
			}
		}

		private Task UpdateUIAsync(Action action)
		{
			return Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Normal).GetTask();
		}

		public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}

		public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
		{
			if (!_installationComplete)
			{
				return Task.FromResult((false, "Installation is still in progress. Please wait for it to complete."));
			}

			return Task.FromResult((true, (string)null));
		}
	}
}

