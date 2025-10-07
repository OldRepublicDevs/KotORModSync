// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Controls;
using KOTORModSync.Core;

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
			// Access MainConfigInstance through casting and store parent reference
			if ( mainWindow is MainWindow mw )
			{
				MainConfigInstance = mw.MainConfigInstance;
				ParentWindow = mw;
			}

			Logger.LogVerbose("SettingsDialog.InitializeFromMainWindow start");
			Logger.LogVerbose($"SettingsDialog: Source='{MainConfigInstance?.sourcePathFullName}', Dest='{MainConfigInstance?.destinationPathFullName}'");

			// Initialize directory pickers with current values
			ComboBox styleComboBox = this.FindControl<ComboBox>("StyleComboBox");

			// Set the current theme selection based on the actual current theme
			string currentTheme = ThemeManager.GetCurrentStylePath();
			Logger.LogVerbose($"SettingsDialog: Current theme path='{currentTheme}'");

			// Map theme paths to ComboBox indices
			int selectedIndex = 0;
			if ( string.Equals(currentTheme, "/Styles/KotorStyle.axaml", StringComparison.OrdinalIgnoreCase) )
				selectedIndex = 0; // K1 Style
			else if ( string.Equals(currentTheme, "/Styles/Kotor2Style.axaml", StringComparison.OrdinalIgnoreCase) )
				selectedIndex = 1; // TSL Style

			Logger.LogVerbose($"SettingsDialog: Setting ComboBox SelectedIndex to {selectedIndex}");
			if ( styleComboBox != null )
				styleComboBox.SelectedIndex = selectedIndex;

			// Initialize directory picker controls with current paths
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

			Logger.LogVerbose("SettingsDialog.InitializeFromMainWindow end");
		}

		[UsedImplicitly]
		private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
		{
			// Handle directory changes and update MainConfig
			if ( MainConfigInstance == null ) return;

			try
			{
				Logger.LogVerbose($"SettingsDialog.OnDirectoryChanged type={e.PickerType} path='{e.Path}'");
				if ( e.PickerType == DirectoryPickerType.ModDirectory )
				{
					// Update mod directory path
					var modDirectory = new DirectoryInfo(e.Path);
					MainConfigInstance.sourcePath = modDirectory;
					Logger.LogVerbose($"SettingsDialog: MainConfig.sourcePath set -> '{MainConfigInstance.sourcePathFullName}'");
				}
				else if ( e.PickerType == DirectoryPickerType.KotorDirectory )
				{
					// Update KOTOR directory path
					var kotorDirectory = new DirectoryInfo(e.Path);
					MainConfigInstance.destinationPath = kotorDirectory;
					Logger.LogVerbose($"SettingsDialog: MainConfig.destinationPath set -> '{MainConfigInstance.destinationPathFullName}'");
				}

				// Trigger synchronization in the parent MainWindow to update Step 1 pickers
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
			// Handle theme selection changes
			if ( sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem )
			{
				// This will be handled by the main window when the dialog closes
			}
		}

		[UsedImplicitly]
		private void OK_Click(object sender, RoutedEventArgs e) => Close(dialogResult: true);
		[UsedImplicitly]
		private void Cancel_Click(object sender, RoutedEventArgs e) => Close(dialogResult: false);

		[UsedImplicitly]
		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		[UsedImplicitly]
		private void ToggleMaximizeButton_Click(object sender, RoutedEventArgs e) =>
			WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

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