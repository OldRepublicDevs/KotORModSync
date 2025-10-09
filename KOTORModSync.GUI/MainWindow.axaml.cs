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
using JetBrains.Annotations;
using KOTORModSync.CallbackDialogs;
using KOTORModSync.Controls;
using KOTORModSync.Converters;
using KOTORModSync.Core;
using KOTORModSync.Models;
using KOTORModSync.Services;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;
using KOTORModSync.Dialogs;
using ReactiveUI;
using SharpCompress.Archives;
using static KOTORModSync.Core.Services.ModManagementService;
using NotNullAttribute = JetBrains.Annotations.NotNullAttribute;

namespace KOTORModSync
{
	[SuppressMessage(category: "ReSharper", checkId: "UnusedParameter.Local")]
	public sealed partial class MainWindow : Window
	{
		public static readonly DirectProperty<MainWindow, ModComponent> CurrentComponentProperty =
			AvaloniaProperty.RegisterDirect<MainWindow, ModComponent>(
				nameof(CurrentComponent),
				o => (o?.CurrentComponent),
				(o, v) => o.CurrentComponent = v
			);
		[CanBeNull] private ModComponent _currentComponent;
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
		private bool? _rootSelectionState;
		private bool _editorMode;
		private bool _isClosingProgressWindow;
		private string _lastLoadedFileName;
		// ModManagementService provides comprehensive mod management functionality
		private readonly ModManagementService _modManagementService;
		// Public property for binding
		public ModManagementService ModManagementService => _modManagementService;
		// DownloadCacheService manages downloaded files and their associated Extract instructions
		private readonly DownloadCacheService _downloadCacheService;
		// Public property for access
		public DownloadCacheService DownloadCacheService => _downloadCacheService;
		// Service layer for modular functionality
		private readonly ModListService _modListService;
		private readonly ValidationService _validationService;
		private readonly UIStateService _uiStateService;
		private readonly InstructionManagementService _instructionManagementService;
		private readonly SelectionService _selectionService;
		private readonly FileSystemService _fileSystemService;
		private readonly GuiPathService _guiPathService;
		private readonly DialogService _dialogService;
		private readonly MenuBuilderService _menuBuilderService;
		private readonly DragDropService _dragDropService;
		private readonly FileLoadingService _fileLoadingService;
		private readonly Services.ComponentEditorService _componentEditorService;
		private readonly ComponentSelectionService _componentSelectionService;
		private readonly DownloadOrchestrationService _downloadOrchestrationService;
		private readonly FilterUIService _filterUIService;
		private readonly MarkdownRenderingService _markdownRenderingService;
		private readonly InstructionBrowsingService _instructionBrowsingService;
		private readonly InstructionGenerationService _instructionGenerationService;
		private readonly ValidationDisplayService _validationDisplayService;
		private readonly SettingsService _settingsService;
		private readonly StepNavigationService _stepNavigationService;
		// UI control properties
		private ListBox ModListBox => this.FindControl<ListBox>("ModListBoxElement");
		public bool IsClosingMainWindow;
		public bool? RootSelectionState
		{
			get => _rootSelectionState;
			set
			{
				if ( _rootSelectionState == value ) return;

				// Suppress the event handler to prevent infinite recursion
				_suppressSelectAllCheckBoxEvents = true;

				try
				{
					// Update the property value
					_ = SetAndRaise(RootSelectionStateProperty, ref _rootSelectionState, value);

					// Perform the actual selection logic
					_componentSelectionService.HandleSelectAllCheckbox(value, ComponentCheckboxChecked, ComponentCheckboxUnchecked);

					// Update counts and progress
					UpdateModCounts();
					UpdateStepProgress();
					ResetDownloadStatusDisplay();
				}
				finally
				{
					_suppressSelectAllCheckBoxEvents = false;
				}
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
				UpdateStepProgress(); // Update step validation when EditorMode changes
				RefreshModListVisuals(); // Update validation visuals safely
			}
		}
		// Direct Avalonia properties for proper change notification/binding
		public static readonly DirectProperty<MainWindow, bool> EditorModeProperty =
			AvaloniaProperty.RegisterDirect<MainWindow, bool>(
				nameof(EditorMode),
				o => o._editorMode,
				(o, v) => o.EditorMode = v
			);
		public static readonly DirectProperty<MainWindow, bool?> RootSelectionStateProperty =
			AvaloniaProperty.RegisterDirect<MainWindow, bool?>(
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
				// Load persisted settings
				LoadSettings();
				// Initialize the installation service
				_ = new InstallationService();
				// Initialize the mod management service
				_modManagementService = new ModManagementService(MainConfigInstance);
				_modManagementService.ModOperationCompleted += OnModOperationCompleted;
				_modManagementService.ModValidationCompleted += OnModValidationCompleted;
				// Initialize the download cache service
				_downloadCacheService = new DownloadCacheService();
				// Initialize modular service layer
				_modListService = new ModListService(MainConfigInstance);
				_validationService = new ValidationService(MainConfigInstance);
				_uiStateService = new UIStateService(MainConfigInstance, _validationService);
				_instructionManagementService = new InstructionManagementService();
				_selectionService = new SelectionService(MainConfigInstance);
				_fileSystemService = new FileSystemService();
				_guiPathService = new GuiPathService(MainConfigInstance, _fileSystemService);
				_dialogService = new DialogService(this);
				_menuBuilderService = new MenuBuilderService(_modManagementService, this);
				_dragDropService = new DragDropService(this, () => MainConfig.AllComponents, () => ProcessComponentsAsync(MainConfig.AllComponents));
				_fileLoadingService = new FileLoadingService(MainConfigInstance, this);
				_componentEditorService = new Services.ComponentEditorService(MainConfigInstance, this);
				_componentSelectionService = new ComponentSelectionService(MainConfigInstance);
				_downloadOrchestrationService = new DownloadOrchestrationService(_downloadCacheService, MainConfigInstance, this);
				_filterUIService = new FilterUIService(MainConfigInstance);
				_markdownRenderingService = new MarkdownRenderingService();
				_instructionBrowsingService = new InstructionBrowsingService(MainConfigInstance, _dialogService);
				_instructionGenerationService = new InstructionGenerationService(MainConfigInstance, this, _downloadOrchestrationService);
				_validationDisplayService = new ValidationDisplayService(_validationService, () => MainConfig.AllComponents);
				_settingsService = new SettingsService(MainConfigInstance, this);
				_stepNavigationService = new StepNavigationService(MainConfigInstance, _validationService);
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
				// Virtual file system will be initialized per-component as needed
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

			// Virtual file system will be initialized per-component as needed
		}
		private void UpdatePathDisplays()
		{
			TextBlock modPathDisplay = this.FindControl<TextBlock>(name: "CurrentModPathDisplay");
			TextBlock kotorPathDisplay = this.FindControl<TextBlock>(name: "CurrentKotorPathDisplay");
			UpdatePathDisplays(modPathDisplay, kotorPathDisplay);

			// Refresh all tooltips
			RefreshAllTooltips();
		}
		/// <summary>
		/// Loads persisted settings and applies them to the application
		/// </summary>
		private void LoadSettings()
		{
			try
			{
				AppSettings settings = SettingsManager.LoadSettings();
				settings.ApplyToMainConfig(MainConfigInstance, out string theme);
				// Editor Mode is intentionally NOT persisted - always starts as false
				EditorMode = false;
				// Apply theme
				if ( !string.IsNullOrEmpty(theme) )
				{
					ApplyTheme(theme);
					// Explicitly apply theme to this window since it's not yet in the Application's window collection
					ThemeManager.ApplyCurrentToWindow(this);
				}
				// Update UI to reflect loaded paths
				UpdatePathDisplays();
				// Update directory picker controls with persisted paths (even if they don't exist)
				UpdateDirectoryPickersFromSettings(settings);
				Logger.LogVerbose("Settings loaded and applied successfully");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "Failed to load settings");
			}
		}
		/// <summary>
		/// Updates directory picker controls with persisted settings, even if directories don't exist
		/// </summary>
		private void UpdateDirectoryPickersFromSettings(AppSettings settings)
		{
			try
			{
				// Update mod directory pickers
				if ( !string.IsNullOrEmpty(settings.SourcePath) )
				{
					DirectoryPickerControl modPicker = this.FindControl<DirectoryPickerControl>("ModDirectoryPicker");
					DirectoryPickerControl step1ModPicker = this.FindControl<DirectoryPickerControl>("Step1ModDirectoryPicker");
					UpdateDirectoryPickerWithPath(modPicker, settings.SourcePath);
					UpdateDirectoryPickerWithPath(step1ModPicker, settings.SourcePath);
				}
				// Update KOTOR directory pickers
				if ( !string.IsNullOrEmpty(settings.DestinationPath) )
				{
					DirectoryPickerControl kotorPicker = this.FindControl<DirectoryPickerControl>("KotorDirectoryPicker");
					DirectoryPickerControl step1KotorPicker = this.FindControl<DirectoryPickerControl>("Step1KotorDirectoryPicker");
					UpdateDirectoryPickerWithPath(kotorPicker, settings.DestinationPath);
					UpdateDirectoryPickerWithPath(step1KotorPicker, settings.DestinationPath);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to update directory pickers from settings");
			}
		}
		/// <summary>
		/// Updates a directory picker control with a path, including setting the combobox selection
		/// </summary>
		private static void UpdateDirectoryPickerWithPath(DirectoryPickerControl picker, string path)
		{
			if ( picker == null || string.IsNullOrEmpty(path) )
				return;
			try
			{
				// Set the current path (updates textbox and display)
				picker.SetCurrentPath(path);
				// Also update the combobox selection
				ComboBox comboBox = picker.FindControl<ComboBox>("PathSuggestions");
				if ( comboBox != null )
				{
					// Add the path to the combobox items if it's not already there
					List<string> currentItems = (comboBox.ItemsSource as IEnumerable<string>)?.ToList() ?? new List<string>();
					if ( !currentItems.Contains(path) )
					{
						currentItems.Insert(0, path); // Add to the beginning
													  // Keep only the first 20 items
						if ( currentItems.Count > 20 )
							currentItems = currentItems.Take(20).ToList();
						comboBox.ItemsSource = currentItems;
					}
					// Set the path as the selected item
					comboBox.SelectedItem = path;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to update directory picker with path: {path}");
			}
		}
		/// <summary>
		/// Saves current application settings to disk
		/// </summary>
		private void SaveSettings()
		{
			try
			{
				string currentTheme = ThemeManager.GetCurrentStylePath();
				var settings = AppSettings.FromCurrentState(MainConfigInstance, currentTheme);
				SettingsManager.SaveSettings(settings);
				Logger.LogVerbose("Settings saved successfully");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "Failed to save settings");
			}
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
			if ( !(sender is ComboBox comboBox) || !(comboBox.SelectedItem is string path) )
				return;
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
				// Don't refresh ItemsSource here to avoid recursive selection updates
			}
			_suppressPathEvents = false;
			_suppressComboEvents = false;
		}

