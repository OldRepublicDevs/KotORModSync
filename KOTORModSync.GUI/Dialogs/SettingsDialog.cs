// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Controls;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;
using KOTORModSync.Services;

namespace KOTORModSync.Dialogs
{
	internal partial class SettingsDialog : Window
	{
		[CanBeNull] private MainConfig _mainConfigInstance;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;
		private List<GitHubRelease> _holopatcherReleases = new List<GitHubRelease>();

		private class GitHubRelease
		{
			public string TagName { get; set; }
			public string Name { get; set; }
			public bool PreRelease { get; set; }
			public bool Draft { get; set; }
			public List<GitHubAsset> Assets { get; set; }
			public string Body { get; set; }
		}

		private class GitHubAsset
		{
			public string Name { get; set; }
			public string BrowserDownloadUrl { get; set; }
		}

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
			Opened += async (s, e) => await InitializeHolopatcherVersionsAsync();
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		public void InitializeFromMainWindow(Window mainWindow)
		{

			if ( mainWindow is MainWindow mw )
			{
				MainConfigInstance = mw.MainConfigInstance;
				ParentWindow = mw;
			}

			ThemeManager.ApplyCurrentToWindow(this);

			Logger.LogVerbose("SettingsDialog.InitializeFromMainWindow start");
			Logger.LogVerbose($"SettingsDialog: Source='{MainConfigInstance?.sourcePathFullName}', Dest='{MainConfigInstance?.destinationPathFullName}'");


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
			LoadFileEncodingSettings();

			Logger.LogVerbose("SettingsDialog.InitializeFromMainWindow end");
		}

		private void LoadFileEncodingSettings()
		{
			try
			{
				ComboBox fileEncodingComboBox = this.FindControl<ComboBox>("FileEncodingComboBox");
				if ( fileEncodingComboBox != null && MainConfigInstance != null )
				{
					string encoding = MainConfigInstance.fileEncoding ?? "utf-8";
					fileEncodingComboBox.SelectedIndex = encoding.Equals("windows-1252", StringComparison.OrdinalIgnoreCase) ||
														 encoding.Equals("cp-1252", StringComparison.OrdinalIgnoreCase) ||
														 encoding.Equals("cp1252", StringComparison.OrdinalIgnoreCase)
						? 1
						: 0;
					Logger.LogVerbose($"SettingsDialog: File encoding set to '{encoding}' (index {fileEncodingComboBox.SelectedIndex})");
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to load file encoding settings");
			}
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

		private void SaveAppSettings()
		{
			try
			{
				if ( MainConfigInstance == null )
				{
					Logger.LogWarning("Cannot save settings: MainConfigInstance is null");
					return;
				}

				string currentTheme = ThemeManager.GetCurrentStylePath();
				var settings = Models.AppSettings.FromCurrentState(MainConfigInstance, currentTheme);
				Models.SettingsManager.SaveSettings(settings);

				Logger.Log("Application settings saved successfully");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to save application settings");
			}
		}

		[UsedImplicitly]
		private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
		{

			if ( MainConfigInstance == null ) return;

			try
			{
				Logger.LogVerbose($"SettingsDialog.OnDirectoryChanged type={e.PickerType} path='{e.Path}'");
				switch ( e.PickerType )
				{
					case DirectoryPickerType.ModDirectory:
						{
							var modDirectory = new DirectoryInfo(e.Path);
							MainConfigInstance.sourcePath = modDirectory;
							Logger.LogVerbose($"SettingsDialog: MainConfig.sourcePath set -> '{MainConfigInstance.sourcePathFullName}'");
							break;
						}
					case DirectoryPickerType.KotorDirectory:
						{
							var kotorDirectory = new DirectoryInfo(e.Path);
							MainConfigInstance.destinationPath = kotorDirectory;
							Logger.LogVerbose($"SettingsDialog: MainConfig.destinationPath set -> '{MainConfigInstance.destinationPathFullName}'");
							break;
						}
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
		private void FileEncodingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if ( !(sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem) )
				return;

			if ( MainConfigInstance == null )
				return;

			string encodingTag = selectedItem.Tag?.ToString() ?? "utf-8";
			MainConfigInstance.fileEncoding = encodingTag;
			Logger.LogVerbose($"File encoding changed to: {encodingTag}");
		}

		[UsedImplicitly]
		private void OK_Click(object sender, RoutedEventArgs e)
		{
			SaveTelemetrySettings();
			SaveAppSettings();
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
				await Logger.LogExceptionAsync(ex, "[Telemetry] Failed to show privacy details");
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


		private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
		{
			if ( !_mouseDownForWindowMoving )
				return;
			PointerPoint currentPoint = e.GetCurrentPoint(this);
			var newPoint = new PixelPoint(Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X), Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y));
			Position = newPoint;
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

		private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) => _mouseDownForWindowMoving = false;

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
					default:
						current = current.GetVisualParent();
						break;
				}
			}
			return false;
		}

