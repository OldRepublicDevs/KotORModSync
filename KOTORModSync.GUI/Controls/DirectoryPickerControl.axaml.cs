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
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Controls
{
	public partial class DirectoryPickerControl : UserControl
	{
		public static readonly StyledProperty<string> TitleProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, string>(nameof(Title));

		public static readonly StyledProperty<string> WatermarkProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, string>(nameof(Watermark));

		public static readonly StyledProperty<DirectoryPickerType> PickerTypeProperty =
			AvaloniaProperty.Register<DirectoryPickerControl, DirectoryPickerType>(nameof(PickerType));

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

		public DirectoryPickerType PickerType
		{
			get => GetValue(PickerTypeProperty);
			set => SetValue(PickerTypeProperty, value);
		}

		// Events
		public event EventHandler<DirectoryChangedEventArgs> DirectoryChanged;

		private TextBlock _titleTextBlock;
		private TextBlock _currentPathDisplay;
		private TextBox _pathInput;
		private ComboBox _pathSuggestions;
		private bool _suppressEvents;
		private bool _suppressSelection;
		// Persist current value even if visual children are not yet available
		private string _pendingPath;
		private CancellationTokenSource _pathSuggestCts;

		public DirectoryPickerControl()
		{
			InitializeComponent();
			DataContext = this;
			Logger.LogVerbose($"DirectoryPickerControl[Type={PickerType}] constructed");
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			_titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock");
			_currentPathDisplay = this.FindControl<TextBlock>("CurrentPathDisplay");
			_pathInput = this.FindControl<TextBox>("PathInput");
			_pathSuggestions = this.FindControl<ComboBox>("PathSuggestions");

			Logger.LogVerbose("DirectoryPickerControl.OnApplyTemplate");
			UpdateTitle();
			UpdateWatermark();
			InitializePathSuggestions();
			// Re-apply pending path if set prior to template application
			if ( string.IsNullOrEmpty(_pendingPath) )
				return;
			Logger.LogVerbose($"DirectoryPickerControl applying pending path in OnApplyTemplate: '{_pendingPath}'");
			SetCurrentPath(_pendingPath);
		}

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			Logger.LogVerbose("DirectoryPickerControl.OnAttachedToVisualTree");
			// Ensure suggestions reflect environment when attached
			InitializePathSuggestions();
			if ( string.IsNullOrEmpty(_pendingPath) )
				return;
			Logger.LogVerbose($"DirectoryPickerControl applying pending path in OnAttachedToVisualTree: '{_pendingPath}'");
			SetCurrentPath(_pendingPath);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if ( change.Property == TitleProperty )
				UpdateTitle();
			else if ( change.Property == WatermarkProperty )
				UpdateWatermark();
			else if ( change.Property == PickerTypeProperty )
				InitializePathSuggestions();
		}

		private void UpdateTitle()
		{
			if ( _titleTextBlock != null )
				_titleTextBlock.Text = Title ?? string.Empty;
		}

		private void UpdateWatermark()
		{
			if ( _pathInput != null )
				_pathInput.Watermark = Watermark ?? string.Empty;
		}

		private void InitializePathSuggestions()
		{
			if ( _pathSuggestions == null ) return;

			try
			{
				if ( PickerType == DirectoryPickerType.ModDirectory )
				{
					InitializeModDirectoryPaths();
					_pathSuggestions.PlaceholderText = "Select from recent mod directories...";
				}
				else if ( PickerType == DirectoryPickerType.KotorDirectory )
				{
					InitializeKotorDirectoryPaths();
					_pathSuggestions.PlaceholderText = "Select from detected KOTOR installations...";
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void InitializeModDirectoryPaths()
		{
			try
			{
				List<string> recentPaths = LoadRecentModPaths();
				_pathSuggestions.ItemsSource = recentPaths;
				Logger.LogVerbose($"DirectoryPickerControl(ModDirectory) initialized suggestions: {recentPaths?.Count ?? 0} entries");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void InitializeKotorDirectoryPaths()
		{
			try
			{
				List<string> defaultPaths = GetDefaultPathsForGame();
				_pathSuggestions.ItemsSource = defaultPaths.Where(Directory.Exists).ToList();
				Logger.LogVerbose($"DirectoryPickerControl(KotorDirectory) initialized defaults: {defaultPaths.Count} total, {(_pathSuggestions.ItemsSource as System.Collections.ICollection)?.Count ?? 0} exist");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private List<string> GetDefaultPathsForGame()
		{
			var paths = new List<string>();
			OSPlatform osType = Utility.GetOperatingSystem();
			Logger.LogVerbose($"DirectoryPickerControl.GetDefaultPathsForGame OS={osType}");

			if ( osType == OSPlatform.Windows )
			{
				// Steam paths
				paths.AddRange(new[]
				{
					@"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
					@"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
					@"C:\Program Files\Steam\steamapps\common\swkotor",
					@"C:\Program Files\Steam\steamapps\common\Knights of the Old Republic II"
				});

				// GOG paths
				paths.AddRange(new[]
				{
					@"C:\Program Files (x86)\GOG Galaxy\Games\Star Wars - KotOR",
					@"C:\Program Files (x86)\GOG Galaxy\Games\Star Wars - KotOR2",
					@"C:\Program Files\GOG Galaxy\Games\Star Wars - KotOR",
					@"C:\Program Files\GOG Galaxy\Games\Star Wars - KotOR2"
				});

				// Origin paths
				paths.AddRange(new[]
				{
					@"C:\Program Files (x86)\Origin Games\Star Wars Knights of the Old Republic",
					@"C:\Program Files (x86)\Origin Games\Star Wars Knights of the Old Republic II - The Sith Lords",
					@"C:\Program Files\Origin Games\Star Wars Knights of the Old Republic",
					@"C:\Program Files\Origin Games\Star Wars Knights of the Old Republic II - The Sith Lords"
				});
			}
			else if ( osType == OSPlatform.OSX )
			{
				string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				paths.AddRange(new[]
				{
					Path.Combine(homeDir, "Library/Application Support/Steam/steamapps/common/swkotor"),
					Path.Combine(homeDir, "Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II"),
					"/Applications/Knights of the Old Republic.app",
					"/Applications/Knights of the Old Republic II.app"
				});
			}
			else if ( osType == OSPlatform.Linux )
			{
				string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				paths.AddRange(new[]
				{
					Path.Combine(homeDir, ".steam/steam/steamapps/common/swkotor"),
					Path.Combine(homeDir, ".steam/steam/steamapps/common/Knights of the Old Republic II"),
					Path.Combine(homeDir, ".local/share/Steam/steamapps/common/swkotor"),
					Path.Combine(homeDir, ".local/share/Steam/steamapps/common/Knights of the Old Republic II")
				});
			}

			Logger.LogVerbose($"DirectoryPickerControl.GetDefaultPathsForGame returning {paths.Count} paths");
			return paths;
		}

		private List<string> LoadRecentModPaths()
		{
			try
			{
				string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KOTORModSync");
				string recentFile = Path.Combine(appDataPath, "recent_mod_paths.txt");

				if ( File.Exists(recentFile) )
				{
					return File.ReadAllLines(recentFile).Where(Directory.Exists).Take(10).ToList();
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}

			return new List<string>();
		}

		private void SaveRecentModPath(string path)
		{
			if ( PickerType != DirectoryPickerType.ModDirectory ) return;

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
			catch ( Exception ex )
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

				if ( _currentPathDisplay != null )
				{
					_currentPathDisplay.Text = string.IsNullOrEmpty(path) ? "Not set" : path;
				}

				if ( _pathInput != null )
				{
					_pathInput.Text = path ?? string.Empty;
				}

				// Only fire the event if explicitly requested (for manual user actions)
				if ( fireEvent && !string.IsNullOrEmpty(path) && Directory.Exists(path) )
				{
					DirectoryChanged?.Invoke(this, new DirectoryChangedEventArgs(path, PickerType));
				}

				_suppressEvents = false;
			}
			catch ( Exception ex )
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
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return string.Empty;
			}
		}

		private async void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var topLevel = TopLevel.GetTopLevel(this);
				if ( topLevel?.StorageProvider == null ) return;

				var options = new FolderPickerOpenOptions
				{
					Title = PickerType == DirectoryPickerType.ModDirectory ? "Select Mod Directory" : "Select KOTOR Installation Directory",
					AllowMultiple = false
				};

				IReadOnlyList<IStorageFolder> result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
				if ( result.Count > 0 )
				{
					string selectedPath = result[0].Path.LocalPath;
					await Logger.LogVerboseAsync($"DirectoryPickerControl[{PickerType}] Browse selected '{selectedPath}'");
					ApplyPath(selectedPath);
				}
				else
				{
					await Logger.LogVerboseAsync($"DirectoryPickerControl[{PickerType}] Browse cancelled/no result");
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private void OnPathInputKeyDown(object sender, KeyEventArgs e)
		{
			if ( e.Key == Key.Enter && _pathInput != null && !string.IsNullOrWhiteSpace(_pathInput.Text) )
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Enter pressed with '{_pathInput.Text}'");
				ApplyPath(_pathInput.Text.Trim());
				e.Handled = true;
			}
		}

		private void PathInput_TextChanged(object sender, TextChangedEventArgs e)
		{
			if ( _suppressEvents || _pathInput == null || _pathSuggestions == null ) return;

			Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] TextChanged: '{_pathInput.Text}'");
			UpdatePathSuggestions(_pathInput, _pathSuggestions, ref _pathSuggestCts, PickerType);
		}

		private void PathInput_LostFocus(object sender, RoutedEventArgs e)
		{
			if ( _suppressEvents || _pathInput == null ) return;

			if ( !string.IsNullOrWhiteSpace(_pathInput.Text) )
			{
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] PathInput lost focus, applying '{_pathInput.Text}'");
				ApplyPath(_pathInput.Text.Trim());
			}
		}

		private void PathSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if ( _suppressEvents || _suppressSelection || _pathSuggestions?.SelectedItem == null ) return;

			string selectedPath = _pathSuggestions.SelectedItem?.ToString();
			if ( string.IsNullOrEmpty(selectedPath) ) return;

			try
			{
				_suppressSelection = true;
				// Defer to end of event cycle to avoid re-entrancy with ItemsSource updates
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
				if ( string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ) return;

				_suppressEvents = true;
				Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] ApplyPath '{path}'");

				// Update displays (without firing event yet)
				SetCurrentPath(path, fireEvent: false);

				// Save to recent if mod directory
				if ( PickerType == DirectoryPickerType.ModDirectory )
				{
					SaveRecentModPath(path);
					// Refresh suggestions list safely without causing selection recursion
					RefreshSuggestionsSafely();
				}
				else if ( PickerType == DirectoryPickerType.KotorDirectory )
				{
					// For KOTOR directory, add the manually typed path to the suggestions list
					AddPathToSuggestions(path);
				}

				// Fire event
				DirectoryChanged?.Invoke(this, new DirectoryChangedEventArgs(path, PickerType));

				_suppressEvents = false;
			}
			catch ( Exception ex )
			{
				_suppressEvents = false;
				Logger.LogException(ex);
			}
		}

		private void RefreshSuggestionsSafely()
		{
			if ( _pathSuggestions == null ) return;

			try
			{
				_suppressEvents = true;
				_suppressSelection = true;

				if ( PickerType == DirectoryPickerType.ModDirectory )
				{
					List<string> recent = LoadRecentModPaths();
					_pathSuggestions.ItemsSource = recent;
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Refreshed suggestions: {recent?.Count ?? 0}");
				}
				else if ( PickerType == DirectoryPickerType.KotorDirectory )
				{
					var defaults = GetDefaultPathsForGame().Where(Directory.Exists).ToList();
					_pathSuggestions.ItemsSource = defaults;
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Refreshed defaults that exist: {defaults.Count}");
				}

				// Do not force SelectedItem to avoid triggering SelectionChanged repeatedly
			}
			finally
			{
				_suppressSelection = false;
				_suppressEvents = false;
			}
		}

		private void AddPathToSuggestions(string path)
		{
			if ( _pathSuggestions == null || string.IsNullOrEmpty(path) ) return;

			try
			{
				var currentItems = (_pathSuggestions.ItemsSource as IEnumerable<string>)?.ToList() ?? new List<string>();

				// Add the path if it's not already in the list
				if ( !currentItems.Contains(path) )
				{
					currentItems.Insert(0, path); // Add to the beginning
												  // Keep only the first 20 items to avoid cluttering
					if ( currentItems.Count > 20 )
						currentItems = currentItems.Take(20).ToList();

					_pathSuggestions.ItemsSource = currentItems;
					Logger.LogVerbose($"DirectoryPickerControl[{PickerType}] Added path to suggestions: '{path}'");
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

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
					if ( string.IsNullOrWhiteSpace(expanded) )
					{
						// If empty, return default paths based on the picker type
						if ( pickerType == DirectoryPickerType.ModDirectory )
							return PathUtilities.GetDefaultPathsForMods().ToList();
						if ( pickerType == DirectoryPickerType.KotorDirectory )
							return PathUtilities.GetDefaultPathsForGame().ToList();
						return results;
					}

					string normalized = expanded;
					bool endsWithSep = normalized.EndsWith(Path.DirectorySeparatorChar.ToString());

					// Handle root directory case (like C:\)
					bool isRootDir = false;
					if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
					{
						// Check if this is a drive root (e.g., "C:\")
						if ( normalized.Length >= 2 && normalized[1] == ':' &&
							 (normalized.Length == 2 || (normalized.Length == 3 && normalized[2] == Path.DirectorySeparatorChar)) )
						{
							isRootDir = true;
							normalized = normalized.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
						}
					}
					else
					{
						// For Unix systems, check if this is the root directory
						if ( normalized == "/" || normalized.EndsWith(value: ":/") )
							isRootDir = true;
					}

					string baseDir;
					string fragment;

					if ( isRootDir )
					{
						baseDir = normalized;
						fragment = string.Empty;
					}
					else
					{
						baseDir = endsWithSep ? normalized : Path.GetDirectoryName(normalized);
						if ( string.IsNullOrEmpty(baseDir) )
							baseDir = Path.GetPathRoot(normalized);
						fragment = endsWithSep ? string.Empty : Path.GetFileName(normalized);
					}

					if ( !string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir) )
					{
						IEnumerable<string> dirs = Enumerable.Empty<string>();
						try
						{
							dirs = Directory.EnumerateDirectories(baseDir);
						}
						catch ( Exception ex )
						{
							Logger.LogVerbose($"Failed to enumerate directories in {baseDir}: {ex.Message}");
						}

						if ( string.IsNullOrEmpty(fragment) )
						{
							// If no fragment, add all directories
							results.AddRange(dirs);
						}
						else
						{
							// Filter directories by fragment
							results.AddRange(dirs.Where(d =>
								Path.GetFileName(d).IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0));
						}
					}

					return results;
				}, token).ContinueWith(t =>
				{
					if ( token.IsCancellationRequested || t.IsFaulted ) return;
					Dispatcher.UIThread.Post(() =>
					{
						// For KOTOR directory picker, we want to preserve existing items (like default paths) and add new ones
						if ( combo.ItemsSource is IEnumerable<string> existingItems )
						{
							var newResults = t.Result.ToList();

							// Add any existing items that aren't already in the results
							foreach ( string item in existingItems )
							{
								if ( !newResults.Contains(item) && Directory.Exists(item) )
									newResults.Add(item);
							}

							var current = (combo.ItemsSource as IEnumerable<string>)?.ToList();
							if ( current is null || !current.SequenceEqual(newResults) )
								combo.ItemsSource = newResults;
						}
						else
						{
							var current = (combo.ItemsSource as IEnumerable<string>)?.ToList();
							if ( current is null || !current.SequenceEqual(t.Result) )
								combo.ItemsSource = t.Result;
						}

						// Only auto-open when the corresponding TextBox has focus
						if ( t.Result.Count > 0 && input.IsKeyboardFocusWithin )
							combo.IsDropDownOpen = true;
					});
				}, token);
			}
			catch ( Exception ex )
			{
				Logger.LogVerbose($"Error updating path suggestions: {ex.Message}");
			}
		}
	}

	public enum DirectoryPickerType
	{
		ModDirectory,
		KotorDirectory
	}

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