		private bool TryApplySourcePath(string text)
		{
			bool result = _guiPathService.TryApplySourcePath(text, _ => ScanModDirectoryForDownloads());
			if ( result )
			{
				_ = GuiPathService.AddToRecentModsAsync(text);
			}
			return result;
		}

		private bool TryApplyInstallPath(string text)
		{
			return _guiPathService.TryApplyDestinationPath(text);
		}

		private static void UpdatePathSuggestions(TextBox input, ComboBox combo, ref CancellationTokenSource cts)
		{
			GuiPathService.UpdatePathSuggestions(input, combo, ref cts);
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
			await GuiPathService.AddToRecentModsAsync(path);
			UpdatePathDisplays();
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

		public static List<ModComponent> ComponentsList => MainConfig.AllComponents;
		/// <summary>
		/// Gets the available tier options for the tier combobox.
		/// </summary>
		/// <returns>A list of string options for the tier combobox.</returns>
		public static List<string> TierOptions => CategoryTierDefinitions.TierDefinitions.Keys.ToList();
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

		[CanBeNull]
		public ModComponent CurrentComponent
		{
			get => _currentComponent;
			set
			{
				// Unsubscribe from previous component's property changes
				if ( _currentComponent != null )
				{
					_currentComponent.PropertyChanged -= OnCurrentComponentPropertyChanged;
				}
				_ = SetAndRaise(CurrentComponentProperty, ref _currentComponent, value);
				// Subscribe to new component's property changes
				if ( _currentComponent != null )
				{
					_currentComponent.PropertyChanged += OnCurrentComponentPropertyChanged;
				}
			}
		}

		private bool IgnoreInternalTabChange { get; set; }
		/// <summary>
		/// Handles property changes on the currently selected component to refresh validation state
		/// </summary>
		private void OnCurrentComponentPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if ( sender is ModComponent component && component == _currentComponent )
			{
				// Refresh validation state for this component in the mod list
				RefreshComponentValidationState(component);
			}
		}