		private async Task InitializeHolopatcherVersionsAsync()
		{
			try
			{
				await FetchHolopatcherReleasesAsync();
				UpdateCurrentVersionLabel();
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to initialize HoloPatcher versions");
			}
		}

		private async void RefreshVersionsButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var button = this.FindControl<Button>("RefreshVersionsButton");
				if ( button != null )
					button.IsEnabled = false;

				await FetchHolopatcherReleasesAsync();

				if ( button != null )
					button.IsEnabled = true;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to refresh HoloPatcher versions");
			}
		}

		private async void DownloadVersionButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var comboBox = this.FindControl<ComboBox>("HolopatcherVersionComboBox");
				if ( comboBox?.SelectedItem == null )
				{
					var dialog = new MessageDialog("No Version Selected", "Please select a HoloPatcher version to download.", "OK");
					await dialog.ShowDialog(this);
					return;
				}

				var selectedRelease = comboBox.SelectedItem as GitHubRelease;
				if ( selectedRelease == null )
					return;

				await DownloadAndInstallHolopatcherAsync(selectedRelease);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to download HoloPatcher");
			}
		}

		private async Task FetchHolopatcherReleasesAsync()
		{
			try
			{
				await Logger.LogAsync("Fetching HoloPatcher releases from GitHub...");

				using ( var client = new HttpClient() )
				{
					client.DefaultRequestHeaders.UserAgent.ParseAdd("KOTORModSync/1.0");
					client.Timeout = TimeSpan.FromSeconds(30);

					string url = "https://api.github.com/repos/NickHugi/PyKotor/releases";
					var response = await client.GetAsync(url);
					response.EnsureSuccessStatusCode();

					string json = await response.Content.ReadAsStringAsync();
					using ( JsonDocument doc = JsonDocument.Parse(json) )
					{
						_holopatcherReleases.Clear();

						foreach ( JsonElement releaseElement in doc.RootElement.EnumerateArray() )
						{
							string tagName = releaseElement.GetProperty("tag_name").GetString();

							// Filter for patcher releases (tags ending with "-patcher")
							if ( !tagName.ToLowerInvariant().Contains("patcher") )
								continue;

							var release = new GitHubRelease
							{
								TagName = tagName,
								Name = releaseElement.GetProperty("name").GetString(),
								PreRelease = releaseElement.GetProperty("prerelease").GetBoolean(),
								Draft = releaseElement.GetProperty("draft").GetBoolean(),
								Body = releaseElement.TryGetProperty("body", out var body) ? body.GetString() : "",
								Assets = new List<GitHubAsset>()
							};

							if ( releaseElement.TryGetProperty("assets", out JsonElement assetsElement) )
							{
								foreach ( JsonElement assetElement in assetsElement.EnumerateArray() )
								{
									release.Assets.Add(new GitHubAsset
									{
										Name = assetElement.GetProperty("name").GetString(),
										BrowserDownloadUrl = assetElement.GetProperty("browser_download_url").GetString()
									});
								}
							}

							if ( !release.Draft )
								_holopatcherReleases.Add(release);
						}
					}
				}

				// Update ComboBox
				var comboBox = this.FindControl<ComboBox>("HolopatcherVersionComboBox");
				if ( comboBox != null )
				{
					comboBox.ItemsSource = _holopatcherReleases;
					comboBox.DisplayMemberBinding = new Avalonia.Data.Binding("TagName");
					if ( _holopatcherReleases.Count > 0 )
						comboBox.SelectedIndex = 0;
				}

				await Logger.LogAsync($"Found {_holopatcherReleases.Count} HoloPatcher releases");
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to fetch HoloPatcher releases from GitHub");
			}
		}

		private async Task DownloadAndInstallHolopatcherAsync(GitHubRelease release)
		{
			try
			{
				await Logger.LogAsync($"Downloading PyKotor source code {release.TagName}...");

				string baseDir = Utility.GetBaseDirectory();
				string resourcesDir = Utility.GetResourcesDirectory(baseDir);

				// Download the PyKotor source code from GitHub
				string downloadUrl = $"https://github.com/NickHugi/PyKotor/archive/refs/tags/{release.TagName}.zip";
				string tempFile = Path.Combine(Path.GetTempPath(), $"PyKotor-{release.TagName}.zip");

				await Logger.LogAsync($"Downloading from {downloadUrl}...");

				using ( var client = new HttpClient() )
				{
					client.Timeout = TimeSpan.FromMinutes(10);
					client.DefaultRequestHeaders.UserAgent.ParseAdd("KOTORModSync/1.0");

					var response = await client.GetAsync(downloadUrl);
					response.EnsureSuccessStatusCode();

					using ( var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None) )
					{
						await response.Content.CopyToAsync(fs);
					}
				}

				await Logger.LogAsync("Download complete. Extracting...");

				// Extract to temp directory
				var tempDir = Path.Combine(Path.GetTempPath(), "pykotor_extract_" + Guid.NewGuid().ToString());
				Directory.CreateDirectory(tempDir);

				try
				{
					ZipFile.ExtractToDirectory(tempFile, tempDir);

					// Find the extracted PyKotor directory (GitHub adds a folder like PyKotor-{tag})
					var extractedDirs = Directory.GetDirectories(tempDir, "PyKotor-*");
					if ( extractedDirs.Length == 0 )
					{
						throw new DirectoryNotFoundException("Could not find PyKotor directory in extracted archive.");
					}

					string pyKotorExtracted = extractedDirs[0];
					string pyKotorDest = Path.Combine(resourcesDir, "PyKotor");

					// Remove old PyKotor if it exists
					if ( Directory.Exists(pyKotorDest) )
					{
						Directory.Delete(pyKotorDest, recursive: true);
					}

					// Copy the new PyKotor
					CopyDirectory(pyKotorExtracted, pyKotorDest);

					await Logger.LogAsync($"PyKotor {release.TagName} installed successfully!");

					var successDialog = new MessageDialog(
						"Installation Complete",
						$"PyKotor {release.TagName} source code has been installed.\n\nHoloPatcher will use this version.",
						"OK"
					);
					await successDialog.ShowDialog(this);

					UpdateCurrentVersionLabel();
				}
				finally
				{
					// Cleanup
					if ( Directory.Exists(tempDir) )
						Directory.Delete(tempDir, recursive: true);
					if ( File.Exists(tempFile) )
						File.Delete(tempFile);
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to download and install PyKotor");
				var errorDialog = new MessageDialog(
					"Installation Failed",
					$"Failed to install PyKotor:\n\n{ex.Message}",
					"OK"
				);
				await errorDialog.ShowDialog(this);
			}
		}

		private static void CopyDirectory(string sourceDir, string destDir)
		{
			Directory.CreateDirectory(destDir);

			foreach ( string file in Directory.GetFiles(sourceDir, "*.py", SearchOption.AllDirectories) )
			{
				string relativePath = Path.GetRelativePath(sourceDir, file);
				string destFile = Path.Combine(destDir, relativePath);

				Directory.CreateDirectory(Path.GetDirectoryName(destFile));
				File.Copy(file, destFile, overwrite: true);
			}
		}

		private void UpdateCurrentVersionLabel()
		{
			try
			{
				var label = this.FindControl<TextBlock>("CurrentVersionLabel");
				if ( label == null )
					return;

				string baseDir = Utility.GetBaseDirectory();
				string resourcesDir = Utility.GetResourcesDirectory(baseDir);

				// Check for PyKotor source directory
				string pyKotorPath = Path.Combine(resourcesDir, "PyKotor");
				string holopatcherPath = Path.Combine(pyKotorPath, "Tools", "HoloPatcher", "src", "holopatcher");

				if ( Directory.Exists(holopatcherPath) )
				{
					// Try to read version from config.py
					string configPath = Path.Combine(holopatcherPath, "config.py");
					if ( File.Exists(configPath) )
					{
						try
						{
							string configContent = File.ReadAllText(configPath);
							var versionMatch = System.Text.RegularExpressions.Regex.Match(
								configContent,
								@"""currentVersion"":\s*""([^""]+)"""
							);
							if ( versionMatch.Success )
							{
								label.Text = $"Current: PyKotor v{versionMatch.Groups[1].Value} (Python source)";
								return;
							}
						}
						catch { }
					}

					label.Text = "Current: Using bundled PyKotor source";
				}
				else
				{
					label.Text = "Current: PyKotor not found";
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to update current version label");
			}
		}

		private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var button = this.FindControl<Button>("CheckForUpdatesButton");
				var statusText = this.FindControl<TextBlock>("UpdateStatusText");

				if ( button != null )
				{
					button.IsEnabled = false;
					button.Content = "Checking...";
				}

				if ( statusText != null )
					statusText.Text = "Checking for updates...";

				// Get the auto-update service from the application
				var app = Avalonia.Application.Current as App;
				if ( app == null )
				{
					if ( statusText != null )
						statusText.Text = "Error: Unable to access update service.";
					Logger.Log("Unable to access App instance for update check");
					return;
				}

				// Create and use AutoUpdateService
				using ( var updateService = new AutoUpdateService() )
				{
					updateService.Initialize();
					bool updatesAvailable = await updateService.CheckForUpdatesAsync();

					if ( statusText != null )
					{
						statusText.Text = updatesAvailable
							? "Updates found! Check the update dialog."
							: "You are running the latest version.";
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to check for updates");
				var statusText = this.FindControl<TextBlock>("UpdateStatusText");
				if ( statusText != null )
					statusText.Text = $"Error checking for updates: {ex.Message}";
			}
			finally
			{
				var button = this.FindControl<Button>("CheckForUpdatesButton");
				if ( button != null )
				{
					button.IsEnabled = true;
					button.Content = "Check for Updates";
				}
			}
		}
	}
}