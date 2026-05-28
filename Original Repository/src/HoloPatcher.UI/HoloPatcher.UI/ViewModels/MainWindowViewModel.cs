using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KOTORModSync;
using KOTORModSync.Common;
using KOTORModSync.Config;
using KOTORModSync.Installation;
using KOTORModSync.Logger;
using KOTORModSync.Namespaces;
using KOTORModSync.TSLPatcher;
using KOTORModSync.Reader;
using KOTORModSync.Uninstall;
using Andastra.Utility;
using HoloPatcher.UI;
using HoloPatcher.UI.Rte;
using HoloPatcher.UI.Update;
using HoloPatcher.UI.Views;
using HoloPatcher.UI.Views.Dialogs;
using JetBrains.Annotations;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HoloPatcher.UI.ViewModels
{

    public class MainWindowViewModel : ViewModelBase
    {
        private readonly PatchLogger _logger = new PatchLogger();
        private readonly RobustLogger _pykotorLogger;
        private readonly StringBuilder _logTextBuilder = new StringBuilder();
        private readonly List<FormattedLogEntry> _logEntries = new List<FormattedLogEntry>();

        /// <summary>
        /// Gets all log entries for formatting. Used by View to format log display.
        /// </summary>
        public IReadOnlyList<FormattedLogEntry> GetLogEntries() => _logEntries.AsReadOnly();

        private string _logText = string.Empty;
        private bool _isTaskRunning;
        private int _progressValue;
        private int _progressMaximum = 100;
        [CanBeNull]
        private string _selectedNamespace;
        [CanBeNull]
        private string _selectedGamePath;
        private string _modPath = string.Empty;
        private Avalonia.Media.IBrush _logTextColor = Avalonia.Media.Brushes.White;
        private bool _isRtfContent = false;
        private string _rtfContent = string.Empty;
        private bool _isModSelectionVisible = true;
        private RteDocument _activeRteDocument;

        /// <summary>
        /// Whether the mod selection UI (combobox, Browse button, ? button) should be visible.
        /// Set to false when a mod is auto-opened from a tslpatchdata folder next to the executable.
        /// </summary>
        public bool IsModSelectionVisible
        {
            get => _isModSelectionVisible;
            set => SetProperty(ref _isModSelectionVisible, value);
        }

        public RteDocument ActiveRteDocument
        {
            get => _activeRteDocument;
            private set => SetProperty(ref _activeRteDocument, value);
        }

        /// <summary>
        /// Whether the current version is an alpha/pre-release version.
        /// Used to conditionally show alpha warnings and disclaimers.
        /// </summary>
        public bool IsAlphaVersion => Core.IsAlphaVersion(Core.VersionLabel);

        /// <summary>
        /// Window title matching Python's format: "HoloPatcher {VERSION_LABEL}"
        /// Includes alpha disclaimer only if version is alpha.
        /// </summary>
        public string WindowTitle
        {
            get
            {
                string title = $"HoloPatcher {Core.VersionLabel}";
                if (IsAlphaVersion)
                {
                    title += " [ALPHA - NOT FOR PRODUCTION USE]";
                }
                return title;
            }
        }

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        /// <summary>
        /// Color for the log text - changes based on log type for consistency with Python
        /// </summary>
        public Avalonia.Media.IBrush LogTextColor
        {
            get => _logTextColor;
            set => SetProperty(ref _logTextColor, value);
        }

        /// <summary>
        /// Whether we're displaying RTF content (true) or plain text logs (false)
        /// </summary>
        public bool IsRtfContent
        {
            get => _isRtfContent;
            set
            {
                if (SetProperty(ref _isRtfContent, value))
                {
                    OnPropertyChanged(nameof(IsNotRtfContent));
                    Console.WriteLine($"[RTF] IsRtfContent set to: {value}");
                }
            }
        }

        /// <summary>
        /// Inverse of IsRtfContent for XAML binding
        /// </summary>
        public bool IsNotRtfContent => !_isRtfContent;

        /// <summary>
        /// RTF content to display in RichTextBox
        /// </summary>
        public string RtfContent
        {
            get => _rtfContent;
            set
            {
                if (SetProperty(ref _rtfContent, value))
                {
                    Console.WriteLine($"[RTF] RtfContent set, length: {value?.Length ?? 0}");
                }
            }
        }

        public bool IsTaskRunning
        {
            get => _isTaskRunning;
            set
            {
                if (SetProperty(ref _isTaskRunning, value))
                {
                    OnIsTaskRunningChanged(value);
                }
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public int ProgressMaximum
        {
            get => _progressMaximum;
            set => SetProperty(ref _progressMaximum, value);
        }

        [CanBeNull]
        public string SelectedNamespace
        {
            get => _selectedNamespace;
            set
            {
                if (SetProperty(ref _selectedNamespace, value))
                {
                    OnSelectedNamespaceChanged(value);
                }
            }
        }

        [CanBeNull]
        public string SelectedGamePath
        {
            get => _selectedGamePath;
            set
            {
                if (SetProperty(ref _selectedGamePath, value))
                {
                    OnSelectedGamePathChanged(value);
                }
            }
        }

        public string ModPath
        {
            get => _modPath;
            set
            {
                if (SetProperty(ref _modPath, value))
                {
                    // Update RobustLogger to write to installlog.txt when ModPath is set
                    if (!string.IsNullOrEmpty(value))
                    {
                        string logFilePath = Core.GetLogFilePath(value);
                        _pykotorLogger.SetLogFilePath(logFilePath);
                    }
                }
            }
        }

        private List<PatcherNamespace> _loadedNamespaces = new List<PatcherNamespace>();
        // Can be null if no cancellation token source created
        private CancellationTokenSource _cancellationTokenSource;
        // Can be null if no config reader loaded
        private ConfigReader _currentConfigReader;
        private LogLevel _logLevel = LogLevel.Warnings;
        private readonly bool _oneShot = false;

        public ObservableCollection<string> Namespaces { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> GamePaths { get; } = new ObservableCollection<string>();

        public bool CanInstall => !IsTaskRunning &&
                                  !string.IsNullOrEmpty(SelectedNamespace) &&
                                  !string.IsNullOrEmpty(SelectedGamePath);

        public ICommand BrowseModCommand { get; }
        public ICommand BrowseGamePathCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ValidateIniCommand { get; }
        public ICommand UninstallModCommand { get; }
        public ICommand FixPermissionsCommand { get; }
        public ICommand FixCaseSensitivityCommand { get; }
        public ICommand OpenUrlCommand { get; }
        public ICommand CheckUpdatesCommand { get; }
        public ICommand ShowNamespaceInfoCommand { get; }
        public ICommand CreateRteCommand { get; }

        public MainWindowViewModel()
        {
            // Initialize RobustLogger for pykotor errors/exceptions/warnings/info
            // Will set log file path when mod is loaded
            _pykotorLogger = new RobustLogger();

            // Initialize commands
            BrowseModCommand = new AsyncRelayCommand(BrowseMod);
            BrowseGamePathCommand = new AsyncRelayCommand(BrowseGamePath);
            InstallCommand = new AsyncRelayCommand(Install);
            ExitCommand = new RelayCommand(Exit);
            ValidateIniCommand = new AsyncRelayCommand(ValidateIni);
            UninstallModCommand = new AsyncRelayCommand(UninstallMod);
            FixPermissionsCommand = new AsyncRelayCommand(FixPermissions);
            FixCaseSensitivityCommand = new AsyncRelayCommand(FixCaseSensitivity);
            OpenUrlCommand = new RelayCommand<string>(OpenUrl);
            CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdates);
            ShowNamespaceInfoCommand = new AsyncRelayCommand(ShowNamespaceInfo);
            CreateRteCommand = new AsyncRelayCommand(CreateRte);

            // Subscribe to logger events
            _logger.VerboseLogged += OnLogEntry;
            _logger.NoteLogged += OnLogEntry;
            _logger.WarningLogged += OnLogEntry;
            _logger.ErrorLogged += OnLogEntry;

            // Initialize with welcome message
            AddLogEntry("Welcome to HoloPatcher!", LogType.Note);
            AddLogEntry("Select a mod and your KOTOR directory to begin.", LogType.Note);

            // Try to detect KOTOR installations
            DetectGamePaths();

            // Try to auto-open mod from tslpatchdata folder next to executable
            _ = TryAutoOpenMod();
        }

        private void OnLogEntry([CanBeNull] object sender, PatchLog log)
        {
            WriteLogEntry(log);
        }

        private void WriteLogEntry(PatchLog log)
        {
            // Write ALL logs to file regardless of log level - matches Python behavior
            // Python: log_file.write(f"{log.formatted_message}\n") happens BEFORE filtering
            try
            {
                string logFilePath = Core.GetLogFilePath(ModPath);
                if (!string.IsNullOrEmpty(logFilePath))
                {
                    string directory = Path.GetDirectoryName(logFilePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.AppendAllText(logFilePath, log.FormattedMessage + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail
                _pykotorLogger.Error($"Failed to write log file: {ex.Message}");
                Debug.WriteLine($"Failed to write log file: {ex.Message}");
            }

            // Filter by log level for UI display only
            LogType minLevel = GetLogTypeForLevel(_logLevel);
            if ((int)log.LogType < (int)minLevel)
            {
                return;
            }

            // Add to UI with formatting
            AddLogEntry(log.FormattedMessage, log.LogType);
        }

        private LogType GetLogTypeForLevel(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Errors:
                    return LogType.Warning;
                case LogLevel.General:
                    return LogType.Warning;
                case LogLevel.Full:
                    return LogType.Verbose;
                case LogLevel.Warnings:
                    return LogType.Note;
                case LogLevel.Nothing:
                    return LogType.Warning;
                default:
                    return LogType.Warning;
            }
        }

        /// <summary>
        /// Adds a log entry with formatting. Matches Python's write_log behavior.
        /// The View's FormatLogText() method uses the FormattedLogEntry list to render
        /// colored, styled text matching Python's tkinter tag_configure implementation.
        /// </summary>
        public void AddLogEntry(string message, LogType logType = LogType.Note)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Store formatted log entry for styled rendering in View
                var entry = new FormattedLogEntry(message, logType);
                _logEntries.Add(entry);

                // Update plain text property to trigger PropertyChanged event
                // The View's FormatLogText() uses _logEntries for actual rendering
                _logTextBuilder.AppendLine(message);
                LogText = _logTextBuilder.ToString();
            });
        }

        /// <summary>
        /// Helper method to parse log type from message prefix like [ERROR], [WARNING], etc.
        /// </summary>
        private LogType ParseLogTypeFromMessage(string message)
        {
            if (message.StartsWith("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("[CRITICAL]", StringComparison.OrdinalIgnoreCase))
            {
                return LogType.Error;
            }
            if (message.StartsWith("[WARNING]", StringComparison.OrdinalIgnoreCase))
            {
                return LogType.Warning;
            }
            if (message.StartsWith("[DEBUG]", StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith("[VERBOSE]", StringComparison.OrdinalIgnoreCase))
            {
                return LogType.Verbose;
            }
            return LogType.Note; // Default to INFO/NOTE
        }

        private void LogExceptionToDebugConsole(Exception ex, string context = "")
        {
            string contextPrefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
            string message = $"{contextPrefix}EXCEPTION: {ex.GetType().Name}: {ex.Message}";

            // Write using RobustLogger (which writes to installlog.txt)
            _pykotorLogger.Exception(message, ex);

            Debug.WriteLine(message);
            Debug.WriteLine($"{contextPrefix}STACK TRACE:");
            Debug.WriteLine(ex.StackTrace);

            if (ex.InnerException != null)
            {
                Debug.WriteLine($"{contextPrefix}INNER EXCEPTION: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                Debug.WriteLine($"{contextPrefix}INNER STACK TRACE:");
                Debug.WriteLine(ex.InnerException.StackTrace);
            }

            // Also write to console for good measure
            Console.Error.WriteLine(message);
            Console.Error.WriteLine($"{contextPrefix}STACK TRACE:");
            Console.Error.WriteLine(ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"{contextPrefix}INNER EXCEPTION: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                Console.Error.WriteLine($"{contextPrefix}INNER STACK TRACE:");
                Console.Error.WriteLine(ex.InnerException.StackTrace);
            }
        }

        private async Task BrowseMod()
        {
            // Can be null if window not available
            Window window = GetMainWindow();
            if (window is null)
            {
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Mod Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                string path = folders[0].Path.LocalPath;
                await LoadModFromPath(path);
            }
        }

        private async Task BrowseGamePath()
        {
            Window window = GetMainWindow();
            if (window is null)
            {
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select KOTOR Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                string path = folders[0].Path.LocalPath;
                if (!GamePaths.Contains(path))
                {
                    GamePaths.Add(path);
                }
                SelectedGamePath = path;
            }
        }

        private async Task Install()
        {
            if (!PreInstallValidate())
            {
                return;
            }

            IsTaskRunning = true;
            ProgressValue = 0;
            _cancellationTokenSource = new CancellationTokenSource();
            ClearLogText();

            try
            {
                // Can be null if namespace not found
                PatcherNamespace selectedNs = _loadedNamespaces.FirstOrDefault(ns => ns.Name == SelectedNamespace);
                if (selectedNs is null)
                {
                    throw new InvalidOperationException("Selected namespace not found.");
                }

                // Use Core.InstallMod which handles path resolution correctly (matches Python)
                // Python: installer = ModInstaller(namespace_mod_path, game_path, ini_file_path, logger)
                // where namespace_mod_path = ini_file_path.parent (parent of the ini file)
                string tslPatchDataPath = Path.Combine(ModPath, "tslpatchdata");
                string iniFilePath = Path.Combine(tslPatchDataPath, selectedNs.ChangesFilePath());

                if (!File.Exists(iniFilePath))
                {
                    throw new FileNotFoundException($"Changes INI file not found: {iniFilePath}");
                }

                // Python: namespace_mod_path: CaseAwarePath = ini_file_path.parent
                // The modPath for ModInstaller should be the parent of the ini file, not the mod root
                string namespaceModPath = Path.GetDirectoryName(iniFilePath) ?? tslPatchDataPath;

                var installer = new ModInstaller(namespaceModPath, SelectedGamePath, iniFilePath, _logger)
                {
                    TslPatchDataPath = tslPatchDataPath
                };

                // Check for confirmation message
                // Can be null if no confirmation message
                string confirmMsg = Core.GetConfirmMessage(installer);
                if (!string.IsNullOrEmpty(confirmMsg) && !_oneShot)
                {
                    MsBox.Avalonia.Base.IMsBox<ButtonResult> confirmBox = MessageBoxManager.GetMessageBoxStandard(
                        "This mod requires confirmation",
                        confirmMsg,
                        ButtonEnum.OkCancel,
                        Icon.Question);
                    ButtonResult result = await confirmBox.ShowAsync();
                    if (result != ButtonResult.Ok)
                    {
                        IsTaskRunning = false;
                        return;
                    }
                }

                AddLogEntry("Starting installation...", LogType.Note);

                // Calculate total patches for progress
                int totalPatches = Core.CalculateTotalPatches(installer);
                ProgressMaximum = totalPatches;

                DateTime installStartTime = DateTime.UtcNow;

                await Task.Run(() =>
                {
                    installer.Install(
                        _cancellationTokenSource.Token,
                        progress =>
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                ProgressValue = progress;
                            });
                        });
                }, _cancellationTokenSource.Token);

                TimeSpan installTime = DateTime.UtcNow - installStartTime;
                int numErrors = _logger.Errors.Count();
                int numWarnings = _logger.Warnings.Count();
                int numPatches = installer.Config().PatchCount();

                string timeStr = Core.FormatInstallTime(installTime);
                _logger.AddNote(
                    $"The installation is complete with {numErrors} errors and {numWarnings} warnings.{Environment.NewLine}" +
                    $"Total install time: {timeStr}{Environment.NewLine}" +
                    $"Total patches: {numPatches}");

                ProgressValue = ProgressMaximum;

                // Show completion message
                if (numErrors > 0)
                {
                    MsBox.Avalonia.Base.IMsBox<ButtonResult> errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Install completed with errors!",
                        $"The install completed with {numErrors} errors and {numWarnings} warnings! The installation may not have been successful, check the logs for more details.{Environment.NewLine}{Environment.NewLine}Total install time: {timeStr}{Environment.NewLine}Total patches: {numPatches}",
                        ButtonEnum.Ok,
                        Icon.Error);
                    await errorBox.ShowAsync();
                }
                else if (numWarnings > 0)
                {
                    MsBox.Avalonia.Base.IMsBox<ButtonResult> warningBox = MessageBoxManager.GetMessageBoxStandard(
                        "Install completed with warnings",
                        $"The install completed with {numWarnings} warnings! Review the logs for details. The script in the 'uninstall' folder of the mod directory will revert these changes.{Environment.NewLine}{Environment.NewLine}Total install time: {timeStr}{Environment.NewLine}Total patches: {numPatches}",
                        ButtonEnum.Ok,
                        Icon.Warning);
                    await warningBox.ShowAsync();
                }
                else
                {
                    MsBox.Avalonia.Base.IMsBox<ButtonResult> infoBox = MessageBoxManager.GetMessageBoxStandard(
                        "Install complete!",
                        $"Check the logs for details on what has been done. Utilize the script in the 'uninstall' folder of the mod directory to revert these changes.{Environment.NewLine}{Environment.NewLine}Total install time: {timeStr}{Environment.NewLine}Total patches: {numPatches}",
                        ButtonEnum.Ok,
                        Icon.Success);
                    await infoBox.ShowAsync();
                }
            }
            catch (OperationCanceledException ex)
            {
                LogExceptionToDebugConsole(ex, "Install (Cancelled)");
                AddLogEntry("[WARNING] Installation was cancelled.");
            }
            catch (Exception ex)
            {
                LogExceptionToDebugConsole(ex, "Install");
                AddLogEntry($"[ERROR] Installation failed: {ex.Message}");
                MsBox.Avalonia.Base.IMsBox<ButtonResult> errorBox = MessageBoxManager.GetMessageBoxStandard(
                    ex.GetType().Name,
                    $"An unexpected error occurred during the installation and the installation was forced to terminate.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                    ButtonEnum.Ok,
                    Icon.Error);
                await errorBox.ShowAsync();
            }
            finally
            {
                IsTaskRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        public void ClearLogText()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _logTextBuilder.Clear();
                _logEntries.Clear(); // Clear formatted log entries too
                LogText = string.Empty;
                RtfContent = string.Empty;
                IsRtfContent = false;
                ActiveRteDocument = null;
                OnPropertyChanged(nameof(ActiveRteDocument));
            });
        }

        private void Exit()
        {
            Window window = GetMainWindow();
            window?.Close();
        }

        private async Task ValidateIni()
        {
            if (!PreInstallValidate())
            {
                return;
            }

            IsTaskRunning = true;
            ClearLogText();

            await Task.Run(() =>
            {
                try
                {
                    // Can be null if namespace not found
                    PatcherNamespace selectedNs = _loadedNamespaces.FirstOrDefault(ns => ns.Name == SelectedNamespace);
                    if (selectedNs is null)
                    {
                        throw new InvalidOperationException("Selected namespace not found.");
                    }

                    Core.ValidateConfig(ModPath, _loadedNamespaces, SelectedNamespace, _logger);
                }
                catch (Exception ex)
                {
                    LogExceptionToDebugConsole(ex, "ValidateIni");
                    AddLogEntry($"[ERROR] Validation failed: {ex.Message}");
                }
                finally
                {
                    IsTaskRunning = false;
                    _logger.AddNote("Config reader test is complete.");
                }
            });
        }

        private async Task UninstallMod()
        {
            if (!ValidateModPathAndGamePath())
            {
                return;
            }

            IsTaskRunning = true;

            try
            {
                string backupRoot = ModPath;
                while (!Directory.Exists(Path.Combine(backupRoot, "tslpatchdata")) && !string.IsNullOrEmpty(Path.GetDirectoryName(backupRoot)))
                {
                    backupRoot = Path.GetDirectoryName(backupRoot) ?? backupRoot;
                }

                string backupsLocation = Path.Combine(backupRoot, "backup");

                if (!Directory.Exists(backupsLocation))
                {
                    AddLogEntry($"[ERROR] No backup directory found at {backupsLocation}");
                    return;
                }

                var uninstaller = new ModUninstaller(new CaseAwarePath(backupsLocation), new CaseAwarePath(SelectedGamePath), _logger);

                await Task.Run(() =>
                {
                    uninstaller.UninstallSelectedMod(
                        showErrorDialog: (title, msg) =>
                        {
                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                MsBox.Avalonia.Base.IMsBox<ButtonResult> box = MessageBoxManager.GetMessageBoxStandard(title, msg, ButtonEnum.Ok, Icon.Error);
                                await box.ShowAsync();
                            }).Wait();
                        },
                        showYesNoDialog: (title, msg) =>
                        {
                            bool result = false;
                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                MsBox.Avalonia.Base.IMsBox<ButtonResult> box = MessageBoxManager.GetMessageBoxStandard(title, msg, ButtonEnum.YesNo, Icon.Question);
                                ButtonResult res = await box.ShowAsync();
                                result = res == ButtonResult.Yes;
                            }).Wait();
                            return result;
                        },
                        showYesNoCancelDialog: (title, msg) =>
                        {
                            bool? result = null;
                            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                MsBox.Avalonia.Base.IMsBox<ButtonResult> box = MessageBoxManager.GetMessageBoxStandard(title, msg, ButtonEnum.YesNoCancel, Icon.Question);
                                ButtonResult res = await box.ShowAsync();
                                if (res == ButtonResult.Yes)
                                {
                                    result = true;
                                }
                                else if (res == ButtonResult.No)
                                {
                                    result = false;
                                }
                                else
                                {
                                    result = null;
                                }
                            }).Wait();
                            return result;
                        }
                    );
                });

                AddLogEntry("Uninstall process finished.");
            }
            catch (Exception ex)
            {
                LogExceptionToDebugConsole(ex, "UninstallMod");
                AddLogEntry($"[ERROR] Uninstall failed: {ex.Message}");
            }
            finally
            {
                IsTaskRunning = false;
            }
        }

        private async Task FixPermissions()
        {
            Window window = GetMainWindow();
            if (window is null)
            {
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Directory to Fix Permissions",
                AllowMultiple = false
            });

            if (folders.Count == 0)
            {
                return;
            }

            string directory = folders[0].Path.LocalPath;

            MsBox.Avalonia.Base.IMsBox<ButtonResult> confirmBox = MessageBoxManager.GetMessageBoxStandard(
                "Warning!",
                "This is not a toy. Really continue?",
                ButtonEnum.YesNo,
                Icon.Warning);
            ButtonResult result = await confirmBox.ShowAsync();
            if (result != ButtonResult.Yes)
            {
                return;
            }

            IsTaskRunning = true;
            ClearLogText();
            AddLogEntry("Please wait, this may take awhile...");

            await Task.Run(() =>
            {
                try
                {
                    SystemHelpers.FixPermissions(directory, msg => AddLogEntry(msg));

                    int numFiles = 0;
                    int numFolders = 0;
                    if (Directory.Exists(directory))
                    {
                        numFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length;
                        numFolders = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories).Length;
                    }

                    string extraMsg = $"{numFiles} files and {numFolders} folders finished processing.";
                    AddLogEntry(extraMsg);

                    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        MsBox.Avalonia.Base.IMsBox<ButtonResult> successBox = MessageBoxManager.GetMessageBoxStandard(
                            "Successfully acquired permission",
                            $"The operation was successful. {extraMsg}",
                            ButtonEnum.Ok,
                            Icon.Success);
                        await successBox.ShowAsync();
                    });
                }
                catch (Exception ex)
                {
                    LogExceptionToDebugConsole(ex, "FixPermissions");
                    AddLogEntry($"[ERROR] Failed to fix permissions: {ex.Message}");
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        MsBox.Avalonia.Base.IMsBox<ButtonResult> errorBox = MessageBoxManager.GetMessageBoxStandard(
                            "Could not acquire permission!",
                            $"Permissions denied! Check the logs for more details.{Environment.NewLine}{ex.Message}",
                            ButtonEnum.Ok,
                            Icon.Error);
                        await errorBox.ShowAsync();
                    });
                }
                finally
                {
                    IsTaskRunning = false;
                    _logger.AddNote("File/Folder permissions fixer task completed.");
                }
            });
        }

        private async Task FixCaseSensitivity()
        {
            Window window = GetMainWindow();
            if (window is null)
            {
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Directory to Fix Case Sensitivity",
                AllowMultiple = false
            });

            if (folders.Count == 0)
            {
                return;
            }

            string directory = folders[0].Path.LocalPath;

            IsTaskRunning = true;
            ClearLogText();
            AddLogEntry("Please wait, this may take awhile...");

            await Task.Run(() =>
            {
                try
                {
                    bool madeChange = false;
                    SystemHelpers.FixCaseSensitivity(directory, msg =>
                    {
                        AddLogEntry(msg);
                        madeChange = true;
                    });

                    if (!madeChange)
                    {
                        AddLogEntry("Nothing to change - all files/folders already correct case.");
                    }
                    AddLogEntry("iOS case rename task completed.");
                }
                catch (Exception ex)
                {
                    LogExceptionToDebugConsole(ex, "FixCaseSensitivity");
                    AddLogEntry($"[ERROR] Failed to fix case sensitivity: {ex.Message}");
                }
                finally
                {
                    IsTaskRunning = false;
                }
            });
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogExceptionToDebugConsole(ex, "OpenUrl");
                AddLogEntry($"[ERROR] Failed to open URL: {ex.Message}");
            }
        }

        private async Task CheckUpdates()
        {
            try
            {
                bool useBetaChannel = false;
                Dictionary<string, object> updateInfo = await Config.GetRemoteHolopatcherUpdateInfoAsync(useBetaChannel);
                if (updateInfo.Count == 0)
                {
                    await ShowErrorAsync("Update Error", "Unable to fetch update information. Please try again later.");
                    return;
                }

                RemoteUpdateInfo remoteInfo = RemoteUpdateInfoParser.FromDictionary(updateInfo);
                string latestVersion = remoteInfo.GetChannelVersion(useBetaChannel);

                if (Config.RemoteVersionNewer(Config.CurrentVersion, latestVersion))
                {
                    string notes = remoteInfo.GetChannelNotes(useBetaChannel);
                    string message = $"A newer version of HoloPatcher ({latestVersion}) is available.{Environment.NewLine}{Environment.NewLine}{notes}";
                    string choice = await ShowChoiceDialogAsync("Update Available", message, "Update", "Manual Download");

                    if (choice == "Update")
                    {
                        await RunAutoUpdate(remoteInfo, useBetaChannel);
                    }
                    else if (choice == "Manual Download")
                    {
                        OpenUrl(remoteInfo.GetDownloadPage(useBetaChannel));
                    }
                }
                else
                {
                    string message = $"You are already running the latest version of HoloPatcher ({Core.VersionLabel}).";
                    string choice = await ShowChoiceDialogAsync("No Updates Found", message, "Reinstall");
                    if (choice == "Reinstall")
                    {
                        await RunAutoUpdate(remoteInfo, useBetaChannel);
                    }
                }
            }
            catch (Exception ex)
            {
                LogExceptionToDebugConsole(ex, "CheckUpdates");
                await ShowErrorAsync("Unable to fetch latest version", ex.Message);
            }
        }

        private async Task ShowNamespaceInfo()
        {
            if (string.IsNullOrEmpty(SelectedNamespace))
            {
                MsBox.Avalonia.Base.IMsBox<ButtonResult> infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "No namespace selected",
                    "Please select a namespace first.",
                    ButtonEnum.Ok,
                    Icon.Info);
                await infoBox.ShowAsync();
                return;
            }

            string description = Core.GetNamespaceDescription(_loadedNamespaces, SelectedNamespace);
            MsBox.Avalonia.Base.IMsBox<ButtonResult> descBox = MessageBoxManager.GetMessageBoxStandard(
                SelectedNamespace,
                string.IsNullOrEmpty(description) ? "No description available." : description,
                ButtonEnum.Ok,
                Icon.Info);
            await descBox.ShowAsync();
        }

        private async Task<string> ShowChoiceDialogAsync(string title, string message, params string[] options)
        {
            Window window = GetMainWindow();
            if (window is null)
            {
                return null;
            }

            var dialog = new ChoiceDialog(title, message, options);
            return await dialog.ShowDialog<string>(window);
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            MsBox.Avalonia.Base.IMsBox<ButtonResult> box = MessageBoxManager.GetMessageBoxStandard(
                title,
                message,
                ButtonEnum.Ok,
                Icon.Error);
            await box.ShowAsync();
        }

        private async Task RunAutoUpdate(RemoteUpdateInfo info, bool useBetaChannel)
        {
            Window window = GetMainWindow();
            if (window is null)
            {
                AddLogEntry("[ERROR] Unable to locate window for updater UI.");
                return;
            }

            var updater = new AutoUpdater(info, window, useBetaChannel);
            await updater.RunAsync();
        }

        /// <summary>
        /// Opens RTE editor dialog - matches Python's create_rte_content functionality.
        /// </summary>
        private async Task CreateRte()
        {
            Window window = GetMainWindow();
            if (window is null)
            {
                AddLogEntry("[ERROR] Unable to open RTE editor: window not available.");
                return;
            }

            var editor = new RteEditorWindow(ModPath);
            await editor.ShowDialog(window);
        }

        /// <summary>
        /// Loads and displays RTE (Rich Text Editor) content from JSON format.
        /// Matches Python's load_rte_content function.
        /// </summary>
        /// <param name="rteContent">JSON-formatted RTE content string</param>
        private void LoadRteContent(string rteContent)
        {
            try
            {
                ActiveRteDocument = RteDocument.Parse(rteContent);
                OnPropertyChanged(nameof(ActiveRteDocument));
                IsRtfContent = true;
                RtfContent = string.Empty;
            }
            catch (Exception ex)
            {
                LogExceptionToDebugConsole(ex, "LoadRteContent");
                AddLogEntry($"[ERROR] Failed to load RTE content: {ex.Message}");
            }
        }

        /// <summary>
        /// RTE document structure matching Python's JSON format.
        /// This is a legacy structure - use HoloPatcher.Rte.RteDocument instead.
        /// </summary>
        private class LegacyRteDocument
        {
            public string Content { get; set; } = string.Empty;
            public Dictionary<string, Dictionary<string, string>> TagConfigs { get; set; } = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, List<string[]>> Tags { get; set; } = new Dictionary<string, List<string[]>>();
        }

        private async Task LoadModFromPath(string path)
        {
            try
            {
                Core.ModInfo modInfo = Core.LoadMod(path);
                ModPath = modInfo.ModPath;
                _loadedNamespaces = modInfo.Namespaces;
                _currentConfigReader = modInfo.ConfigReader;

                AddLogEntry($"Loaded {_loadedNamespaces.Count} namespace(s)");

                // Update UI on main thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Namespaces.Clear();
                    foreach (PatcherNamespace ns in _loadedNamespaces)
                    {
                        string displayName = !string.IsNullOrWhiteSpace(ns.Name) ? ns.Name : ns.ChangesFilePath();
                        Namespaces.Add(displayName);
                    }

                    if (Namespaces.Count > 0)
                    {
                        SelectedNamespace = Namespaces[0];
                        OnNamespaceSelected();
                    }
                });
            }
            catch (Exception ex)
            {
                LogExceptionToDebugConsole(ex, "LoadModFromPath");
                AddLogEntry($"[ERROR] Failed to load mod: {ex.Message}");
                MsBox.Avalonia.Base.IMsBox<ButtonResult> errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Could not find a mod located at the given folder.{Environment.NewLine}{ex.Message}",
                    ButtonEnum.Ok,
                    Icon.Error);
                await errorBox.ShowAsync();
            }
        }

        private void OnNamespaceSelected()
        {
            if (string.IsNullOrEmpty(SelectedNamespace) || string.IsNullOrEmpty(ModPath))
            {
                return;
            }

            try
            {
                Core.NamespaceInfo namespaceInfo = Core.LoadNamespaceConfig(ModPath, _loadedNamespaces, SelectedNamespace, _currentConfigReader);
                _logLevel = namespaceInfo.LogLevel;

                // Update game paths based on namespace
                if (namespaceInfo.GamePaths.Count > 0)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        GamePaths.Clear();
                        foreach (string gamePath in namespaceInfo.GamePaths)
                        {
                            if (!GamePaths.Contains(gamePath))
                            {
                                GamePaths.Add(gamePath);
                            }
                        }
                        if (GamePaths.Count > 0 && string.IsNullOrEmpty(SelectedGamePath))
                        {
                            SelectedGamePath = GamePaths[0];
                        }
                    });
                }

                // Load and display info.rtf/rte content
                if (!string.IsNullOrEmpty(namespaceInfo.InfoContent))
                {
                    Console.WriteLine($"[RTF] OnNamespaceSelected: InfoContent length={namespaceInfo.InfoContent.Length}, IsRtf={namespaceInfo.IsRtf}");
                    ClearLogText();

                    if (namespaceInfo.IsRtf)
                    {
                        // RTF content - render directly using AvRichTextBox!
                        Console.WriteLine("[RTF] Setting IsRtfContent=true and RtfContent on UI thread");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            IsRtfContent = true;
                            RtfContent = namespaceInfo.InfoContent;
                            Console.WriteLine($"[RTF] Properties set: IsRtfContent={IsRtfContent}, RtfContent length={RtfContent?.Length ?? 0}");
                        }, Avalonia.Threading.DispatcherPriority.Normal);
                    }
                    else if (namespaceInfo.InfoContent.TrimStart().StartsWith("{"))
                    {
                        // RTE (JSON) content
                        Console.WriteLine("[RTF] Detected RTE (JSON) content");
                        LoadRteContent(namespaceInfo.InfoContent);
                    }
                    else
                    {
                        // Plain text content
                        Console.WriteLine("[RTF] Detected plain text content");
                        IsRtfContent = false;
                        AddLogEntry(namespaceInfo.InfoContent);
                    }
                }
                else
                {
                    Console.WriteLine("[RTF] InfoContent is empty");
                    IsRtfContent = false;
                }
            }
            catch (Exception ex)
            {
                LogExceptionToDebugConsole(ex, "OnNamespaceSelected");
                AddLogEntry($"[ERROR] Failed to load namespace config: {ex.Message}");
            }
        }

        private void OnSelectedNamespaceChanged([CanBeNull] string value)
        {
            OnPropertyChanged(nameof(CanInstall));
            if (!string.IsNullOrEmpty(value))
            {
                OnNamespaceSelected();
            }
        }

        private void DetectGamePaths()
        {
            // Common installation paths for KOTOR
            string[] commonPaths = new[]
            {
            @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
            @"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
            @"C:\Program Files\Steam\steamapps\common\Knights of the Old Republic II",
            @"C:\Program Files\Steam\steamapps\common\swkotor",
            @"C:\GOG Games\Star Wars - KotOR",
            @"C:\GOG Games\Star Wars - KotOR2",
        };

            foreach (string path in commonPaths)
            {
                if (Directory.Exists(path) && Installation.DetermineGame(path) != null)
                {
                    if (!GamePaths.Contains(path))
                    {
                        GamePaths.Add(path);
                    }
                }
            }

            if (GamePaths.Count > 0)
            {
                SelectedGamePath = GamePaths[0];
                AddLogEntry($"Detected {GamePaths.Count} KOTOR installation(s).");
            }
        }

        [CanBeNull]
        private static Window GetMainWindow()
        {
            return App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }

        private void OnIsTaskRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(CanInstall));
        }


        private void OnSelectedGamePathChanged([CanBeNull] string value)
        {
            OnPropertyChanged(nameof(CanInstall));
        }

        private bool PreInstallValidate()
        {
            if (IsTaskRunning)
            {
                MsBox.Avalonia.Base.IMsBox<ButtonResult> infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "Task already running",
                    "Wait for the previous task to finish.",
                    ButtonEnum.Ok,
                    Icon.Info);
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => await infoBox.ShowAsync());
                return false;
            }

            if (string.IsNullOrEmpty(ModPath) || !Directory.Exists(ModPath))
            {
                MsBox.Avalonia.Base.IMsBox<ButtonResult> infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "No mod chosen",
                    "Select your mod directory first.",
                    ButtonEnum.Ok,
                    Icon.Info);
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => await infoBox.ShowAsync());
                return false;
            }

            if (string.IsNullOrEmpty(SelectedGamePath))
            {
                MsBox.Avalonia.Base.IMsBox<ButtonResult> infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "No KOTOR directory chosen",
                    "Select your KOTOR directory first.",
                    ButtonEnum.Ok,
                    Icon.Info);
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => await infoBox.ShowAsync());
                return false;
            }

            var gamePath = new CaseAwarePath(SelectedGamePath);
            if (!gamePath.IsDirectory())
            {
                MsBox.Avalonia.Base.IMsBox<ButtonResult> infoBox = MessageBoxManager.GetMessageBoxStandard(
                    "Invalid KOTOR directory chosen",
                    "Select a valid path to your KOTOR install.",
                    ButtonEnum.Ok,
                    Icon.Info);
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => await infoBox.ShowAsync());
                return false;
            }

            return true;
        }

        private bool ValidateGamePathAndNamespace()
        {
            return PreInstallValidate() && !string.IsNullOrEmpty(SelectedNamespace);
        }

        private bool ValidateModPathAndGamePath()
        {
            if (string.IsNullOrEmpty(ModPath) || string.IsNullOrEmpty(SelectedGamePath))
            {
                AddLogEntry("[ERROR] Please select both a mod and game directory.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Attempts to auto-open a mod if a tslpatchdata folder exists next to the executable
        /// that contains changes.ini and/or namespaces.ini.
        /// </summary>
        private async Task TryAutoOpenMod()
        {
            try
            {
                // Get the directory where the executable is located
                string exeDirectory = AppContext.BaseDirectory;
                if (string.IsNullOrEmpty(exeDirectory))
                {
                    // Fallback to current directory
                    exeDirectory = Directory.GetCurrentDirectory();
                }

                // Check for tslpatchdata folder next to executable
                string tslPatchDataPath = Path.Combine(exeDirectory, "tslpatchdata");

                // Check if tslpatchdata folder exists and contains the required files
                if (Directory.Exists(tslPatchDataPath))
                {
                    string changesIniPath = Path.Combine(tslPatchDataPath, "changes.ini");
                    string namespacesIniPath = Path.Combine(tslPatchDataPath, "namespaces.ini");

                    // Check if at least one of the required files exists
                    if (File.Exists(changesIniPath) || File.Exists(namespacesIniPath))
                    {
                        // Auto-open the mod using the parent directory (the mod root)
                        string modPath = exeDirectory;
                        AddLogEntry($"Auto-opening mod from: {modPath}");

                        // Hide the mod selection UI since we're auto-opening
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            IsModSelectionVisible = false;
                        });

                        // Load the mod
                        await LoadModFromPath(modPath);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail - just continue normally
                LogExceptionToDebugConsole(ex, "TryAutoOpenMod");
                Debug.WriteLine($"Failed to auto-open mod: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Represents a formatted log entry with its log type for color/styling.
    /// Matches Python's log tag system.
    /// </summary>
    public class FormattedLogEntry
    {
        public string Message { get; }
        public LogType LogType { get; }
        public string TagName { get; }

        public FormattedLogEntry(string message, LogType logType)
        {
            Message = message;
            LogType = logType;

            // Map LogType to tag name matching Python's log_to_tag function
            switch (logType)
            {
                case LogType.Note:
                    TagName = "INFO";
                    break;
                case LogType.Verbose:
                    TagName = "DEBUG";
                    break;
                case LogType.Warning:
                    TagName = "WARNING";
                    break;
                case LogType.Error:
                    TagName = "ERROR";
                    break;
                default:
                    TagName = "INFO";
                    break;
            }
        }
    }
}
