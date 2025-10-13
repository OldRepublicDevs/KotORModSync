// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Controls;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
	internal partial class SettingsDialog : Window
	{
		[CanBeNull] private MainConfig _mainConfigInstance;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public MainConfig MainConfigInstance
		{
			get => _mainConfigInstance;
			set
			{
				_mainConfigInstance = value;
				DataContext = this;
			}
		}

		[CanBeNull]
		public MainWindow ParentWindow { get; set; }

		public SettingsDialog()
		{
			InitializeComponent();
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		public void InitializeFromMainWindow(Window mainWindow)
		{

			if ( mainWindow is MainWindow mw )
			{
				MainConfigInstance = mw.MainConfigInstance;
				ParentWindow = mw;
			}

			Logger.LogVerbose("SettingsDialog.InitializeFromMainWindow start");
			Logger.LogVerbose($"SettingsDialog: Source='{MainConfigInstance?.sourcePathFullName}', Dest='{MainConfigInstance?.destinationPathFullName}'");

			ComboBox styleComboBox = this.FindControl<ComboBox>("StyleComboBox");

			string currentTheme = ThemeManager.GetCurrentStylePath();
			Logger.LogVerbose($"SettingsDialog: Current theme path='{currentTheme}'");

			int selectedIndex = SettingsDialog.GetThemeIndexFromTargetGame();
			if ( selectedIndex == -1 )
			{
				if ( string.Equals(currentTheme, "/Styles/KotorStyle.axaml", StringComparison.OrdinalIgnoreCase) )
					selectedIndex = 0;
				else if ( string.Equals(currentTheme, "/Styles/Kotor2Style.axaml", StringComparison.OrdinalIgnoreCase) )
					selectedIndex = 1;
			}

			Logger.LogVerbose($"SettingsDialog: Setting ComboBox SelectedIndex to {selectedIndex}");
			if ( styleComboBox != null )
				styleComboBox.SelectedIndex = selectedIndex;

			DirectoryPickerControl modDirectoryPicker = this.FindControl<DirectoryPickerControl>(name: "ModDirectoryPicker");
			DirectoryPickerControl kotorDirectoryPicker = this.FindControl<DirectoryPickerControl>(name: "KotorDirectoryPicker");

			if ( modDirectoryPicker != null && MainConfigInstance != null )
			{
				Logger.LogVerbose($"SettingsDialog: Applying mod dir -> '{MainConfigInstance.sourcePathFullName}'");
				modDirectoryPicker.SetCurrentPath(MainConfigInstance.sourcePathFullName ?? string.Empty);
			}

			if ( kotorDirectoryPicker != null && MainConfigInstance != null )
			{
				Logger.LogVerbose($"SettingsDialog: Applying kotor dir -> '{MainConfigInstance.destinationPathFullName}'");
				kotorDirectoryPicker.SetCurrentPath(MainConfigInstance.destinationPathFullName ?? string.Empty);
			}

			LoadTelemetrySettings();

			Logger.LogVerbose("SettingsDialog.InitializeFromMainWindow end");
		}

		private static int GetThemeIndexFromTargetGame()
		{
			if ( string.IsNullOrWhiteSpace(MainConfig.TargetGame) )
				return -1;

			string game = MainConfig.TargetGame.Trim();

			if ( game.Equals("K1", StringComparison.OrdinalIgnoreCase) ||
				 game.Equals("KOTOR1", StringComparison.OrdinalIgnoreCase) )
			{
				Logger.LogVerbose($"SettingsDialog: Target game '{game}' -> K1 theme");
				return 0;
			}

			if ( game.Equals("TSL", StringComparison.OrdinalIgnoreCase) ||
				 game.Equals("K2", StringComparison.OrdinalIgnoreCase) ||
				 game.Equals("KOTOR2", StringComparison.OrdinalIgnoreCase) )
			{
				Logger.LogVerbose($"SettingsDialog: Target game '{game}' -> TSL theme");
				return 1;
			}

			Logger.LogVerbose($"SettingsDialog: Unknown target game '{game}', using default theme");
			return -1;
		}

		private void LoadTelemetrySettings()
		{
			try
			{
				var telemetryConfig = TelemetryConfiguration.Load();

				CheckBox enableTelemetryCheckBox = this.FindControl<CheckBox>("EnableTelemetryCheckBox");

				if ( enableTelemetryCheckBox != null )
					enableTelemetryCheckBox.IsChecked = telemetryConfig.IsEnabled;

				Logger.LogVerbose($"[Telemetry] Loaded telemetry settings: Enabled={telemetryConfig.IsEnabled}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "[Telemetry] Failed to load telemetry settings");
			}
		}

		private void SaveTelemetrySettings()
		{
			try
			{
				var telemetryConfig = TelemetryConfiguration.Load();

				CheckBox enableTelemetryCheckBox = this.FindControl<CheckBox>("EnableTelemetryCheckBox");

				bool wasEnabled = telemetryConfig.IsEnabled;
				bool isNowEnabled = enableTelemetryCheckBox?.IsChecked ?? true;

				telemetryConfig.SetUserConsent(isNowEnabled);

				TelemetryService.Instance.UpdateConfiguration(telemetryConfig);

				Logger.Log($"[Telemetry] Telemetry settings saved: Enabled={isNowEnabled}");

				if ( !wasEnabled && isNowEnabled )
				{
					Logger.Log("[Telemetry] Telemetry has been enabled");
				}
				else if ( wasEnabled && !isNowEnabled )
				{
					Logger.Log("[Telemetry] Telemetry has been disabled");
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "[Telemetry] Failed to save telemetry settings");
			}
		}

		[UsedImplicitly]
		private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
		{

			if ( MainConfigInstance == null ) return;

			try
			{
				Logger.LogVerbose($"SettingsDialog.OnDirectoryChanged type={e.PickerType} path='{e.Path}'");
				if ( e.PickerType == DirectoryPickerType.ModDirectory )
				{

					var modDirectory = new DirectoryInfo(e.Path);
					MainConfigInstance.sourcePath = modDirectory;
					Logger.LogVerbose($"SettingsDialog: MainConfig.sourcePath set -> '{MainConfigInstance.sourcePathFullName}'");
				}
				else if ( e.PickerType == DirectoryPickerType.KotorDirectory )
				{

					var kotorDirectory = new DirectoryInfo(e.Path);
					MainConfigInstance.destinationPath = kotorDirectory;
					Logger.LogVerbose($"SettingsDialog: MainConfig.destinationPath set -> '{MainConfigInstance.destinationPathFullName}'");
				}

				if ( ParentWindow == null )
					return;
				Logger.LogVerbose("SettingsDialog: Triggering parent window directory synchronization");
				ParentWindow.SyncDirectoryPickers(e.PickerType, e.Path);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private void StyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

			if ( sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem )
			{

			}
		}

		[UsedImplicitly]
		private void OK_Click(object sender, RoutedEventArgs e)
		{

			SaveTelemetrySettings();
			Close(dialogResult: true);
		}

		[UsedImplicitly]
		private void Cancel_Click(object sender, RoutedEventArgs e) => Close(dialogResult: false);

		[UsedImplicitly]
		private async void ViewPrivacyDetails_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var telemetryConfig = TelemetryConfiguration.Load();
				string privacySummary = telemetryConfig.GetPrivacySummary();

				await InformationDialog.ShowInformationDialogAsync(this, privacySummary);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "[Telemetry] Failed to show privacy details");
			}
		}

		[UsedImplicitly]
		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		[UsedImplicitly]
		private void ToggleMaximizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			if ( !(sender is Button maximizeButton) )
				return;
			if ( WindowState == WindowState.Maximized )
			{
				WindowState = WindowState.Normal;
				maximizeButton.Content = "▢";
			}
			else
			{
				WindowState = WindowState.Maximized;
				maximizeButton.Content = "▣";
			}
		}

		[UsedImplicitly]
		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		public string GetSelectedTheme()
		{
			ComboBox styleComboBox = this.FindControl<ComboBox>(name: "StyleComboBox");
			return styleComboBox?.SelectedItem is ComboBoxItem selectedItem ? selectedItem.Tag?.ToString() ?? "/Styles/KotorStyle.axaml" : "/Styles/KotorStyle.axaml";
		}

		private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
		{
			if ( !_mouseDownForWindowMoving ) return;
			PointerPoint currentPoint = e.GetCurrentPoint(this);
			var newPoint = new PixelPoint(Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X), Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y));
			Position = newPoint;
		}

		private void InputElement_OnPointerPressed(object sender, PointerEventArgs e)
		{
			if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen ) return;
			if ( ShouldIgnorePointerForWindowDrag(e) ) return;
			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) => _mouseDownForWindowMoving = false;

		private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
		{
			if ( !(e.Source is Visual source) ) return false;
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
					default:
						current = current.GetVisualParent();
						break;
				}
			}
			return false;
		}
	}
}