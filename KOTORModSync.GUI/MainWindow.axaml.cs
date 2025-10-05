// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vector = Avalonia.Vector;
using JetBrains.Annotations;
using KOTORModSync.CallbackDialogs;
using KOTORModSync.Controls;
using KOTORModSync.Converters;
using KOTORModSync.Dialogs;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Utility;
using ReactiveUI;
using SharpCompress.Archives;
using static KOTORModSync.Core.Services.ModManagementService;
using Component = KOTORModSync.Core.Component;
using NotNullAttribute = JetBrains.Annotations.NotNullAttribute;


namespace KOTORModSync
{
	[SuppressMessage(category: "ReSharper", checkId: "UnusedParameter.Local")]
	public sealed partial class MainWindow : Window
	{
		public static readonly DirectProperty<MainWindow, Component> CurrentComponentProperty =
			AvaloniaProperty.RegisterDirect<MainWindow, Component>(
				nameof(CurrentComponent),
				o => o?.CurrentComponent,
				(o, v) => o.CurrentComponent = v
			);

		[CanBeNull] private Component _currentComponent;
		private bool _ignoreWindowMoveWhenClickingComboBox;
		private bool _initialize = true;
		private bool _installRunning;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;
		private OutputWindow _outputWindow;
		private bool _progressWindowClosed;
		private string _searchText;
		private CancellationTokenSource _modSuggestCts;
		private CancellationTokenSource _installSuggestCts;
		private bool _suppressPathEvents;
		private bool _suppressComboEvents;
		private bool _rootSelectionState;
		private bool _editorMode;
		private bool _isClosingProgressWindow;
		private CrossPlatformFileWatcher _modDirectoryWatcher;
		private Component _draggedComponent;
		private string _lastLoadedFileName;

		// InstallationService provides centralized validation and installation logic
		private readonly InstallationService _installationService;

		// ModManagementService provides comprehensive mod management functionality
		private readonly ModManagementService _modManagementService;

		// Public property for binding
		public ModManagementService ModManagementService => _modManagementService;

		// UI control properties
		private ListBox ModListBox => this.FindControl<ListBox>("ModListBoxElement");
		public bool IsClosingMainWindow;

		public bool RootSelectionState
		{
			get => _rootSelectionState;
			set
			{
				if ( _rootSelectionState == value ) return;
				_ = SetAndRaise(RootSelectionStateProperty, ref _rootSelectionState, value);
			}
		}

		public bool EditorMode
		{
			get => _editorMode;
			set
			{
				if ( _editorMode == value ) return;
				_ = SetAndRaise(EditorModeProperty, ref _editorMode, value);
				UpdateMenuVisibility();
				RefreshModListItems();
				BuildGlobalActionsMenu();
			}
		}

		// Direct Avalonia properties for proper change notification/binding
		public static readonly DirectProperty<MainWindow, bool> EditorModeProperty =
			AvaloniaProperty.RegisterDirect<MainWindow, bool>(
				nameof(EditorMode),
				o => o._editorMode,
				(o, v) => o.EditorMode = v
			);

		public static readonly DirectProperty<MainWindow, bool> RootSelectionStateProperty =
			AvaloniaProperty.RegisterDirect<MainWindow, bool>(
				nameof(RootSelectionState),
				o => o._rootSelectionState,
				(o, v) => o.RootSelectionState = v
			);

		public MainWindow()
		{
			try
			{
				InitializeComponent();
				DataContext = this;
				InitializeControls();
				InitializeTopMenu();
				UpdateMenuVisibility();
				InitializeDirectoryPickers();
				InitializeModListBox();

				// Initialize the logger
				Logger.Initialize();

				// Initialize the installation service
				_installationService = new InstallationService();

				// Initialize the mod management service
				_modManagementService = new ModManagementService(MainConfigInstance);
				_modManagementService.ModOperationCompleted += OnModOperationCompleted;
				_modManagementService.ModValidationCompleted += OnModValidationCompleted;

				// Create callback objects for use with KOTORModSync.Core
				CallbackObjects.SetCallbackObjects(
					new ConfirmationDialogCallback(this),
					new OptionsDialogCallback(this),
					new InformationDialogCallback(this)
				);

				PropertyChanged += SearchText_PropertyChanged;

				// Fixes an annoying problem on Windows where selecting in the console window causes the app to hang.
				// Selection now is only possible through ctrl + m or right click -> mark, which still causes the same hang but is less accidental at least.
				if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
				{
					ConsoleConfig.DisableQuickEdit();
					ConsoleConfig.DisableConsoleCloseButton();
				}

				AddHandler(DragDrop.DropEvent, Drop);
				AddHandler(DragDrop.DragOverEvent, DragOver);

				// Initialize file system watcher for mod directory
				InitializeModDirectoryWatcher();

				// Initialize global actions menu
				BuildGlobalActionsMenu();
			}
			catch ( Exception e )
			{
				Logger.LogException(e, customMessage: "A fatal error has occurred loading the main window");
				throw;
			}
		}

		private static void UpdatePathDisplays(TextBlock modPathDisplay, TextBlock kotorPathDisplay)
		{
			if ( modPathDisplay != null )
				modPathDisplay.Text = MainConfig.SourcePath?.FullName ?? "Not set";

			if ( kotorPathDisplay != null )
				kotorPathDisplay.Text = MainConfig.DestinationPath?.FullName ?? "Not set";
		}

		private void UpdatePathDisplays()
		{
			TextBlock modPathDisplay = this.FindControl<TextBlock>(name: "CurrentModPathDisplay");
			TextBlock kotorPathDisplay = this.FindControl<TextBlock>(name: "CurrentKotorPathDisplay");
			UpdatePathDisplays(modPathDisplay, kotorPathDisplay);
		}

		[UsedImplicitly]
		private void ModPathInput_LostFocus(object sender, RoutedEventArgs e)
		{
			if ( sender is TextBox tb )
			{
				if ( _suppressPathEvents ) return;
				bool applied = TryApplySourcePath(tb.Text);
				if ( applied )
				{
					UpdatePathDisplays();
					UpdateStepProgress(); // Update step progress when directory is set
				}
				UpdatePathSuggestions(tb, this.FindControl<ComboBox>(name: "ModPathSuggestions"), ref _modSuggestCts);
			}
		}

		[UsedImplicitly]
		private void InstallPathInput_LostFocus(object sender, RoutedEventArgs e)
		{
			if ( sender is TextBox tb )
			{
				if ( _suppressPathEvents ) return;
				bool applied = TryApplyInstallPath(tb.Text);
				if ( applied )
				{
					UpdatePathDisplays();
					UpdateStepProgress(); // Update step progress when directory is set
				}
				UpdatePathSuggestions(tb, this.FindControl<ComboBox>(name: "InstallPathSuggestions"), ref _installSuggestCts);
			}
		}

		[UsedImplicitly]
		private void ModPathSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if ( _suppressComboEvents ) return;
			if ( sender is ComboBox comboBox && comboBox.SelectedItem is string path )
			{
				_suppressPathEvents = true;
				_suppressComboEvents = true;
				TextBox modInput = this.FindControl<TextBox>(name: "ModPathInput");
				if ( modInput != null )
				{
					modInput.Text = path;
					if ( TryApplySourcePath(path) )
					{
						UpdatePathDisplays();
						UpdateStepProgress(); // Update step progress when directory is set
					}
					// Do not refresh ItemsSource here to avoid recursive selection updates
				}
				_suppressPathEvents = false;
				_suppressComboEvents = false;
			}
		}

