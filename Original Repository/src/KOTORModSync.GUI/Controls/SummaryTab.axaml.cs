// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Services;

namespace KOTORModSync.Controls
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public partial class SummaryTab : UserControl
    {
        public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
            AvaloniaProperty.Register<SummaryTab, ModComponent>(nameof(CurrentComponent));

        public static readonly StyledProperty<bool> EditorModeProperty =
            AvaloniaProperty.Register<SummaryTab, bool>(nameof(EditorMode));

        public static readonly StyledProperty<bool> SpoilerFreeModeProperty =
            AvaloniaProperty.Register<SummaryTab, bool>(nameof(SpoilerFreeMode));

        private readonly MarkdownRenderingService _markdownRenderingService = new Services.MarkdownRenderingService();

        [CanBeNull]
        public ModComponent CurrentComponent
        {
            get => MainConfig.CurrentComponent;
            set
            {
                MainConfig.CurrentComponent = value;
                SetValue(CurrentComponentProperty, value);
            }
        }

        public bool EditorMode
        {
            get => GetValue(EditorModeProperty);
            set => SetValue(EditorModeProperty, value);
        }

        public bool SpoilerFreeMode
        {
            get => GetValue(SpoilerFreeModeProperty);
            set => SetValue(SpoilerFreeModeProperty, value);
        }

        public event EventHandler<TappedEventArgs> OpenLinkRequested;
        public event EventHandler<RoutedEventArgs> CopyTextToClipboardRequested;
        public event EventHandler<PointerPressedEventArgs> SummaryOptionPointerPressedRequested;
        public event EventHandler<RoutedEventArgs> CheckBoxChangedRequested;
        public event EventHandler<RoutedEventArgs> JumpToInstructionRequested;

        public SummaryTab()
        {
            InitializeComponent();
            DataContext = this;

            PropertyChanged += OnPropertyChanged;

            // Attach loaded event to set up markdown rendering once the control is fully loaded
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnLoaded(sender, e), DispatcherPriority.Normal);
                return;
            }
            try
            {
                // Attach markdown rendering to all TextBlocks dynamically
                AttachMarkdownRenderingToAllTextBlocks();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error in OnLoaded");
            }
        }

        private void AttachMarkdownRenderingToAllTextBlocks()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(AttachMarkdownRenderingToAllTextBlocks, DispatcherPriority.Normal);
                return;
            }
            try
            {
                // Find ALL TextBlocks in the visual tree
                var allTextBlocks = this.GetVisualDescendants().OfType<TextBlock>().ToList();

                foreach (TextBlock textBlock in allTextBlocks)
                {
                    // Skip TextBlocks that are part of headers, labels, or other UI chrome
                    if (IsUiChromeTextBlock(textBlock))
                    {
                        continue;
                    }

                    // Attach a watcher to this TextBlock's Text property
                    AttachTextPropertyWatcher(textBlock);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error attaching markdown rendering to TextBlocks");
            }
        }

        private static bool IsUiChromeTextBlock(TextBlock textBlock)
        {
            // Skip TextBlocks that are part of the UI structure (headers, labels, etc.)
            if (textBlock.Classes.Contains("summary-section-title", StringComparer.Ordinal))
            {
                return true;
            }

            if (textBlock.Classes.Contains("summary-label", StringComparer.Ordinal))
            {
                return true;
            }

            if (textBlock.Classes.Contains("secondary-text", StringComparer.Ordinal))
            {
                return true;
            }

            if (textBlock.Classes.Contains("summary-option-title", StringComparer.Ordinal))
            {
                return true;
            }

            if (textBlock.Classes.Contains("summary-instruction-action", StringComparer.Ordinal))
            {
                return true;
            }

            // Skip TextBlocks with specific names that shouldn't be markdown-rendered
            if (textBlock.Name != null && (
                textBlock.Name.Contains("Label") ||
                textBlock.Name.Contains("Header") ||
                textBlock.Name.Contains("Title")
            ))
            {
                return true;
            }

            return false;
        }

        private void AttachTextPropertyWatcher(TextBlock textBlock)
        {
            if (textBlock is null)
            {
                return;
            }

            textBlock.GetObservable(TextBlock.TextProperty).Subscribe(newText =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(newText))
                        {
                            _markdownRenderingService.RenderMarkdownToTextBlock(textBlock, newText);

                            // Ensure the TextBlock has the proper foreground color for the current theme
                            string currentTheme = ThemeManager.GetCurrentStylePath();
                            if (currentTheme.Contains("LightStyle")
                                || currentTheme.Contains("FluentLightStyle"))
                            {
                                textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)); // #212121
                            }
                            else if (currentTheme.Contains("Kotor2Style"))
                            {
                                textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0xae, 0x88)); // #18ae88
                            }
                            else if (currentTheme.Contains("KotorStyle"))
                            {
                                textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0xAA, 0xFF)); // #3AAAFF
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, $"Error rendering markdown for TextBlock: {textBlock.Name ?? "unnamed"}");
                    }
                }, DispatcherPriority.Background);
            });
        }

        private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnPropertyChanged(sender, e), DispatcherPriority.Normal);
                return;
            }
            if (e.Property == CurrentComponentProperty)
            {
                UpdateAllFilenamePanels();
            }
            else if (e.Property == SpoilerFreeModeProperty)
            {
                // Spoiler-free mode change is handled automatically by the converter
                // and the TextProperty watchers will re-render markdown
            }
        }

        private void OpenLink_Tapped(object sender, TappedEventArgs e)
        {
            OpenLinkRequested?.Invoke(this, e);
        }

        private void CopyTextToClipboard_Click(object sender, RoutedEventArgs e)
        {
            CopyTextToClipboardRequested?.Invoke(this, e);
        }

        private void SummaryOptionBorder_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => SummaryOptionBorder_PointerPressed(sender, e), DispatcherPriority.Normal);
                return;
            }
            SummaryOptionPointerPressedRequested?.Invoke(this, e);
        }

        private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnCheckBoxChanged(sender, e), DispatcherPriority.Normal);
                return;
            }
            CheckBoxChangedRequested?.Invoke(this, e);
        }

        private void JumpToInstruction_Click(object sender, RoutedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => JumpToInstruction_Click(sender, e), DispatcherPriority.Normal);
                return;
            }
            JumpToInstructionRequested?.Invoke(this, e);
        }

        private async void DownloadModButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is ModComponent component))
            {
                return;
            }

            try
            {
                await Logger.LogVerboseAsync($"[SummaryTab] Download button clicked for: {component.Name}");

                await DownloadOrchestrationService.DownloadModFromUrlAsync(component.ResourceRegistry.Keys.First(), component);
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, $"Error downloading mod from SummaryTab: {component.Name}");
            }
        }

        private async void DownloadLinksExpander_Expanding(object sender, RoutedEventArgs e)
        {
            try
            {
                // Find the expander
                if (!(sender is Expander expander))
                {
                    return;
                }

                // Show confirmation dialog
                bool? result = await Dialogs.ConfirmationDialog.ShowConfirmationDialogAsync(
                    VisualRoot as Window,
                    "**WARNING:** This may contain spoilers!\n\nDownload URLs may reveal story details or mod content that could spoil your playthrough.\n\n**Tip:** You can download this mod by pressing the **Home** button and then pressing **'Fetch Downloads'** without viewing spoiler content.\n\nDo you really want to continue and show these URLs?",
                    yesButtonText: "Yes, Show URLs",
                    noButtonText: "No, Keep Hidden",
                    yesButtonTooltip: "Show the download URLs (may contain spoilers)",
                    noButtonTooltip: "Keep the URLs hidden",
                    closeButtonTooltip: "Cancel and keep URLs hidden"
                );

                // If user declined, collapse the expander again
                if (result != true)
                {
                    expander.IsExpanded = false;
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to show spoiler warning");
            }
        }

        private void UpdateAllFilenamePanels()
        {
            if (CurrentComponent is null || ModLinkRepeater is null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var panels = this.GetVisualDescendants().OfType<StackPanel>()
                        .Where(sp => string.Equals(sp.Name, "SummaryFilenamesPanel", StringComparison.Ordinal)).ToList();

                    int urlIndex = 0;
                    foreach (StackPanel panel in panels)
                    {
                        var urls = CurrentComponent.ResourceRegistry.Keys.ToList();
                        if (urlIndex < urls.Count)
                        {
                            string url = urls[urlIndex];
                            PopulateFilenamesPanel(panel, url);
                            urlIndex++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Error updating filename panels in SummaryTab");
                }
            }, DispatcherPriority.Background);
        }

        [SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        private void PopulateFilenamesPanel(StackPanel panel, string url)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => PopulateFilenamesPanel(panel, url), DispatcherPriority.Normal);
                return;
            }
            if (panel is null || string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            panel.Children.Clear();

            if (CurrentComponent is null || CurrentComponent.ResourceRegistry is null)
            {
                return;
            }

            if (!CurrentComponent.ResourceRegistry.TryGetValue(url, out ResourceMetadata resourceMetadata) || resourceMetadata.Files.Count == 0)
            {
                return;
            }

            var headerText = new TextBlock
            {
                Text = "Files:",
                FontSize = 10,
                Opacity = 0.6,
                Margin = new Thickness(0, 2, 0, 2),
            };
            panel.Children.Add(headerText);

            foreach (System.Collections.Generic.KeyValuePair<string, bool?> filenameEntry in resourceMetadata.Files)
            {
                string filename = filenameEntry.Key;
                bool? shouldDownload = resourceMetadata.Files[filename];

                double opacity;
                if (shouldDownload == true)
                {
                    opacity = 1.0;
                }
                else if (shouldDownload == false)
                {
                    opacity = 0.5;
                }
                else
                {
                    opacity = 0.7;
                }

                var fileText = new TextBlock
                {
                    Text = $"{filename}",
                    FontSize = 11,
                    Opacity = opacity,
                    Margin = new Thickness(0, 1, 0, 1),
                };

                panel.Children.Add(fileText);
            }
        }

        /// <summary>
        /// Refreshes the markdown content in all TextBlocks within the SummaryTab.
        /// This method should be called when switching to the summary tab to ensure
        /// all markdown content is properly rendered.
        /// </summary>
        public void RefreshMarkdownContent()
        {
            try
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        // Update filename panels first
                        UpdateAllFilenamePanels();

                        // Find all TextBlocks and trigger markdown rendering
                        var allTextBlocks = this.GetVisualDescendants().OfType<TextBlock>().ToList();

                        foreach (TextBlock textBlock in allTextBlocks)
                        {
                            // Skip UI chrome TextBlocks
                            if (IsUiChromeTextBlock(textBlock))
                            {
                                continue;
                            }

                            // Trigger markdown rendering for content TextBlocks
                            if (!string.IsNullOrWhiteSpace(textBlock.Text))
                            {
                                _markdownRenderingService.RenderMarkdownToTextBlock(textBlock, textBlock.Text);

                                // Ensure the TextBlock has the proper foreground color for the current theme
                                string currentTheme = ThemeManager.GetCurrentStylePath();
                                if (currentTheme.Contains("LightStyle")
                                    || currentTheme.Contains("FluentLightStyle"))
                                {
                                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)); // #212121
                                }
                                else if (currentTheme.Contains("Kotor2Style"))
                                {
                                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0xae, 0x88)); // #18ae88
                                }
                                else if (currentTheme.Contains("KotorStyle"))
                                {
                                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0xAA, 0xFF)); // #3AAAFF
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "Error refreshing markdown content in SummaryTab");
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error in RefreshMarkdownContent");
            }
        }
    }
}
