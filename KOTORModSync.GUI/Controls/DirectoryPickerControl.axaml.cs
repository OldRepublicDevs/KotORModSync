using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KOTORModSync.Core;

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
        private bool _suppressEvents = false;
        private bool _suppressSelection = false;

        public DirectoryPickerControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock");
            _currentPathDisplay = this.FindControl<TextBlock>("CurrentPathDisplay");
            _pathInput = this.FindControl<TextBox>("PathInput");
            _pathSuggestions = this.FindControl<ComboBox>("PathSuggestions");

            UpdateTitle();
            UpdateWatermark();
            InitializePathSuggestions();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TitleProperty)
            {
                UpdateTitle();
            }
            else if (change.Property == WatermarkProperty)
            {
                UpdateWatermark();
            }
            else if (change.Property == PickerTypeProperty)
            {
                InitializePathSuggestions();
            }
        }

        private void UpdateTitle()
        {
            if (_titleTextBlock != null)
            {
                _titleTextBlock.Text = Title ?? string.Empty;
            }
        }

        private void UpdateWatermark()
        {
            if (_pathInput != null)
            {
                _pathInput.Watermark = Watermark ?? string.Empty;
            }
        }

        private void InitializePathSuggestions()
        {
            if (_pathSuggestions == null) return;

            try
            {
                if (PickerType == DirectoryPickerType.ModDirectory)
                {
                    InitializeModDirectoryPaths();
                }
                else if (PickerType == DirectoryPickerType.KotorDirectory)
                {
                    InitializeKotorDirectoryPaths();
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
                _pathSuggestions.ItemsSource = recentPaths;
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
                _pathSuggestions.ItemsSource = defaultPaths.Where(Directory.Exists).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private List<string> GetDefaultPathsForGame()
        {
            var paths = new List<string>();
			OSPlatform osType = global::KOTORModSync.Core.Utility.Utility.GetOperatingSystem();

            if (osType == OSPlatform.Windows)
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
            else if (osType == OSPlatform.OSX)
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
            else if (osType == OSPlatform.Linux)
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

            return paths;
        }

        private List<string> LoadRecentModPaths()
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

        public void SetCurrentPath(string path)
        {
            try
            {
                _suppressEvents = true;
                
                if (_currentPathDisplay != null)
                {
                    _currentPathDisplay.Text = string.IsNullOrEmpty(path) ? "Not set" : path;
                }
                
                if (_pathInput != null)
                {
                    _pathInput.Text = path ?? string.Empty;
                }
                
                _suppressEvents = false;
            }
            catch (Exception ex)
            {
                _suppressEvents = false;
                Logger.LogException(ex);
            }
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.StorageProvider == null) return;

                var options = new FolderPickerOpenOptions
                {
                    Title = PickerType == DirectoryPickerType.ModDirectory ? "Select Mod Directory" : "Select KOTOR Installation Directory",
                    AllowMultiple = false
                };

				IReadOnlyList<IStorageFolder> result = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (result?.Count > 0)
                {
					string selectedPath = result[0].Path.LocalPath;
                    ApplyPath(selectedPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        private void OnPathInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _pathInput != null && !string.IsNullOrWhiteSpace(_pathInput.Text))
            {
                ApplyPath(_pathInput.Text.Trim());
                e.Handled = true;
            }
        }

        private void PathInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || _pathInput == null) return;
            
            if (!string.IsNullOrWhiteSpace(_pathInput.Text))
            {
                ApplyPath(_pathInput.Text.Trim());
            }
        }

        private void PathSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents || _suppressSelection || _pathSuggestions?.SelectedItem == null) return;

            string selectedPath = _pathSuggestions.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedPath)) return;

            try
            {
                _suppressSelection = true;
                // Defer to end of event cycle to avoid re-entrancy with ItemsSource updates
                Dispatcher.UIThread.Post(() =>
                {
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

                // Update displays
                SetCurrentPath(path);

                // Save to recent if mod directory
                if (PickerType == DirectoryPickerType.ModDirectory)
                {
                    SaveRecentModPath(path);
                    // Refresh suggestions list safely without causing selection recursion
                    RefreshSuggestionsSafely();
                }

                // Fire event
                DirectoryChanged?.Invoke(this, new DirectoryChangedEventArgs(path, PickerType));

                _suppressEvents = false;
            }
            catch (Exception ex)
            {
                _suppressEvents = false;
                Logger.LogException(ex);
            }
        }

        private void RefreshSuggestionsSafely()
        {
            if (_pathSuggestions == null) return;

            try
            {
                _suppressEvents = true;
                _suppressSelection = true;

                if (PickerType == DirectoryPickerType.ModDirectory)
                {
                    List<string> recent = LoadRecentModPaths();
                    _pathSuggestions.ItemsSource = recent;
                }
                else if (PickerType == DirectoryPickerType.KotorDirectory)
                {
                    List<string> defaults = GetDefaultPathsForGame().Where(Directory.Exists).ToList();
                    _pathSuggestions.ItemsSource = defaults;
                }

                // Do not force SelectedItem to avoid triggering SelectionChanged repeatedly
            }
            finally
            {
                _suppressSelection = false;
                _suppressEvents = false;
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