		[UsedImplicitly]
		private void InstallPathSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if ( _suppressComboEvents )
			    return;
			if ( sender is ComboBox comboBox && comboBox.SelectedItem is string path )
			{
				_suppressPathEvents = true;
				_suppressComboEvents = true;
				TextBox installInput = this.FindControl<TextBox>(name: "InstallPathInput");
				if ( installInput != null )
				{
					installInput.Text = path;
					if ( TryApplyInstallPath(path) )
					{
						UpdatePathDisplays();
						UpdateStepProgress(); // Update step progress when directory is set
					}
					// Do not refresh ItemsSource here to avoid recursive selection updates
				}
				_suppressPathEvents = false;
				_suppressComboEvents = false;
			}
		}





		private bool TryApplySourcePath(string text)
		{
			try
			{
				string p = PathUtilities.ExpandPath(text);
				if ( string.IsNullOrWhiteSpace(p) || !Directory.Exists(p) ) return false;
				MainConfigInstance.sourcePath = new DirectoryInfo(p);
				AddToRecentMods(text);

				// Update file watcher when source path changes
				SetupModDirectoryWatcher(p);

				return true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return false;
			}
		}

		private bool TryApplyInstallPath(string text)
		{
			try
			{
				string p = PathUtilities.ExpandPath(text);
				if ( string.IsNullOrWhiteSpace(p) || !Directory.Exists(p) ) return false;
				MainConfigInstance.destinationPath = new DirectoryInfo(p);
				return true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return false;
			}
		}

		private static void UpdatePathSuggestions(TextBox input, ComboBox combo, ref CancellationTokenSource cts)
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
						// If empty, return default paths based on the input type
						if ( input.Name == "ModPathInput" )
							return PathUtilities.GetDefaultPathsForMods().ToList();
						if ( input.Name == "InstallPathInput" )
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
						// If this is the mod directory combo, preserve recent paths
						if ( combo.Name == "ModPathSuggestions" && combo.ItemsSource is IEnumerable<string> existingItems )
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

		[UsedImplicitly]
		private void OnPathInputKeyDown(object sender, KeyEventArgs e)
		{
			try
			{
				if ( e.Key != Key.Enter ) return;
				if ( !(sender is TextBox tb) ) return;
				if ( _suppressPathEvents ) return;
				string name = tb.Name ?? string.Empty;
				bool pathSet = false;
				if ( name == "ModPathInput" )
					pathSet = TryApplySourcePath(tb.Text);
				else if ( name == "InstallPathInput" )
					pathSet = TryApplyInstallPath(tb.Text);

				if ( pathSet )
					UpdateStepProgress(); // Update step progress when directory is set via Enter key
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		// simplistic MRU using user settings file under AppData
		private async void AddToRecentMods(string path)
		{
			try
			{
				if ( string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) )
					return;

				string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				string folder = Path.Combine(appData, path2: "KOTORModSync");
				string file = Path.Combine(folder, path2: "recent_mod_dirs.txt");
				_ = Directory.CreateDirectory(folder);

				// Load existing entries
				List<string> existing = await PathManagementService.LoadRecentModDirectoriesAsync(file);

				// Add the new path using the service
				PathManagementService.AddToRecentDirectories(path, existing, maxCount: 20);

				// Save to file
				await PathManagementService.SaveRecentModDirectoriesAsync(existing, file);

				// Intentionally do NOT mutate ComboBox.ItemsSource here

				// Update the path display
				UpdatePathDisplays();
			}
			catch ( Exception ex )
			{
				await Logger.LogVerboseAsync($"Error adding to recent mods: {ex.Message}");
			}
		}

		[UsedImplicitly]
		private async void BrowseModDir_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string[] result = await ShowFileDialog(isFolderDialog: true, windowName: "Select your mod directory");
				if ( !(result?.Length > 0) )
					return;
				TextBox modInput = this.FindControl<TextBox>(name: "ModPathInput");
				if ( modInput == null )
					return;
				modInput.Text = result[0];
				if ( !TryApplySourcePath(result[0]) )
					return;
				UpdatePathDisplays();
				UpdateStepProgress(); // Update step progress when directory is set

				// Update suggestions
				ComboBox modCombo = this.FindControl<ComboBox>(name: "ModPathSuggestions");
				if ( modCombo != null )
					UpdatePathSuggestions(modInput, modCombo, ref _modSuggestCts);
			}
			catch ( Exception exc )
			{
				await Logger.LogExceptionAsync(exc);
			}
		}

		public static List<Component> ComponentsList => MainConfig.AllComponents;

		[CanBeNull]
		public string SearchText
		{
			get => _searchText;
			set
			{
				if ( _searchText == value )
					return; // prevent recursion problems

				_searchText = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
			}
		}

		public MainConfig MainConfigInstance = new MainConfig();
		private DownloadProgressWindow _currentDownloadWindow;

		[CanBeNull]
		public Component CurrentComponent
		{
			get => _currentComponent;
			set => SetAndRaise(CurrentComponentProperty, ref _currentComponent, value);
		}

		private bool IgnoreInternalTabChange { get; set; }

		private void InitializeTopMenu()
		{
			var menu = new Menu();

			var fileMenu = new MenuItem { Header = "File" };
			var fileItems = new List<MenuItem>
			{
				new MenuItem
				{
					Header = "Open TOML",
					Command = ReactiveCommand.Create( () => LoadInstallFile_Click(new object(), new RoutedEventArgs()) ),
				},
				new MenuItem
				{
					Header = "Close TOML",
					Command = ReactiveCommand.Create( () => CloseTOMLFile_Click(new object(), new RoutedEventArgs()) ),
				},
				new MenuItem
				{
					Header = "Save TOML",
					Command = ReactiveCommand.Create( () => SaveModFileAs_Click(new object(), new RoutedEventArgs()) ),
					IsVisible = EditorMode,
				},
				new MenuItem
				{
					Header = "Exit",
					Command = ReactiveCommand.Create( () => CloseButton_Click(new object(), new RoutedEventArgs()) ),
				},
			};
			fileMenu.ItemsSource = fileItems;

			// Tools menu
			var toolsMenu = new MenuItem { Header = "Tools" };

			var toolItems = new List<MenuItem>
			{
				new MenuItem
				{
					Header = "Create modbuild documentation.",
					Command = ReactiveCommand.Create( () => DocsButton_Click(new object(), new RoutedEventArgs()) ),
					IsVisible = EditorMode,
				},
				new MenuItem
				{
					Header = "Parse text instructions into TOML using Regex",
					Command = ReactiveCommand.Create( () => LoadMarkdown_Click(new object(), new RoutedEventArgs()) ),
					IsVisible = EditorMode,
				},
				new MenuItem
				{
					Header = "Fix iOS case sensitivity.",
					Command = ReactiveCommand.Create( () => FixIosCaseSensitivityClick(new object(), new RoutedEventArgs()) ),
				},
				new MenuItem
				{
					Header = "Fix file/folder permissions.",
					Command = ReactiveCommand.Create( () => FixPathPermissionsClick(new object(), new RoutedEventArgs()) ),
				},
				new MenuItem
				{
					Header = "Settings",
					Command = ReactiveCommand.Create( () => OpenSettings_Click(new object(), new RoutedEventArgs()) ),
				},
				new MenuItem
				{
					Header = "Open Output Window",
					Command = ReactiveCommand.Create( () => OpenOutputWindow_Click(new object(), new RoutedEventArgs()) ),
				},
			};
			ToolTip.SetTip(
				toolItems[0],
				value:
				"Create documentation for all instructions in the loaded setup. Useful if you need human-readable documentation of your TOML."
			);
			ToolTip.SetTip(
				toolItems[1],
				value:
				"Attempts to create and load a toml instructions file from various text sources using custom regex."
			);
			ToolTip.SetTip(
				toolItems[2],
				value:
				"Lowercase all files/folders recursively at the given path. Necessary for iOS installs."
			);
			ToolTip.SetTip(
				toolItems[3],
				value:
				"Fixes various file/folder permissions. On Unix, this will also find case-insensitive duplicate file/folder names."
			);
			if ( Utility.GetOperatingSystem() != OSPlatform.Windows )
			{
				var filePermFixTool = new MenuItem
				{
					Header = "Fix file and folder permissions",
					Command = ReactiveCommand.Create(() => ResolveDuplicateFilesAndFolders(new object(), new RoutedEventArgs())),
				};
				ToolTip.SetTip(
					filePermFixTool,
					"(Linux/Mac only) This will acquire a list of any case-insensitive duplicates in the mod directory or"
					+ " the kotor directory, including subfolders, and resolve them."
				);
				toolItems.Add(filePermFixTool);
			}
			toolsMenu.ItemsSource = toolItems;

			// Help menu
			var helpMenu = new MenuItem { Header = "Help" };
			var deadlystreamMenu = new MenuItem
			{
				Header = "DeadlyStream",
				ItemsSource = new[]
				{
					new MenuItem
					{
						Header = "Discord",
						Command = ReactiveCommand.Create(() => UrlUtilities.OpenUrl("https://discord.gg/nDkHXfc36s")),
					},
					new MenuItem
					{
						Header = "Website",
						Command = ReactiveCommand.Create(() => UrlUtilities.OpenUrl("https://deadlystream.com")),
					},
				},
			};

			var neocitiesMenu = new MenuItem
			{
				Header = "KOTOR Community Portal",
				ItemsSource = new[]
				{
					new MenuItem
					{
						Header = "Discord",
						Command = ReactiveCommand.Create(() => UrlUtilities.OpenUrl("https://discord.com/invite/kotor")),
					},
					new MenuItem
					{
						Header = "Website",
						Command = ReactiveCommand.Create(() => UrlUtilities.OpenUrl("https://kotor.neocities.org")),
					},
				},
			};

			var pcgamingwikiMenu = new MenuItem
			{
				Header = "PCGamingWiki",
				ItemsSource = new[]
				{
					new MenuItem
					{
						Header = "KOTOR 1",
						Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl( "https://www.pcgamingwiki.com/wiki/Star_Wars:_Knights_of_the_Old_Republic" ) ),
					},
					new MenuItem
					{
						Header = "KOTOR 2: TSL",
						Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl( "https://www.pcgamingwiki.com/wiki/Star_Wars:_Knights_of_the_Old_Republic_II_-_The_Sith_Lords" ) ),
					},
				},
			};

			helpMenu.ItemsSource = new[] { deadlystreamMenu, neocitiesMenu, pcgamingwikiMenu };


			var engineRewritesMenu = new MenuItem
			{
				Header = "Open-Source Odyssey/Aurora Engines",
				ItemsSource = new[]
				{
					new MenuItem
					{
						Header = "KotOR.js",
						Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/KobaltBlu/KotOR.js") ),
					},
					new MenuItem
					{
						Header = "NorthernLights",
						Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/lachjames/NorthernLights") ),
					},
					new MenuItem
					{
						Header = "reone",
						Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/seedhartha/reone") ),
					},
				},
			};

			var otherProjectsMenu = new MenuItem
			{
				Header = "Other Projects",
				ItemsSource = new[]
				{
					new MenuItem
					{
						Header = "PyKotor Library",
						Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/NickHugi/PyKotor") ),
						ItemsSource = new []
						{
							new MenuItem
							{
								Header = "HoloPatcher",
								ItemsSource = new []
								{
									new MenuItem
									{
										Header = "DeadlyStream",
										Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://deadlystream.com/files/file/2243-holopatcher") ),
									},
									new MenuItem
									{
										Header = "GitHub",
										Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/NickHugi/PyKotor") ),
									},
								},
							},
							new MenuItem
							{
								Header = "Holocron Toolset",
								ItemsSource = new []
								{
									new MenuItem
									{
										Header = "GitHub",
										Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/NickHugi/PyKotor/blob/master/Tools/HolocronToolset") ),
									},
									new MenuItem
									{
										Header = "DeadlyStream",
										Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://deadlystream.com/files/file/1982-holocron-toolset") ),
									},
									new MenuItem
									{
										Header = "Discord",
										Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://discord.gg/hfAqtkVEzQ") ),
									},
								},
							},
							new MenuItem
							{
								Header = "Auto-Translate / Font Creator",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://deadlystream.com/files/file/2375-kotor-autotranslate-tool") ),
							},
							new MenuItem
							{
								Header = "KotorDiff",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://deadlystream.com/files/file/2364-kotordiff") ),
							},
						},
					},
					new MenuItem
					{
						Header = "LIP Composer / reone toolkit",
						ItemsSource = new []
						{
							new MenuItem
							{
								Header = "DeadlyStream",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://deadlystream.com/files/file/1862-reone-toolkit") ),
							},
							new MenuItem
							{
								Header = "GitHub",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/seedhartha/reone/wiki/Tooling") ),
							},
						},
					},
					engineRewritesMenu,
				},
			};

			var aboutMenu = new MenuItem
			{
				Header = "About",
				ItemsSource = new[]
				{
					new MenuItem
					{
						Header = "The ModSync Project",
						ItemsSource = new []
						{
							new MenuItem
							{
								Header = "DeadlyStream",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://deadlystream.com/files/file/2317-kotormodsync/") ),
							},
							new MenuItem
							{
								Header = "GitHub",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/th3w1zard1/KOTORModSync") ),
							},
						},
					},
					new MenuItem
					{
						Header = "HoloPatcher",
						ItemsSource = new []
						{
							new MenuItem
							{
								Header = "DeadlyStream",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://deadlystream.com/files/file/2243-holopatcher") ),
							},
							new MenuItem
							{
								Header = "GitHub",
								Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl("https://github.com/NickHugi/PyKotor") ),
							},
						},
					},
				},
			};

			var moreMenu = new MenuItem
			{
				Header = "More",
				ItemsSource = new[]
				{
					otherProjectsMenu,
					new MenuItem
					{
						Header = "Modding Tools",
						Command = ReactiveCommand.Create( () => UrlUtilities.OpenUrl(url: "https://deadlystream.github.io/ds-kotor-modding-wiki/en/#!pages/tools_overview.md") ),
					},
				},
			};

			menu.ItemsSource = new[] { fileMenu, toolsMenu, helpMenu, aboutMenu, moreMenu };
			Menu topMenu = this.FindControl<Menu>(name: "TopMenu");
			if ( topMenu is null )
				return;
			topMenu.ItemsSource = menu.Items;
		}

		private void UpdateMenuVisibility()
		{
			Menu topMenu = this.FindControl<Menu>(name: "TopMenu");
			if ( topMenu is null )
				return;

			// Update File menu items
			if ( topMenu.Items[0] is MenuItem fileMenu && fileMenu.Items is IList fileItems )
			{
				// Close TOML (index 1)
				if ( fileItems.Count > 1 && fileItems[1] is MenuItem closeTomlItem )
					closeTomlItem.IsVisible = EditorMode;

				// Save TOML (index 2)
				if ( fileItems.Count > 2 && fileItems[2] is MenuItem saveTomlItem )
					saveTomlItem.IsVisible = EditorMode;
			}

			// Update Tools menu items
			if ( topMenu.Items[1] is MenuItem toolsMenu && toolsMenu.Items is IList toolItems )
			{
				// Create modbuild documentation (index 0)
				if ( toolItems.Count > 0 && toolItems[0] is MenuItem docsItem )
					docsItem.IsVisible = EditorMode;

				// Parse text instructions (index 1)
				if ( toolItems.Count > 1 && toolItems[1] is MenuItem parseItem )
					parseItem.IsVisible = EditorMode;
			}
		}

		/// <summary>
		/// Refreshes all mod list items to update their visibility and context menus based on current editor mode.
		/// </summary>
		private void RefreshModListItems()
		{
			try
			{
				if ( ModListBox == null )
					return;

				// Force all ModListItem controls to refresh their context menus and visibility
				foreach ( object item in ModListBox.Items )
				{
					if ( !(item is Component component) )
						continue;

					// Find the container for this item
					if ( ModListBox.ContainerFromItem(item) is ListBoxItem container )
					{
						// Find the ModListItem control
						ModListItem modListItem = container.GetVisualDescendants().OfType<ModListItem>().FirstOrDefault();
						if ( modListItem != null )
						{
							// Trigger context menu rebuild
							modListItem.ContextMenu = BuildContextMenuForComponent(component);

							// Update editor mode visibility for child elements
							if ( modListItem.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexBlock )
								indexBlock.IsVisible = EditorMode;

							if ( modListItem.FindControl<TextBlock>("DragHandle") is TextBlock dragHandle )
								dragHandle.IsVisible = EditorMode;

							// Update index if in editor mode
							if ( EditorMode )
							{
								int index = MainConfigInstance?.allComponents.IndexOf(component) ?? -1;
								if ( index >= 0 && modListItem.FindControl<TextBlock>("IndexTextBlock") is TextBlock indexTextBlock )
									indexTextBlock.Text = $"#{index + 1}";
							}
						}
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private static void DragOver(object sender, DragEventArgs e)
		{
			e.DragEffects = e.Data.Contains(DataFormats.Files)
				? DragDropEffects.Copy
				: DragDropEffects.None;
			e.Handled = true;
		}

		private async void Drop(object sender, DragEventArgs e)
		{
			try
			{
				if ( !e.Data.Contains(DataFormats.Files) )
					return;

				// Attempt to get the data as a string array (file paths)
				if ( !(e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> items) )
					return;

				// Processing the first file
				IStorageItem storageItem = items.FirstOrDefault();
				string filePath = storageItem?.TryGetLocalPath();
				if ( string.IsNullOrEmpty(filePath) )
					return;

				string fileExt = Path.GetExtension(filePath);
				switch ( storageItem )
				{
					// Check if the storageItem is a file
					case IStorageFile _ when fileExt.Equals(value: ".toml", StringComparison.OrdinalIgnoreCase)
											 || fileExt.Equals(value: ".tml", StringComparison.OrdinalIgnoreCase):

						{
							// Use the unified TOML loading method
							_ = await LoadTomlFile(filePath, fileType: "TOML file");
							break;
						}
					case IStorageFile _:
						(IArchive archive, FileStream archiveStream) = ArchiveHelper.OpenArchive(filePath);
						if ( archive is null || archiveStream is null )
							return;

						string exePath = ArchiveHelper.AnalyzeArchiveForExe(archiveStream, archive);
						await Logger.LogVerboseAsync(exePath);

						break;
					case IStorageFolder _:
						// Handle folder logic
						break;
					default:
						throw new NullReferenceException(filePath);
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		protected override void OnClosing(WindowClosingEventArgs e)
		{
			base.OnClosing(e);
			if ( IsClosingMainWindow )
				return;

			// Always cancel the initial closing event
			e.Cancel = true;

			// Run the asynchronous dialog handling in a separate task
			HandleClosingAsync();
		}

		private async void HandleClosingAsync()
		{
			try
			{
				// If result is not true, do nothing and the app remains open
				bool? result = await ConfirmationDialog.ShowConfirmationDialog(this, confirmText: "Really close?");
				if ( result != true )
					return;

				// Clean up file watcher
				_modDirectoryWatcher?.Dispose();

				// Start a new app closing event.
				IsClosingMainWindow = true;
				await Dispatcher.UIThread.InvokeAsync(Close);
			}
			catch ( Exception e )
			{
				await Logger.LogExceptionAsync(e);
			}
		}

		public new event EventHandler<PropertyChangedEventArgs> PropertyChanged;

		public void InitializeControls()
		{
			if ( MainGrid.ColumnDefinitions == null || MainGrid.ColumnDefinitions.Count != 2 )
				throw new NullReferenceException(message: "MainGrid incorrectly defined, expected 3 columns.");

			// set title and version
			Title = $"KOTORModSync v{MainConfig.CurrentVersion}";
			TitleTextBlock.Text = Title;

			ColumnDefinition componentListColumn = MainGrid.ColumnDefinitions[0]
												   ?? throw new NullReferenceException(message: "Column 0 of MainGrid (component list column) not defined.");

			// Column 0
			componentListColumn.Width = new GridLength(300);

			// Column 1
			RawEditTextBox.LostFocus +=
				RawEditTextBox_LostFocus; // Prevents RawEditTextBox from being cleared when clicking elsewhere (?)
			RawEditTextBox.DataContext = new ObservableCollection<string>();

			GuiEditGrid.DataContext = CurrentComponent;

			// Update initial step progress
			UpdateStepProgress();

			_ = Logger.LogVerboseAsync("Setting up window move event handlers...");
			// Attach event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
			FindComboBoxesInWindow(this);
		}

		private void SearchText_PropertyChanged([NotNull] object sender, [NotNull] PropertyChangedEventArgs e)
		{
			try
			{
				if ( e.PropertyName != nameof(SearchText) )
					return;

				// Filter the ListBox items based on search text
				if ( !string.IsNullOrWhiteSpace(SearchText) )
				{
					FilterModList(SearchText);
				}
				else
				{
					// Show all items when search is cleared
					RefreshModList();
				}
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}

		private void InitializeModListBox()
		{
			try
			{
				if ( ModListBox == null )
					return;

				// Set up selection changed event
				ModListBox.SelectionChanged += ModListBox_SelectionChanged;

				// Set up drag and drop
				SetupDragAndDrop();

				// Setup SelectAllCheckBox event
				if ( this.FindControl<CheckBox>("SelectAllCheckBox") is CheckBox selectAllCheckBox )
					selectAllCheckBox.IsCheckedChanged += SelectAllCheckBox_IsCheckedChanged;

				// Set up keyboard shortcuts
				SetupKeyboardShortcuts();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void SetupKeyboardShortcuts()
		{
			if ( ModListBox == null )
				return;

			ModListBox.KeyDown += (sender, e) =>
			{
				try
				{
					if ( !EditorMode )
						return;

					if ( !(ModListBox.SelectedItem is Component component) )
						return;

					// Ctrl+Up - Move selected mod up
					if ( e.Key == Key.Up && e.KeyModifiers == KeyModifiers.Control )
					{
						MoveComponentListItem(component, -1);
						e.Handled = true;
					}
					// Ctrl+Down - Move selected mod down
					else if ( e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Control )
					{
						MoveComponentListItem(component, 1);
						e.Handled = true;
					}
					// Delete - Remove selected mod
					else if ( e.Key == Key.Delete )
					{
						CurrentComponent = component;
						_ = DeleteModWithConfirmation(component);
						e.Handled = true;
					}
					// Space - Toggle selection
					else if ( e.Key == Key.Space )
					{
						component.IsSelected = !component.IsSelected;
						UpdateModCounts();
						e.Handled = true;
					}
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex);
				}
			};
		}

		[UsedImplicitly]
		private async Task DeleteModWithConfirmation(Component component)
		{
			try
			{
				if ( component == null )
				{
					Logger.Log(message: "No component provided for deletion.");
					return;
				}

				bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					$"Are you sure you want to delete the mod '{component.Name}'? This action cannot be undone.",
					yesButtonText: "Delete",
					noButtonText: "Cancel"
				);

				if ( confirm == true )
				{
					CurrentComponent = component;
					RemoveComponentButton_Click(null, null);
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		// Build context menu for a specific component (individual mod operations only)
		public ContextMenu BuildContextMenuForComponent(Component component)
		{
			var contextMenu = new ContextMenu();

			if ( component == null )
				return contextMenu;

			// Basic operations - available for all modes
			_ = contextMenu.Items.Add(new MenuItem
			{
				Header = component.IsSelected ? "☑️ Deselect Mod" : "☐ Select Mod",
				Command = ReactiveCommand.Create(() =>
				{
					component.IsSelected = !component.IsSelected;
					UpdateModCounts();
					if ( component.IsSelected )
						ComponentCheckboxChecked(component, new HashSet<Component>());
					else
						ComponentCheckboxUnchecked(component, new HashSet<Component>());
				})
			});

			// Editor mode items
			if ( EditorMode )
			{
				_ = contextMenu.Items.Add(new Separator());

				// Movement operations
				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "⬆️ Move Up",
					Command = ReactiveCommand.Create(() => MoveComponentListItem(component, -1)),
					InputGesture = new KeyGesture(Key.Up, KeyModifiers.Control)
				});

				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "⬇️ Move Down",
					Command = ReactiveCommand.Create(() => MoveComponentListItem(component, 1)),
					InputGesture = new KeyGesture(Key.Down, KeyModifiers.Control)
				});

				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "📊 Move to Top",
					Command = ReactiveCommand.Create(() => _modManagementService.MoveModToPosition(component, 0))
				});

				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "📊 Move to Bottom",
					Command = ReactiveCommand.Create(() => _modManagementService.MoveModToPosition(component, MainConfig.AllComponents.Count - 1))
				});

				_ = contextMenu.Items.Add(new Separator());

				// CRUD operations
				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "🗑️ Delete Mod",
					Command = ReactiveCommand.CreateFromTask(async () =>
					{
						CurrentComponent = component;
						bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
							this,
							$"Are you sure you want to delete the mod '{component.Name}'? This action cannot be undone.",
							yesButtonText: "Delete",
							noButtonText: "Cancel"
						);
						if ( confirm == true )
						{
							RemoveComponentButton_Click(null, null);
						}
					})
				});

				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "🔄 Duplicate Mod",
					Command = ReactiveCommand.Create(() =>
					{
						Component duplicated = _modManagementService.DuplicateMod(component);
						if ( duplicated != null )
						{
							SetCurrentComponent(duplicated);
							SetTabInternal(TabControl, GuiEditTabItem);
						}
					})
				});

				_ = contextMenu.Items.Add(new Separator());

				// Editing operations
				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "📝 Edit Instructions",
					Command = ReactiveCommand.Create(() =>
					{
						SetCurrentComponent(component);
						SetTabInternal(TabControl, GuiEditTabItem);
					})
				});

				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "📄 Edit Raw TOML",
					Command = ReactiveCommand.Create(() =>
					{
						SetCurrentComponent(component);
						SetTabInternal(TabControl, RawEditTabItem);
					})
				});

				_ = contextMenu.Items.Add(new Separator());

				// Testing and validation
				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "🧪 Test Install This Mod",
					Command = ReactiveCommand.Create(() =>
					{
						CurrentComponent = component;
						InstallModSingle_Click(null, null);
					})
				});

				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "🔍 Validate Mod Files",
					Command = ReactiveCommand.Create(() =>
					{
						ModValidationResult validation = _modManagementService.ValidateMod(component);
						if ( !validation.IsValid )
						{
							_ = InformationDialog.ShowInformationDialog(this,
								$"Validation failed for '{component.Name}':\n\n" +
								string.Join("\n", validation.Errors.Take(5)));
						}
						else
						{
							_ = InformationDialog.ShowInformationDialog(this,
								$"✅ '{component.Name}' validation passed!");
						}
					})
				});
			}

			return contextMenu;
		}

		// Build global actions menu (for DropDownButton and Mod List context menu)
		private void BuildGlobalActionsMenu()
		{
			// Build DropDownButton menu
			DropDownButton globalActionsButton = this.FindControl<DropDownButton>("GlobalActionsButton");
			if ( globalActionsButton?.Flyout is MenuFlyout globalActionsFlyout )
			{
				BuildMenuFlyoutItems(globalActionsFlyout);
			}

			// Build Mod List context menu - find the ScrollViewer that contains the ListBox
			ListBox modListBox = this.FindControl<ListBox>("ModListBoxElement");
			var scrollViewer = modListBox?.Parent as ScrollViewer;
			if ( scrollViewer?.ContextMenu is ContextMenu modListContextMenu )
			{
				BuildContextMenuItems(modListContextMenu);
			}
		}

		// Helper method to build menu items for MenuFlyout (DropDownButton)
		private void BuildMenuFlyoutItems(MenuFlyout menu)
		{
			menu.Items.Clear();

			// Refresh and Validate All Mods - available in both modes, at top
			_ = menu.Items.Add(new MenuItem
			{
				Header = "🔄 Refresh List",
				Command = ReactiveCommand.Create(() => RefreshComponents_Click(null, null)),
				InputGesture = new KeyGesture(Key.F5)
			});

			_ = menu.Items.Add(new MenuItem
			{
				Header = "🔄 Validate All Mods",
				Command = ReactiveCommand.Create(async () =>
				{
					Dictionary<Component, ModValidationResult> results = _modManagementService.ValidateAllMods();
					int errorCount = results.Count(r => !r.Value.IsValid);
					int warningCount = results.Sum(r => r.Value.Warnings.Count);

					await InformationDialog.ShowInformationDialog(this,
						"Validation complete!\n\n" +
						$"Errors: {errorCount}\n" +
						$"Warnings: {warningCount}\n\n" +
						$"Valid mods: {results.Count(r => r.Value.IsValid)}/{results.Count}");
				})
			});

			_ = menu.Items.Add(new Separator());

			if ( EditorMode )
			{
				// Global operations
				_ = menu.Items.Add(new MenuItem
				{
					Header = "➕ Add New Mod",
					Command = ReactiveCommand.Create(() =>
					{
						Component newMod = _modManagementService.CreateMod();
						if ( newMod != null )
						{
							SetCurrentComponent(newMod);
							SetTabInternal(TabControl, GuiEditTabItem);
						}
					})
				});

				_ = menu.Items.Add(new Separator());

				// Selection operations (useful for ordered lists)
				_ = menu.Items.Add(new MenuItem
				{
					Header = "🔎 Select by Name",
					Command = ReactiveCommand.Create(() => _modManagementService.SortMods())
				});

				_ = menu.Items.Add(new MenuItem
				{
					Header = "🔎 Select by Category",
					Command = ReactiveCommand.Create(() => _modManagementService.SortMods(ModSortCriteria.Category))
				});

				_ = menu.Items.Add(new MenuItem
				{
					Header = "🔎 Select by Tier",
					Command = ReactiveCommand.Create(() => _modManagementService.SortMods(ModSortCriteria.Tier))
				});

				_ = menu.Items.Add(new Separator());

				// Tools and utilities
				_ = menu.Items.Add(new MenuItem
				{
					Header = "⚙️ Mod Management Tools",
					Command = ReactiveCommand.Create(async () => await ShowModManagementDialog())
				});

				_ = menu.Items.Add(new MenuItem
				{
					Header = "📈 Mod Statistics",
					Command = ReactiveCommand.Create(async () =>
					{
						ModStatistics stats = _modManagementService.GetModStatistics();
						string statsText = "📊 Mod Statistics\n\n" +
										   $"Total Mods: {stats.TotalMods}\n" +
										   $"Selected: {stats.SelectedMods}\n" +
										   $"Downloaded: {stats.DownloadedMods}\n\n" +
										   $"Categories:\n{string.Join("\n", stats.Categories.Select(c => $"  • {c.Key}: {c.Value}"))}\n\n" +
										   $"Tiers:\n{string.Join("\n", stats.Tiers.Select(t => $"  • {t.Key}: {t.Value}"))}\n\n" +
										   $"Average Instructions/Mod: {stats.AverageInstructionsPerMod:F1}\n" +
										   $"Average Options/Mod: {stats.AverageOptionsPerMod:F1}";

						await InformationDialog.ShowInformationDialog(this, statsText);
					})
				});

				_ = menu.Items.Add(new Separator());

				// File operations at the bottom
				_ = menu.Items.Add(new MenuItem
				{
					Header = "💾 Save Config",
					Command = ReactiveCommand.Create(() => SaveModFileAs_Click(null, null)),
					InputGesture = new KeyGesture(Key.S, KeyModifiers.Control)
				});

				_ = menu.Items.Add(new MenuItem
				{
					Header = "❌ Close TOML",
					Command = ReactiveCommand.Create(() => CloseTOMLFile_Click(null, null))
				});
			}
		}

		// Helper method to build menu items for ContextMenu (right-click)
		private void BuildContextMenuItems(ContextMenu menu)
		{
			menu.Items.Clear();

			// Refresh and Validate All Mods - available in both modes, at top
			_ = menu.Items.Add(new MenuItem
			{
				Header = "🔄 Refresh List",
				Command = ReactiveCommand.Create(() => RefreshComponents_Click(null, null)),
				InputGesture = new KeyGesture(Key.F5)
			});

			_ = menu.Items.Add(new MenuItem
			{
				Header = "🔄 Validate All Mods",
				Command = ReactiveCommand.Create(async () =>
				{
					Dictionary<Component, ModValidationResult> results = _modManagementService.ValidateAllMods();
					int errorCount = results.Count(r => !r.Value.IsValid);
					int warningCount = results.Sum(r => r.Value.Warnings.Count);

					await InformationDialog.ShowInformationDialog(this,
						"Validation complete!\n\n" +
						$"Errors: {errorCount}\n" +
						$"Warnings: {warningCount}\n\n" +
						$"Valid mods: {results.Count(r => r.Value.IsValid)}/{results.Count}");
				})
			});

			_ = menu.Items.Add(new Separator());

			if ( !EditorMode )
				return;
			// Global operations
			_ = menu.Items.Add(new MenuItem
			{
				Header = "➕ Add New Mod",
				Command = ReactiveCommand.Create(() =>
				{
					Component newMod = _modManagementService.CreateMod();
					if ( newMod == null )
						return;
					SetCurrentComponent(newMod);
					SetTabInternal(TabControl, GuiEditTabItem);
				})
			});

			_ = menu.Items.Add(new Separator());

			// Selection operations (useful for ordered lists)
			_ = menu.Items.Add(new MenuItem
			{
				Header = "🔎 Select by Name",
				Command = ReactiveCommand.Create(() => _modManagementService.SortMods())
			});

			_ = menu.Items.Add(new MenuItem
			{
				Header = "🔎 Select by Category",
				Command = ReactiveCommand.Create(() => _modManagementService.SortMods(ModSortCriteria.Category))
			});

			_ = menu.Items.Add(new MenuItem
			{
				Header = "🔎 Select by Tier",
				Command = ReactiveCommand.Create(() => _modManagementService.SortMods(ModSortCriteria.Tier))
			});

			_ = menu.Items.Add(new Separator());

			// Tools and utilities
			_ = menu.Items.Add(new MenuItem
			{
				Header = "⚙️ Mod Management Tools",
				Command = ReactiveCommand.Create(async () => await ShowModManagementDialog())
			});

			_ = menu.Items.Add(new MenuItem
			{
				Header = "📈 Mod Statistics",
				Command = ReactiveCommand.Create(async () =>
				{
					ModStatistics stats = _modManagementService.GetModStatistics();
					string statsText = "📊 Mod Statistics\n\n" +
											   $"Total Mods: {stats.TotalMods}\n" +
											   $"Selected: {stats.SelectedMods}\n" +
											   $"Downloaded: {stats.DownloadedMods}\n\n" +
											   $"Categories:\n{string.Join("\n", stats.Categories.Select(c => $"  • {c.Key}: {c.Value}"))}\n\n" +
											   $"Tiers:\n{string.Join("\n", stats.Tiers.Select(t => $"  • {t.Key}: {t.Value}"))}\n\n" +
											   $"Average Instructions/Mod: {stats.AverageInstructionsPerMod:F1}\n" +
											   $"Average Options/Mod: {stats.AverageOptionsPerMod:F1}";

					await InformationDialog.ShowInformationDialog(this, statsText);
				})
			});

			_ = menu.Items.Add(new Separator());

			// File operations at the bottom
			_ = menu.Items.Add(new MenuItem
			{
				Header = "💾 Save Config",
				Command = ReactiveCommand.Create(() => SaveModFileAs_Click(null, null)),
				InputGesture = new KeyGesture(Key.S, KeyModifiers.Control)
			});

			_ = menu.Items.Add(new MenuItem
			{
				Header = "❌ Close TOML",
				Command = ReactiveCommand.Create(() => CloseTOMLFile_Click(null, null))
			});
		}

		private void SetupDragAndDrop()
		{
			if ( ModListBox == null )
				return;

			// Only enable drag and drop in editor mode
			ModListBox.PointerPressed += ModListBox_PointerPressed;
			ModListBox.AddHandler(DragDrop.DragOverEvent, ModListBox_DragOver);
			ModListBox.AddHandler(DragDrop.DropEvent, ModListBox_Drop);
		}

		private void ModListBox_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			try
			{
				if ( !EditorMode )
					return;

				if ( !e.GetCurrentPoint(ModListBox).Properties.IsLeftButtonPressed )
					return;
				// Find if we clicked on a drag handle
				if ( !(e.Source is Visual visual) )
					return;
				var textBlock = visual as TextBlock;
				if ( textBlock == null && visual is Control control )
				{
					IEnumerable<TextBlock> descendants = control.GetVisualDescendants().OfType<TextBlock>();
					textBlock = descendants.FirstOrDefault(tb => tb.Text == "⋮⋮");
				}

				if ( textBlock?.Text != "⋮⋮" )
					return;
				// Find the ListBoxItem
				ListBoxItem listBoxItem = visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
				if ( !(listBoxItem?.DataContext is Component component) )
					return;
				_draggedComponent = component;
				var data = new DataObject();
				data.Set("Component", component);
				_ = DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void ModListBox_DragOver(object sender, DragEventArgs e)
		{
			try
			{
				if ( !EditorMode || _draggedComponent == null || !e.Data.Contains("Component") )
				{
					e.DragEffects = DragDropEffects.None;
					e.Handled = true;
					return;
				}

				// Check if we're over a valid drop target
				if ( e.Source is Visual visual )
				{
					ListBoxItem listBoxItem = visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
					if ( listBoxItem?.DataContext is Component targetComponent && targetComponent != _draggedComponent )
					{
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

		private void ModListBox_Drop(object sender, DragEventArgs e)
		{
			try
			{
				if ( !EditorMode || _draggedComponent == null )
				{
					_draggedComponent = null;
					return;
				}

				// Find the drop target
				if ( e.Source is Visual visual )
				{
					ListBoxItem listBoxItem = visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
					if ( listBoxItem?.DataContext is Component targetComponent )
					{
						int targetIndex = MainConfig.AllComponents.IndexOf(targetComponent);
						int currentIndex = MainConfig.AllComponents.IndexOf(_draggedComponent);

						if ( targetIndex != currentIndex && targetIndex >= 0 && currentIndex >= 0 )
						{
							// Perform the move
							MainConfig.AllComponents.RemoveAt(currentIndex);
							MainConfig.AllComponents.Insert(targetIndex, _draggedComponent);

							// Refresh the list
							_ = Dispatcher.UIThread.InvokeAsync(async () =>
							{
								await ProcessComponentsAsync(MainConfig.AllComponents);
								await Logger.LogVerboseAsync($"Moved '{_draggedComponent.Name}' from index #{currentIndex + 1} to #{targetIndex + 1}");
							});
						}
					}
				}

				_draggedComponent = null;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private async Task ShowModManagementDialog()
		{
			try
			{
				var dialogService = new ModManagementDialogService(this, _modManagementService,
					() => MainConfigInstance.allComponents.ToList(),
					(components) => MainConfigInstance.allComponents = components);

				var dialog = new ModManagementDialog(_modManagementService, dialogService);
				await dialog.ShowDialog(this);

				if ( dialog.ModificationsApplied )
				{
					await ProcessComponentsAsync(MainConfig.AllComponents);
					await Logger.LogVerboseAsync("Applied mod management changes");
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		// Called from ModListItem when drag handle is pressed
		public void StartDragComponent(Component component, PointerPressedEventArgs e)
		{
			if ( !EditorMode )
				return;

			_draggedComponent = component;
			var data = new DataObject();
			data.Set("Component", component);
			_ = DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
		}

		private void ModListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				if ( ModListBox.SelectedItem is Component component )
					SetCurrentComponent(component);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private bool _suppressSelectAllCheckBoxEvents;

		private void SelectAllCheckBox_IsCheckedChanged(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( !(sender is CheckBox checkBox) || _suppressSelectAllCheckBoxEvents )
					return;

				var finishedComponents = new HashSet<Component>();

				// Handle different checkbox states
				switch ( checkBox.IsChecked )
				{
					case true:
						// Checkbox is checked - select all components
						foreach ( Component component in MainConfig.AllComponents )
						{
							component.IsSelected = true;
							ComponentCheckboxChecked(component, finishedComponents, suppressErrors: true);
						}
						break;
					case false:
						// Checkbox is unchecked - deselect all components
						foreach ( Component component in MainConfig.AllComponents )
						{
							component.IsSelected = false;
							ComponentCheckboxUnchecked(component, finishedComponents, suppressErrors: true);
						}
						break;
					case null:
						// Checkbox is in indeterminate state - this typically means some but not all are selected
						// When clicked in this state, select all (common UI pattern)
						foreach ( Component component in MainConfig.AllComponents )
						{
							component.IsSelected = true;
							ComponentCheckboxChecked(component, finishedComponents, suppressErrors: true);
						}
						break;
				}

				// Update counts first, then step progress to ensure consistency
				UpdateModCounts();
				UpdateStepProgress();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private HashSet<string> _selectedCategories = new HashSet<string>();
		private string _selectedMinTier = "Any";

		private void FilterModList(string searchText)
		{
			try
			{
				if ( ModListBox == null )
					return;

				var searchOptions = new ModSearchOptions
				{
					SearchInName = true,
					SearchInAuthor = true,
					SearchInCategory = true,
					SearchInDescription = true
				};

				List<Component> filteredComponents = _modManagementService.SearchMods(searchText, searchOptions);

				// Apply tier and category filters
				filteredComponents = ApplyTierAndCategoryFilters(filteredComponents);

				PopulateModList(filteredComponents);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private List<Component> ApplyTierAndCategoryFilters(List<Component> components)
		{
			// Define tier hierarchy (lower index = higher priority)
			var tierHierarchy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			{
				{ "Essential", 0 },
				{ "Recommended", 1 },
				{ "Suggested", 2 },
				{ "Optional", 3 }
			};

			int minTierLevel = _selectedMinTier == "Any" ? int.MaxValue :
				(tierHierarchy.TryGetValue(_selectedMinTier, out int level) ? level : int.MaxValue);

			return components.Where(c =>
			{
				// Check tier filter
				if ( minTierLevel != int.MaxValue )
				{
					if ( string.IsNullOrEmpty(c.Tier) )
						return false;

					if ( tierHierarchy.TryGetValue(c.Tier, out int componentTierLevel) )
					{
						if ( componentTierLevel < minTierLevel )
							return false;
					}
				}

				// Check category filter
				if ( _selectedCategories.Count > 0 )
				{
					if ( string.IsNullOrEmpty(c.Category) || !_selectedCategories.Contains(c.Category) )
						return false;
				}

				return true;
			}).ToList();
		}

		private void RefreshCategoryCheckboxes()
		{
			try
			{
				StackPanel categoryPanel = this.FindControl<StackPanel>("CategoryFilterPanel");
				if ( categoryPanel == null )
					return;

				// Get all unique categories from loaded components
				var categories = MainConfig.AllComponents
					.Select(c => c.Category)
					.Where(cat => !string.IsNullOrEmpty(cat))
					.Distinct()
					.OrderBy(cat => cat)
					.ToList();

				// Clear existing checkboxes
				categoryPanel.Children.Clear();

				// Initialize selected categories if empty (all selected by default)
				if ( _selectedCategories.Count == 0 )
				{
					_selectedCategories = new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase);
				}

				// Create checkbox for each category
				foreach ( string category in categories )
				{
					var checkBox = new CheckBox
					{
						Content = category,
						IsChecked = _selectedCategories.Contains(category),
						Margin = new Thickness(0, 2, 0, 2)
					};

					// Add tooltip with category definition
					ToolTip.SetTip(checkBox, CategoryTierDefinitions.GetCategoryDescription(category));

					checkBox.IsCheckedChanged += (s, e) =>
					{
						if ( checkBox.IsChecked == true )
							_ = _selectedCategories.Add(category);
						else
							_ = _selectedCategories.Remove(category);

						ApplyAllFilters();
					};

					categoryPanel.Children.Add(checkBox);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void ApplyAllFilters()
		{
			FilterModList(SearchText ?? string.Empty);
		}

		private void RefreshModList()
		{
			try
			{
				PopulateModList(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void RefreshModListVisuals()
		{
			try
			{
				if ( ModListBox == null || ModListBox.ItemsSource == null )
					return;

				// Force re-evaluation of all mod list items by refreshing the ItemsSource
				IEnumerable currentItems = ModListBox.ItemsSource;
				ModListBox.ItemsSource = null;
				ModListBox.ItemsSource = currentItems;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void PopulateModList(List<Component> components)
		{
			try
			{
				if ( ModListBox == null )
					return;

				ModListBox.Items.Clear();

				foreach ( Component component in components )
				{
					_ = ModListBox.Items.Add(component);
				}

				UpdateModCounts();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		public void UpdateModCounts()
		{
			try
			{
				TextBlock modCountText = this.FindControl<TextBlock>("ModCountText");
				TextBlock selectedCountText = this.FindControl<TextBlock>("SelectedCountText");

				if ( modCountText != null )
				{
					int totalCount = MainConfig.AllComponents.Count;
					modCountText.Text = totalCount == 1 ? "1 mod" : $"{totalCount} mods";
				}

				if ( selectedCountText != null )
				{
					int selectedCount = MainConfig.AllComponents.Count(c => c.IsSelected);
					selectedCountText.Text = selectedCount == 1 ? "1 selected" : $"{selectedCount} selected";
				}

				// Update SelectAllCheckBox state
				if ( !(this.FindControl<CheckBox>("SelectAllCheckBox") is CheckBox selectAllCheckBox) ) return;
				{
					_suppressSelectAllCheckBoxEvents = true;
					try
					{
						int totalCount = MainConfig.AllComponents.Count;
						int selectedCount = MainConfig.AllComponents.Count(c => c.IsSelected);

						if ( selectedCount == 0 )
							selectAllCheckBox.IsChecked = false;
						else if ( selectedCount == totalCount )
							selectAllCheckBox.IsChecked = true;
						else
							selectAllCheckBox.IsChecked = null;
					}
					finally
					{
						_suppressSelectAllCheckBoxEvents = false;
					}
				}

				// Refresh mod list visuals to update border colors
				RefreshModListVisuals();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		public static void FilterControlListItems([NotNull] object item, [NotNull] string searchText)
		{
			if ( searchText == null )
				throw new ArgumentNullException(nameof(searchText));

			if ( !(item is Control controlItem) )
				return; // no components loaded/created

			if ( controlItem.Tag is Component thisComponent )
				ApplySearchVisibility(controlItem, thisComponent.Name, searchText);

			// Iterate through the child items (TreeViewItem only)
			IEnumerable<ILogical> controlItemArray = controlItem.GetLogicalChildren();
			foreach ( TreeViewItem childItem in controlItemArray.OfType<TreeViewItem>() )
			{
				// Recursively filter the child item (TreeViewItem only)
				FilterControlListItems(childItem, searchText);
			}
		}

		private static void ApplySearchVisibility(
			[NotNull] Visual item,
			[NotNull] string itemName,
			[NotNull] string searchText
		)
		{
			if ( item is null )
				throw new ArgumentNullException(nameof(item));

			if ( itemName is null )
				throw new ArgumentNullException(nameof(itemName));

			if ( searchText is null )
				throw new ArgumentNullException(nameof(searchText));

			// Check if the item matches the search text
			// Show or hide the item based on the match
			item.IsVisible = SearchUtilities.ShouldBeVisible(itemName, searchText);
		}

		private void FindProblemControls([CanBeNull] Control control)
		{
			if ( !(control is ILogical visual) )
				throw new ArgumentNullException(nameof(control));

			if ( control is ComboBox || control is MenuItem )
			{
				control.Tapped -= ComboBox_Opened;
				control.PointerCaptureLost -= ComboBox_Opened;
				control.Tapped += ComboBox_Opened;
				control.PointerCaptureLost += ComboBox_Opened;
			}

			if ( visual.LogicalChildren.IsNullOrEmptyOrAllNull() )
				return;

			// ReSharper disable once PossibleNullReferenceException
			foreach ( ILogical child in visual.LogicalChildren )
			{
				if ( child is Control childControl )
					FindProblemControls(childControl);
			}
		}

		// Prevents a combobox click from dragging the window around.
		public void FindComboBoxesInWindow([NotNull] Window thisWindow)
		{
			if ( thisWindow is null )
				throw new ArgumentNullException(nameof(thisWindow));

			FindProblemControls(thisWindow);
		}

		private void ComboBox_Opened([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			_mouseDownForWindowMoving = false;
			_ignoreWindowMoveWhenClickingComboBox = true;
		}

		private void InputElement_OnPointerMoved([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if ( !_mouseDownForWindowMoving )
				return;

			if ( _ignoreWindowMoveWhenClickingComboBox )
			{
				_ignoreWindowMoveWhenClickingComboBox = false;
				_mouseDownForWindowMoving = false;
				return;
			}

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen )
				return;

			// Don't start window drag if clicking on interactive controls
			if ( ShouldIgnorePointerForWindowDrag(e) )
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
		{
			// Get the element under the pointer
			if ( !(e.Source is Visual source) )
				return false;

			// Walk up the visual tree to check if we're clicking on an interactive element
			Visual current = source;
			while ( current != null && current != this )
			{
				// Check if we're clicking on any interactive control
				if ( current is Button ||
					 current is TextBox ||
					 current is ComboBox ||
					 current is ListBox ||
					 current is MenuItem ||
					 current is Menu ||
					 current is Expander ||
					 current is Slider ||
					 current is TabControl ||
					 current is TabItem )
				{
					return true;
				}

				// Check if the element has context menu or flyout open
				if ( current is Control control )
				{
					if ( control.ContextMenu?.IsOpen == true )
						return true;

					if ( control is Button button && button.Flyout?.IsOpen == true )
						return true;

					if ( control is DropDownButton dropDownButton && dropDownButton.Flyout?.IsOpen == true )
						return true;
				}

				// Move up the visual tree
				current = current.GetVisualParent();
			}

			return false;
		}

		private void InputElement_OnPointerReleased([NotNull] object sender, [NotNull] PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;

		[UsedImplicitly]
		private void CloseButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) =>
			Close();

		[UsedImplicitly]
		private void MinimizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) =>
			WindowState = WindowState.Minimized;

		[ItemCanBeNull]
		public async Task<string> SaveFile(
			string saveFileName = null
		)
		{
			try
			{
				IStorageFile file = await StorageProvider.SaveFilePickerAsync(
					new FilePickerSaveOptions
					{
						DefaultExtension = "toml",
						FileTypeChoices = /*defaultExts ??*/
							new List<FilePickerFileType> { FilePickerFileTypes.All },
						ShowOverwritePrompt = true,
						SuggestedFileName = saveFileName ?? "my_toml_instructions.toml",
					}
				);

				string filePath = file?.TryGetLocalPath();
				if ( !(filePath is null) )
				{
					await Logger.LogAsync($"Selected file: {filePath}");
					return filePath;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}

			return null;
		}

		public async Task<string[]> ShowFileDialog(
			bool isFolderDialog,
			bool allowMultiple = false,
			IStorageFolder startFolder = null,
			string windowName = null
		)
		{
			try
			{
				if ( !(VisualRoot is Window) )
				{
					await Logger.LogErrorAsync($"Could not open {(isFolderDialog ? "folder" : "file")} dialog - parent window not found");
					return null;
				}

				if ( isFolderDialog )
				{
					// Start async operation to open the dialog.
					IReadOnlyList<IStorageFolder> result = await StorageProvider.OpenFolderPickerAsync(
						new FolderPickerOpenOptions
						{
							Title = windowName ?? "Choose the folder",
							AllowMultiple = allowMultiple,
							SuggestedStartLocation = startFolder,
						}
					);
					return result.Select(s => s.TryGetLocalPath()).ToArray();
				}
				else
				{
					// Start async operation to open the dialog.
					IReadOnlyList<IStorageFile> result = await StorageProvider.OpenFilePickerAsync(
						new FilePickerOpenOptions
						{
							Title = windowName ?? "Choose the file(s)",
							AllowMultiple = allowMultiple,
							FileTypeFilter = /*filters ?? */new[] // todo: fix custom filters
							{
										FilePickerFileTypes.All, FilePickerFileTypes.TextPlain,
							},
						}
					);
					string[] files = result.Select(s => s.TryGetLocalPath()).ToArray();
					if ( files.Length > 0 )
						return files; // Retrieve the first selected file path
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}

			return null;
		}


		[UsedImplicitly]
		private async void LoadInstallFile_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				// Open the file dialog to select a TOML file
				string[] result = await ShowFileDialog(
					windowName: "Load the TOML instruction file you've downloaded/created",
					isFolderDialog: false
				);
				if ( result is null || result.Length <= 0 )
					return;

				string filePath = result[0];
				if ( !PathValidator.IsValidPath(filePath) )
					return;

				// Use the unified TOML loading method
				_ = await LoadTomlFile(filePath, fileType: "instruction file");
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		public async void LoadMarkdown_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				// Open the file dialog to select a file
				string[] result = await ShowFileDialog(isFolderDialog: false, windowName: "Load your markdown file.");
				if ( result is null || result.Length <= 0 )
					return;

				string filePath = result[0];
				if ( string.IsNullOrEmpty(filePath) )
					return; // user cancelled

				using ( var reader = new StreamReader(filePath) )
				{
					string fileContents = await reader.ReadToEndAsync();

					// Open Regex Import Dialog to let user adjust patterns and preview matches
					var dialog = new RegexImportDialog(fileContents, MarkdownImportProfile.CreateDefault());
					MarkdownParserResult parseResult = null;
					dialog.Closed += async (_, __) =>
					{
						if ( !dialog.LoadSuccessful || !(dialog.DataContext is RegexImportDialogViewModel vm) )
							return;
						parseResult = vm.ConfirmLoad();
						await Logger.LogAsync($"Markdown parsing completed. Found {parseResult.Components?.Count ?? 0} components with {parseResult.Components?.Sum(c => c.ModLink.Count) ?? 0} total links.");
						if ( !(parseResult.Warnings?.Count > 0) )
							return;
						await Logger.LogWarningAsync($"Markdown parsing completed with {parseResult.Warnings.Count} warnings.");
						foreach ( string warning in parseResult.Warnings )
							await Logger.LogWarningAsync($"  - {warning}");
					};
					await dialog.ShowDialog(this);
					if ( parseResult is null )
						return;

					// If no existing components, just load the new ones
					if ( MainConfig.AllComponents.Count == 0 )
					{
						MainConfigInstance.allComponents = new List<Component>(parseResult.Components);
						await Logger.LogAsync($"Loaded {parseResult.Components.Count} components from markdown.");

						// Auto-generate instructions for components without them
						await TryAutoGenerateInstructionsForComponents(parseResult.Components.ToList());
					}
					else
					{
						// Ask user what to do with existing components
						bool? confirmResult = await ShowConfigLoadConfirmation(fileType: "markdown file");

						if ( confirmResult == true ) // User clicked "Merge"
						{
							// Show conflict resolution dialog
							var conflictDialog = new ComponentMergeConflictDialog(
										MainConfig.AllComponents,
										new List<Component>(parseResult.Components),
										"Currently Loaded Components",
										"Markdown File",
										FuzzyMatcher.FuzzyMatchComponents);

							await conflictDialog.ShowDialog(this);

							if ( conflictDialog.UserConfirmed && conflictDialog.MergedComponents != null )
							{
								int originalCount = MainConfig.AllComponents.Count;
								MainConfigInstance.allComponents = conflictDialog.MergedComponents;
								int newCount = MainConfig.AllComponents.Count;
								await Logger.LogAsync($"Merged {parseResult.Components.Count} parsed components with existing {originalCount} components. Total components now: {newCount}");

								// Auto-generate instructions for newly added components
								await TryAutoGenerateInstructionsForComponents(MainConfig.AllComponents);
							}
							else
							{
								await Logger.LogAsync("Merge cancelled by user.");
								return;
							}
						}
						else if ( confirmResult == false ) // User clicked "Overwrite"
						{
							// Replace all existing components with new ones
							MainConfigInstance.allComponents = new List<Component>(parseResult.Components);
							await Logger.LogAsync($"Overwrote existing config with {parseResult.Components.Count} components from markdown.");

							// Auto-generate instructions for components without them
							await TryAutoGenerateInstructionsForComponents(parseResult.Components.ToList());
						}
						else // User cancelled (null)
							return;
					}
					await ProcessComponentsAsync(MainConfig.AllComponents);
				}
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private void OpenLink_Click([NotNull] object sender, [NotNull] TappedEventArgs e)
		{
			if ( !(sender is TextBlock textBlock) )
				return;

			try
			{
				string url = textBlock.Text;
				if ( string.IsNullOrEmpty(url) )
					throw new InvalidOperationException(message: "url (textBlock.Text) cannot be null/empty");

				UrlUtilities.OpenUrl(url);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to open URL: {ex.Message}");
			}
		}

		[UsedImplicitly]
		private async void BrowseSourceFiles_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				var button = (Button)sender;
				// Get the item's data context based on the clicked button
				Instruction thisInstruction = (Instruction)button.DataContext
											?? throw new NullReferenceException(message: "Could not find instruction instance");

				// Open the file dialog to select a file

				IStorageFolder startFolder = null;
				if ( !(MainConfig.SourcePath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.SourcePath.FullName);
				string[] filePaths = await ShowFileDialog(
					windowName: "Select the files to perform this instruction on",
					isFolderDialog: false,
					allowMultiple: true,
					startFolder: startFolder
				);
				if ( filePaths is null )
				{
					await Logger.LogVerboseAsync("User did not select any files.");
					return;
				}

				await Logger.LogVerboseAsync($"Selected files: [{string.Join($",{Environment.NewLine}", filePaths)}]");
				List<string> files = filePaths.ToList();
				if ( files.Count == 0 )
				{
					_ = Logger.LogVerboseAsync("No files chosen in BrowseSourceFiles_Click, returning to previous values");
					return;
				}

				if ( files.IsNullOrEmptyOrAllNull() )
				{
					throw new ArgumentOutOfRangeException(
						nameof(sender),
						$"Invalid files found. Please report this issue to the developer: [{string.Join(separator: ",", files)}]"
					);
				}

				// Replace path with prefixed variables.
				for ( int i = 0; i < files.Count; i++ )
				{
					string filePath = files[i];
					files[i] = MainConfig.SourcePath != null
						? Utility.RestoreCustomVariables(filePath)
						: filePath;
				}

				if ( MainConfig.SourcePath is null )
					_ = Logger.LogWarningAsync("Not using custom variables <<kotorDirectory>> and <<modDirectory>> due to directories not being set prior.");


				thisInstruction.Source = files;

				// refresh the text box
				// ReSharper disable once InvertIf
				if ( button.Tag is TextBox sourceTextBox )
				{
					string convertedItems = new ListToStringConverter().Convert(
						files,
						typeof(string),
						parameter: null,
						CultureInfo.CurrentCulture
					) as string;

					sourceTextBox.Text = convertedItems;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private async void BrowseSourceFromFolders_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				var button = (Button)sender;
				Instruction thisInstruction = (Instruction)button.DataContext
											?? throw new NullReferenceException(message: "Could not find instruction instance");

				if ( !(button.Tag is TextBox sourceTextBox) )
					return;

				IStorageFolder startFolder = null;
				if ( !(MainConfig.SourcePath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.SourcePath.FullName);
				string[] folderPaths = await ShowFileDialog(
					windowName: "Select the folder to perform this instruction on",
					isFolderDialog: true,
					allowMultiple: true,
					startFolder: startFolder
				);

				if ( folderPaths is null || folderPaths.Length == 0 )
				{
					await Logger.LogVerboseAsync("User did not select any folders.");
					return;
				}

				var modifiedFolders = folderPaths.SelectMany(
					thisFolder => new DirectoryInfo(thisFolder)
						.EnumerateDirectories(searchPattern: "*", SearchOption.AllDirectories).Select(
							folder => folder.FullName + Path.DirectorySeparatorChar + "*.*"
						)
				).ToList();

				thisInstruction.Source = modifiedFolders;

				string convertedItems = new ListToStringConverter().Convert(
					modifiedFolders,
					typeof(string),
					parameter: null,
					CultureInfo.CurrentCulture
				) as string;

				sourceTextBox.Text = convertedItems;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}


		[UsedImplicitly]
		private async void BrowseDestination_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				Button button = (Button)sender ?? throw new InvalidOperationException();
				Instruction thisInstruction = (Instruction)button.DataContext
											?? throw new NullReferenceException(message: "Could not find instruction instance");

				IStorageFolder startFolder = null;
				if ( !(MainConfig.DestinationPath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.DestinationPath.FullName);

				// Open the folder dialog to select a folder
				string[] result = await ShowFileDialog(isFolderDialog: true, startFolder: startFolder);
				if ( result is null || result.Length <= 0 )
					return;

				string folderPath = result[0];
				if ( folderPath is null )
				{
					_ = Logger.LogVerboseAsync($"No folder chosen in BrowseDestination_Click. Will continue using '{thisInstruction.Destination}'");
					return;
				}

				if ( MainConfig.SourcePath is null )
				{
					_ = Logger.LogAsync(message: "Directories not set, setting raw folder path without custom variable <<kotorDirectory>>");
					thisInstruction.Destination = folderPath;
					return;
				}

				thisInstruction.Destination = Utility.RestoreCustomVariables(folderPath);

				// refresh the text box
				if ( button.Tag is TextBox destinationTextBox )
					destinationTextBox.Text = thisInstruction.Destination;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void SaveButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(
						this,
						message: "Please select a component from the list or create a new one before saving."
					);
					return;
				}

				await Logger.LogVerboseAsync($"Selected '{CurrentComponent.Name}'");

				if ( !await ShouldSaveChanges() )
					return;

				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private async void FixPathPermissionsClick([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				string[] results = await ShowFileDialog(
					windowName: "Select the folder(s) you'd like to fix permissions to.",
					isFolderDialog: true,
					allowMultiple: true
				);

				if ( results is null || results.Length <= 0 )
					return; // user cancelled.

				foreach ( string folder in results )
				{
					DirectoryInfo thisDir = PathHelper.TryGetValidDirectoryInfo(folder);
					if ( thisDir is null || !thisDir.Exists )
					{
						_ = Logger.LogErrorAsync($"Directory not found: '{folder}', skipping...");
						continue;
					}

					await FilePermissionHelper.FixPermissionsAsync(thisDir);
					Logger.Log($"Completed FixPathPermissions at '{thisDir.FullName}'");
				}

			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async void FixIosCaseSensitivityClick([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				string[] results = await ShowFileDialog(
					windowName: "Select the folder(s) you'd like to lowercase all files/folders inside",
					isFolderDialog: true,
					allowMultiple: true
				);

				if ( results is null || results.Length <= 0 )
					return; // user cancelled.

				int numObjectsRenamed = 0;
				foreach ( string folder in results )
				{
					var thisDir = new DirectoryInfo(folder);
					if ( !thisDir.Exists )
					{
						_ = Logger.LogErrorAsync($"Directory not found: '{thisDir.FullName}', skipping...");
						continue;
					}

					numObjectsRenamed += await UIUtilities.FixIOSCaseSensitivity(thisDir);
				}

				Logger.Log($"Successfully renamed {numObjectsRenamed} files/folders.");
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}


		private async void ResolveDuplicateFilesAndFolders([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				bool? answer = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					"This button will resolve all case-sensitive duplicate files/folders in your install directory and your mod download directory."
					+ Environment.NewLine
					+ " WARNING: This method may take a while and cannot be stopped until it finishes. Really continue?"
				);
				if ( answer != true )
					return;

				await Logger.LogAsync("Finding duplicate case-insensitive folders/files in the install destination...");
				string destPath = MainConfig.DestinationPath?.FullName;
				if ( string.IsNullOrEmpty(destPath) )
				{
					await Logger.LogErrorAsync("Destination path is null or empty, skipping duplicate file/folder search.");
					return;
				}
				IEnumerable<FileSystemInfo> duplicates = PathHelper.FindCaseInsensitiveDuplicates(destPath);
				var fileSystemInfos = duplicates.ToList();
				foreach ( FileSystemInfo duplicate in fileSystemInfos )
				{
					await Logger.LogWarningAsync(duplicate?.FullName + " is duplicated on the storage drive.");
				}

				answer = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					"Duplicate file/folder search finished."
					+ Environment.NewLine
					+ $" Found {fileSystemInfos.Count} files/folders that have duplicates in your install dir."
					+ Environment.NewLine
					+ " Delete all duplicates except the ones most recently modified?"
				);
				if ( answer != true )
					return;

				IEnumerable<IGrouping<string, FileSystemInfo>> groupedDuplicates = fileSystemInfos.GroupBy(fs => fs.Name.ToLowerInvariant());

				foreach ( IGrouping<string, FileSystemInfo> group in groupedDuplicates )
				{
					var orderedDuplicates = group.OrderByDescending(fs => fs.LastWriteTime).ToList();
					if ( orderedDuplicates.Count <= 1 )
						continue;

					for ( int i = 1; i < orderedDuplicates.Count; i++ )
					{
						try
						{
							switch ( orderedDuplicates[i] )
							{
								case FileInfo fileInfo:
									fileInfo.Delete();
									break;
								case DirectoryInfo directoryInfo:
									directoryInfo.Delete(recursive: true); // recursive delete
									break;
								default:
									Logger.Log(orderedDuplicates[i].FullName + " does not exist somehow?");
									continue;
							}

							await Logger.LogAsync($"Deleted {orderedDuplicates[i].FullName}");
						}
						catch ( Exception deletionException )
						{
							await Logger.LogExceptionAsync(
								deletionException,
								$"Failed to delete {orderedDuplicates[i].FullName}"
							);
						}
					}
				}
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}


		[UsedImplicitly]
		private async void ValidateButton_Click([CanBeNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				(bool validationResult, _) = await InstallationService.ValidateInstallationEnvironmentAsync(
					MainConfigInstance,
					async message => await ConfirmationDialog.ShowConfirmationDialog(this, message) == true
				);

				// If validation failed, run detailed analysis
				var modIssues = new List<Dialogs.ValidationIssue>();
				var systemIssues = new List<string>();

				if ( !validationResult )
				{
					// Analyze what went wrong
					await AnalyzeValidationFailures(modIssues, systemIssues);
				}

				// Show new validation dialog
				_ = await ValidationDialog.ShowValidationDialog(
					this,
					validationResult,
					validationResult
						? "No issues found. Your mods are ready to install!"
						: "Some issues need to be resolved before installation can proceed.",
					modIssues.Count > 0 ? modIssues : null,
					systemIssues.Count > 0 ? systemIssues : null,
					() => OpenOutputWindow_Click(null, null)
				);

				// Update step progress after validation
				if ( validationResult )
				{
					// Validation succeeded - this counts as progress toward Step 4
					UpdateStepProgress();
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private async Task AnalyzeValidationFailures(List<Dialogs.ValidationIssue> modIssues, List<string> systemIssues)
		{
			try
			{
				// Check system-level issues
				if ( MainConfig.DestinationPath == null || MainConfig.SourcePath == null )
				{
					systemIssues.Add("⚙️ Directories not configured\n" +
									"Both Mod Directory and KOTOR Install Directory must be set.\n" +
									"Solution: Click Settings and configure both directories.");
					return; // Can't continue without directories
				}

				if ( !MainConfig.AllComponents.Any() )
				{
					systemIssues.Add("📋 No mods loaded\n" +
									"No mod configuration file has been loaded.\n" +
									"Solution: Click 'File > Load Installation File' to load a mod list.");
					return;
				}

				if ( !MainConfig.AllComponents.Any(c => c.IsSelected) )
				{
					systemIssues.Add("☑️ No mods selected\n" +
									"At least one mod must be selected for installation.\n" +
									"Solution: Check the boxes next to mods you want to install.");
					return;
				}

				// Check each selected component
				foreach ( Component component in MainConfig.AllComponents.Where(c => c.IsSelected) )
				{
					// Check if downloaded
					if ( !component.IsDownloaded )
					{
						var issue = new Dialogs.ValidationIssue
						{
							Icon = "📥",
							ModName = component.Name,
							IssueType = "Missing Download",
							Description = "The mod archive file is not in your Mod Directory. The installer cannot proceed without this file.",
							Solution = component.ModLink.Count > 0
								? $"Solution: Click 'Fetch Downloads' to auto-download, or manually download from: {component.ModLink[0]}"
								: "Solution: Click 'Fetch Downloads' to auto-download, or manually download the mod file and place it in your Mod Directory."
						};
						modIssues.Add(issue);
						continue; // Other checks won't be meaningful without the download
					}

					// Check if it has instructions
					if ( component.Instructions.Count == 0 && component.Options.Count == 0 )
					{
						var issue = new Dialogs.ValidationIssue
						{
							Icon = "❌",
							ModName = component.Name,
							IssueType = "Missing Instructions",
							Description = "This mod has no installation instructions defined. It cannot be installed.",
							Solution = "Solution: This is a configuration error with the mod itself. Contact the mod list creator or disable this mod."
						};
						modIssues.Add(issue);
					}

					// Run component validation for detailed file/path issues
					var validator = new ComponentValidation(component, MainConfig.AllComponents);
					bool componentValid = validator.Run();
					if ( !componentValid )
					{
						List<string> errors = validator.GetErrors();
						if ( errors.Count <= 0 )
						    continue;
						var issue = new Dialogs.ValidationIssue
						{
							Icon = "🔧",
							ModName = component.Name,
							IssueType = "Installation Configuration Error",
							Description = string.Join("\n", errors.Take(3)) + (errors.Count > 3 ? $"\n... and {errors.Count - 3} more errors" : ""),
							Solution = "Solution: Check the Output Window for detailed logs. This usually means missing files in the mod archive or incorrect file paths."
						};
						modIssues.Add(issue);
					}
				}

				// Check for system-level issues that weren't caught earlier
				if ( !Utility.IsDirectoryWritable(MainConfig.DestinationPath) )
				{
					systemIssues.Add("🔒 KOTOR Directory Not Writable\n" +
									"The installer cannot write to your KOTOR installation directory.\n" +
									"Solution: Run KOTORModSync as Administrator, or install KOTOR to a different location (like Documents folder).");
				}

				if ( !Utility.IsDirectoryWritable(MainConfig.SourcePath) )
				{
					systemIssues.Add("🔒 Mod Directory Not Writable\n" +
									"The installer cannot write to your Mod Directory.\n" +
									"Solution: Ensure you have write permissions for this folder, or choose a different Mod Directory.");
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				systemIssues.Add("❌ Unexpected Error\n" +
								"An error occurred during validation analysis.\n" +
								"Solution: Check the Output Window for details.");
			}
		}

		[UsedImplicitly]
		private async void AddComponentButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			// Create a new default component with a new GUID
			try
			{
				var newComponent = new Component
				{
					Guid = Guid.NewGuid(),
					Name = "new mod_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()),
				};

				// Add the new component to the collection
				MainConfigInstance.allComponents.Add(newComponent);

				// Load into the editor
				LoadComponentDetails(newComponent);

				// Refresh the TreeView to reflect the changes
				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void RefreshComponents_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception exc )
			{
				await Logger.LogExceptionAsync(exc);
			}
		}

		[UsedImplicitly]
		private async void CloseTOMLFile_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				await Logger.LogAsync(message: "Closing TOML configuration and clearing component list...");

				// Clear the components list
				MainConfigInstance.allComponents = new List<Component>();

				// Clear current component
				SetCurrentComponent(c: null);

				// Hide the tabs that should only be visible when components are loaded
				SummaryTabItem.IsVisible = false;
				GuiEditTabItem.IsVisible = false;
				RawEditTabItem.IsVisible = false;

				// Set to the initial tab
				SetTabInternal(TabControl, InitialTab);

				// Clear the mod list box
				ModListBox?.Items.Clear();

				// Update step progress and mod counts
				UpdateStepProgress();
				UpdateModCounts();

				await Logger.LogAsync(message: "TOML configuration closed successfully. Component list cleared.");
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private async void RemoveComponentButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			// Get the selected component from the TreeView
			try
			{
				if ( CurrentComponent is null )
				{
					Logger.Log(message: "No component loaded into editor - nothing to remove.");
					return;
				}

				// Check for dependent components
				var dependentComponents = MainConfig.AllComponents
					.Where(c => c.Dependencies.Contains(CurrentComponent.Guid) ||
							   c.Restrictions.Contains(CurrentComponent.Guid) ||
							   c.InstallBefore.Contains(CurrentComponent.Guid) ||
							   c.InstallAfter.Contains(CurrentComponent.Guid))
					.ToList();

				if ( dependentComponents.Any() )
				{
					// Log the dependent components
					Logger.Log($"Cannot remove '{CurrentComponent.Name}' - {dependentComponents.Count} components depend on it:");
					foreach ( Component dependent in dependentComponents )
					{
						var dependencyTypes = new List<string>();
						if ( dependent.Dependencies.Contains(CurrentComponent.Guid) )
							dependencyTypes.Add("Dependency");
						if ( dependent.Restrictions.Contains(CurrentComponent.Guid) )
							dependencyTypes.Add("Restriction");
						if ( dependent.InstallBefore.Contains(CurrentComponent.Guid) )
							dependencyTypes.Add("InstallBefore");
						if ( dependent.InstallAfter.Contains(CurrentComponent.Guid) )
							dependencyTypes.Add("InstallAfter");

						Logger.Log($"  - {dependent.Name} ({string.Join(", ", dependencyTypes)})");
					}

					// Show dependency unlinking dialog
					(bool confirmed, List<Component> componentsToUnlink) = await DependencyUnlinkDialog.ShowUnlinkDialog(
						this, CurrentComponent, dependentComponents);

					if ( !confirmed )
						return;

					// Unlink the dependencies
					foreach ( Component componentToUnlink in componentsToUnlink )
					{
						componentToUnlink.Dependencies.Remove(CurrentComponent.Guid);
						componentToUnlink.Restrictions.Remove(CurrentComponent.Guid);
						componentToUnlink.InstallBefore.Remove(CurrentComponent.Guid);
						componentToUnlink.InstallAfter.Remove(CurrentComponent.Guid);

						Logger.Log($"Unlinked dependencies from '{componentToUnlink.Name}'");
					}
				}

				// Remove the selected component from the collection
				_ = MainConfigInstance.allComponents.Remove(CurrentComponent);
				SetCurrentComponent(c: null);

				// Refresh the TreeView to reflect the changes
				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void SetDirectories_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				IStorageFolder startFolder = null;
				if ( !(MainConfig.DestinationPath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.DestinationPath.FullName);

				// Open the folder dialog to select a folder
				string[] result = await ShowFileDialog(
					windowName: "Select your <<kotorDirectory>> (path to the game install)",
					isFolderDialog: true,
					startFolder: startFolder
				);
				if ( result?.Length > 0 )
				{
					string chosenFolder = result[0];
					if ( chosenFolder != null )
					{
						var kotorInstallDir = new DirectoryInfo(chosenFolder);
						MainConfigInstance.destinationPath = kotorInstallDir;
					}
				}
				else
				{
					await Logger.LogVerboseAsync("User cancelled selecting <<kotorDirectory>>");
				}

				if ( !(MainConfig.SourcePath is null) )
					startFolder = await StorageProvider.TryGetFolderFromPathAsync(MainConfig.SourcePath.FullName) ?? startFolder;

				// Open the folder dialog to select a folder
				result = await ShowFileDialog(
					windowName: "Select your <<modDirectory>> where ALL your mods are downloaded.",
					isFolderDialog: true,
					startFolder: startFolder
				);
				if ( result?.Length > 0 )
				{
					string chosenFolder = result[0];
					if ( chosenFolder != null )
					{
						var modDirectory = new DirectoryInfo(chosenFolder);
						MainConfigInstance.sourcePath = modDirectory;
					}
				}
				else
				{
					await Logger.LogVerboseAsync(message: "User cancelled selecting <<modDirectory>>");
				}

				// Update step progress when directories are set via traditional dialog
				UpdateStepProgress();
			}
			catch ( ArgumentNullException ) { }
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, customMessage: "Unknown error - please report to a developer");
			}
		}

		[UsedImplicitly]
		private async void InstallModSingle_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				if ( _installRunning )
				{
					await InformationDialog.ShowInformationDialog(
						this,
						message: "There's already another installation running, please check the output window."
					);
					return;
				}

				if ( MainConfigInstance is null || MainConfig.DestinationPath is null )
				{
					var informationDialog = new InformationDialog { InfoText = "Please set your directories first" };
					_ = await informationDialog.ShowDialog<bool?>(this);
					return;
				}

				if ( CurrentComponent is null )
				{
					var informationDialog = new InformationDialog
					{
						InfoText = "Please choose a mod to install from the left list first",
					};
					_ = await informationDialog.ShowDialog<bool?>(this);
					return;
				}

				string name = CurrentComponent.Name; // use correct name even if user clicks another component.

				bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					CurrentComponent.Directions
					+ Environment.NewLine
					+ Environment.NewLine
					+ "Press Yes to execute the provided directions now."
				);
				if ( confirm != true )
				{
					await Logger.LogAsync($"User cancelled install of '{name}'");
					return;
				}

				try
				{
					_installRunning = true;

					Component.InstallExitCode exitCode = await InstallationService.InstallSingleComponentAsync(
						CurrentComponent,
						MainConfig.AllComponents
					);
					_installRunning = false;

					if ( exitCode != 0 )
					{
						await InformationDialog.ShowInformationDialog(
							this,
							$"There was a problem installing '{name}':"
							+ Environment.NewLine
							+ Utility.GetEnumDescription(exitCode)
							+ Environment.NewLine
							+ Environment.NewLine
							+ " Check the output window for details."
						);
					}
					else
					{
						await Logger.LogAsync($"Successfully installed '{name}'");

						// Mark Step 4 as complete after successful single mod installation
						CheckBox step4Check = this.FindControl<CheckBox>(name: "Step4Checkbox");
						if ( step4Check != null ) step4Check.IsChecked = true;
						UpdateStepProgress();
					}
				}
				catch ( Exception )
				{
					_installRunning = false;
					throw;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void StartInstall_Click([CanBeNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( _installRunning )
				{
					await InformationDialog.ShowInformationDialog(
						this,
						message: "There's already an installation running, please check the output window."
					);
					return;
				}

				(bool success, string informationMessage) = await InstallationService.ValidateInstallationEnvironmentAsync(
					MainConfigInstance,
					async message => await ConfirmationDialog.ShowConfirmationDialog(this, message) == true
				);
				if ( !success )
				{
					await InformationDialog.ShowInformationDialog(this, informationMessage);
					return;
				}

				// Update step progress after successful validation during installation
				UpdateStepProgress();

				if ( await ConfirmationDialog.ShowConfirmationDialog(
						this,
						"WARNING! While there is code in place to prevent incorrect instructions from running,"
						+ $" the program cannot predict every possible mistake a user could make in a config file.{Environment.NewLine}"
						+ " Additionally, some mod builds can be 20GB or larger! Due to this, KOTORModSync will not explicitly create any backups."
						+ " Please ensure you've backed up your Install directory"
						+ $" and you've ensured you're running a Vanilla installation.{Environment.NewLine}{Environment.NewLine}"
						+ " Are you sure you're ready to continue?"
					)
					!= true )
				{
					return;
				}

				if ( await ConfirmationDialog.ShowConfirmationDialog(this, confirmText: "Really install all mods?") != true )
					return;

				var progressWindow = new ProgressWindow { ProgressBar = { Value = 0 }, Topmost = true };
				DateTime installStartTime = DateTime.UtcNow;
				int warningCount = 0;
				int errorCount = 0;
				void LogCounter(string message)
				{
					try
					{
						if ( string.IsNullOrEmpty(message) )
							return;
						// Logged message format: [timestamp] [Warning]/[Error] ...
						if ( message.IndexOf(value: "[Warning]", StringComparison.OrdinalIgnoreCase) >= 0 )
							warningCount++;
						if ( message.IndexOf(value: "[Error]", StringComparison.OrdinalIgnoreCase) >= 0 )
							errorCount++;
					}
					catch { /* best effort */ }
				}
				void ExceptionCounter(Exception _)
				{
					try { errorCount++; }
					catch ( Exception ex )
					{
						Logger.LogException(ex);
					}
				}
				Logger.Logged += LogCounter;
				Logger.ExceptionLogged += ExceptionCounter;
				progressWindow.CancelRequested += (_, __) =>
					// Trigger existing closing flow which asks for confirmation
					progressWindow.Close();

				_isClosingProgressWindow = false;
				if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
				{
					_ = Logger.LogVerboseAsync("Disabling the close button on the console window, to prevent an install from being interrupted...");
					ConsoleConfig.DisableConsoleCloseButton();
				}

				try
				{
					_ = Logger.LogAsync("Start installing all mods...");
					_installRunning = true;

					progressWindow.Closed += ProgressWindowClosed;
					progressWindow.Closing += async (sender2, e2) =>
					{
						// If the window is already in the process of closing, do nothing
						if ( _isClosingProgressWindow )
							return;

						// Otherwise, prevent the window from closing and show the confirmation dialog
						e2.Cancel = true;

						// Create and show the confirmation dialog
						bool? result = await ConfirmationDialog.ShowConfirmationDialog(
							this,
							confirmText:
							"Closing the progress window will stop the install after the current instruction completes. Really cancel the install?"
						);

						// If the result is true, the user confirmed they want to close the window
						if ( !(result is true) )
							return;
						// Mark the window as in the process of closing
						_isClosingProgressWindow = true;

						// Re-initiate the closing of the window
						progressWindow.Close();
					};
					progressWindow.Show();
					_progressWindowClosed = false;

					Component.InstallExitCode exitCode = Component.InstallExitCode.UnknownError;

					var selectedMods = MainConfig.AllComponents.Where(thisComponent => thisComponent.IsSelected).ToList();
					for ( int index = 0; index < selectedMods.Count; index++ )
					{
						if ( _progressWindowClosed )
						{
							_installRunning = false;
							_ = Logger.LogAsync(message: "User cancelled install by closing the progress window.");
							return;
						}

						Component component = selectedMods[index];
						await Dispatcher.UIThread.InvokeAsync(
							async () =>
							{
								progressWindow.ProgressTextBlock.Text = $"Installing '{component.Name}'..."
																		+ Environment.NewLine
																		+ Environment.NewLine
																		+ "Executing the provided directions..."
																		+ Environment.NewLine
																		+ Environment.NewLine
																		+ component.Directions;

								double percentComplete = selectedMods.Count == 0 ? 0 : (double)index / selectedMods.Count;
								progressWindow.Topmost = true;
								int installedCount = index;
								progressWindow.UpdateMetrics(
									percentComplete,
									installedCount,
									selectedMods.Count,
									installStartTime,
									warningCount,
									errorCount,
									component.Name
								);

								// Additional fallback options
								await Task.Delay(millisecondsDelay: 100); // Introduce a small delay
								await Dispatcher.UIThread.InvokeAsync(() => { }); // Invoke an empty action to ensure UI updates are processed
								await Task.Delay(millisecondsDelay: 50); // Introduce another small delay
							}
						);

						// Ensure the UI updates are processed
						await Task.Yield();
						await Task.Delay(millisecondsDelay: 200);

						if ( !component.IsSelected )
						{
							await Logger.LogAsync($"Skipping install of '{component.Name}' (unchecked)");
							continue;
						}

						await Logger.LogAsync($"Start Install of '{component.Name}'...");
						exitCode = await InstallationService.InstallSingleComponentAsync(
							component,
							MainConfig.AllComponents
						);
						await Logger.LogAsync($"Install of '{component.Name}' finished with exit code {exitCode}");

						if ( exitCode != 0 )
						{
							bool? confirm = await ConfirmationDialog.ShowConfirmationDialog(
								this,
								$"There was a problem installing '{component.Name}':"
								+ Environment.NewLine
								+ Utility.GetEnumDescription(exitCode)
								+ Environment.NewLine
								+ Environment.NewLine
								+ " Check the output window for details."
								+ Environment.NewLine
								+ Environment.NewLine
								+ $"Skip '{component.Name}' and install the next mod anyway? (NOT RECOMMENDED!)"
							);

							if ( confirm == true )
								continue;

							await Logger.LogAsync(message: "Install cancelled");
							break;
						}

						await Logger.LogAsync($"Finished installed '{component.Name}'");
					}

					if ( exitCode != Component.InstallExitCode.Success )
						return;
					await InformationDialog.ShowInformationDialog(
						this,
						message: "Install Completed. Check the output window for information."
					);
					await Logger.LogAsync(message: "Install completed.");

					// Mark Step 4 as complete after successful installation
					CheckBox step4Check = this.FindControl<CheckBox>(name: "Step4Checkbox");
					if ( step4Check != null )
						step4Check.IsChecked = true;
					UpdateStepProgress();
				}
				catch ( Exception )
				{
					await Logger.LogErrorAsync(message: "Terminating install due to unhandled exception:");
					throw;
				}
				finally
				{
					_installRunning = false;
					_isClosingProgressWindow = true;
					progressWindow.Close();
					// Unsubscribe metrics counters
					Logger.Logged -= LogCounter;
					Logger.ExceptionLogged -= ExceptionCounter;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private void ProgressWindowClosed([CanBeNull] object sender, [CanBeNull] EventArgs e)
		{
			try
			{
				if ( !(sender is ProgressWindow progressWindow) )
					return;

				progressWindow.ProgressBar.Value = 0;
				progressWindow.Closed -= ProgressWindowClosed;
				progressWindow.Dispose();
				_progressWindowClosed = true;
				if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
				{
					_ = Logger.LogVerboseAsync("Install terminated, re-enabling the close button in the console window");
					ConsoleConfig.EnableCloseButton();
				}
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}

		[UsedImplicitly]
		private async void DocsButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				string file = await SaveFile(
					saveFileName: "mod_documentation.txt"
				);

				if ( file is null )
					return; // user cancelled

				string docs = Component.GenerateModDocumentation(MainConfig.AllComponents);
				await FileUtilities.SaveDocsToFileAsync(file, docs);
				string message = $"Saved documentation of {MainConfig.AllComponents.Count} mods to '{file}'";
				await Logger.LogAsync(message);
				await InformationDialog.ShowInformationDialog(this, message);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, customMessage: "Error generating and saving documentation");
				await InformationDialog.ShowInformationDialog(
					this,
					message: "An unexpected error occurred while generating and saving documentation."
				);
			}
		}


		/// <summary>
		///     Event handler for the TabControl's SelectionChanged event.
		///     This method manages tab selection changes in the TabControl and performs various actions based on the user's
		///     interaction.
		///     When the user selects a different tab, the method first checks if an internal tab change is being ignored. If so,
		///     it immediately returns without performing any further actions.
		///     Additionally, this method relies on a component being currently loaded for proper operation. If no component is
		///     loaded, the method will log a verbose
		///     message, indicating that the tab functionality won't work until a component is loaded.
		///     The method identifies the last selected tab and the newly selected tab and logs their headers to provide user
		///     feedback about their selections.
		///     However, it assumes that the TabControl's SelectionChanged event arguments will always have valid items.
		///     If not, it will log a verbose message, indicating that it couldn't resolve the tab item.
		///     **Caution**: The method tries to resolve the names of the tabs based on their headers, and it assumes that this
		///     information will be available.
		///     If any tab lacks a header, it may lead to unexpected behavior or errors.
		///     If there are no components in the MainConfig or the current component is null, the method defaults to the initial
		///     tab and logs a verbose message.
		///     However, the conditions under which a component is considered "null" or whether the MainConfig contains any valid
		///     components are not explicitly detailed in this method.
		///     The method then compares the names of the current and last selected tabs in lowercase to detect if the user clicked
		///     on the same tab.
		///     If so, it logs a message and returns without performing any further actions.
		///     **Warning**: The logic in this method may trigger swapping of tabs based on certain conditions, such as selecting
		///     the "raw edit" tab or changing from the "raw edit" tab to another.
		///     It is important to be aware of these tab-swapping behaviors to avoid unexpected changes in the user interface.
		///     The method determines whether the tab should be swapped based on the selected tab's name.
		///     If the new tab is "raw edit", it calls the LoadIntoRawEditTextBox method to check if the current component should
		///     be loaded into the raw editor.
		///     The specific criteria for loading a component into the raw editor are not detailed within this method.
		///     If the last tab was "raw edit", the method checks if changes should be saved before swapping to the new tab.
		///     The method finally decides whether to prevent the tab change and returns accordingly.
		///     Depending on the conditions mentioned earlier, tab swapping may be cancelled, which might not be immediately
		///     apparent to the user.
		///     Furthermore, this method modifies the visibility of certain UI elements (RawEditTextBox and ApplyEditorButton)
		///     based on the selected tab.
		///     Specifically, it shows or hides these elements when the "raw edit" tab is selected, which could impact user
		///     interactions if not understood properly.
		/// </summary>
		/// <param name="sender">The object that raised the event (expected to be a TabControl).</param>
		/// <param name="e">The event arguments containing information about the selection change.</param>
		[UsedImplicitly]
		private async void TabControl_SelectionChanged([NotNull] object sender, [NotNull] SelectionChangedEventArgs e)
		{
			try
			{
				if ( IgnoreInternalTabChange )
					return;

				try
				{
					if ( !(sender is TabControl tabControl) )
					{
						await Logger.LogErrorAsync(message: "Sender is not a TabControl control");
						return;
					}

					if ( CurrentComponent is null )
					{
						await Logger.LogVerboseAsync(message: "No component loaded, tabs can't be used until one is loaded first.");
						SetTabInternal(tabControl, InitialTab);
						return;
					}

					// Get the last selected TabItem
					// ReSharper disable once PossibleNullReferenceException
					if ( e.RemovedItems.IsNullOrEmptyOrAllNull() || !(e.RemovedItems[0] is TabItem lastSelectedTabItem) )
					{
						await Logger.LogVerboseAsync(message: "Previous tab item could not be resolved somehow?");
						return;
					}

					await Logger.LogVerboseAsync($"User is attempting to swap from: {lastSelectedTabItem.Header}");

					// Get the new selected TabItem
					// ReSharper disable once PossibleNullReferenceException
					if ( e.AddedItems.IsNullOrEmptyOrAllNull() || !(e.AddedItems[0] is TabItem attemptedTabSelection) )
					{
						await Logger.LogVerboseAsync("Attempted tab item could not be resolved somehow?");
						return;
					}

					await Logger.LogVerboseAsync($"User is attempting to swap to: {attemptedTabSelection.Header}");

					// Don't show content of any tabs (except the hidden one) if there's no content.
					if ( MainConfig.AllComponents.IsNullOrEmptyCollection() || CurrentComponent is null )
					{
						SetTabInternal(tabControl, InitialTab);
						await Logger.LogVerboseAsync("No config loaded, defaulting to initial tab.");
						return;
					}

					string tabName = GetControlNameFromHeader(attemptedTabSelection)?.ToLowerInvariant();
					string lastTabName = GetControlNameFromHeader(lastSelectedTabItem)?.ToLowerInvariant();

					// do nothing if clicking the same tab
					if ( tabName == lastTabName )
					{
						await Logger.LogVerboseAsync($"Selected tab is already the current tab '{tabName}'");
						return;
					}


					bool shouldSwapTabs = true;
					if ( tabName == "raw edit" )
					{
						shouldSwapTabs = await LoadIntoRawEditTextBox(CurrentComponent);
					}
					else if ( lastTabName == "raw edit" )
					{
						shouldSwapTabs = await ShouldSaveChanges();
						if ( shouldSwapTabs )
						{
							// unload the raw editor
							RawEditTextBox.Text = string.Empty;
						}
					}

					// Prevent the attempted tab change
					if ( !shouldSwapTabs )
					{
						SetTabInternal(tabControl, lastSelectedTabItem);
						return;
					}

					// Show/hide the appropriate content based on the selected tab
					RawEditTextBox.IsVisible = tabName == "raw edit";
					ApplyEditorButton.IsVisible = tabName == "raw edit";
				}
				catch ( Exception exception )
				{
					await Logger.LogExceptionAsync(exception);
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				// Get the ComboBox
				if ( !(sender is ComboBox comboBox) )
				{
					Logger.Log(message: "Sender is not a ComboBox.");
					return;
				}

				// Get the instruction
				if ( !(comboBox.DataContext is Instruction thisInstruction) )
				{
					Logger.Log(message: "ComboBox's DataContext must be an instruction for this method.");
					return;
				}

				// Get the selected item
				string selectedItem = comboBox.SelectedItem as string;

				// Convert Items to a List<string> and find the index
				var itemsList = comboBox.Items.Cast<string>().ToList();
				int index = itemsList.IndexOf(selectedItem);

				// Assign to instruction.
				thisInstruction.Arguments = index.ToString();
				thisInstruction.Action = Instruction.ActionType.Patcher;
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}

		[CanBeNull]
		private TabItem GetCurrentTabItem([CanBeNull] TabControl tabControl) =>
			(tabControl ?? TabControl)?.SelectedItem as TabItem;

		[CanBeNull]
		private static string GetControlNameFromHeader([CanBeNull] TabItem tabItem) => tabItem?.Header?.ToString();

		private void SetTabInternal([NotNull] TabControl tabControl, TabItem tabItem)
		{
			if ( tabControl is null )
				throw new ArgumentNullException(nameof(tabControl));

			IgnoreInternalTabChange = true;
			tabControl.SelectedItem = tabItem;
			IgnoreInternalTabChange = false;
		}

		private async void LoadComponentDetails([NotNull] Component selectedComponent)
		{
			if ( selectedComponent == null )
				throw new ArgumentNullException(nameof(selectedComponent));

			bool confirmLoadOverwrite = true;
			if ( GetControlNameFromHeader(GetCurrentTabItem(TabControl))?.ToLowerInvariant() == "raw edit" )
				confirmLoadOverwrite = await LoadIntoRawEditTextBox(selectedComponent);
			else if ( selectedComponent != CurrentComponent )
				confirmLoadOverwrite = await ShouldSaveChanges();

			if ( !confirmLoadOverwrite )
				return;

			// set the currently tracked component to what's being loaded.
			SetCurrentComponent(selectedComponent);

			// default to SummaryTabItem.
			if ( InitialTab.IsSelected || TabControl.SelectedIndex == int.MaxValue )
				SetTabInternal(TabControl, SummaryTabItem);
		}

		public void SetCurrentComponent([CanBeNull] Component c)
		{
			CurrentComponent = c;
			GuiEditGrid.DataContext = c;

			if ( c == null )
				return;
			// Make tabs visible when a component is selected
			SummaryTabItem.IsVisible = true;
			GuiEditTabItem.IsVisible = true;
			RawEditTabItem.IsVisible = true;

			// Switch to Summary tab
			SetTabInternal(TabControl, SummaryTabItem);
		}

		private async Task<bool> LoadIntoRawEditTextBox([NotNull] Component selectedComponent)
		{
			if ( selectedComponent is null )
				throw new ArgumentNullException(nameof(selectedComponent));

			_ = Logger.LogVerboseAsync($"Loading '{selectedComponent.Name}' into the raw editor...");
			if ( CurrentComponentHasChanges() )
			{
				bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					"You're attempting to load the component into the raw editor, but"
					+ " there may be unsaved changes still in the editor. Really continue?"
				);

				// double check with user before overwrite
				if ( confirmResult != true )
					return false;
			}

			// populate raw editor
			RawEditTextBox.Text = selectedComponent.SerializeComponent();

			return true;
		}

		// todo: figure out if this is needed.
		// ReSharper disable once MemberCanBeMadeStatic.Local
		private void RawEditTextBox_LostFocus([NotNull] object sender, [NotNull] RoutedEventArgs e) => e.Handled = true;

		private bool CurrentComponentHasChanges() => CurrentComponent != null
													&& !string.IsNullOrWhiteSpace(RawEditTextBox.Text)
													&& RawEditTextBox.Text != CurrentComponent.SerializeComponent();

		/// <summary>
		/// Loads a TOML file with merge strategy choice
		/// </summary>
		/// <param name="filePath">Path to the TOML file</param>
		/// <param name="fileType">Type of file for display purposes</param>
		/// <returns>True if successful, false if cancelled or failed</returns>
		private async Task<bool> LoadTomlFile(string filePath, string fileType = "instruction file")
		{
			try
			{
				// Verify the file
				var fileInfo = new FileInfo(filePath);
				const int maxInstructionSize = 524288000; // instruction file larger than 500mb is probably unsupported
				if ( fileInfo.Length > maxInstructionSize )
				{
					await Logger.LogAsync($"Invalid {fileType} selected: '{fileInfo.Name}' - file too large");
					return false;
				}

				// Load components from the TOML file
				List<Component> newComponents = Component.ReadComponentsFromFile(filePath);

				// If no existing components, just load the new ones
				if ( MainConfig.AllComponents.Count == 0 )
				{
					MainConfigInstance.allComponents = newComponents;
					_lastLoadedFileName = Path.GetFileName(filePath);
					await Logger.LogAsync($"Loaded {newComponents.Count} components from {fileType}.");
					await ProcessComponentsAsync(MainConfig.AllComponents);
					return true;
				}

				// Ask user what to do with existing components
				bool? result = await ShowConfigLoadConfirmation(fileType);

				if ( result == true ) // User clicked "Merge"
				{
					// Ask user which merge strategy to use
					MergeStrategy? mergeStrategy = await ShowMergeStrategyDialog();
					if ( mergeStrategy == null ) // User cancelled
						return false;

					// Show conflict resolution dialog
					var conflictDialog = new ComponentMergeConflictDialog(
						MainConfig.AllComponents,
						newComponents,
						"Currently Loaded Components",
						fileType,
						(existing, incoming) =>
						{
							// Match based on selected strategy
							if ( mergeStrategy.Value == MergeStrategy.ByGuid )
							{
								return existing.Guid == incoming.Guid;
							}
							else
							{
								// Use fuzzy matching for name/author
								return FuzzyMatcher.FuzzyMatchComponents(existing, incoming);
							}
						});

					await conflictDialog.ShowDialog(this);

					if ( conflictDialog.UserConfirmed && conflictDialog.MergedComponents != null )
					{
						int originalCount = MainConfig.AllComponents.Count;
						MainConfigInstance.allComponents = conflictDialog.MergedComponents;
						int newCount = MainConfig.AllComponents.Count;
						_lastLoadedFileName = Path.GetFileName(filePath);

						string strategyName = mergeStrategy.Value == MergeStrategy.ByGuid ? "GUID matching" : "name/author matching";
						await Logger.LogAsync($"Merged {newComponents.Count} components from {fileType} with existing {originalCount} components using {strategyName}. Total components now: {newCount}");
					}
					else
					{
						await Logger.LogAsync("Merge cancelled by user.");
						return false;
					}
				}
				else if ( result == false ) // User clicked "Overwrite"
				{
					// Replace all existing components with new ones
					MainConfigInstance.allComponents = newComponents;
					_lastLoadedFileName = Path.GetFileName(filePath);
					await Logger.LogAsync($"Overwrote existing config with {newComponents.Count} components from {fileType}.");
				}
				else // User cancelled
				{
					return false;
				}

				await ProcessComponentsAsync(MainConfig.AllComponents);
				return true;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}

		/// <summary>
		/// Shows a dialog to choose merge strategy for TOML or markdown files
		/// </summary>
		/// <param name="fileType">Type of file being loaded (e.g., "TOML", "markdown")</param>
		/// <returns>Selected merge strategy or null if cancelled</returns>
		private async Task<MergeStrategy?> ShowMergeStrategyDialog(string fileType = "TOML")
		{
			string confirmText = $"How would you like to merge the {fileType} components?\n\n" +
								"• GUID Matching: Matches components by their unique GUID (recommended for TOML files)\n" +
								"• Name/Author Matching: Matches components by name and author using fuzzy matching";

			bool? result = await ConfirmationDialog.ShowConfirmationDialog(this, confirmText, yesButtonText: "GUID Matching", noButtonText: "Name/Author Matching");

			if ( result == null ) return null; // Cancelled
			return result == true ? MergeStrategy.ByGuid : MergeStrategy.ByNameAndAuthor;
		}

		/// <summary>
		///     Asynchronous method that determines if changes should be saved before performing an action.
		///     This method checks if the current component has any changes and prompts the user for confirmation if necessary.
		///     The method attempts to deserialize the raw config text from the "RawEditTextBox" into a new Component instance.
		///     If the deserialization process fails due to syntax errors, it will display a confirmation dialog to the user
		///     despite the 'noPrompt' boolean,
		///     offering to discard the changes and continue with the last attempted action. If the user chooses to discard,
		///     the method returns true, indicating that the changes should not be saved.
		///     The method then tries to find the corresponding component in the "MainConfig.AllComponents" collection.
		///     If the index of the current component cannot be found or is out of range, the method logs an error,
		///     displays an information dialog to the user, and returns false, indicating that the changes cannot be saved.
		///     If all checks pass successfully, the method updates the properties of the component in the
		///     "MainConfig.AllComponents" collection
		///     with the deserialized new component, sets the current component to the new one, and refreshes the tree view to
		///     reflect the changes.
		///     **Note**: This method involves multiple asynchronous operations and may not complete immediately.
		///     Any unexpected exceptions that occur during the process are caught, logged, and displayed to the user via an
		///     information dialog.
		/// </summary>
		/// <param name="noPrompt">A boolean flag indicating whether the user should be prompted to save changes. Default is false.</param>
		/// <returns>
		///     True if the changes should be saved or if no changes are detected. False if the user chooses not to save or if
		///     an error occurs.
		/// </returns>
		private async Task<bool> ShouldSaveChanges(bool noPrompt = false)
		{
			string output;
			try
			{
				if ( !CurrentComponentHasChanges() )
				{
					await Logger.LogVerboseAsync(message: "No changes detected, ergo nothing to save.");
					return true;
				}

				if ( !noPrompt
					&& await ConfirmationDialog.ShowConfirmationDialog(
						this,
						confirmText: "Are you sure you want to save?"
					)
					!= true )
				{
					return false;
				}

				// Get the selected component from the tree view
				if ( CurrentComponent is null )
				{
					output = "CurrentComponent is null which shouldn't ever happen in this context."
							+ Environment.NewLine
							+ "Please report this issue to a developer, this should never happen.";

					await Logger.LogErrorAsync(output);
					await InformationDialog.ShowInformationDialog(this, output);
					return false;
				}

				if ( RawEditTextBox.Text == null )
					return true;
				var newComponent = Component.DeserializeTomlComponent(RawEditTextBox.Text);
				if ( newComponent is null )
				{
					bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
						this,
						"Could not deserialize your raw config text into a Component instance in memory."
						+ " There may be syntax errors, check the output window for details."
						+ Environment.NewLine
						+ Environment.NewLine
						+ "Would you like to discard your changes and continue with your last attempted action?"
					);

					return confirmResult == true;
				}

				// Find the corresponding component in the collection
				int index = MainConfig.AllComponents.IndexOf(CurrentComponent);
				if ( index == -1 )
				{
					string componentName = string.IsNullOrWhiteSpace(newComponent.Name)
						? "."
						: $" '{newComponent.Name}'.";
					output = $"Could not find the index of component{componentName}"
							 + " Ensure you single-clicked on a component on the left before pressing save."
							 + " Please back up your work and try again.";
					await Logger.LogErrorAsync(output);
					await InformationDialog.ShowInformationDialog(this, output);

					return false;
				}

				// Update the properties of the existing component while preserving GUID
				Component existingComponent = MainConfigInstance.allComponents[index];

				// Copy all properties from newComponent to existingComponent
				existingComponent.Name = newComponent.Name;
				existingComponent.Author = newComponent.Author;
				existingComponent.Category = newComponent.Category;
				existingComponent.Tier = newComponent.Tier;
				existingComponent.Description = newComponent.Description;
				existingComponent.Directions = newComponent.Directions;
				existingComponent.InstallationMethod = newComponent.InstallationMethod;
				existingComponent.ModLink = newComponent.ModLink;
				existingComponent.Language = newComponent.Language;
				existingComponent.Dependencies = newComponent.Dependencies;
				existingComponent.Restrictions = newComponent.Restrictions;
				existingComponent.InstallAfter = newComponent.InstallAfter;
				existingComponent.Options = newComponent.Options;
				existingComponent.Instructions = newComponent.Instructions;

				SetCurrentComponent(existingComponent);

				// Refresh the tree view to reflect the changes
				await ProcessComponentsAsync(MainConfig.AllComponents);
				await Logger.LogAsync($"Saved '{newComponent.Name}' successfully. Refer to the output window for more information.");

				return true;
			}
			catch ( Exception ex )
			{
				output = "An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialog(this, output + Environment.NewLine + ex.Message);
				return false;
			}
		}

		private async Task<bool?> ShowConfigLoadConfirmation(string fileType)
		{
			if ( MainConfig.AllComponents.Count == 0 )
				return true; // No existing config, proceed without confirmation

			// If editor mode is disabled, always overwrite (return false means overwrite)
			if ( !EditorMode )
				return false;

			string confirmText = $"You already have a config loaded. Do you want to merge the {fileType} with existing components or load it as a new config?";
			return await ConfirmationDialog.ShowConfirmationDialog(this, confirmText, yesButtonText: "Merge", noButtonText: "Load as New");
		}

		private async void MoveComponentListItem([CanBeNull] Component componentToMove, int relativeIndex)
		{
			try
			{
				if ( componentToMove == null )
					return;

				_ = _modManagementService.MoveModRelative(componentToMove, relativeIndex);
				await ProcessComponentsAsync(MainConfig.AllComponents);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private void OnModOperationCompleted(object sender, ModOperationEventArgs e)
		{
			try
			{
				switch ( e.Operation )
				{
					case ModOperation.Create:
					case ModOperation.Delete:
					case ModOperation.Move:
						Dispatcher.UIThread.Post(async () =>
						{
							try
							{
								await ProcessComponentsAsync(MainConfig.AllComponents);
								UpdateModCounts();
							}
							catch ( Exception ex )
							{
								await Logger.LogExceptionAsync(ex);
							}
						});
						break;
					case ModOperation.Read:
					case ModOperation.Update:
					case ModOperation.Duplicate:
					case ModOperation.AddDependency:
					case ModOperation.RemoveDependency:
					case ModOperation.AddRestriction:
					case ModOperation.RemoveRestriction:
					case ModOperation.Batch:
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void OnModValidationCompleted(object sender, ModValidationEventArgs e)
		{
			try
			{
				// Update UI to reflect validation results
				Dispatcher.UIThread.Post(UpdateModCounts);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private void MoveUpButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) =>
			MoveComponentListItem(CurrentComponent, relativeIndex: -1);

		[UsedImplicitly]
		private void MoveDownButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) =>
			MoveComponentListItem(CurrentComponent, relativeIndex: 1);

		[UsedImplicitly]
		private void GenerateGuidButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				GuidGeneratedTextBox.Text = Guid.NewGuid().ToString();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private async void SaveModFileAs_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				string defaultFileName = !string.IsNullOrEmpty(_lastLoadedFileName) ? _lastLoadedFileName : "my_toml_instructions.toml";
				string filePath = await SaveFile(
					saveFileName: defaultFileName
				);
				if ( filePath is null )
					return;

				await Logger.LogVerboseAsync($"Saving TOML config to {filePath}");

				using ( var writer = new StreamWriter(filePath) )
				{
					foreach ( Component c in MainConfig.AllComponents )
					{
						string tomlContents = c.SerializeComponent();
						await writer.WriteLineAsync(tomlContents);
					}
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		public void ComponentCheckboxChecked(
			[NotNull] Component component,
				[NotNull] HashSet<Component> visitedComponents,
				bool suppressErrors = false
			)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));
			if ( visitedComponents is null )
				throw new ArgumentNullException(nameof(visitedComponents));

			try
			{
				// Check if the component has already been visited
				if ( visitedComponents.Contains(component) )
				{
					// Conflicting component that cannot be resolved automatically
					if ( !suppressErrors )
						Logger.LogError($"Component '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
				}

				// Add the component to the visited set
				_ = visitedComponents.Add(component);

				Dictionary<string, List<Component>> conflicts = Component.GetConflictingComponents(
					component.Dependencies,
					component.Restrictions,
					MainConfig.AllComponents
				);

				// Handling conflicts based on what's defined for THIS component
				if ( conflicts.TryGetValue(key: "Dependency", out List<Component> dependencyConflicts) )
				{
					// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
					foreach ( Component conflictComponent in dependencyConflicts )
					{
						// ReSharper disable once InvertIf
						if ( conflictComponent?.IsSelected == false )
						{
							conflictComponent.IsSelected = true;
							ComponentCheckboxChecked(conflictComponent, visitedComponents);
						}
					}
				}

				if ( conflicts.TryGetValue(key: "Restriction", out List<Component> restrictionConflicts) )
				{
					// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
					foreach ( Component conflictComponent in restrictionConflicts )
					{
						// ReSharper disable once InvertIf
						if ( conflictComponent?.IsSelected == true )
						{
							conflictComponent.IsSelected = false;
							ComponentCheckboxUnchecked(conflictComponent, visitedComponents);
						}
					}
				}

				// Handling OTHER component's defined restrictions based on the change to THIS component.
				// ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
				foreach ( Component c in MainConfig.AllComponents )
				{
					if ( !c.IsSelected || !c.Restrictions.Contains(component.Guid) )
						continue;

					c.IsSelected = false;
					ComponentCheckboxUnchecked(c, visitedComponents);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
			}
		}

		public void ComponentCheckboxUnchecked(
			[NotNull] Component component,
				[CanBeNull] HashSet<Component> visitedComponents,
				bool suppressErrors = false
			)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));

			visitedComponents = visitedComponents ?? new HashSet<Component>();
			try
			{
				// Check if the component has already been visited
				if ( visitedComponents.Contains(component) )
				{
					// Conflicting component that cannot be resolved automatically
					if ( !suppressErrors )
						Logger.LogError($"Component '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
				}

				// Update the select all checkbox state
				if ( !suppressErrors )
					UpdateModCounts();

				// Add the component to the visited set
				_ = visitedComponents.Add(component);

				// Handling OTHER component's defined dependencies based on the change to THIS component.
				foreach ( Component c in MainConfig.AllComponents.Where(c => c.IsSelected && c.Dependencies.Contains(component.Guid)) )
				{
					c.IsSelected = false;
					ComponentCheckboxUnchecked(c, visitedComponents);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
			}
		}

		// Set up the event handler for the checkbox
		private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( !(sender is CheckBox checkBox) )
					return;
				if ( !(checkBox.Tag is Component thisComponent) )
					return;

				if ( checkBox.IsChecked == true )
					ComponentCheckboxChecked(thisComponent, new HashSet<Component>());
				else if ( checkBox.IsChecked == false )
					ComponentCheckboxUnchecked(thisComponent, new HashSet<Component>());
				else
					Logger.LogVerbose($"Could not determine new checkBox checked bool for {thisComponent.Name}");

				// Update step progress when mod selection changes
				UpdateStepProgress();
				UpdateModCounts();
			}
			catch ( Exception exception )
			{
				Console.WriteLine(exception);
			}
		}

		// Public method that can be called from ModListItem
		public void OnComponentCheckBoxChanged(object sender, RoutedEventArgs e) => OnCheckBoxChanged(sender, e);

		private async Task ProcessComponentsAsync([NotNull][ItemNotNull] List<Component> componentsList)
		{
			try
			{
				// Clear the existing list
				ModListBox?.Items.Clear();

				// Use the ComponentProcessingService to process components
				ComponentProcessingResult result = await ComponentProcessingService.ProcessComponentsAsync(componentsList);

				if ( result.IsEmpty )
				{
					// Show empty state or hide tabs when no components
					SummaryTabItem.IsVisible = false;
					GuiEditTabItem.IsVisible = false;
					RawEditTabItem.IsVisible = false;
					SetTabInternal(TabControl, InitialTab);
					UpdateStepProgress();
					UpdateModCounts();
					return;
				}

				if ( !result.Success )
				{
					if ( !result.HasCircularDependencies )
						return;

					// Detect cycles and show resolution dialog
					CircularDependencyDetector.CircularDependencyResult cycleInfo =
						CircularDependencyDetector.DetectCircularDependencies(componentsList);

					// Only show dialog if there are actual circular dependencies
					if ( !cycleInfo.HasCircularDependencies || cycleInfo.Cycles.Count <= 0 )
						return;
					(bool retry, List<Component> resolvedComponents) = await CircularDependencyResolutionDialog.ShowResolutionDialog(
						this,
						componentsList,
						cycleInfo);

					if ( retry && resolvedComponents != null )
					{
						// User made changes and wants to retry - recursively call this method
						await ProcessComponentsAsync(resolvedComponents);
					}
					return;
				}

				// Use reordered components if available
				List<Component> componentsToProcess = result.ReorderedComponents ?? result.Components;

				// Populate the list box with components
				PopulateModList(componentsToProcess);

				// Refresh category filter checkboxes with loaded components
				RefreshCategoryCheckboxes();

				if ( componentsToProcess.Count > 0 || TabControl is null )
				{
					// Show the tabs when components are loaded
					SummaryTabItem.IsVisible = true;
					GuiEditTabItem.IsVisible = true;
					RawEditTabItem.IsVisible = true;

					// Update step progress after components are loaded
					UpdateStepProgress();
					return;
				}

				// Hide the tabs when no components are loaded
				SummaryTabItem.IsVisible = false;
				GuiEditTabItem.IsVisible = false;
				RawEditTabItem.IsVisible = false;

				SetTabInternal(TabControl, InitialTab);
				// Update step progress after initial tab is set
				UpdateStepProgress();
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void AddNewInstruction_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var addButton = (Button)sender;
				var thisInstruction = addButton.Tag as Instruction;
				var thisComponent = addButton.Tag as Component;

				if ( thisInstruction is null && thisComponent is null )
					throw new NullReferenceException(message: "Cannot find instruction instance from button.");

				int index;
				if ( !(thisComponent is null) )
				{
					thisInstruction = new Instruction();
					index = thisComponent.Instructions.Count;
					thisComponent.CreateInstruction(index);
				}
				else
				{
					Component parentComponent = thisInstruction.GetParentComponent();
					index = parentComponent.Instructions.IndexOf(thisInstruction);
					parentComponent.CreateInstruction(index);
				}

				await Logger.LogVerboseAsync($"Component '{CurrentComponent.Name}': Instruction '{thisInstruction.Action}' created at index #{index}");

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void DeleteInstruction_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				Instruction thisInstruction = (Instruction)((Button)sender).Tag
					?? throw new NullReferenceException($"Could not get instruction instance from button's tag: {((Button)sender).Content}");
				int index = thisInstruction.GetParentComponent().Instructions.IndexOf(thisInstruction);

				thisInstruction.GetParentComponent().DeleteInstruction(index);
				await Logger.LogVerboseAsync($"Component '{CurrentComponent.Name}': instruction '{thisInstruction.Action}' deleted at index #{index}");

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveInstructionUp_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisInstruction = (Instruction)((Button)sender).Tag;
				int index = CurrentComponent.Instructions.IndexOf(thisInstruction);

				if ( thisInstruction is null )
				{
					await Logger.LogExceptionAsync(new InvalidOperationException(message: "The sender does not correspond to a instruction."));
					return;
				}

				CurrentComponent.MoveInstructionToIndex(thisInstruction, index - 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveInstructionDown_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisInstruction = (Instruction)((Button)sender).Tag;
				int index = CurrentComponent.Instructions.IndexOf(thisInstruction);

				if ( thisInstruction is null )
					throw new NullReferenceException($"Could not get instruction instance from button's tag: {((Button)sender).Content}");

				CurrentComponent.MoveInstructionToIndex(thisInstruction, index + 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private void OpenOutputWindow_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			if ( _outputWindow?.IsVisible == true )
				_outputWindow.Close();

			_outputWindow = new OutputWindow();
			_outputWindow.Show();
		}

		[UsedImplicitly]
		private async void OpenSettings_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				var settingsDialog = new SettingsDialog();
				settingsDialog.InitializeFromMainWindow(this);

				bool result = await settingsDialog.ShowDialog<bool>(this);
				if ( result )
				{
					// Apply theme changes
					string selectedTheme = settingsDialog.GetSelectedTheme();
					ApplyTheme(selectedTheme);
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		private static void ApplyTheme(string stylePath)
		{
			try
			{
				ThemeManager.UpdateStyle(stylePath);
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}

		[UsedImplicitly]
		private async void StyleComboBox_SelectionChanged(
			[NotNull] object sender,
			[NotNull] SelectionChangedEventArgs e
		)
		{
			try
			{
				if ( _initialize )
				{
					_initialize = false;
					return;
				}

				if ( !(sender is ComboBox comboBox) )
					return;

				var selectedItem = (ComboBoxItem)comboBox.SelectedItem;
				if ( !(selectedItem?.Tag is string stylePath) )
				{
					await Logger.LogErrorAsync("stylePath cannot be rendered from tag, returning immediately");
					return;
				}


				ThemeManager.UpdateStyle(stylePath);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

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
		private async void AddNewOption_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var addButton = (Button)sender;
				var thisOption = addButton.Tag as Option;
				var thisComponent = addButton.Tag as Component;

				if ( thisOption is null && thisComponent is null )
					throw new NullReferenceException("Cannot find option instance from button.");

				int index;
				if ( thisOption is null )
				{
					thisOption = new Option();
					index = CurrentComponent.Options.Count;
				}
				else
				{
					index = CurrentComponent.Options.IndexOf(thisOption);
				}

				CurrentComponent.CreateOption(index);
				await Logger.LogVerboseAsync($"Component '{CurrentComponent.Name}': Option '{thisOption.Name}' created at index #{index}");

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void DeleteOption_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisOption = (Option)((Button)sender).Tag;
				int index = CurrentComponent.Options.IndexOf(thisOption);

				CurrentComponent.DeleteOption(index);
				await Logger.LogVerboseAsync($"Component '{CurrentComponent.Name}': instruction '{thisOption?.Name}' deleted at index #{index}");

				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveOptionUp_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisOption = (Option)((Button)sender).Tag;
				int index = CurrentComponent.Options.IndexOf(thisOption);

				if ( thisOption is null )
					throw new NullReferenceException($"Could not get option instance from button's tag: {((Button)sender).Content}");

				CurrentComponent.MoveOptionToIndex(thisOption, index - 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void MoveOptionDown_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}

				var thisOption = (Option)((Button)sender).Tag;
				int index = CurrentComponent.Options.IndexOf(thisOption);

				if ( thisOption is null )
					throw new NullReferenceException($"Could not get option instance from button's tag: {((Button)sender).Content}");

				CurrentComponent.MoveOptionToIndex(thisOption, index + 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async void CopyTextToClipboard_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( Clipboard is null )
					throw new NullReferenceException(nameof(Clipboard));

				await Clipboard.SetTextAsync((string)((MenuItem)sender).DataContext);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		// Getting Started Tab Event Handlers
		private void HomeButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				TabControl tabControl = this.FindControl<TabControl>("TabControl");
				TabItem initialTab = this.FindControl<TabItem>("InitialTab");
				if ( tabControl != null && initialTab != null )
					SetTabInternal(tabControl, initialTab);
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}

		[UsedImplicitly]
		private async void Step1Button_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Open the Set Directories dialog
				await ShowSetDirectoriesDialog();
				UpdateStepProgress();
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async void Step2Button_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Trigger load instruction file
				await LoadInstructionFile();
				UpdateStepProgress();
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async void GettingStartedValidateButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await RunValidation();
				UpdateStepProgress();
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async void InstallButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await StartInstallation();
				UpdateStepProgress();
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		private async Task ShowSetDirectoriesDialog()
		{
			// Navigate to the Set Directories tab or open dialog
			Menu topMenu = this.FindControl<Menu>(name: "TopMenu");
			if ( topMenu?.Items.Count > 0 && topMenu.Items[1] is MenuItem fileMenu )
			{
				if ( fileMenu.Items.Count > 1 && fileMenu.Items[1] is MenuItem setDirItem )
				{
					// Simulate clicking the Set Directories menu item
					await Task.Delay(millisecondsDelay: 100); // Brief delay for UI responsiveness
					SetDirectories_Click(setDirItem, new RoutedEventArgs());
				}
			}
		}

		private async Task LoadInstructionFile()
		{
			// Navigate to Load Installation File
			Menu topMenu = this.FindControl<Menu>(name: "TopMenu");
			if ( topMenu?.Items.Count > 0 && topMenu.Items[1] is MenuItem fileMenu )
			{
				if ( fileMenu.Items.Count > 0 && fileMenu.Items[0] is MenuItem loadFileItem )
				{
					// Simulate clicking the Load Installation File menu item
					await Task.Delay(millisecondsDelay: 100); // Brief delay for UI responsiveness
					LoadInstallFile_Click(loadFileItem, new RoutedEventArgs());
				}
			}
		}

		private async Task RunValidation()
		{
			// Run the validation process
			await Task.Delay(millisecondsDelay: 100); // Brief delay for UI responsiveness
			ValidateButton_Click(sender: null, new RoutedEventArgs());
		}

		private async Task StartInstallation()
		{
			// Start the installation process
			await Task.Delay(millisecondsDelay: 100); // Brief delay for UI responsiveness
			StartInstall_Click(sender: null, new RoutedEventArgs());
		}

		private void UpdateStepProgress()
		{
			try
			{
				// Find the step completion indicators and progress bar
				Border step1Border = this.FindControl<Border>("Step1Border");
				Border step1Indicator = this.FindControl<Border>("Step1CompleteIndicator");
				TextBlock step1Text = this.FindControl<TextBlock>("Step1CompleteText");

				Border step2Border = this.FindControl<Border>("Step2Border");
				Border step2Indicator = this.FindControl<Border>("Step2CompleteIndicator");
				TextBlock step2Text = this.FindControl<TextBlock>("Step2CompleteText");

				Border step3Border = this.FindControl<Border>("Step3Border");
				Border step3Indicator = this.FindControl<Border>("Step3CompleteIndicator");
				TextBlock step3Text = this.FindControl<TextBlock>("Step3CompleteText");

				Border step4Border = this.FindControl<Border>("Step4Border");
				Border step4Indicator = this.FindControl<Border>("Step4CompleteIndicator");
				TextBlock step4Text = this.FindControl<TextBlock>("Step4CompleteText");

				ProgressBar progressBar = this.FindControl<ProgressBar>("OverallProgressBar");
				TextBlock progressText = this.FindControl<TextBlock>("ProgressText");

				bool canUpdateProgress = progressBar != null && progressText != null;

				// Check Step 1: Directories are set
				bool step1Complete = !string.IsNullOrEmpty(MainConfig.SourcePath?.FullName) &&
									!string.IsNullOrEmpty(MainConfig.DestinationPath?.FullName);
				UpdateStepCompletion(step1Border, step1Indicator, step1Text, step1Complete);

				// Check Step 2: Components are loaded (only counts after Step 1)
				bool step2Complete = step1Complete && MainConfig.AllComponents?.Count > 0;
				UpdateStepCompletion(step2Border, step2Indicator, step2Text, step2Complete);

				// Check Step 3: At least one component is selected (always complete if any component is selected)
				bool step3Complete = MainConfig.AllComponents?.Any(c => c.IsSelected) == true;
				UpdateStepCompletion(step3Border, step3Indicator, step3Text, step3Complete);

				// Check Step 4: Installation completed (only counts after Step 3)
				bool step4Complete = step3Complete && step4Indicator?.Background != null &&
									!Equals(step4Indicator.Background, Brushes.Transparent);
				UpdateStepCompletion(step4Border, step4Indicator, step4Text, step4Complete);

				// Update progress bar (0-4 scale)
				int completedSteps = (step1Complete ? 1 : 0) + (step2Complete ? 1 : 0) +
									(step3Complete ? 1 : 0) + (step4Complete ? 1 : 0);
				if ( canUpdateProgress )
				{
					progressBar.Value = completedSteps;

					// Update progress text
					string[] messages = {
							"Complete the steps above to get started",
							"Great start! Continue with the next steps",
							"Almost there! Just a few more steps",
							"Excellent progress! You're almost ready",
							"🎉 All steps completed! You're ready to install mods",
						};
					progressText.Text = messages[Math.Min(completedSteps, messages.Length - 1)];
				}

				// No need to update displays - using reusable DirectoryPickerControl now
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}

		private static void UpdateStepCompletion(Border stepBorder, Border indicator, TextBlock text, bool isComplete)
		{
			if ( stepBorder == null || indicator == null || text == null ) return;

			if ( isComplete )
			{
				// COMPLETION EFFECT - Fill the entire step area with theme color
				stepBorder.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // Green
				stepBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Lighter green
				stepBorder.BorderThickness = new Thickness(3);

				indicator.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Lighter green
				text.Foreground = Brushes.White;
				text.Text = "🎉 COMPLETE! 🎉";
			}
			else
			{
				// Reset to normal state - let CSS styling handle the colors
				stepBorder.Background = Brushes.Transparent;
				// Remove hardcoded BorderBrush to let CSS classes take effect
				stepBorder.ClearValue(Border.BorderBrushProperty);
				stepBorder.BorderThickness = new Thickness(uniformLength: 2);

				indicator.Background = Brushes.Transparent;
				// Remove hardcoded Foreground to let CSS classes take effect
				text.ClearValue(TextBlock.ForegroundProperty);
				text.Text = "";
			}
		}

		// Directory Picker Event Handler - handles both sidebar and Step 1 directory changes
		private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
		{
			try
			{
				if ( e.PickerType == DirectoryPickerType.ModDirectory )
				{
					// Update MainConfig
					MainConfigInstance.sourcePath = new DirectoryInfo(e.Path);
					Logger.Log($"Mod directory set to: {e.Path}");

					// Update all mod directory pickers
					SyncDirectoryPickers(DirectoryPickerType.ModDirectory, e.Path);
				}
				else if ( e.PickerType == DirectoryPickerType.KotorDirectory )
				{
					// Update MainConfig
					MainConfigInstance.destinationPath = new DirectoryInfo(e.Path);
					Logger.Log($"KOTOR installation directory set to: {e.Path}");

					// Update all kotor directory pickers
					SyncDirectoryPickers(DirectoryPickerType.KotorDirectory, e.Path);
				}

				// Update step progress
				UpdateStepProgress();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void InitializeDirectoryPickers()
		{
			try
			{
				// Initialize current paths from MainConfig
				DirectoryPickerControl modPicker = this.FindControl<DirectoryPickerControl>("ModDirectoryPicker");
				DirectoryPickerControl kotorPicker = this.FindControl<DirectoryPickerControl>("KotorDirectoryPicker");
				DirectoryPickerControl step1ModPicker = this.FindControl<DirectoryPickerControl>("Step1ModDirectoryPicker");
				DirectoryPickerControl step1KotorPicker = this.FindControl<DirectoryPickerControl>("Step1KotorDirectoryPicker");

				if ( modPicker != null && MainConfig.SourcePath != null )
					modPicker.SetCurrentPath(MainConfig.SourcePath.FullName);

				if ( kotorPicker != null && MainConfig.DestinationPath != null )
					kotorPicker.SetCurrentPath(MainConfig.DestinationPath.FullName);

				if ( step1ModPicker != null && MainConfig.SourcePath != null )
					step1ModPicker.SetCurrentPath(MainConfig.SourcePath.FullName);

				if ( step1KotorPicker != null && MainConfig.DestinationPath != null )
					step1KotorPicker.SetCurrentPath(MainConfig.DestinationPath.FullName);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		public void SyncDirectoryPickers(DirectoryPickerType pickerType, string path)
		{
			try
			{
				var allPickers = new List<DirectoryPickerControl>();

				if ( pickerType == DirectoryPickerType.ModDirectory )
				{
					DirectoryPickerControl mainPicker = this.FindControl<DirectoryPickerControl>("ModDirectoryPicker");
					DirectoryPickerControl step1Picker = this.FindControl<DirectoryPickerControl>("Step1ModDirectoryPicker");

					if ( mainPicker != null ) allPickers.Add(mainPicker);
					if ( step1Picker != null ) allPickers.Add(step1Picker);
				}
				else if ( pickerType == DirectoryPickerType.KotorDirectory )
				{
					DirectoryPickerControl mainPicker = this.FindControl<DirectoryPickerControl>("KotorDirectoryPicker");
					DirectoryPickerControl step1Picker = this.FindControl<DirectoryPickerControl>("Step1KotorDirectoryPicker");

					if ( mainPicker != null ) allPickers.Add(mainPicker);
					if ( step1Picker != null ) allPickers.Add(step1Picker);
				}

				// Update all pickers with the new path
				foreach ( DirectoryPickerControl picker in allPickers ) picker.SetCurrentPath(path);

				// Update step progress when directories are synchronized from settings window
				UpdateStepProgress();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private void InitializeModDirectoryWatcher()
		{
			try
			{
				if ( MainConfig.SourcePath != null && Directory.Exists(MainConfig.SourcePath.FullName) )
					SetupModDirectoryWatcher(MainConfig.SourcePath.FullName);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to initialize mod directory watcher");
			}
		}

		private void SetupModDirectoryWatcher(string path)
		{
			try
			{
				// Dispose existing watcher if any
				_modDirectoryWatcher?.Dispose();

				// Create cross-platform file watcher
				_modDirectoryWatcher = new CrossPlatformFileWatcher(
					path: path,
					filter: "*.*",
					notifyFilters: NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
					includeSubdirectories: false
				);

				_modDirectoryWatcher.Created += OnModDirectoryChanged;
				_modDirectoryWatcher.Deleted += OnModDirectoryChanged;
				_modDirectoryWatcher.Changed += OnModDirectoryChanged;
				_modDirectoryWatcher.Error += OnModDirectoryWatcherError;

				// Start watching
				_modDirectoryWatcher.StartWatching();

				Logger.LogVerbose($"Cross-platform file system watcher initialized for: {path}");

				// Initial scan
				ScanModDirectoryForDownloads();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to setup mod directory watcher");
			}
		}

		private void OnModDirectoryChanged(object sender, FileSystemEventArgs e)
		{
			// Debounce file system events by dispatching to UI thread
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					ScanModDirectoryForDownloads();
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Error processing mod directory change");
				}
			}, DispatcherPriority.Background);
		}

		private void OnModDirectoryWatcherError(object sender, ErrorEventArgs e)
		{
			Logger.LogException(e.GetException(), "File watcher error occurred");

			// Attempt to restart the watcher
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					if (MainConfig.SourcePath != null && Directory.Exists(MainConfig.SourcePath.FullName))
					{
						Logger.LogVerbose("Attempting to restart file watcher after error");
						SetupModDirectoryWatcher(MainConfig.SourcePath.FullName);
					}
				}
				catch (Exception ex)
				{
					Logger.LogException(ex, "Failed to restart file watcher");
				}
			}, DispatcherPriority.Background);
		}

		private void ScanModDirectoryForDownloads()
		{
			try
			{
				if ( MainConfig.SourcePath == null || !Directory.Exists(MainConfig.SourcePath.FullName) )
					return;

				if ( MainConfig.AllComponents.Count == 0 )
					return;

				Logger.LogVerbose($"[FileValidation] Starting scan. Mod directory: {MainConfig.SourcePath.FullName}");

				int downloadedCount = 0;
				int totalSelected = 0;

				// Check each component using proper instruction-based validation
				foreach ( Component component in MainConfig.AllComponents )
				{
					if ( !component.IsSelected )
						continue;

					totalSelected++;

					// Use VirtualFileSystemProvider to properly validate file existence based on instructions
					bool hasAllFiles = ValidateComponentFilesExist(component);
					component.IsDownloaded = hasAllFiles;

					Logger.LogVerbose($"[FileValidation] Component '{component.Name}': {(hasAllFiles ? "DOWNLOADED" : "MISSING")}");

					if ( hasAllFiles ) downloadedCount++;
				}

				// Update download status text
				TextBlock statusText = this.FindControl<TextBlock>("DownloadStatusText");
				if ( statusText != null )
				{
					if ( totalSelected == 0 )
					{
						statusText.Text = "No mods selected for installation.";
						statusText.Foreground = Brushes.Gray;
					}
					else if ( downloadedCount == totalSelected )
					{
						statusText.Text = $"✅ All {totalSelected} selected mod(s) are downloaded!";
						statusText.Foreground = Brushes.Green;
					}
					else
					{
						statusText.Text = $"⚠️ {downloadedCount}/{totalSelected} selected mod(s) downloaded. {totalSelected - downloadedCount} missing.";
						statusText.Foreground = Brushes.Orange;
					}
				}

				Logger.LogVerbose($"Download scan complete: {downloadedCount}/{totalSelected} mods ready");

				// Refresh the mod list items to update tooltips and validation states
				RefreshModListItems();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error scanning mod directory for downloads");
			}
		}

		/// <summary>
		/// Validates that all required files for a component exist using instruction-based validation.
		/// This method properly checks file existence based on the actual instructions,
		/// rather than just doing simple filename matching.
		/// </summary>
		/// <param name="component">The component to validate</param>
		/// <returns>True if all required files exist, false otherwise</returns>
		private bool ValidateComponentFilesExist(Component component)
		{
			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )
				{
					// Components without instructions might still be valid (e.g., meta-mods with only dependencies)
					// So we return true here - they're considered "downloaded" as they don't require files
					return true;
				}

				// Check each instruction's source files
				foreach ( Instruction instruction in component.Instructions )
				{
					// Only validate instructions that require source files from the mod directory
					// Skip Choose (uses GUIDs), DelDuplicate (optional), Delete (optional), etc.
					bool requiresSourceFiles = instruction.Action == Instruction.ActionType.Extract ||
					                            instruction.Action == Instruction.ActionType.Execute ||
					                            instruction.Action == Instruction.ActionType.Patcher ||
					                            instruction.Action == Instruction.ActionType.Move ||
					                            instruction.Action == Instruction.ActionType.Copy;

					if ( !requiresSourceFiles )
						continue;

					if ( instruction.Source.Count == 0 )
					{
						// This instruction type requires source files but has none - this is a problem
						Logger.LogVerbose($"Component '{component.Name}': Instruction with action '{instruction.Action}' has no source files");
						return false;
					}

					foreach ( string source in instruction.Source )
					{
						// Skip kotorDirectory paths - those are destination files, not source files we need to download
						if ( source.Contains("<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase) )
							continue;

						// Replace custom variables in the source path
						string resolvedSource = Utility.ReplaceCustomVariables(source);

						// Use PathHelper to expand wildcards and get actual file paths
						// Use a real file system provider for actual file existence checks
						var realFileSystemProvider = new RealFileSystemProvider();
						List<string> expandedPaths = PathHelper.EnumerateFilesWithWildcards(
							new List<string> { resolvedSource },
							realFileSystemProvider,
							includeSubFolders: false
						);

						// If no files match the pattern, the component is missing files
						if ( expandedPaths == null || expandedPaths.Count == 0 )
						{
							Logger.LogVerbose($"Component '{component.Name}': No files found matching pattern: {resolvedSource}");
							return false;
						}

						// Check if all expanded paths actually exist using real file system
						foreach ( string expandedPath in expandedPaths )
						{
							if ( !File.Exists(expandedPath) )
							{
								Logger.LogVerbose($"Component '{component.Name}': File does not exist: {expandedPath}");
								return false;
							}
						}
					}
				}

				return true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error validating files for component '{component?.Name}'");
				return false;
			}
		}

		private async void ScrapeDownloadsButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( MainConfig.SourcePath == null || !Directory.Exists(MainConfig.SourcePath.FullName) )
				{
					await InformationDialog.ShowInformationDialog(this,
						"Please set your Mod Directory in Settings before downloading mods.");
					return;
				}

				if ( MainConfig.AllComponents.Count == 0 )
				{
					await InformationDialog.ShowInformationDialog(this,
						"Please load an instruction file before downloading mods.");
					return;
				}

				// Get selected components that need downloads
				var componentsToDownload = MainConfig.AllComponents
					.Where(c => c.IsSelected && !c.IsDownloaded && c.ModLink.Count > 0)
					.ToList();

				if ( componentsToDownload.Count == 0 )
				{
					await InformationDialog.ShowInformationDialog(this,
						"All selected mods are already downloaded, or no download links are available.");
					return;
				}

				// Create and show the download progress window
				var progressWindow = new DownloadProgressWindow();
				_currentDownloadWindow = progressWindow;

				// Create a dictionary to track download progress for each URL
				var urlToProgressMap = new Dictionary<string, DownloadProgress>();

				// Add all downloads to the progress window (grouped by mod)
				foreach ( Component component in componentsToDownload )
				{
					if ( component.ModLink.Count == 1 )
					{
						// Single URL - create individual download
						var progressItem = new DownloadProgress
						{
							ModName = component.Name,
							Url = component.ModLink[0],
							Status = DownloadStatus.Pending,
							StatusMessage = "Waiting to start...",
							ProgressPercentage = 0
						};

						progressWindow.AddDownload(progressItem);
						urlToProgressMap[component.ModLink[0]] = progressItem;
					}
					else
					{
						// Multiple URLs - create grouped download
						var groupedProgress = DownloadProgress.CreateGrouped(component.Name, component.ModLink);
						progressWindow.AddDownload(groupedProgress);

						// Add each child download to the URL map for the download manager
						foreach ( DownloadProgress childProgress in groupedProgress.ChildDownloads )
						{
							urlToProgressMap[childProgress.Url] = childProgress;
						}
					}
				}

				// Create download manager and handlers that will be shared
						var httpClient = new System.Net.Http.HttpClient();
						httpClient.Timeout = TimeSpan.FromMinutes(10);

						var handlers = new List<IDownloadHandler>
						{
										new DeadlyStreamDownloadHandler(httpClient),
										new MegaDownloadHandler(),
					new NexusModsDownloadHandler(httpClient, null),
							new GameFrontDownloadHandler(httpClient),
					new DirectDownloadHandler(httpClient),
						};

						var downloadManager = new DownloadManager(handlers);

				// Wire up download control events
				progressWindow.DownloadControlRequested += async (s, args) =>
				{
					await HandleDownloadControl(args, urlToProgressMap, downloadManager, progressWindow);
				};

				// Show the window
				progressWindow.Show();

				// Start downloads in background
				_ = Task.Run(async () =>
				{
					try
					{
						await Logger.LogVerboseAsync($"[Download] Starting download process for {componentsToDownload.Count} components");
						await Logger.LogVerboseAsync($"[Download] Starting download manager with {urlToProgressMap.Count} URLs");

						// Use the new progress-aware download method with cancellation support
						_ = await downloadManager.DownloadAllWithProgressAsync(
						urlToProgressMap,
						MainConfig.SourcePath.FullName,
						progressWindow.CancellationToken);

						await Logger.LogVerboseAsync("[Download] Download manager completed");

						// Mark downloads as completed
						progressWindow.MarkCompleted();

						// Refresh download status on UI thread
						await Dispatcher.UIThread.InvokeAsync(ScanModDirectoryForDownloads);
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex, "Error during mod download");
						await Dispatcher.UIThread.InvokeAsync(async () =>
						{
							await InformationDialog.ShowInformationDialog(this,
								$"An error occurred while downloading mods:\n\n{ex.Message}");
						});
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error starting download process");
				await InformationDialog.ShowInformationDialog(this,
					$"An error occurred while starting downloads:\n\n{ex.Message}");
			}
		}

		private async Task HandleDownloadControl(
			DownloadControlEventArgs args,
			Dictionary<string, DownloadProgress> urlToProgressMap,
			DownloadManager downloadManager,
			DownloadProgressWindow progressWindow)
		{
			try
			{
				DownloadProgress progress = args.Progress;
				await Logger.LogVerboseAsync($"[Download Control] {args.Action} requested for {progress.ModName}");

				switch ( args.Action )
				{
					case DownloadControlAction.Start:
						// Start downloads immediately
						if ( progress.Status == DownloadStatus.Pending )
						{
							if ( progress.IsGrouped )
							{
								// Start all child downloads
								progress.AddLog("Starting all downloads immediately (user requested)");
								_ = Task.Run(async () =>
								{
									try
									{
										var groupUrlMap = new Dictionary<string, DownloadProgress>();
										foreach ( DownloadProgress child in progress.ChildDownloads )
										{
											if ( child.Status == DownloadStatus.Pending && !string.IsNullOrEmpty(child.Url) )
											{
												groupUrlMap[child.Url] = child;
											}
										}

										if ( groupUrlMap.Count > 0 )
										{
											_ = await downloadManager.DownloadAllWithProgressAsync(
												groupUrlMap,
												MainConfig.SourcePath?.FullName ?? string.Empty,
												CancellationToken.None);
										}

										await Dispatcher.UIThread.InvokeAsync(ScanModDirectoryForDownloads);
									}
									catch ( Exception ex )
									{
										await Logger.LogExceptionAsync(ex, "Error during immediate grouped download start");
									}
								});
							}
							else if ( !string.IsNullOrEmpty(progress.Url) )
							{
								// Start single download
								progress.AddLog("Starting download immediately (user requested)");
								_ = Task.Run(async () =>
								{
									try
									{
										var singleUrlMap = new Dictionary<string, DownloadProgress> { { progress.Url, progress } };
										_ = await downloadManager.DownloadAllWithProgressAsync(
											singleUrlMap,
											MainConfig.SourcePath?.FullName ?? string.Empty,
											CancellationToken.None);

										await Dispatcher.UIThread.InvokeAsync(ScanModDirectoryForDownloads);
									}
									catch ( Exception ex )
									{
										await Logger.LogExceptionAsync(ex, "Error during immediate download start");
									}
								});
							}
						}
						break;

					case DownloadControlAction.Pause:
						// For now, just update the status to indicate pausing is not fully implemented
						progress.AddLog("Pause functionality is not yet fully implemented");
						await Logger.LogWarningAsync("[Download Control] Pause functionality is not yet fully implemented");
						await InformationDialog.ShowInformationDialog(progressWindow,
							"Pause functionality is not yet fully implemented. Use the Cancel button to stop all downloads.");
						break;

					case DownloadControlAction.Retry:
						// Retry downloads
						if ( progress.IsGrouped )
						{
							// Retry all child downloads
							progress.AddLog("Retrying all downloads (user requested)");

							_ = Task.Run(async () =>
							{
								try
								{
									var groupUrlMap = new Dictionary<string, DownloadProgress>();

									foreach ( DownloadProgress child in progress.ChildDownloads )
									{
										if ( !string.IsNullOrEmpty(child.Url) )
										{
											// Reset child progress state
											child.Status = DownloadStatus.Pending;
											child.ProgressPercentage = 0;
											child.BytesDownloaded = 0;
											child.StatusMessage = "Retrying...";
											child.ErrorMessage = string.Empty;
											child.Exception = null;
											child.StartTime = default;
											child.EndTime = null;

											// Delete existing file if it exists
											try
											{
												string fileName = Path.GetFileName(new Uri(child.Url).AbsolutePath);
												if ( !string.IsNullOrEmpty(fileName) )
												{
													string filePath = Path.Combine(MainConfig.SourcePath?.FullName ?? string.Empty, fileName);
													if ( File.Exists(filePath) )
													{
														File.Delete(filePath);
														child.AddLog($"Deleted existing file: {fileName}");
													}
												}
											}
											catch ( Exception ex )
											{
												await Logger.LogExceptionAsync(ex, $"Error deleting existing file for retry: {child.Url}");
											}

											groupUrlMap[child.Url] = child;
										}
									}

									if ( groupUrlMap.Count > 0 )
									{
										_ = await downloadManager.DownloadAllWithProgressAsync(
											groupUrlMap,
											MainConfig.SourcePath?.FullName ?? string.Empty,
											CancellationToken.None);
									}

									await Dispatcher.UIThread.InvokeAsync(ScanModDirectoryForDownloads);
								}
								catch ( Exception ex )
								{
									await Logger.LogExceptionAsync(ex, "Error during grouped download retry");
								}
							});
						}
						else if ( !string.IsNullOrEmpty(progress.Url) )
						{
							// Retry single download
							progress.AddLog("Retrying download (user requested)");

							// Reset progress state
							progress.Status = DownloadStatus.Pending;
							progress.ProgressPercentage = 0;
							progress.BytesDownloaded = 0;
							progress.StatusMessage = "Retrying...";
							progress.ErrorMessage = string.Empty;
							progress.Exception = null;
							progress.StartTime = default;
							progress.EndTime = null;

							// Delete existing file if it exists
							try
							{
								string fileName = Path.GetFileName(new Uri(progress.Url).AbsolutePath);
								if ( !string.IsNullOrEmpty(fileName) )
								{
									string filePath = Path.Combine(MainConfig.SourcePath?.FullName ?? string.Empty, fileName);
									if ( File.Exists(filePath) )
									{
										File.Delete(filePath);
										progress.AddLog($"Deleted existing file: {fileName}");
									}
								}
							}
							catch ( Exception ex )
							{
								await Logger.LogExceptionAsync(ex, "Error deleting existing file for retry");
							}

							// Start the download
							_ = Task.Run(async () =>
							{
								try
								{
									var singleUrlMap = new Dictionary<string, DownloadProgress> { { progress.Url, progress } };
									_ = await downloadManager.DownloadAllWithProgressAsync(
										singleUrlMap,
										MainConfig.SourcePath?.FullName ?? string.Empty,
										CancellationToken.None);

									await Dispatcher.UIThread.InvokeAsync(ScanModDirectoryForDownloads);
								}
								catch ( Exception ex )
								{
									await Logger.LogExceptionAsync(ex, "Error during download retry");
								}
							});
						}
						break;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error handling download control");
			}
		}

		private async void DownloadStatusButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// If download window is active, bring it to the front
				if ( _currentDownloadWindow != null && _currentDownloadWindow.IsVisible )
				{
					_currentDownloadWindow.Activate();
					_currentDownloadWindow.Focus();
					return;
				}

				// Otherwise show download status summary dialog
				int downloadedCount = MainConfig.AllComponents.Count(c => c.IsSelected && c.IsDownloaded);
				int totalSelected = MainConfig.AllComponents.Count(c => c.IsSelected);

				string statusMessage;
				if ( totalSelected == 0 )
				{
					statusMessage = "No mods are currently selected for installation.";
				}
				else if ( downloadedCount == totalSelected )
				{
					statusMessage = $"All {totalSelected} selected mod(s) are downloaded and ready for installation!";
				}
				else
				{
					int missing = totalSelected - downloadedCount;
					statusMessage = "Download Status:\n\n" +
									$"• Downloaded: {downloadedCount}/{totalSelected}\n" +
									$"• Missing: {missing}\n\n" +
									"Click 'Fetch Downloads' to automatically download missing mods.";
				}

				await InformationDialog.ShowInformationDialog(this, statusMessage);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error showing download status");
			}
		}

		private void OpenModDirectoryButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( MainConfig.SourcePath == null || !Directory.Exists(MainConfig.SourcePath.FullName) )
				{
					Logger.LogWarning("Mod directory is not set or does not exist.");
					return;
				}

				// Open the directory in the file explorer
				if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
					_ = System.Diagnostics.Process.Start("explorer.exe", MainConfig.SourcePath.FullName);
				else if ( Utility.GetOperatingSystem() == OSPlatform.OSX )
					_ = System.Diagnostics.Process.Start("open", MainConfig.SourcePath.FullName);
				else // Linux
					_ = System.Diagnostics.Process.Start("xdg-open", MainConfig.SourcePath.FullName);

				Logger.Log($"Opened mod directory: {MainConfig.SourcePath.FullName}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to open mod directory");
			}
		}

		/// <summary>
		/// Attempts to auto-generate instructions for components that don't have any.
		/// </summary>
		private static async Task TryAutoGenerateInstructionsForComponents(List<Component> components)
		{
			if ( components == null || components.Count == 0 )
				return;

			try
			{
				int generatedCount = 0;
				int skippedCount = 0;

				foreach ( Component component in components )
				{
					// Skip if already has instructions
					if ( component.Instructions.Count > 0 )
					{
						skippedCount++;
						continue;
					}

					// Try to generate instructions
					bool success = component.TryGenerateInstructionsFromArchive();
					if ( success )
					{
						generatedCount++;
						await Logger.LogAsync($"Auto-generated instructions for '{component.Name}': {component.InstallationMethod}");
					}
				}

				if ( generatedCount > 0 )
				{
					await Logger.LogAsync($"Auto-generated instructions for {generatedCount} component(s). Skipped {skippedCount} component(s) that already had instructions.");
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		#region Filter Event Handlers

		[UsedImplicitly]
		private void TierFilter_Changed(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				var comboBox = sender as ComboBox;
				if ( !(comboBox?.SelectedItem is ComboBoxItem selectedItem) )
					return;
				_selectedMinTier = selectedItem.Content?.ToString() ?? "Any";
				ApplyAllFilters();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private void SelectFilteredMods_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( ModListBox == null )
					return;

				// Select all visible (filtered) mods
				foreach ( object item in ModListBox.Items )
				{
					if ( item is Component component )
					{
						component.IsSelected = true;
						ComponentCheckboxChecked(component, new HashSet<Component>());
					}
				}

				UpdateModCounts();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private void DeselectFilteredMods_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( ModListBox == null )
					return;

				// Deselect all visible (filtered) mods
				foreach ( object item in ModListBox.Items )
				{
					if ( item is Component component )
					{
						component.IsSelected = false;
						ComponentCheckboxUnchecked(component, new HashSet<Component>());
					}
				}

				UpdateModCounts();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private void SelectAllCategories_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				StackPanel categoryPanel = this.FindControl<StackPanel>("CategoryFilterPanel");
				if ( categoryPanel == null )
					return;

				// Check all category checkboxes
				foreach ( Control child in categoryPanel.Children )
				{
					if ( child is CheckBox checkBox )
						checkBox.IsChecked = true;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private void DeselectAllCategories_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				StackPanel categoryPanel = this.FindControl<StackPanel>("CategoryFilterPanel");
				if ( categoryPanel == null )
					return;

				// Uncheck all category checkboxes
				foreach ( Control child in categoryPanel.Children )
				{
					if ( child is CheckBox checkBox )
						checkBox.IsChecked = false;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private void ClearAllFilters_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Reset tier filter
				ComboBox minTierComboBox = this.FindControl<ComboBox>("MinTierComboBox");
				if ( minTierComboBox != null )
					minTierComboBox.SelectedIndex = 0; // "Any"

				// Select all categories
				SelectAllCategories_Click(sender, e);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		[UsedImplicitly]
		private async void JumpToCurrentStep_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Find the ScrollViewer in the Getting Started tab
				var gettingStartedTab = this.FindControl<TabItem>("InitialTab");
				if ( gettingStartedTab?.Content is ScrollViewer scrollViewer )
				{
					// Determine the current step based on completion status
					Border targetStepBorder;

					// Check Step 1: Directories
					bool step1Complete = !string.IsNullOrEmpty(MainConfig.SourcePath?.FullName) &&
										!string.IsNullOrEmpty(MainConfig.DestinationPath?.FullName);
					if ( !step1Complete )
					{
						targetStepBorder = this.FindControl<Border>("Step1Border");
					}
					// Check Step 2: Components loaded
					else
					{
						bool step2Complete = MainConfig.AllComponents.Count > 0;
						if ( !step2Complete )
						{
							targetStepBorder = this.FindControl<Border>("Step2Border");
						}
						// Check Step 3: At least one component selected
						else
						{
							bool step3Complete = MainConfig.AllComponents.Any(c => c.IsSelected);
							targetStepBorder = this.FindControl<Border>(!step3Complete ? "Step3Border" :
								// Check Step 4: Download status
								// For now, assume Step 4 (Download) is the next step if Step 3 is complete
								// In a more sophisticated implementation, we could check download status
								"Step4Border");
						}
					}

					if ( targetStepBorder != null )
					{
						// Calculate the position to scroll to
						// Get the bounds of the target step relative to the ScrollViewer's content
						Rect targetBounds = targetStepBorder.Bounds;

						// Calculate the offset to center the target step in the viewport
						double targetOffset = targetBounds.Top - scrollViewer.Viewport.Height / 2 + targetBounds.Height / 2;

						// Ensure we don't scroll past the content bounds
						targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));

						// Scroll to the target position (Avalonia doesn't have SmoothScrollToVerticalOffset)
						scrollViewer.Offset = new Vector(0, targetOffset);

						// Briefly highlight the target step
						await HighlightStep(targetStepBorder);
					}
					else
					{
						// All steps complete - scroll to the progress section
						// Find the progress section by looking for a Border with "progress-section" class
						Border progressSection = FindProgressSection(scrollViewer.Content as Panel);
						if ( progressSection != null )
						{
							Rect progressBounds = progressSection.Bounds;
							double targetOffset = progressBounds.Top - (scrollViewer.Viewport.Height / 2) + (progressBounds.Height / 2);
							targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));
							scrollViewer.Offset = new Vector(0, targetOffset);
						}
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private async Task HighlightStep(Border stepBorder)
		{
			try
			{
				// Store original border properties
				IBrush originalBorderBrush = stepBorder.BorderBrush;
				Thickness originalBorderThickness = stepBorder.BorderThickness;

				// Create highlight effect
				stepBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); // Gold
				stepBorder.BorderThickness = new Thickness(3);

				// Wait for the highlight effect
				await Task.Delay(1000);

				// Restore original appearance
				stepBorder.BorderBrush = originalBorderBrush;
				stepBorder.BorderThickness = originalBorderThickness;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}

		private Border FindProgressSection(Panel panel)
		{
			if ( panel == null ) return null;

			foreach ( var child in panel.Children )
			{
				if ( child is Border border && border.Classes.Contains("progress-section") )
				{
					return border;
				}
				if ( child is Panel childPanel )
				{
					var result = FindProgressSection(childPanel);
					if ( result != null ) return result;
				}
			}
			return null;
		}

		#endregion

	}
}
