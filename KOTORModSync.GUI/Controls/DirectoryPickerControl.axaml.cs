// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Controls
{
	public partial class DirectoryPickerControl : UserControl
	{
		public static readonly StyledProperty<string> TitleProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, string>(nameof(Title));

		public static readonly StyledProperty<string> WatermarkProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, string>(nameof(Watermark));

		public static readonly StyledProperty<string> DescriptionProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, string>(nameof(Description));

		public static readonly StyledProperty<DirectoryPickerType> PickerTypeProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, DirectoryPickerType>(nameof(PickerType));

		public static readonly StyledProperty<bool> EnableFileWatcherProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, bool>(nameof(EnableFileWatcher), defaultValue: true);

		public string Title
		{
			get => GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		public string Watermark
		{
			get => GetValue(WatermarkProperty);
			set => SetValue(WatermarkProperty, value);
		}

		public string Description
		{
			get => GetValue(DescriptionProperty);
			set => SetValue(DescriptionProperty, value);
		}

		public DirectoryPickerType PickerType
		{
			get => GetValue(PickerTypeProperty);
			set => SetValue(PickerTypeProperty, value);
		}

		public bool EnableFileWatcher
		{
			get => GetValue(EnableFileWatcherProperty);
			set => SetValue(EnableFileWatcherProperty, value);
		}

		public event EventHandler<DirectoryChangedEventArgs> DirectoryChanged;

		private TextBlock _titleTextBlock;
		private TextBlock _descriptionTextBlock;
		private TextBlock _currentPathDisplay;
		private TextBox _pathInput;
		private ComboBox _pathSuggestions;
		private bool _suppressEvents;
		private bool _suppressSelection;

		private string _pendingPath;
		private CancellationTokenSource _pathSuggestCts;
		private FileSystemWatcher _fileSystemWatcher;

		public DirectoryPickerControl()
		{
			InitializeComponent();
			DataContext = this;
			Logger.LogVerbose($"DirectoryPickerControl[Type={PickerType}] constructed");
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);
			CleanupFileSystemWatcher();
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock");
			_descriptionTextBlock = this.FindControl<TextBlock>("DescriptionTextBlock");
			_currentPathDisplay = this.FindControl<TextBlock>("CurrentPathDisplay");
			_pathInput = this.FindControl<TextBox>("PathInput");
			_pathSuggestions = this.FindControl<ComboBox>("PathSuggestions");

			Logger.LogVerbose("DirectoryPickerControl.OnApplyTemplate");
			UpdateTitle();
			UpdateDescription();
			UpdateWatermark();
			InitializePathSuggestions();

			if (string.IsNullOrEmpty(_pendingPath))
				return;
			Logger.LogVerbose($"DirectoryPickerControl applying pending path in OnApplyTemplate: '{_pendingPath}'");
			SetCurrentPath(_pendingPath);
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			Logger.LogVerbose("DirectoryPickerControl.OnAttachedToVisualTree");

			InitializePathSuggestions();
			if (string.IsNullOrEmpty(_pendingPath))
				return;
			Logger.LogVerbose($"DirectoryPickerControl applying pending path in OnAttachedToVisualTree: '{_pendingPath}'");
			SetCurrentPath(_pendingPath);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if (change.Property == TitleProperty)
				UpdateTitle();
			else if (change.Property == DescriptionProperty)
				UpdateDescription();
			else if (change.Property == WatermarkProperty)
				UpdateWatermark();
			else if (change.Property == PickerTypeProperty)
				InitializePathSuggestions();
		}

		private void UpdateTitle()
		{
			if (_titleTextBlock != null)
				_titleTextBlock.Text = Title ?? string.Empty;
		}

		private void UpdateDescription()
		{
			if (_descriptionTextBlock != null)
			{
				_descriptionTextBlock.Text = Description ?? string.Empty;
				_descriptionTextBlock.IsVisible = !string.IsNullOrEmpty(Description);
			}
		}

		private void UpdateWatermark()
		{
			if (_pathInput != null)
				_pathInput.Watermark = Watermark ?? string.Empty;
		}

		public void InitializePathSuggestions()
		{
			if (_pathSuggestions is null)
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] InitializePathSuggestions: _pathSuggestions is null");
				return;
			}

			try
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] InitializePathSuggestions: Starting initialization");

				if (PickerType == DirectoryPickerType.ModDirectory)
				{
					InitializeModDirectoryPaths();
					_pathSuggestions.PlaceholderText = "Select from recent mod directories...";
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] InitializePathSuggestions: ModDirectory initialized, ItemsSource count: {(_pathSuggestions.ItemsSource as IEnumerable<object>)?.Count() ?? 0}");
				}
				else if (PickerType == DirectoryPickerType.KotorDirectory)
				{
					InitializeKotorDirectoryPaths();
					_pathSuggestions.PlaceholderText = "Select from detected KOTOR installations...";
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] InitializePathSuggestions: KotorDirectory initialized, ItemsSource count: {(_pathSuggestions.ItemsSource as IEnumerable<object>)?.Count() ?? 0}");
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}
		}

		private void InitializeModDirectoryPaths()
		{
			try
			{
				List<string> recentPaths = LoadRecentModPaths();
				Logger.LogVerbose($"DirectoryPickerControl(ModDirectory) LoadRecentModPaths returned: {recentPaths?.Count ?? 0} paths");
				if (recentPaths != null && recentPaths.Count > 0)
				{
					Logger.LogVerbose($"DirectoryPickerControl(ModDirectory) Recent paths: {string.Join(", ", recentPaths)}");
				}

				_pathSuggestions.ItemsSource = recentPaths;
				Logger.LogVerbose($"DirectoryPickerControl(ModDirectory) Set ItemsSource, current ItemsSource count: {(_pathSuggestions.ItemsSource as IEnumerable<object>)?.Count() ?? 0}");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}
		}

		private void InitializeKotorDirectoryPaths()
		{
			try
			{
				List<string> defaultPaths = GetDefaultPathsForGame();
				Logger.LogVerbose($"DirectoryPickerControl(KotorDirectory) GetDefaultPathsForGame returned: {defaultPaths?.Count ?? 0} paths");

				var newPaths = defaultPaths.Where(Directory.Exists).ToList();
				Logger.LogVerbose($"DirectoryPickerControl(KotorDirectory) Found {newPaths.Count} existing paths");
				if (newPaths.Count > 0)
				{
					Logger.LogVerbose($"DirectoryPickerControl(KotorDirectory) Existing paths: {string.Join(", ", newPaths)}");
				}

				List<string> currentItems = (_pathSuggestions?.ItemsSource as IEnumerable<string>)?.ToList()
										 ?? new List<string>();

				foreach (string path in newPaths)
				{
					if (!currentItems.Exists(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase)))
						currentItems.Add(path);
				}

				string currentSelection = _pathSuggestions?.SelectedItem?.ToString();

				if (!(_pathSuggestions is null))
				{
					_pathSuggestions.ItemsSource = currentItems;
				}
				Logger.LogVerbose($"DirectoryPickerControl(KotorDirectory) Set ItemsSource with {currentItems.Count} items");

				if (!string.IsNullOrEmpty(currentSelection))
				{
					_pathSuggestions.SelectedItem = currentSelection;
				}

				Logger.LogVerbose($"DirectoryPickerControl(KotorDirectory) added {newPaths.Count} default paths, total items: {currentItems.Count}");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}
		}

		private static List<string> GetDefaultPathsForGame()
		{
			var paths = new List<string>();
			OSPlatform osType = UtilityHelper.GetOperatingSystem();
			Logger.LogVerbose($"DirectoryPickerControl.GetDefaultPathsForGame OS={osType}");

			if (osType == OSPlatform.Windows)
			{

				paths.AddRange(new[]
				{
					@"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
					@"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
					@"C:\Program Files\Steam\steamapps\common\swkotor",
					@"C:\Program Files\Steam\steamapps\common\Knights of the Old Republic II",
				});

				paths.AddRange(new[]
				{
					@"C:\Program Files (x86)\GOG Galaxy\Games\Star Wars - KotOR",
					@"C:\Program Files (x86)\GOG Galaxy\Games\Star Wars - KotOR2",
					@"C:\Program Files\GOG Galaxy\Games\Star Wars - KotOR",
					@"C:\Program Files\GOG Galaxy\Games\Star Wars - KotOR2",
				});

				paths.AddRange(new[]
				{
					@"C:\Program Files (x86)\Origin Games\Star Wars Knights of the Old Republic",
					@"C:\Program Files (x86)\Origin Games\Star Wars Knights of the Old Republic II - The Sith Lords",
					@"C:\Program Files\Origin Games\Star Wars Knights of the Old Republic",
					@"C:\Program Files\Origin Games\Star Wars Knights of the Old Republic II - The Sith Lords",
				});
			}
			else if (osType == OSPlatform.OSX)
			{
				string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				paths.AddRange(new[]
				{
					Path.Combine(homeDir, "Library/Application Support/Steam/steamapps/common/swkotor"),
					Path.Combine(homeDir, "Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II"),
					"/Applications/Knights of the Old Republic.app",
					"/Applications/Knights of the Old Republic II.app",
				});
			}
			else if (osType == OSPlatform.Linux)
			{
				string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				paths.AddRange(new[]
				{
					Path.Combine(homeDir, ".steam/steam/steamapps/common/swkotor"),
					Path.Combine(homeDir, ".steam/steam/steamapps/common/Knights of the Old Republic II"),
					Path.Combine(homeDir, ".local/share/Steam/steamapps/common/swkotor"),
					Path.Combine(homeDir, ".local/share/Steam/steamapps/common/Knights of the Old Republic II"),
				});
			}

			Logger.LogVerbose($"DirectoryPickerControl.GetDefaultPathsForGame returning {paths.Count} paths");
			return paths;
		}

		private static List<string> LoadRecentModPaths()
		{
			try
			{
				string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KOTORModSync");
				string recentFile = Path.Combine(appDataPath, "recent_mod_paths.txt");

				if (File.Exists(recentFile))
				{
					return File.ReadAllLines(recentFile).Where(Directory.Exists).Take(10).ToList();
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}

			return new List<string>();
		}

		private void SaveRecentModPath(string path)
		{
			if (PickerType != DirectoryPickerType.ModDirectory) return;

			try
			{
				string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KOTORModSync");
				_ = Directory.CreateDirectory(appDataPath);
				string recentFile = Path.Combine(appDataPath, "recent_mod_paths.txt");

				List<string> recentPaths = LoadRecentModPaths();
				_ = recentPaths.Remove(path);
				recentPaths.Insert(0, path);
				recentPaths = recentPaths.Take(10).ToList();

				File.WriteAllLines(recentFile, recentPaths);
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}
		}

		public void SetCurrentPath(string path, bool fireEvent = false)
		{
			try
			{
				_pendingPath = path;
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] SetCurrentPath -> '{path}' (fireEvent={fireEvent})");
				_suppressEvents = true;

				if (_currentPathDisplay != null)
				{
					_currentPathDisplay.Text = string.IsNullOrEmpty(path) ? "Not set" : path;
				}

				if (_pathInput != null)
				{
					_pathInput.Text = path ?? string.Empty;
				}

				// If we have a valid directory and the UI is ready, add to suggestions
				if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && _pathInput != null)
				{
					if (PickerType == DirectoryPickerType.ModDirectory)
					{
						SaveRecentModPath(path);
					}
					else if (PickerType == DirectoryPickerType.KotorDirectory)
					{
						AddPathToSuggestions(path);
					}
				}

				if (fireEvent && !string.IsNullOrEmpty(path) && Directory.Exists(path))
				{
					DirectoryChanged?.Invoke(this, new DirectoryChangedEventArgs(path, PickerType));
				}

				_suppressEvents = false;
			}
			catch (Exception ex)
			{
				_suppressEvents = false;
				Logger.LogException(ex);
			}
		}

		public string GetCurrentPath()
		{
			try
			{
				string value = _pathInput?.Text ?? _pendingPath ?? string.Empty;
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] GetCurrentPath -> '{value}'");
				return value;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
				return string.Empty;
			}
		}

		/// <summary>
		/// Gets the current path, ensuring it's properly synchronized with the settings system.
		/// </summary>
		public string GetCurrentPathForSettings()
		{
			try
			{
				string path = GetCurrentPath();

				// If we have a valid path, save it to recent paths for mod directory
				if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && PickerType == DirectoryPickerType.ModDirectory)
				{
					SaveRecentModPath(path);
				}

				return path;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
				return string.Empty;
			}
		}

		/// <summary>
		/// Sets the current path from settings, ensuring proper synchronization.
		/// </summary>
		public void SetCurrentPathFromSettings(string path)
		{
			try
			{
				if (string.IsNullOrEmpty(path))
					return;

				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] SetCurrentPathFromSettings -> '{path}'");

				// Set the path
				SetCurrentPath(path, fireEvent: false);

				// If it's a valid directory, add to suggestions
				if (Directory.Exists(path))
				{
					if (PickerType == DirectoryPickerType.ModDirectory)
					{
						SaveRecentModPath(path);
					}
					else if (PickerType == DirectoryPickerType.KotorDirectory)
					{
						AddPathToSuggestions(path);
					}

					// Refresh the ComboBox to show the updated suggestions
					RefreshSuggestionsSafely();
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private async void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var topLevel = TopLevel.GetTopLevel(this);
				if (topLevel?.StorageProvider is null) return;

				var options = new FolderPickerOpenOptions
				{
					Title = PickerType == DirectoryPickerType.ModDirectory ? "Select Mod Directory" : "Select KOTOR Installation Directory",
					AllowMultiple = false,
				};

				string currentPath = GetCurrentPath();
				if (!string.IsNullOrWhiteSpace(currentPath))
				{
					string startPath = FindClosestExistingParent(currentPath);
					if (!string.IsNullOrEmpty(startPath))

					{
						try
						{
							IStorageFolder startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(startPath)).ConfigureAwait(true);
							if (startFolder != null)
							{
								options.SuggestedStartLocation = startFolder;
								await Logger.LogVerboseAsync($"DirectoryPickerControl[{PickerType}] Browse starting at '{startPath}'").ConfigureAwait(false);
							}
						}
						catch (Exception ex)
						{
							await Logger.LogVerboseAsync($"DirectoryPickerControl[{PickerType}] Could not set start location: {ex.Message}").ConfigureAwait(false);
						}
					}
				}

				IReadOnlyList<IStorageFolder> result = await topLevel.StorageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
				if (result.Count > 0)
				{
					string selectedPath = result[0].Path.LocalPath;
					await Logger.LogVerboseAsync($"DirectoryPickerControl[{PickerType}] Browse selected '{selectedPath}'").ConfigureAwait(false);
					ApplyPath(selectedPath);
				}
				else
				{
					await Logger.LogVerboseAsync($"DirectoryPickerControl[{PickerType}] Browse cancelled/no result").ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
			}
		}

		private string FindClosestExistingParent(string path)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(path))
					return null;

				string expandedPath = PathUtilities.ExpandPath(path);
				if (string.IsNullOrWhiteSpace(expandedPath))
					return null;

				if (Directory.Exists(expandedPath))
					return expandedPath;

				string current = expandedPath;
				while (!string.IsNullOrEmpty(current))
				{
					try
					{
						string parent = Path.GetDirectoryName(current);
						if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
						{

							break;
						}

						if (Directory.Exists(parent))
						{
							Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Found existing parent: '{parent}' for path '{path}'");
							return parent;
						}

						current = parent;
					}
					catch (Exception ex)
					{
						Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Error checking parent of '{current}': {ex.Message}");
						break;
					}
				}

				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] No existing parent found for '{path}'");
				return null;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
				return null;
			}
		}

		private void OnPathInputKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && _pathInput != null && !string.IsNullOrWhiteSpace(_pathInput.Text))
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Enter pressed with '{_pathInput.Text}'");
				ApplyPath(_pathInput.Text.Trim());
				e.Handled = true;
			}
		}

		private void PathInput_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_suppressEvents || _pathInput is null || _pathSuggestions is null) return;

			Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] TextChanged: '{_pathInput.Text}'");
			UpdatePathSuggestions(_pathInput, _pathSuggestions, ref _pathSuggestCts, PickerType);

			SetupFileSystemWatcher(_pathInput.Text);
		}

		private void PathInput_LostFocus(object sender, RoutedEventArgs e)
		{
			if (_suppressEvents || _pathInput is null) return;

			if (!string.IsNullOrWhiteSpace(_pathInput.Text))
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] PathInput lost focus, applying '{_pathInput.Text}'");
				ApplyPath(_pathInput.Text.Trim());
			}
		}

		private void PathSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_suppressEvents || _suppressSelection || _pathSuggestions?.SelectedItem is null) return;

			string selectedPath = _pathSuggestions.SelectedItem?.ToString();
			if (string.IsNullOrEmpty(selectedPath)) return;

			try
			{
				_suppressSelection = true;

				Dispatcher.UIThread.Post(() =>
				{
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Suggestion selected '{selectedPath}'");
					ApplyPath(selectedPath);
				}, DispatcherPriority.Background);
			}
			finally
			{
				_suppressSelection = false;
			}
		}

		private void ApplyPath(string path)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

				_suppressEvents = true;
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] ApplyPath '{path}'");

				SetCurrentPath(path, fireEvent: false);

				if (PickerType == DirectoryPickerType.ModDirectory)
				{
					SaveRecentModPath(path);

					RefreshSuggestionsSafely();
				}
				else if (PickerType == DirectoryPickerType.KotorDirectory)
				{

					AddPathToSuggestions(path);
				}

				DirectoryChanged?.Invoke(this, new DirectoryChangedEventArgs(path, PickerType));

				_suppressEvents = false;
			}
			catch (Exception ex)
			{
				_suppressEvents = false;
				Logger.LogException(ex);
			}
		}

		public void RefreshSuggestionsSafely()
		{
			if (_pathSuggestions is null) return;

			try
			{
				_suppressEvents = true;
				_suppressSelection = true;

				if (PickerType == DirectoryPickerType.ModDirectory)
				{
					List<string> recent = LoadRecentModPaths();
					_pathSuggestions.ItemsSource = recent;
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Refreshed suggestions: {recent?.Count ?? 0}");
				}
				else if (PickerType == DirectoryPickerType.KotorDirectory)
				{
					var defaults = GetDefaultPathsForGame().Where(Directory.Exists).ToList();
					_pathSuggestions.ItemsSource = defaults;
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Refreshed defaults that exist: {defaults.Count}");
				}

			}
			finally
			{
				_suppressSelection = false;
				_suppressEvents = false;
			}
		}

		private void AddPathToSuggestions(string path)
		{
			if (_pathSuggestions is null || string.IsNullOrEmpty(path)) return;

			try
			{
				var currentItems = (_pathSuggestions.ItemsSource as IEnumerable<string>)?.ToList() ?? new List<string>();

				if (!currentItems.Contains(path, StringComparer.Ordinal))
				{
					currentItems.Insert(0, path);

					if (currentItems.Count > 20)
						currentItems = currentItems.Take(20).ToList();

					_pathSuggestions.ItemsSource = currentItems;
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Added path to suggestions: '{path}'");
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private static void UpdatePathSuggestions(TextBox input, ComboBox combo, ref CancellationTokenSource cts, DirectoryPickerType pickerType)
		{
			try
			{
				cts?.Cancel();
				cts = new CancellationTokenSource();
				CancellationToken token = cts.Token;
				string typed = input.Text ?? string.Empty;
				_ = Task.Run<IList<string>>(() =>
				{
					var results = new List<string>();
					string expanded = PathUtilities.ExpandPath(typed);
					if (string.IsNullOrWhiteSpace(expanded))
					{

						if (pickerType == DirectoryPickerType.ModDirectory)
							return PathUtilities.GetDefaultPathsForMods().ToList();
						if (pickerType == DirectoryPickerType.KotorDirectory)
							return PathUtilities.GetDefaultPathsForGame().ToList();
						return results;
					}

					string normalized = expanded;

					bool endsWithSep = normalized.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);

					bool isRootDir = false;
					if (UtilityHelper.GetOperatingSystem() == OSPlatform.Windows)
					{

						if (normalized.Length >= 2 && normalized[1] == ':' &&
							 (normalized.Length == 2 || (normalized.Length == 3 && normalized[2] == Path.DirectorySeparatorChar)))
						{
							isRootDir = true;
							normalized = normalized.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
						}
					}
					else
					{

						if (string.Equals(normalized, "/", StringComparison.Ordinal) || normalized.EndsWith(value: ":/", StringComparison.Ordinal))
							isRootDir = true;
					}

					string baseDir;
					string fragment;

					if (isRootDir)
					{
						baseDir = normalized;
						fragment = string.Empty;
					}
					else
					{
						baseDir = endsWithSep ? normalized : Path.GetDirectoryName(normalized);
						if (string.IsNullOrEmpty(baseDir))
							baseDir = Path.GetPathRoot(normalized);
						fragment = endsWithSep ? string.Empty : Path.GetFileName(normalized);
					}

					if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
					{
						try
						{
							IEnumerable<string> dirs = Directory.EnumerateDirectories(baseDir);

							if (string.IsNullOrEmpty(fragment))
							{

								results.AddRange(dirs);
							}
							else
							{

								results.AddRange(dirs.Where(d =>
									Path.GetFileName(d).IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0));
							}
						}
						catch (Exception ex)
						{
							Logger.LogVerbose($"Failed to enumerate directories in {baseDir}: {ex.Message}");
						}
					}

					return results;
				}, token).ContinueWith(t =>
				{
					if (token.IsCancellationRequested || t.IsFaulted) return;
					Dispatcher.UIThread.Post(() =>
					{

						if (combo.ItemsSource is IEnumerable<string> existingItems)
						{
							var newResults = t.Result.ToList();

							foreach (string item in existingItems)
							{
								if (!newResults.Contains(item, StringComparer.Ordinal) && Directory.Exists(item))
									newResults.Add(item);
							}

							var current = (combo.ItemsSource as IEnumerable<string>)?.ToList();
							if (current is null || !current.SequenceEqual(newResults, StringComparer.Ordinal))
								combo.ItemsSource = newResults;
						}
						else
						{
							var current = (combo.ItemsSource as IEnumerable<string>)?.ToList();
							if (current is null || !current.SequenceEqual(t.Result, StringComparer.Ordinal))
								combo.ItemsSource = t.Result;
						}

						if (t.Result.Count > 0 && input.IsKeyboardFocusWithin)
							combo.IsDropDownOpen = true;
					});
				}, token);
			}
			catch (Exception ex)
			{
				Logger.LogVerbose($"Error updating path suggestions: {ex.Message}");
			}
		}

		private void SetupFileSystemWatcher(string path)
		{
			// Check if file watcher is enabled via settings
			if (!EnableFileWatcher)
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] File watcher is disabled via settings");
				return;
			}

			try
			{

				CleanupFileSystemWatcher();

				if (string.IsNullOrWhiteSpace(path))
					return;

				string expandedPath = PathUtilities.ExpandPath(path);
				if (string.IsNullOrWhiteSpace(expandedPath))
					return;

				string watchPath = null;
				if (Directory.Exists(expandedPath))
				{
					watchPath = expandedPath;
				}
				else
				{

					string parent = Path.GetDirectoryName(expandedPath);
					if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
					{
						watchPath = parent;
					}
				}

				if (string.IsNullOrEmpty(watchPath))
					return;

				_fileSystemWatcher = new FileSystemWatcher(watchPath)
				{
					NotifyFilter = NotifyFilters.DirectoryName,
					IncludeSubdirectories = false,
					InternalBufferSize = 65536, // 64KB (maximum recommended size)
				};

				_fileSystemWatcher.Created += OnFileSystemChanged;
				_fileSystemWatcher.Deleted += OnFileSystemChanged;
				_fileSystemWatcher.Renamed += OnFileSystemChanged;

				_fileSystemWatcher.EnableRaisingEvents = true;
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Watching directory: '{watchPath}'");
			}
			catch (Exception ex)
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Could not setup file system watcher: {ex.Message}");
				CleanupFileSystemWatcher();
			}
		}

		private void CleanupFileSystemWatcher()
		{
			if (_fileSystemWatcher != null)
			{
				_fileSystemWatcher.EnableRaisingEvents = false;
				_fileSystemWatcher.Created -= OnFileSystemChanged;
				_fileSystemWatcher.Deleted -= OnFileSystemChanged;
				_fileSystemWatcher.Renamed -= OnFileSystemChanged;
				_fileSystemWatcher.Dispose();
				_fileSystemWatcher = null;
			}
		}

		private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
		{
			try
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] File system changed: {e.ChangeType} - {e.FullPath}");

				Dispatcher.UIThread.Post(() =>
				{
					if (_pathInput != null && _pathSuggestions != null)
					{
						UpdatePathSuggestions(_pathInput, _pathSuggestions, ref _pathSuggestCts, PickerType);
					}
				}, DispatcherPriority.Background);
			}
			catch (Exception ex)
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Error handling file system change: {ex.Message}");
			}
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
	public enum DirectoryPickerType
	{
		ModDirectory,
		KotorDirectory,
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
	public class DirectoryChangedEventArgs : EventArgs
	{
		public string Path { get; }
		public DirectoryPickerType PickerType { get; }

		public DirectoryChangedEventArgs(string path, DirectoryPickerType pickerType)
		{
			Path = path;
			PickerType = pickerType;
		}
	}
}