		private void InitializeTopMenu()
		{
			var menu = new Menu();
			var fileMenu = new MenuItem { Header = "File" };
			var fileItems = new List<MenuItem>
			{
				new MenuItem
				{
					Header = "Open File",
					Command = ReactiveCommand.Create( () => LoadFile_Click(new object(), new RoutedEventArgs()) ),
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
				"Lowercase all files/folders recursively at the given path. Necessary for iOS installs."
			);
			ToolTip.SetTip(
				toolItems[2],
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
			}
		}

		/// <summary>
		/// Refreshes all mod list items to update their visibility and context menus based on current editor mode.
		/// </summary>
		/// <summary>
		/// Lightweight method to refresh a single component's visual state without rescanning files.
		/// This just triggers the ModListItem to update its border colors and validation state.
		/// </summary>
		private void RefreshSingleComponentVisuals(ModComponent component)
		{
			ModListService.RefreshSingleComponentVisuals(ModListBox, component);
		}

		private void RefreshModListItems()
		{
			try
			{
				_modListService.RefreshModListItems(ModListBox, EditorMode, BuildContextMenuForComponent);
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
				{
					Logger.LogVerbose("No files dropped");
					return;
				}
				// Attempt to get the data as a string array (file paths)
				if ( !(e.Data.Get(DataFormats.Files) is IEnumerable<IStorageItem> items) )
				{
					Logger.LogVerbose("Dropped items were not IStorageItem enumerable");
					return;
				}
				// Processing the first file
				IStorageItem storageItem = items.FirstOrDefault();
				string filePath = storageItem?.TryGetLocalPath();
				if ( string.IsNullOrEmpty(filePath) )
				{
					Logger.LogVerbose("Dropped item had no path");
					return;
				}
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
						{
							Logger.LogVerbose("Dropped item was not an archive");
							return;
						}
						string exePath = ArchiveHelper.AnalyzeArchiveForExe(archiveStream, archive);
						await Logger.LogVerboseAsync(exePath);
						break;
					case IStorageFolder _:
						// Handle folder logic
						Logger.LogVerbose("Dropped item was a folder, not supported");
						break;
					default:
						Logger.LogVerbose("Dropped item was not a valid file or folder");
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
				bool? result = (
					EditorMode is true
					? await ConfirmationDialog.ShowConfirmationDialog(this, confirmText: "Really close KOTORModSync Please save your changes before pressing Quit?",
					noButtonText: "Cancel",
					yesButtonText: "Quit"
				)
					: true
				);
				if ( result != true )
					return;
				// Save settings before closing
				SaveSettings();
				// Clean up file watcher
				_fileSystemService?.Dispose();
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
					FilterModList(SearchText);
				else
					// Show all items when search is cleared
					RefreshModList();
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
					if ( !(ModListBox.SelectedItem is ModComponent component) )
						return;
					switch ( e.Key )
					{
						// Ctrl+Up - Move selected mod up
						case Key.Up when e.KeyModifiers == KeyModifiers.Control:
							MoveComponentListItem(component, -1);
							e.Handled = true;
							break;
						// Ctrl+Down - Move selected mod down
						case Key.Down when e.KeyModifiers == KeyModifiers.Control:
							MoveComponentListItem(component, 1);
							e.Handled = true;
							break;
						// Delete - Remove selected mod
						case Key.Delete:
							CurrentComponent = component;
							_ = DeleteModWithConfirmation(component);
							e.Handled = true;
							break;
						// Space - Toggle selection
						case Key.Space:
							component.IsSelected = !component.IsSelected;
							UpdateModCounts();
							e.Handled = true;
							break;
					}
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex);
				}
			};
		}
		[UsedImplicitly]
		private async Task DeleteModWithConfirmation(ModComponent component)
		{
			try
			{
				if ( component is null )
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
		public ContextMenu BuildContextMenuForComponent(ModComponent component)
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
						ComponentCheckboxChecked(component, new HashSet<ModComponent>());
					else
						ComponentCheckboxUnchecked(component, new HashSet<ModComponent>());
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
					Command = ReactiveCommand.Create(() => ModManagementService.MoveModToPosition(component, 0))
				});
				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "📊 Move to Bottom",
					Command = ReactiveCommand.Create(() => ModManagementService.MoveModToPosition(component, MainConfig.AllComponents.Count - 1))
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
							RemoveComponentButton_Click(null, null);
					})
				});
				_ = contextMenu.Items.Add(new MenuItem
				{
					Header = "🔄 Duplicate Mod",
					Command = ReactiveCommand.Create(() =>
					{
						ModComponent duplicated = ModManagementService.DuplicateMod(component);
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
						ModValidationResult validation = ModManagementService.ValidateMod(component);
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
				BuildMenuFlyoutItems(globalActionsFlyout);
			// Build Mod List context menu - find the ScrollViewer that contains the ListBox
			ListBox modListBox = this.FindControl<ListBox>("ModListBoxElement");
			var scrollViewer = modListBox?.Parent as ScrollViewer;
			if ( scrollViewer?.ContextMenu is ContextMenu modListContextMenu )
				BuildContextMenuItems(modListContextMenu);
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
					Dictionary<ModComponent, ModValidationResult> results = ModManagementService.ValidateAllMods();
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
						ModComponent newMod = ModManagementService.CreateMod();
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
					Command = ReactiveCommand.Create(() => ModManagementService.SortMods())
				});
				_ = menu.Items.Add(new MenuItem
				{
					Header = "🔎 Select by Category",
					Command = ReactiveCommand.Create(() => ModManagementService.SortMods(ModSortCriteria.Category))
				});
				_ = menu.Items.Add(new MenuItem
				{
					Header = "🔎 Select by Tier",
					Command = ReactiveCommand.Create(() => ModManagementService.SortMods(ModSortCriteria.Tier))
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
						ModStatistics stats = ModManagementService.GetModStatistics();
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
					Dictionary<ModComponent, ModValidationResult> results = ModManagementService.ValidateAllMods();
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
						ModComponent newMod = ModManagementService.CreateMod();
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
					Command = ReactiveCommand.Create(() => ModManagementService.SortMods())
				});
				_ = menu.Items.Add(new MenuItem
				{
					Header = "🔎 Select by Category",
					Command = ReactiveCommand.Create(() => ModManagementService.SortMods(ModSortCriteria.Category))
				});
				_ = menu.Items.Add(new MenuItem
				{
					Header = "🔎 Select by Tier",
					Command = ReactiveCommand.Create(() => ModManagementService.SortMods(ModSortCriteria.Tier))
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
						ModStatistics stats = ModManagementService.GetModStatistics();
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
			_dragDropService.HandlePointerPressed(e, ModListBox, EditorMode);
		}
		private void ModListBox_DragOver(object sender, DragEventArgs e)
		{
			_dragDropService.HandleDragOver(e, EditorMode, this);
		}
		private void ModListBox_Drop(object sender, DragEventArgs e)
		{
			_dragDropService.HandleDrop(e, EditorMode);
		}
		private async Task ShowModManagementDialog()
		{
			try
			{
				var dialogService = new ModManagementDialogService(this, ModManagementService,
					() => MainConfigInstance.allComponents.ToList(),
					(components) => MainConfigInstance.allComponents = components);
				var dialog = new ModManagementDialog(ModManagementService, dialogService);
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
		public void StartDragComponent(ModComponent component, PointerPressedEventArgs e)
		{
			_dragDropService.StartDragComponent(component, e, EditorMode);
		}
		private void ModListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				Logger.LogVerbose($"[ModListBox_SelectionChanged] START - SelectedItem type: {ModListBox.SelectedItem?.GetType().Name ?? "null"}");
				if ( ModListBox.SelectedItem is ModComponent component )
				{
					Logger.LogVerbose($"[ModListBox_SelectionChanged] ModComponent selected: '{component.Name}' (GUID={component.Guid})");
					Logger.LogVerbose($"[ModListBox_SelectionChanged] ModComponent has {component.Instructions.Count} instructions, {component.Options.Count} options");
					Logger.LogVerbose("[ModListBox_SelectionChanged] Calling SetCurrentComponent");
					SetCurrentComponent(component);
					Logger.LogVerbose("[ModListBox_SelectionChanged] SetCurrentComponent completed");
				}
				else
				{
					Logger.LogVerbose("[ModListBox_SelectionChanged] SelectedItem is not a ModComponent");
				}
				Logger.LogVerbose("[ModListBox_SelectionChanged] COMPLETED");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "[ModListBox_SelectionChanged] Exception occurred");
			}
		}
		private bool _suppressSelectAllCheckBoxEvents;
		private void SelectAllCheckBox_IsCheckedChanged(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( !(sender is CheckBox checkBox) || _suppressSelectAllCheckBoxEvents )
					return;
				_componentSelectionService.HandleSelectAllCheckbox(checkBox.IsChecked, ComponentCheckboxChecked, ComponentCheckboxUnchecked);
				// Update counts first, then step progress to ensure consistency
				UpdateModCounts();
				UpdateStepProgress();
				// Reset download status display when selections change
				ResetDownloadStatusDisplay();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}
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
				List<ModComponent> filteredComponents = ModManagementService.SearchMods(searchText, searchOptions);
				PopulateModList(filteredComponents);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
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
				ModListService.RefreshModListVisuals(ModListBox, UpdateStepProgress);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
			}
		}
		/// <summary>
		/// Refreshes all tooltips in the mod list after virtual file system changes
		/// </summary>
		private void RefreshAllTooltips()
		{
			try
			{
				if ( ModListBox == null ) return;

				// Get all ModListItem controls and refresh their tooltips
				var modListItems = ModListBox.GetVisualDescendants().OfType<Controls.ModListItem>();
				foreach ( var item in modListItems )
				{
					if ( item.DataContext is ModComponent component )
					{
						item.UpdateTooltip(component);
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error refreshing tooltips");
			}
		}

		/// <summary>
		/// Refreshes the validation state for a specific component in the mod list
		/// </summary>
		private void RefreshComponentValidationState(ModComponent component)
		{
			try
			{
				if ( ModListBox == null || component == null )
					return;
				// Use dispatcher to ensure we're on the UI thread
				Dispatcher.UIThread.Post(() =>
				{
					try
					{
						// Find the container for this specific component
						if ( !(ModListBox.ContainerFromItem(component) is ListBoxItem container) )
							return;
						// Find the ModListItem control
						ModListItem modListItem = container.GetVisualDescendants().OfType<ModListItem>().FirstOrDefault();
						if ( modListItem == null )
							return;
						// Update validation state for this specific component
						modListItem.UpdateValidationState(component);
					}
					catch ( Exception ex )
					{
						Logger.LogException(ex, "Error refreshing component validation state on UI thread");
					}
				}, DispatcherPriority.Normal);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error posting component validation refresh to UI thread");
			}
		}
		private void PopulateModList(List<ModComponent> components)
		{
			try
			{
				ModListService.PopulateModList(ModListBox, components, UpdateModCounts);
				// Update tier/category selection UI (only when not searching - use all components)
				if ( string.IsNullOrWhiteSpace(SearchText) && MainConfig.AllComponents.Count > 0 )
					InitializeFilterUi(MainConfig.AllComponents);
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
				_modListService.UpdateModCounts(
					this.FindControl<TextBlock>("ModCountText"),
					this.FindControl<TextBlock>("SelectedCountText"),
					this.FindControl<CheckBox>("SelectAllCheckBox"),
					suppress => _suppressSelectAllCheckBoxEvents = suppress
				);
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
			if ( controlItem.Tag is ModComponent thisComponent )
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
				switch ( current )
				{
					// Check if we're clicking on any interactive control
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
					// Check if the element has context menu or flyout open
					case Control control when control.ContextMenu?.IsOpen == true:
						return true;
					default:
						// Move up the visual tree
						current = current.GetVisualParent();
						break;
				}
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
			return await _dialogService.ShowSaveFileDialogAsync(saveFileName ?? "my_toml_instructions.toml", "toml");
		}
		public async Task<string[]> ShowFileDialog(
			bool isFolderDialog,
			bool allowMultiple = false,
			IStorageFolder startFolder = null,
			string windowName = null
		)
		{
			return await _dialogService.ShowFileDialogAsync(isFolderDialog, allowMultiple, startFolder, windowName);
		}

		[UsedImplicitly]
		private async void LoadFile_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				// Open the file dialog to select a file
				string[] result = await ShowFileDialog(
					windowName: "Load a TOML or Markdown instruction file",
					isFolderDialog: false
				);
				if ( result is null || result.Length <= 0 )
					return;
				string filePath = result[0];
				if ( !PathValidator.IsValidPath(filePath) )
					return;
				// Check file extension to determine how to load
				string fileExtension = Path.GetExtension(filePath)?.ToLowerInvariant();
				bool isTomlFile = fileExtension == ".toml" || fileExtension == ".tml";
				// Only try to load as TOML if the file has a .toml or .tml extension
				if ( isTomlFile )
				{
					bool loadedAsToml = false;
					try
					{
						await Logger.LogAsync($"Attempting to load file as TOML: {Path.GetFileName(filePath)}");
						loadedAsToml = await LoadTomlFile(filePath, fileType: "file");
					}
					catch ( Exception tomlEx )
					{
						// TOML parsing failed, will try markdown
						await Logger.LogVerboseAsync($"File is not a valid TOML file: {tomlEx.Message}");
					}
					// If TOML loading succeeded, we're done
					if ( loadedAsToml )
					{
						await Logger.LogAsync("File loaded successfully as TOML.");
						return;
					}
				}
				// Load as markdown (either because it's not a TOML extension, or TOML loading failed)
				await Logger.LogAsync("Attempting to load file as Markdown...");
				await _fileLoadingService.LoadMarkdownFileAsync(
					filePath,
					EditorMode,
					() => ProcessComponentsAsync(MainConfig.AllComponents),
					TryAutoGenerateInstructionsForComponents,
					profile: null
				);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}
		[UsedImplicitly]
		private void LoadInstallFile_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => LoadFile_Click(sender, e);
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
				Instruction thisInstruction = (Instruction)button.DataContext
											?? throw new NullReferenceException(message: "Could not find instruction instance");

				if ( button.Tag is TextBox sourceTextBox )
					await _instructionBrowsingService.BrowseSourceFilesAsync(thisInstruction, sourceTextBox);
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

				if ( button.Tag is TextBox sourceTextBox )
					await _instructionBrowsingService.BrowseSourceFoldersAsync(thisInstruction, sourceTextBox);
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

				if ( button.Tag is TextBox destinationTextBox )
					await _instructionBrowsingService.BrowseDestinationAsync(thisInstruction, destinationTextBox);
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

		[UsedImplicitly]
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

		[UsedImplicitly]
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
		private void ValidateButton_Click([CanBeNull] object sender, [NotNull] RoutedEventArgs e)
		{
			// Run heavy validation in background thread to avoid blocking UI
			Task.Run(async () =>
			{
				try
				{
					(bool validationResult, _) = await InstallationService.ValidateInstallationEnvironmentAsync(
						MainConfigInstance,
						async message => await ConfirmationDialog.ShowConfirmationDialog(this, message) == true
					);
					// If validation failed, run detailed analysis
					var modIssues = new List<ValidationIssue>();
					var systemIssues = new List<string>();
					if ( !validationResult )
					{
						// Analyze what went wrong
						await AnalyzeValidationFailures(modIssues, systemIssues);
					}

					// Show validation dialog and update UI on the UI thread
					await Dispatcher.UIThread.InvokeAsync(async () =>
					{
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
							// Validation succeeded - mark Step 5 as complete
							CheckBox step5Check = this.FindControl<CheckBox>(name: "Step5Checkbox");
							if ( step5Check != null ) step5Check.IsChecked = true;
							UpdateStepProgress();
						}
					});
				}
				catch ( Exception ex )
				{
					await Logger.LogExceptionAsync(ex);
				}
			});
		}

		private async Task AnalyzeValidationFailures(List<ValidationIssue> modIssues, List<string> systemIssues)
		{
			await _validationService.AnalyzeValidationFailures(modIssues, systemIssues);
		}

		[UsedImplicitly]
		private async void AddComponentButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				// Delegate to ComponentEditorService for component creation
				ModComponent newComponent = _componentEditorService.CreateNewComponent();

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
				MainConfigInstance.allComponents = new List<ModComponent>();
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
				await Logger.LogAsync(message: "TOML configuration closed successfully. ModComponent list cleared.");
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		[UsedImplicitly]
		private async void RemoveComponentButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					Logger.Log(message: "No component loaded into editor - nothing to remove.");
					return;
				}

				// Delegate to ComponentEditorService for removal logic
				bool removed = await _componentEditorService.RemoveComponentAsync(CurrentComponent);

				if ( removed )
				{
					SetCurrentComponent(c: null);
					// Refresh the TreeView to reflect the changes
					await ProcessComponentsAsync(MainConfig.AllComponents);
				}
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
					ModComponent.InstallExitCode exitCode = await InstallationService.InstallSingleComponentAsync(
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
		private void StartInstall_Click([CanBeNull] object sender, [NotNull] RoutedEventArgs e)
		{
			// Run validation in background thread to avoid blocking UI
			Task.Run(async () =>
			{
				try
				{
					if ( _installRunning )
					{
						await Dispatcher.UIThread.InvokeAsync(async () =>
						{
							await InformationDialog.ShowInformationDialog(
								this,
								message: "There's already an installation running, please check the output window."
							);
						});
						return;
					}

					(bool success, string informationMessage) = await InstallationService.ValidateInstallationEnvironmentAsync(
						MainConfigInstance,
						async message => await ConfirmationDialog.ShowConfirmationDialog(this, message) == true
					);

					if ( !success )
					{
						await Dispatcher.UIThread.InvokeAsync(async () =>
						{
							await InformationDialog.ShowInformationDialog(this, informationMessage);
						});
						return;
					}

					// Continue with installation on UI thread
					await Dispatcher.UIThread.InvokeAsync(async () =>
					{
						await StartInstallationProcess();
					});
				}
				catch ( Exception ex )
				{
					await Logger.LogExceptionAsync(ex);
				}
			});
		}

		private async Task StartInstallationProcess()
		{
			try
			{
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
					ModComponent.InstallExitCode exitCode = ModComponent.InstallExitCode.UnknownError;
					var selectedMods = MainConfig.AllComponents.Where(thisComponent => thisComponent.IsSelected).ToList();
					for ( int index = 0; index < selectedMods.Count; index++ )
					{
						if ( _progressWindowClosed )
						{
							_installRunning = false;
							_ = Logger.LogAsync(message: "User cancelled install by closing the progress window.");
							return;
						}
						ModComponent component = selectedMods[index];
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
					if ( exitCode != ModComponent.InstallExitCode.Success )
						return;
					await InformationDialog.ShowInformationDialog(
						this,
						message: "Install Completed. Check the output window for information."
					);
					await Logger.LogAsync(message: "Install completed.");
					// Installation completed successfully
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
				if ( Utility.GetOperatingSystem() != OSPlatform.Windows )
					return;
				_ = Logger.LogVerboseAsync("Install terminated, re-enabling the close button in the console window");
				ConsoleConfig.EnableCloseButton();
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
				// Check if there are any components to document
				if ( MainConfig.AllComponents == null || MainConfig.AllComponents.Count == 0 )
				{
					await InformationDialog.ShowInformationDialog(
						this,
						message: "No mod components available to generate documentation."
					);
					return;
				}

				// Prompt user for save location
				string file = await SaveFile(
					saveFileName: "ModList_Documentation.md"
				);

				if ( file is null )
				{
					await Logger.LogVerboseAsync("Documentation export cancelled by user.");
					return; // user cancelled
				}

				// Generate the documentation
				await Logger.LogAsync($"Generating documentation for {MainConfig.AllComponents.Count} mod component(s)...");
				string docs = ModComponent.GenerateModDocumentation(MainConfig.AllComponents);

				if ( string.IsNullOrWhiteSpace(docs) )
				{
					await Logger.LogWarningAsync("Generated documentation is empty.");
					await InformationDialog.ShowInformationDialog(
						this,
						message: "The generated documentation is empty. Please check your mod components."
					);
					return;
				}

				// Save to file
				await FileUtilities.SaveDocsToFileAsync(file, docs);

				// Confirm success
				string successMessage = $"Successfully generated and saved documentation for {MainConfig.AllComponents.Count} mod component(s) to:\n\n{file}";
				await Logger.LogAsync($"Documentation saved to '{file}'");
				await InformationDialog.ShowInformationDialog(this, successMessage);
			}
			catch ( IOException ioEx )
			{
				await Logger.LogExceptionAsync(ioEx, customMessage: "IO error while saving documentation file");
				await InformationDialog.ShowInformationDialog(
					this,
					message: $"Failed to save documentation file. The file may be in use or the path may be invalid.\n\nError: {ioEx.Message}"
				);
			}
			catch ( UnauthorizedAccessException uaEx )
			{
				await Logger.LogExceptionAsync(uaEx, customMessage: "Access denied while saving documentation");
				await InformationDialog.ShowInformationDialog(
					this,
					message: $"Access denied while saving the documentation file. Please check file permissions.\n\nError: {uaEx.Message}"
				);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, customMessage: "Unexpected error generating and saving documentation");
				await InformationDialog.ShowInformationDialog(
					this,
					message: $"An unexpected error occurred while generating and saving documentation.\n\nError: {ex.Message}"
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
				await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] START - IgnoreInternalTabChange={IgnoreInternalTabChange}");
				if ( IgnoreInternalTabChange )
				{
					await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Ignoring internal tab change, returning early");
					return;
				}
				try
				{
					if ( !(sender is TabControl tabControl) )
					{
						await Logger.LogErrorAsync(message: "[TabControl_SelectionChanged] Sender is not a TabControl control");
						return;
					}
					await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] TabControl found, SelectedIndex={tabControl.SelectedIndex}");
					if ( CurrentComponent is null )
					{
						await Logger.LogVerboseAsync("[TabControl_SelectionChanged] CurrentComponent is null, tabs can't be used");
						SetTabInternal(tabControl, InitialTab);
						await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Set to InitialTab");
						return;
					}
					await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] CurrentComponent='{CurrentComponent.Name}' (GUID={CurrentComponent.Guid})");
					// Get the last selected TabItem
					// ReSharper disable once PossibleNullReferenceException
					if ( e.RemovedItems.IsNullOrEmptyOrAllNull() || !(e.RemovedItems[0] is TabItem lastSelectedTabItem) )
					{
						await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Previous tab item could not be resolved");
						return;
					}
					await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] Previous tab: '{lastSelectedTabItem.Header}' (IsVisible={lastSelectedTabItem.IsVisible})");
					// Get the new selected TabItem
					// ReSharper disable once PossibleNullReferenceException
					if ( e.AddedItems.IsNullOrEmptyOrAllNull() || !(e.AddedItems[0] is TabItem attemptedTabSelection) )
					{
						await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Attempted tab item could not be resolved");
						return;
					}
					await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] Target tab: '{attemptedTabSelection.Header}' (IsVisible={attemptedTabSelection.IsVisible})");
					// Don't show content of any tabs (except the hidden one) if there's no content.
					if ( MainConfig.AllComponents.IsNullOrEmptyCollection() || CurrentComponent is null )
					{
						await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] No config loaded (ComponentCount={MainConfig.AllComponents.Count}), defaulting to initial tab");
						SetTabInternal(tabControl, InitialTab);
						return;
					}
					string tabName = GetControlNameFromHeader(attemptedTabSelection)?.ToLowerInvariant();
					string lastTabName = GetControlNameFromHeader(lastSelectedTabItem)?.ToLowerInvariant();
					await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] TabNames: from='{lastTabName}' to='{tabName}'");
					// do nothing if clicking the same tab
					if ( tabName == lastTabName )
					{
						await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] Selected tab is already the current tab '{tabName}', returning");
						return;
					}
					await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] Preparing to swap tabs from '{lastTabName}' to '{tabName}'");
					bool shouldSwapTabs = true;
					if ( tabName == "raw edit" )
					{
						await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Target is 'raw edit', loading into RawEditTextBox");
						shouldSwapTabs = await LoadIntoRawEditTextBox(CurrentComponent);
						await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] LoadIntoRawEditTextBox returned: {shouldSwapTabs}");
					}
					else if ( lastTabName == "raw edit" )
					{
						await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Source was 'raw edit', checking if changes should be saved");
						shouldSwapTabs = await ShouldSaveChanges();
						await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] ShouldSaveChanges returned: {shouldSwapTabs}");
						if ( shouldSwapTabs )
						{
							await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Unloading raw editor");
							RawEditTextBox.Text = string.Empty;
						}
					}
					else if ( tabName == "gui edit" )
					{
						await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] Target is 'gui edit', CurrentComponent='{CurrentComponent?.Name}', InstructionCount={CurrentComponent?.Instructions.Count}, OptionCount={CurrentComponent?.Options.Count}");
						await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] GuiEditGrid.DataContext is {(GuiEditGrid.DataContext == null ? "null" : (GuiEditGrid.DataContext == CurrentComponent ? "CurrentComponent" : "something else"))}");
					}
					// Prevent the attempted tab change
					if ( !shouldSwapTabs )
					{
						await Logger.LogVerboseAsync("[TabControl_SelectionChanged] shouldSwapTabs=false, reverting to previous tab");
						SetTabInternal(tabControl, lastSelectedTabItem);
						return;
					}
					await Logger.LogVerboseAsync("[TabControl_SelectionChanged] Setting visibility for controls based on selected tab");
					// Show/hide the appropriate content based on the selected tab
					RawEditTextBox.IsVisible = tabName == "raw edit";
					ApplyEditorButton.IsVisible = tabName == "raw edit";
					await Logger.LogVerboseAsync($"[TabControl_SelectionChanged] RawEditTextBox.IsVisible={RawEditTextBox.IsVisible}, ApplyEditorButton.IsVisible={ApplyEditorButton.IsVisible}");
					await Logger.LogVerboseAsync("[TabControl_SelectionChanged] COMPLETED SUCCESSFULLY");
				}
				catch ( Exception exception )
				{
					await Logger.LogExceptionAsync(exception, customMessage: "[TabControl_SelectionChanged] Inner exception");
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, customMessage: "[TabControl_SelectionChanged] Outer exception");
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

		//ReSharper disable once AsyncVoidMethod
		private async void LoadComponentDetails([NotNull] ModComponent selectedComponent)
		{
			if ( selectedComponent == null )
				throw new ArgumentNullException(nameof(selectedComponent));
			await Logger.LogVerboseAsync($"[LoadComponentDetails] START for component '{selectedComponent.Name}' (GUID={selectedComponent.Guid})");
			await Logger.LogVerboseAsync($"[LoadComponentDetails] CurrentComponent={(CurrentComponent == null ? "null" : $"'{CurrentComponent.Name}' (GUID={CurrentComponent.Guid})")}");
			bool confirmLoadOverwrite = true;
			string currentTabName = GetControlNameFromHeader(GetCurrentTabItem(TabControl))?.ToLowerInvariant();
			await Logger.LogVerboseAsync($"[LoadComponentDetails] Current tab: '{currentTabName}'");
			if ( currentTabName == "raw edit" )
			{
				await Logger.LogVerboseAsync("[LoadComponentDetails] Current tab is 'raw edit', loading into RawEditTextBox");
				confirmLoadOverwrite = await LoadIntoRawEditTextBox(selectedComponent);
				await Logger.LogVerboseAsync($"[LoadComponentDetails] LoadIntoRawEditTextBox returned: {confirmLoadOverwrite}");
			}
			else if ( selectedComponent != CurrentComponent )
			{
				await Logger.LogVerboseAsync("[LoadComponentDetails] Different component selected, checking if changes should be saved");
				confirmLoadOverwrite = await ShouldSaveChanges();
				await Logger.LogVerboseAsync($"[LoadComponentDetails] ShouldSaveChanges returned: {confirmLoadOverwrite}");
			}
			else
			{
				await Logger.LogVerboseAsync("[LoadComponentDetails] Same component already loaded, no changes to save");
			}
			if ( !confirmLoadOverwrite )
			{
				await Logger.LogVerboseAsync("[LoadComponentDetails] Load cancelled by user, returning");
				return;
			}
			// set the currently tracked component to what's being loaded.
			await Logger.LogVerboseAsync($"[LoadComponentDetails] Setting CurrentComponent to '{selectedComponent.Name}'");
			SetCurrentComponent(selectedComponent);
			await Logger.LogVerboseAsync("[LoadComponentDetails] SetCurrentComponent completed");
			// default to SummaryTabItem only when coming from InitialTab or invalid state
			// Don't switch tabs if user is already on a valid tab (Summary, GUI Edit, or Raw Edit)
			await Logger.LogVerboseAsync($"[LoadComponentDetails] InitialTab.IsSelected={InitialTab.IsSelected}, TabControl.SelectedIndex={TabControl.SelectedIndex}");
			if ( InitialTab.IsSelected || TabControl.SelectedIndex == int.MaxValue )
			{
				await Logger.LogVerboseAsync("[LoadComponentDetails] Switching to SummaryTabItem");
				SetTabInternal(TabControl, SummaryTabItem);
			}
			else
			{
				await Logger.LogVerboseAsync($"[LoadComponentDetails] Keeping current tab '{currentTabName}'");
			}
			await Logger.LogVerboseAsync("[LoadComponentDetails] COMPLETED");
		}

		public void SetCurrentComponent([CanBeNull] ModComponent c)
		{
			Logger.LogVerbose($"[SetCurrentComponent] START with component={(c == null ? "null" : $"'{c.Name}' (GUID={c.Guid})")}");
			// Track if this is a new component being loaded vs. refreshing the current one
			bool isNewComponent = c != null && c != CurrentComponent;
			Logger.LogVerbose($"[SetCurrentComponent] isNewComponent={isNewComponent}, CurrentComponent={(CurrentComponent == null ? "null" : $"'{CurrentComponent.Name}'")}");
			Logger.LogVerbose("[SetCurrentComponent] Setting CurrentComponent property");
			CurrentComponent = c;
			Logger.LogVerbose("[SetCurrentComponent] CurrentComponent property set");
			Logger.LogVerbose($"[SetCurrentComponent] Setting GuiEditGrid.DataContext (current={(GuiEditGrid.DataContext == null ? "null" : "not null")})");
			GuiEditGrid.DataContext = c;
			Logger.LogVerbose($"[SetCurrentComponent] GuiEditGrid.DataContext set to {(c == null ? "null" : $"'{c.Name}'")}");
			if ( c == null )
			{
				Logger.LogVerbose("[SetCurrentComponent] ModComponent is null, returning early");
				return;
			}
			// Make tabs visible when a component is selected
			Logger.LogVerbose($"[SetCurrentComponent] Making tabs visible (Summary={SummaryTabItem.IsVisible}, GuiEdit={GuiEditTabItem.IsVisible}, RawEdit={RawEditTabItem.IsVisible})");
			if ( EditorMode )
			{
				SummaryTabItem.IsVisible = true;
				GuiEditTabItem.IsVisible = true;
				RawEditTabItem.IsVisible = true;
			}
			Logger.LogVerbose("[SetCurrentComponent] Tabs visibility set to true");
			// Refresh category selection control with all available categories
			// RE-ENABLED: Testing CategorySelectionControl
			Logger.LogVerbose($"[SetCurrentComponent] Calling RefreshCategorySelectionControl for component '{c.Name}' with {c.Category.Count} categories");
			RefreshCategorySelectionControl();
			Logger.LogVerbose("[SetCurrentComponent] RefreshCategorySelectionControl completed");
			// Render markdown content for Description and Directions
			Logger.LogVerbose("[SetCurrentComponent] Rendering markdown content for Summary tab");
			RenderMarkdownContent(c);
			Logger.LogVerbose("[SetCurrentComponent] Markdown content rendering completed");
			// Only switch to Summary tab when loading a different component
			// Don't switch tabs when refreshing the current component (e.g., after deleting/moving instructions)
			Logger.LogVerbose($"[SetCurrentComponent] Tab check: isNewComponent={isNewComponent}, InitialTab.IsSelected={InitialTab.IsSelected}, SelectedIndex={TabControl.SelectedIndex}, GuiEditTabItem.IsSelected={GuiEditTabItem.IsSelected}, RawEditTabItem.IsSelected={RawEditTabItem.IsSelected}");
			if ( isNewComponent && (InitialTab.IsSelected || TabControl.SelectedIndex == int.MaxValue || (!GuiEditTabItem.IsSelected && !RawEditTabItem.IsSelected)) )
			{
				Logger.LogVerbose("[SetCurrentComponent] Switching to SummaryTabItem");
				SetTabInternal(TabControl, SummaryTabItem);
			}
			else
			{
				Logger.LogVerbose("[SetCurrentComponent] Not switching tabs");
			}
			Logger.LogVerbose("[SetCurrentComponent] COMPLETED");
		}

		/// <summary>
		/// Renders markdown content for the Description and Directions fields in the Summary tab
		/// </summary>
		/// <param name="component">The component to render markdown content for</param>
		private void RenderMarkdownContent([NotNull] ModComponent component)
		{
			_markdownRenderingService.RenderComponentMarkdown(component, DescriptionTextBlock, DirectionsTextBlock);
		}

		/// <summary>
		/// Removes zero-width break characters inserted for rendering-only wrapping.
		/// </summary>
		// No zero-width breaks are injected anymore; no cleanup required.
		private async Task<bool> LoadIntoRawEditTextBox([NotNull] ModComponent selectedComponent)
		{
			if ( selectedComponent is null )
				throw new ArgumentNullException(nameof(selectedComponent));

			// Delegate to ComponentEditorService for the core logic
			(bool success, string serializedContent) = await _componentEditorService.LoadIntoRawEditorAsync(selectedComponent, RawEditTextBox.Text);

			if ( success )
			{
				// Update the UI with the serialized content
				RawEditTextBox.Text = serializedContent;
			}

			return success;
		}

		// todo: figure out if this is needed.
		// ReSharper disable once MemberCanBeMadeStatic.Local
		private void RawEditTextBox_LostFocus([NotNull] object sender, [NotNull] RoutedEventArgs e) => e.Handled = true;
		/// <summary>
		/// Loads a TOML file with merge strategy choice
		/// </summary>
		/// <param name="filePath">Path to the TOML file</param>
		/// <param name="fileType">Type of file for display purposes</param>
		/// <returns>True if successful, false if cancelled or failed</returns>
		private async Task<bool> LoadTomlFile(string filePath, string fileType = "instruction file")
		{
			bool result = await _fileLoadingService.LoadTomlFileAsync(filePath, EditorMode, () => ProcessComponentsAsync(MainConfig.AllComponents), fileType);
			if ( result )
			{
				_lastLoadedFileName = _fileLoadingService.LastLoadedFileName;
			}
			return result;
		}

		/// <summary>
		///     Asynchronous method that determines if changes should be saved before performing an action.
		///     This method delegates to ComponentEditorService to handle the save logic and then refreshes the UI.
		/// </summary>
		/// <param name="noPrompt">A boolean flag indicating whether the user should be prompted to save changes. Default is false.</param>
		/// <returns>
		///     True if the changes should be saved or if no changes are detected. False if the user chooses not to save or if
		///     an error occurs.
		/// </returns>
		private async Task<bool> ShouldSaveChanges(bool noPrompt = false)
		{
			try
			{
				// Delegate to ComponentEditorService for the core save logic
				bool result = await _componentEditorService.SaveChangesAsync(CurrentComponent, RawEditTextBox.Text, noPrompt);

				if ( result && CurrentComponent != null )
				{
					// Refresh the UI to reflect any changes
					await ProcessComponentsAsync(MainConfig.AllComponents);
				}

				return result;
			}
			catch ( Exception ex )
			{
				string output = "An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialog(this, output + Environment.NewLine + ex.Message);
				return false;
			}
		}

		private async void MoveComponentListItem([CanBeNull] ModComponent componentToMove, int relativeIndex)
		{
			try
			{
				if ( componentToMove == null )
					return;
				_ = ModManagementService.MoveModRelative(componentToMove, relativeIndex);
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
						Dispatcher.UIThread.Post(() =>
						{
							// Fire and forget, exceptions are handled inside the task
							Task _ = ProcessComponentsAsync(MainConfig.AllComponents)
								.ContinueWith(t =>
								{
									try
									{
										if ( t.Exception != null )
										{
											// Flatten and log all exceptions
											Logger.LogException(t.Exception.Flatten());
										}
										else
										{
											UpdateModCounts();
										}
									}
									catch ( Exception ex )
									{
										Logger.LogException(ex);
									}
								}, TaskScheduler.FromCurrentSynchronizationContext());
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
						throw new ArgumentOutOfRangeException(nameof(e.Operation), $"Unexpected ModOperation value: {e.Operation}");
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
				string filePath = await SaveFile(saveFileName: defaultFileName);
				if ( filePath == null )
					return;
				bool success = await _fileLoadingService.SaveTomlFileAsync(filePath, MainConfig.AllComponents);
				if ( success )
				{
					_lastLoadedFileName = _fileLoadingService.LastLoadedFileName;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		public void ComponentCheckboxChecked(
			[NotNull] ModComponent component,
				[NotNull] HashSet<ModComponent> visitedComponents,
				bool suppressErrors = false
			)
		{
			_componentSelectionService.HandleComponentChecked(component, visitedComponents, suppressErrors, RefreshSingleComponentVisuals);
		}

		public void ComponentCheckboxUnchecked(
			[NotNull] ModComponent component,
				[CanBeNull] HashSet<ModComponent> visitedComponents,
				bool suppressErrors = false
			)
		{
			_componentSelectionService.HandleComponentUnchecked(component, visitedComponents ?? new HashSet<ModComponent>(), suppressErrors, RefreshSingleComponentVisuals);
			if ( !suppressErrors )
				UpdateModCounts();
		}

		// Set up the event handler for the checkbox
		private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
		{
			try
			{
				Logger.LogVerbose($"[OnCheckBoxChanged] START - sender type: {sender?.GetType().Name ?? "null"}");
				if ( !(sender is CheckBox checkBox) )
				{
					Logger.LogVerbose("[OnCheckBoxChanged] Sender is not a CheckBox, returning");
					return;
				}
				Logger.LogVerbose($"[OnCheckBoxChanged] CheckBox.IsChecked={checkBox.IsChecked}, Tag type: {checkBox.Tag?.GetType().Name ?? "null"}");
				if ( checkBox.Tag is ModComponent thisComponent )
				{
					Logger.LogVerbose($"[OnCheckBoxChanged] ModComponent: '{thisComponent.Name}' (GUID={thisComponent.Guid}), IsChecked={checkBox.IsChecked}");
					if ( checkBox.IsChecked == true )
					{
						Logger.LogVerbose($"[OnCheckBoxChanged] Checking component '{thisComponent.Name}'");
						ComponentCheckboxChecked(thisComponent, new HashSet<ModComponent>());
					}
					else if ( checkBox.IsChecked == false )
					{
						Logger.LogVerbose($"[OnCheckBoxChanged] Unchecking component '{thisComponent.Name}'");
						ComponentCheckboxUnchecked(thisComponent, new HashSet<ModComponent>());
					}
					else
					{
						Logger.LogVerbose($"[OnCheckBoxChanged] Could not determine checkBox state for component '{thisComponent.Name}' (IsChecked={checkBox.IsChecked})");
					}
					Logger.LogVerbose("[OnCheckBoxChanged] Updating step progress and mod counts");
					// Update step progress when mod selection changes
					UpdateStepProgress();
					UpdateModCounts();
					// Reset download status display when selections change
					ResetDownloadStatusDisplay();
					Logger.LogVerbose("[OnCheckBoxChanged] COMPLETED");
				}
				else if ( checkBox.Tag is Option thisOption )
				{
					Logger.LogVerbose($"[OnCheckBoxChanged] Option: '{thisOption.Name}' (GUID={thisOption.Guid}), IsChecked={checkBox.IsChecked}");
					// Options don't need the full component checkbox logic, just update filtered instructions
					Logger.LogVerbose("[OnCheckBoxChanged] COMPLETED (Option)");
				}
				else
				{
					Logger.LogVerbose("[OnCheckBoxChanged] CheckBox.Tag is neither a ModComponent nor an Option, returning early");
					return; // Early return to avoid expensive operations for non-mod/option checkboxes
				}
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception, customMessage: "[OnCheckBoxChanged] Exception occurred");
				Console.WriteLine(exception);
			}
		}

		// Public method that can be called from ModListItem
		public void OnComponentCheckBoxChanged(object sender, RoutedEventArgs e) => OnCheckBoxChanged(sender, e);
		private async Task ProcessComponentsAsync([NotNull][ItemNotNull] List<ModComponent> modComponentsList)
		{
			try
			{
				// Clear the existing list
				ModListBox?.Items.Clear();
				// Use the ComponentProcessingService to process components
				ComponentProcessingResult result = await Core.Services.ComponentProcessingService.ProcessComponentsAsync(modComponentsList);
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
				if ( !result.Success && result.HasCircularDependencies )
				{
					// Detect cycles and show resolution dialog
					CircularDependencyDetector.CircularDependencyResult cycleInfo =
						CircularDependencyDetector.DetectCircularDependencies(modComponentsList);
					// Only show dialog if there are actual circular dependencies
					if ( cycleInfo.HasCircularDependencies && cycleInfo.Cycles.Count > 0 )
					{
						(bool retry, List<ModComponent> resolvedComponents) = await CircularDependencyResolutionDialog.ShowResolutionDialog(
							this,
							modComponentsList,
							cycleInfo);
						if ( retry && resolvedComponents != null )
						{
							// User made changes and wants to retry - recursively call this method
							await ProcessComponentsAsync(resolvedComponents);
						}
					}
					return;
				}
				// Use reordered components if available
				List<ModComponent> componentsToProcess = result.ReorderedComponents ?? result.Components;
				// Populate the list box with components
				PopulateModList(componentsToProcess);
				if ( componentsToProcess.Count > 0 || TabControl is null )
				{
					// Show the tabs when components are loaded (only in EditorMode)
					if ( EditorMode )
					{
						SummaryTabItem.IsVisible = true;
						GuiEditTabItem.IsVisible = true;
						RawEditTabItem.IsVisible = true;
					}
					// Update step progress after components are loaded
					UpdateStepProgress();
					// Trigger validation scan now that components are loaded
					// This ensures file detection works regardless of whether paths or TOML was loaded first
					ScanModDirectoryForDownloads();
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
				await Logger.LogVerboseAsync($"[AddNewInstruction_Click] START - CurrentComponent={(CurrentComponent == null ? "null" : $"'{CurrentComponent.Name}'")}");
				if ( CurrentComponent is null )
				{
					await Logger.LogVerboseAsync("[AddNewInstruction_Click] CurrentComponent is null, showing dialog");
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}
				var addButton = (Button)sender;
				await Logger.LogVerboseAsync($"[AddNewInstruction_Click] Button tag type: {addButton.Tag?.GetType().Name ?? "null"}");
				var thisInstruction = addButton.Tag as Instruction;
				var thisComponent = addButton.Tag as ModComponent;
				if ( thisInstruction is null && thisComponent is null )
				{
					await Logger.LogErrorAsync("[AddNewInstruction_Click] Cannot find instruction or component instance from button tag");
					throw new NullReferenceException(message: "Cannot find instruction instance from button.");
				}
				int index;
				if ( !(thisComponent is null) )
				{
					await Logger.LogVerboseAsync($"[AddNewInstruction_Click] Tag is ModComponent '{thisComponent.Name}', creating new instruction");
					thisInstruction = new Instruction();
					index = thisComponent.Instructions.Count;
					await Logger.LogVerboseAsync($"[AddNewInstruction_Click] Creating instruction at index {index} (total instructions: {thisComponent.Instructions.Count})");
					thisComponent.CreateInstruction(index);
				}
				else
				{
					await Logger.LogVerboseAsync("[AddNewInstruction_Click] Tag is Instruction, getting parent component");
					ModComponent parentComponent = thisInstruction.GetParentComponent();
					await Logger.LogVerboseAsync($"[AddNewInstruction_Click] Parent component: '{parentComponent?.Name}'");
					index = parentComponent?.Instructions.IndexOf(thisInstruction) ?? throw new NullReferenceException($"Could not get index of instruction '{thisInstruction.Action}' in null parentComponent'");
					await Logger.LogVerboseAsync($"[AddNewInstruction_Click] Creating instruction at index {index}");
					parentComponent.CreateInstruction(index);
				}
				await Logger.LogVerboseAsync($"[AddNewInstruction_Click] Instruction '{thisInstruction.Action}' created at index #{index} for component '{CurrentComponent.Name}'");
				await Logger.LogVerboseAsync("[AddNewInstruction_Click] Calling LoadComponentDetails");
				LoadComponentDetails(CurrentComponent);
				await Logger.LogVerboseAsync("[AddNewInstruction_Click] COMPLETED");
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception, customMessage: "[AddNewInstruction_Click] Exception occurred");
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
				InstructionManagementService.DeleteInstruction(thisInstruction.GetParentComponent(), index);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}

		[UsedImplicitly]
		private async void AutoGenerateInstructions_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				if ( CurrentComponent is null )
				{
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}
				await Logger.LogVerboseAsync($"[AutoGenerateInstructions_Click] START - CurrentComponent='{CurrentComponent.Name}'");
				// Show confirmation dialog to choose source
				bool? useModLinks = await ConfirmationDialog.ShowConfirmationDialog(
					this,
					"Where would you like to source these instructions from?",
					"From Mod Links",
					"From Archive on Disk");
				if ( useModLinks == null )
				{
					await Logger.LogVerboseAsync("[AutoGenerateInstructions_Click] User cancelled source selection");
					return;
				}
				// Confirm before replacing existing instructions
				if ( CurrentComponent.Instructions.Count > 0 )
				{
					bool? confirmed = await ConfirmationDialog.ShowConfirmationDialog(
						this,
						"Replace Existing Instructions",
						"This will replace all existing instructions with auto-generated ones. Continue?");
					if ( confirmed != true )
					{
						await Logger.LogVerboseAsync("[AutoGenerateInstructions_Click] User cancelled replacement confirmation");
						return;
					}
				}
				if ( useModLinks == true )
				{
					await GenerateInstructionsFromModLinks();
				}
				else
				{
					await GenerateInstructionsFromArchive();
				}
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
				await InformationDialog.ShowInformationDialog(this, message: $"Error generating instructions: {exception.Message}");
			}
		}
		private async Task GenerateInstructionsFromModLinks()
		{
			int result = await _instructionGenerationService.GenerateInstructionsFromModLinksAsync(CurrentComponent);
			if ( result > 0 )
				LoadComponentDetails(CurrentComponent);
		}
		private async Task GenerateInstructionsFromArchive()
		{
			bool success = await _instructionGenerationService.GenerateInstructionsFromArchiveAsync(CurrentComponent, () => ShowFileDialog(false, false, null, "Select the mod archive to analyze for auto-generation"));
			if ( success )
				LoadComponentDetails(CurrentComponent);
		}
		/// <summary>
		/// Gets the relative path from a base path to a target path.
		/// </summary>
		private static string GetRelativePath(string basePath, string targetPath)
		{
			if ( string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(targetPath) )
				return targetPath;
			// Normalize paths
			basePath = Path.GetFullPath(basePath);
			targetPath = Path.GetFullPath(targetPath);
			// Check if target is within base path
			if ( !targetPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) )
				return Path.GetFileName(targetPath);
			// Get relative path
			string relativePath = targetPath.Substring(basePath.Length);
			if ( relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()) )
				relativePath = relativePath.Substring(1);
			return relativePath;
		}
		/// <summary>
		/// Downloads a mod from a URL using the existing download system.
		/// </summary>
		private async Task<string> DownloadModFromUrl(string url)
		{
			return await DownloadOrchestrationService.DownloadModFromUrlAsync(url, CurrentComponent);
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
				InstructionManagementService.MoveInstruction(CurrentComponent, thisInstruction, index - 1);
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
				InstructionManagementService.MoveInstruction(CurrentComponent, thisInstruction, index + 1);
				LoadComponentDetails(CurrentComponent);
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception);
			}
		}
		[UsedImplicitly]
		private void OpenOutputWindow_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
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
				if ( !result )
					return;
				// Apply theme changes
				string selectedTheme = settingsDialog.GetSelectedTheme();
				ApplyTheme(selectedTheme);
				// Save settings after applying changes
				SaveSettings();
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}
		[UsedImplicitly]
		private void CreateGithubIssue_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			try
			{
				UrlUtilities.OpenUrl("https://github.com/th3w1zard1/KOTORModSync/issues/new");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to open GitHub issue creation page");
			}
		}
		private static void ApplyTheme(string stylePath)
		{
			ThemeService.ApplyTheme(stylePath);
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

				ThemeService.ApplyTheme(stylePath);
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
				await Logger.LogVerboseAsync($"[AddNewOption_Click] START - CurrentComponent={(CurrentComponent == null ? "null" : $"'{CurrentComponent.Name}'")}");
				if ( CurrentComponent is null )
				{
					await Logger.LogVerboseAsync("[AddNewOption_Click] CurrentComponent is null, showing dialog");
					await InformationDialog.ShowInformationDialog(this, message: "Load a component first");
					return;
				}
				var addButton = (Button)sender;
				await Logger.LogVerboseAsync($"[AddNewOption_Click] Button tag type: {addButton.Tag?.GetType().Name ?? "null"}");
				var thisOption = addButton.Tag as Option;
				var thisComponent = addButton.Tag as ModComponent;
				if ( thisOption is null && thisComponent is null )
				{
					await Logger.LogErrorAsync("[AddNewOption_Click] Cannot find option or component instance from button tag");
					throw new NullReferenceException("Cannot find option instance from button.");
				}
				int index;
				if ( thisOption is null )
				{
					await Logger.LogVerboseAsync("[AddNewOption_Click] Tag is ModComponent, creating new option");
					thisOption = new Option();
					index = CurrentComponent.Options.Count;
					await Logger.LogVerboseAsync($"[AddNewOption_Click] Creating option at index {index} (total options: {CurrentComponent.Options.Count})");
				}
				else
				{
					await Logger.LogVerboseAsync($"[AddNewOption_Click] Tag is Option '{thisOption.Name}', getting index");
					index = CurrentComponent.Options.IndexOf(thisOption);
					await Logger.LogVerboseAsync($"[AddNewOption_Click] Creating option at index {index}");
				}
				CurrentComponent.CreateOption(index);
				await Logger.LogVerboseAsync($"[AddNewOption_Click] Option '{thisOption.Name}' created at index #{index} for component '{CurrentComponent.Name}'");
				await Logger.LogVerboseAsync("[AddNewOption_Click] Calling LoadComponentDetails");
				LoadComponentDetails(CurrentComponent);
				// Refresh the mod list item visual to immediately show the options section
				RefreshSingleComponentVisuals(CurrentComponent);
				await Logger.LogVerboseAsync("[AddNewOption_Click] COMPLETED");
			}
			catch ( Exception exception )
			{
				await Logger.LogExceptionAsync(exception, customMessage: "[AddNewOption_Click] Exception occurred");
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
				InstructionManagementService.DeleteOption(CurrentComponent, index);
				LoadComponentDetails(CurrentComponent);
				// Refresh the mod list item visual to immediately hide the options section if empty
				RefreshSingleComponentVisuals(CurrentComponent);
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
				InstructionManagementService.MoveOption(CurrentComponent, thisOption, index - 1);
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
				InstructionManagementService.MoveOption(CurrentComponent, thisOption, index + 1);
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
				// Show validation results
				ShowValidationResults();
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
			// Navigate to Load File menu item
			Menu topMenu = this.FindControl<Menu>(name: "TopMenu");
			if ( topMenu?.Items.Count > 0 && topMenu.Items[1] is MenuItem fileMenu )
			{
				if ( fileMenu.Items.Count > 0 && fileMenu.Items[0] is MenuItem loadFileItem )
				{
					// Simulate clicking the Load File menu item
					await Task.Delay(millisecondsDelay: 100); // Brief delay for UI responsiveness
					LoadFile_Click(loadFileItem, new RoutedEventArgs());
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
		/// <summary>
		/// Checks if Step 1 (directory setup) is properly completed
		/// </summary>
		private bool IsStep1Complete() => ValidationService.IsStep1Complete();
		private void UpdateStepProgress()
		{
			try
			{
				_uiStateService.UpdateStepProgress(
					this.FindControl<Border>("Step1Border"), this.FindControl<Border>("Step1CompleteIndicator"), this.FindControl<TextBlock>("Step1CompleteText"),
					this.FindControl<Border>("Step2Border"), this.FindControl<Border>("Step2CompleteIndicator"), this.FindControl<TextBlock>("Step2CompleteText"),
					this.FindControl<Border>("Step3Border"), this.FindControl<Border>("Step3CompleteIndicator"), this.FindControl<TextBlock>("Step3CompleteText"),
					this.FindControl<Border>("Step4Border"), this.FindControl<Border>("Step4CompleteIndicator"), this.FindControl<TextBlock>("Step4CompleteText"),
					this.FindControl<Border>("Step5Border"), this.FindControl<Border>("Step5CompleteIndicator"), this.FindControl<TextBlock>("Step5CompleteText"),
					this.FindControl<ProgressBar>("OverallProgressBar"), this.FindControl<TextBlock>("ProgressText"),
					this.FindControl<CheckBox>("Step5Checkbox"),
					EditorMode,
					IsComponentValidForInstallation
				);
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception);
			}
		}
		/// <summary>
		/// Checks if a component is valid for installation using the same logic as ModListItem validation
		/// </summary>
		private bool IsComponentValidForInstallation(ModComponent component) => _validationService.IsComponentValidForInstallation(component, EditorMode);
		/// <summary>
		/// Validates that all ModLinks are valid URLs
		/// </summary>
		private bool AreModLinksValid(List<string> modLinks) => ValidationService.AreModLinksValid(modLinks);
		/// <summary>
		/// Checks if a string is a valid URL
		/// </summary>
		private bool IsValidUrl(string url) => ValidationService.IsValidUrl(url);
		// Directory Picker Event Handler - handles both sidebar and Step 1 directory changes
		private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
		{
			try
			{
				switch ( e.PickerType )
				{
					case DirectoryPickerType.ModDirectory:
						// Update MainConfig
						MainConfigInstance.sourcePath = new DirectoryInfo(e.Path);
						Logger.Log($"Mod directory set to: {e.Path}");
						// Update all mod directory pickers
						SyncDirectoryPickers(DirectoryPickerType.ModDirectory, e.Path);
						break;
					case DirectoryPickerType.KotorDirectory:
						// Update MainConfig
						MainConfigInstance.destinationPath = new DirectoryInfo(e.Path);
						Logger.Log($"KOTOR installation directory set to: {e.Path}");
						// Update all kotor directory pickers
						SyncDirectoryPickers(DirectoryPickerType.KotorDirectory, e.Path);
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(e.PickerType), e.PickerType, "Invalid DirectoryPickerType value in OnDirectoryChanged.");
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
			_fileSystemService.SetupModDirectoryWatcher(path, ScanModDirectoryForDownloads);
			// Initial scan - only if components are already loaded
			if ( MainConfig.AllComponents.Count > 0 )
				ScanModDirectoryForDownloads();
		}
		private void ScanModDirectoryForDownloads()
		{
			// Run in background thread to avoid blocking UI
			Task.Run(() =>
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
					Logger.LogVerbose($"[FileValidation] Scanning {MainConfig.AllComponents.Count} components for download status");

					// Check each component using DownloadCacheService only
					foreach ( ModComponent component in MainConfig.AllComponents )
					{
						if ( !component.IsSelected )
							continue;
						totalSelected++;
						Logger.LogVerbose($"[FileValidation] Checking component: {component.Name} (GUID: {component.Guid})");

						// Check if all URLs are cached in DownloadCacheService
						bool allUrlsCached = true;
						if ( component.ModLink != null && component.ModLink.Count > 0 )
						{
							Logger.LogVerbose($"[FileValidation] ModComponent has {component.ModLink.Count} URLs:");
							foreach ( string url in component.ModLink )
							{
								// Check cache status for each URL
								bool isCached = _downloadCacheService.IsCached(component.Guid, url);
								if ( isCached )
								{
									string cachedArchive = _downloadCacheService.GetArchiveName(component.Guid, url);
									Logger.LogVerbose($"[FileValidation]   URL: {url} - CACHED (Archive: {cachedArchive})");
								}
								else
								{
									Logger.LogVerbose($"[FileValidation]   URL: {url} - NOT CACHED");
									allUrlsCached = false;
								}
							}
						}
						else
						{
							Logger.LogVerbose($"[FileValidation] ModComponent has no URLs");
							allUrlsCached = false; // No URLs means not downloaded
						}

						// Set download status based on cache only
						component.IsDownloaded = allUrlsCached;
						Logger.LogVerbose($"[FileValidation] ModComponent '{component.Name}': {(allUrlsCached ? "DOWNLOADED" : "MISSING")}");
						if ( allUrlsCached ) downloadedCount++;
					}

					Logger.LogVerbose($"Download scan complete: {downloadedCount}/{totalSelected} mods ready");

					// Update UI on the UI thread
					Dispatcher.UIThread.Post(() =>
					{
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

						// Refresh the mod list items to update tooltips and validation states
						RefreshModListItems();
						// Update step progress to reflect any changes in download status
						UpdateStepProgress();
					});
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Error scanning mod directory for downloads");
				}
			});
		}
		/// <summary>
		/// Resets the download status display to its default state when selections change.
		/// This prevents stale download counts from being displayed.
		/// </summary>
		private void ResetDownloadStatusDisplay()
		{
			try
			{
				TextBlock statusText = this.FindControl<TextBlock>("DownloadStatusText");
				if ( statusText != null )
				{
					// Reset to default state - this will be updated when ScanModDirectoryForDownloads is called
					statusText.Text = "Checking download status...";
					statusText.Foreground = Brushes.Gray;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error resetting download status display");
			}
		}
		/// <summary>
		/// Validates that all required files for a component exist using ComponentValidation logic.
		/// This matches the validation logic used in PreinstallValidationService for consistency.
		/// </summary>
		/// <param name="component">The component to validate</param>
		/// <returns>True if all required files exist, false otherwise</returns>
		private bool ValidateComponentFilesExist(ModComponent component)
		{
			return ValidationService.ValidateComponentFilesExist(component);
		}
		private async void ScrapeDownloadsButton_Click(object sender, RoutedEventArgs e)
		{
			await _downloadOrchestrationService.StartDownloadSessionAsync(ScanModDirectoryForDownloads);
		}

		private async void DownloadStatusButton_Click(object sender, RoutedEventArgs e)
		{
			await _downloadOrchestrationService.ShowDownloadStatusAsync();
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
		private static async Task TryAutoGenerateInstructionsForComponents(List<ModComponent> components)
		{
			await Core.Services.ComponentProcessingService.TryAutoGenerateInstructionsForComponentsAsync(components);
		}
		#region Selection Methods
		// Selection by tier/category
		private readonly ObservableCollection<TierFilterItem> _tierItems = new ObservableCollection<TierFilterItem>();
		private readonly ObservableCollection<SelectionFilterItem> _categoryItems = new ObservableCollection<SelectionFilterItem>();
		private void RefreshCategorySelectionControl()
		{
			try
			{
				Logger.LogVerbose($"[RefreshCategorySelectionControl] START - CurrentComponent={(CurrentComponent == null ? "null" : $"'{CurrentComponent.Name}'")}");
				Logger.LogVerbose($"[RefreshCategorySelectionControl] AllComponents count={MainConfig.AllComponents.Count}");
				CategorySelectionControl categoryControl = this.FindControl<CategorySelectionControl>("CategorySelectionControl");
				Logger.LogVerbose($"[RefreshCategorySelectionControl] CategorySelectionControl found: {categoryControl != null}");
				if ( categoryControl != null )
				{
					Logger.LogVerbose($"[RefreshCategorySelectionControl] Calling RefreshCategories with {MainConfig.AllComponents.Count} components");
					categoryControl.RefreshCategories(MainConfig.AllComponents);
					Logger.LogVerbose("[RefreshCategorySelectionControl] RefreshCategories completed");
					// Set the selected categories from the current component
					if ( CurrentComponent != null )
					{
						Logger.LogVerbose($"[RefreshCategorySelectionControl] Setting SelectedCategories from CurrentComponent (count={CurrentComponent.Category.Count})");
						if ( CurrentComponent.Category.Count > 0 )
						{
							Logger.LogVerbose($"[RefreshCategorySelectionControl] Categories to set: {string.Join(", ", CurrentComponent.Category)}");
						}
						categoryControl.SelectedCategories = CurrentComponent.Category;
						Logger.LogVerbose("[RefreshCategorySelectionControl] SelectedCategories set");
					}
					else
					{
						Logger.LogVerbose("[RefreshCategorySelectionControl] CurrentComponent is null, not setting SelectedCategories");
					}
				}
				else
				{
					Logger.LogVerbose("[RefreshCategorySelectionControl] CategorySelectionControl is null, cannot refresh");
				}
				Logger.LogVerbose("[RefreshCategorySelectionControl] COMPLETED");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "[RefreshCategorySelectionControl] Exception occurred");
			}
		}
		private void SelectAll_Click(object sender, RoutedEventArgs e)
		{
			_selectionService.SelectAll((component, visited) => ComponentCheckboxChecked(component, visited));
			UpdateModCounts();
			RefreshModListVisuals();
			UpdateStepProgress();
		}

		private void DeselectAll_Click(object sender, RoutedEventArgs e)
		{
			_selectionService.DeselectAll((component, visited) => ComponentCheckboxUnchecked(component, visited));
			UpdateModCounts();
			RefreshModListVisuals();
			UpdateStepProgress();
		}
		private void InitializeFilterUi(List<ModComponent> components)
		{
			_filterUIService.InitializeFilters(components, TierSelectionComboBox, CategorySelectionItemsControl);
		}
		private void CategoryItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// Category checkbox changed - does nothing until "Apply Category Selections" is clicked
			// No filtering - just tracks what the user selected
		}
		private void SelectByTier_Click(object sender, RoutedEventArgs e)
		{
			var selectedTier = TierSelectionComboBox?.SelectedItem as TierFilterItem;
			_filterUIService.SelectByTier(selectedTier, (c, visited) => ComponentCheckboxChecked(c, visited), () =>
			{
				UpdateModCounts();
				RefreshModListVisuals();
				UpdateStepProgress();
			});
		}
		private void ClearCategorySelection_Click(object sender, RoutedEventArgs e)
		{
			_filterUIService.ClearCategorySelections((item, handler) => item.PropertyChanged += handler);
		}
		private void ApplyCategorySelections_Click(object sender, RoutedEventArgs e)
		{
			_filterUIService.ApplyCategorySelections((c, visited) => ComponentCheckboxChecked(c, visited), () =>
			{
				UpdateModCounts();
				RefreshModListVisuals();
				UpdateStepProgress();
			});
		}
		#endregion
		private void ToggleErroredMods_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is CheckBox checkBox) )
				return;

			bool shouldSelect = checkBox.IsChecked == true;
			_selectionService.ToggleErroredMods(
				shouldSelect,
				IsComponentValidForInstallation,
				(c, visited) => ComponentCheckboxChecked(c, visited),
				(c, visited) => ComponentCheckboxUnchecked(c, visited)
			);

			UpdateModCounts();
			UpdateStepProgress();
		}
		private void ExpandAllSections_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				BasicInfoExpander.IsExpanded = true;
				DescriptionExpander.IsExpanded = true;
				DependenciesExpander.IsExpanded = true;
				InstructionsExpander.IsExpanded = true;
				OptionsExpander.IsExpanded = true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error expanding all sections");
			}
		}
		private void CollapseAllSections_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				BasicInfoExpander.IsExpanded = false;
				DescriptionExpander.IsExpanded = false;
				DependenciesExpander.IsExpanded = false;
				InstructionsExpander.IsExpanded = false;
				OptionsExpander.IsExpanded = false;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error collapsing all sections");
			}
		}
		[UsedImplicitly]
		private async void JumpToCurrentStep_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Find the ScrollViewer in the Getting Started tab
				TabItem gettingStartedTab = this.FindControl<TabItem>("InitialTab");
				if ( !(gettingStartedTab?.Content is ScrollViewer scrollViewer) )
					return;

				await _stepNavigationService.JumpToCurrentStepAsync(scrollViewer, name => this.FindControl<Border>(name));
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}
		#region Validation Results Display

		private void ShowValidationResults()
		{
			_validationDisplayService.ShowValidationResults(
				this.FindControl<Border>("ValidationResultsArea"),
				this.FindControl<TextBlock>("ValidationSummaryText"),
				this.FindControl<Border>("ErrorNavigationArea"),
				this.FindControl<Border>("ErrorDetailsArea"),
				this.FindControl<Border>("ValidationSuccessArea"),
				IsComponentValidForInstallation
			);
		}

		private (string ErrorType, string Description, bool CanAutoFix) GetComponentErrorDetails(ModComponent component)
		{
			return _validationService.GetComponentErrorDetails(component);
		}
		private void PrevErrorButton_Click(object sender, RoutedEventArgs e)
		{
			_validationDisplayService.NavigateToPreviousError(
				this.FindControl<TextBlock>("ErrorCounterText"),
				this.FindControl<TextBlock>("ErrorModNameText"),
				this.FindControl<TextBlock>("ErrorTypeText"),
				this.FindControl<TextBlock>("ErrorDescriptionText"),
				this.FindControl<Button>("AutoFixButton"),
				this.FindControl<Button>("PrevErrorButton"),
				this.FindControl<Button>("NextErrorButton")
			);
		}

		private void NextErrorButton_Click(object sender, RoutedEventArgs e)
		{
			_validationDisplayService.NavigateToNextError(
				this.FindControl<TextBlock>("ErrorCounterText"),
				this.FindControl<TextBlock>("ErrorModNameText"),
				this.FindControl<TextBlock>("ErrorTypeText"),
				this.FindControl<TextBlock>("ErrorDescriptionText"),
				this.FindControl<Button>("AutoFixButton"),
				this.FindControl<Button>("PrevErrorButton"),
				this.FindControl<Button>("NextErrorButton")
			);
		}

		private void AutoFixButton_Click(object sender, RoutedEventArgs e)
		{
			if ( _validationDisplayService.AutoFixCurrentError(RefreshSingleComponentVisuals) )
				ShowValidationResults();
		}

		private void JumpToModButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ModComponent currentError = _validationDisplayService.GetCurrentError();
				if ( currentError == null )
					return;

				(string ErrorType, string Description, bool CanAutoFix) = _validationService.GetComponentErrorDetails(currentError);

				// Select the mod in the mod list first
				if ( ModListBox?.ItemsSource != null )
				{
					ModListBox.SelectedItem = currentError;
					ModListBox.ScrollIntoView(currentError);
				}

				// Switch to appropriate tab based on error type
				TabControl tabControl = this.FindControl<TabControl>("InitialTab");
				if ( tabControl != null )
				{
					// For URL validation errors, switch to GUI Edit tab to show download links
					if ( ErrorType.Contains("Invalid download URLs") )
					{
						TabItem guiEditTab = this.FindControl<TabItem>("GuiEditTabItem");
						if ( guiEditTab != null )
							tabControl.SelectedItem = guiEditTab;
					}
					else
					{
						// For other errors, switch to Summary tab
						TabItem summaryTab = this.FindControl<TabItem>("SummaryTabItem");
						if ( summaryTab != null )
							tabControl.SelectedItem = summaryTab;
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error in JumpToModButton_Click");
			}
		}
		#endregion
		#region URL Validation Helper Methods
		private string GetUrlValidationReason([CanBeNull] string url)
		{
			return ValidationService.GetUrlValidationReason(url);
		}
		#endregion
		private async void JumpToInstruction_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			if ( sender is Button button && button.Tag is Instruction instruction )
			{
				try
				{
					// Switch to GUI Edit mode
					EditorMode = true;
					// Get the ScrollViewer in the GUI Edit tab
					ScrollViewer scrollViewer = ScrollNavigationService.FindScrollViewer(GuiEditTabItem);
					// Use the modular navigation service
					await ScrollNavigationService.NavigateToControlAsync(
						tabItem: GuiEditTabItem,
						expander: InstructionsExpander,
						scrollViewer: scrollViewer,
						targetControl: FindInstructionEditorControl(instruction)
					);
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Failed to jump to instruction");
				}
			}
		}

		[CanBeNull]
		private InstructionEditorControl FindInstructionEditorControl(Instruction targetInstruction)
		{
			if ( !(InstructionsRepeater is null) )
			{
				// Use the modular service to find the control
				return ScrollNavigationService.FindControlRecursive<InstructionEditorControl>(
					InstructionsRepeater.Parent as Control,
					control =>
					{
						if ( control.DataContext is Instruction instruction &&
							   instruction.Guid == targetInstruction.Guid )
						{
							return true;
						}

						return false;
					});
			}
			return null;
		}
	}
}
