// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Dialogs.WizardPages;

namespace KOTORModSync.Dialogs
{
    public partial class InstallWizardDialog : Window
    {
        private readonly List<IWizardPage> _pages = new List<IWizardPage>();
        private readonly ObservableCollection<WizardPageInfo> _pageInfos = new ObservableCollection<WizardPageInfo>();
        private int _currentPageIndex = 0;
        private readonly MainConfig _mainConfig;
        private readonly List<ModComponent> _allComponents;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly SemaphoreSlim _navigationSemaphore = new SemaphoreSlim(1, 1);

        // Installation state
        public bool InstallationCompleted { get; private set; }
        public bool InstallationCancelled { get; private set; }

        // Widescreen state
        private bool _hasWidescreenMods;
        private List<ModComponent> _widescreenMods;

        public InstallWizardDialog()
            : this(new MainConfig(), new List<ModComponent>())
        {
        }

        public InstallWizardDialog([NotNull] MainConfig mainConfig, [NotNull] List<ModComponent> allComponents)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));
            _cancellationTokenSource = new CancellationTokenSource();

            InitializeComponent();
            InitializePages();
            InitializeNavigationList();
            NavigateToPage(0);

            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        private void InitializeNavigationList()
        {
            // Create WizardPageInfo for each page
            for (int i = 0; i < _pages.Count; i++)
            {
                _pageInfos.Add(new WizardPageInfo(_pages[i], i + 1)); // Page numbers start at 1 for display
            }

            // Bind to the navigation list control
            PageNavigationList.ItemsSource = _pageInfos;
        }

        private void InitializePages()
        {
            // 1. Load instruction file
            var loadInstructionPage = new LoadInstructionPage(_mainConfig);
            _pages.Add(loadInstructionPage);

            // 2. Welcome
            _pages.Add(new WelcomePage());

            // 3. BeforeContent (conditional)
            if (!string.IsNullOrWhiteSpace(_mainConfig.preambleContent))
            {
                _pages.Add(new PreamblePage(_mainConfig.preambleContent));
            }

            // 4. Mod workspace directory
            _pages.Add(new ModDirectoryPage(_mainConfig));

            // 5. Game directory
            _pages.Add(new GameDirectoryPage(_mainConfig));

            // 6. AspyrNotice (conditional)
            if (
                (
                    string.Equals(MainConfig.TargetGame, "KOTOR2", StringComparison.Ordinal)
                    || string.Equals(MainConfig.TargetGame, "TSL", StringComparison.Ordinal)
                )
                && !string.IsNullOrWhiteSpace(_mainConfig.aspyrExclusiveWarningContent)
            )
            {
                _pages.Add(new AspyrNoticePage(_mainConfig.aspyrExclusiveWarningContent));
            }

            // 7. ModSelection
            _pages.Add(new ModSelectionPage(_allComponents));

            // 8. DownloadsExplain
            _pages.Add(new DownloadsExplainPage());

            // 9. Validate
            _pages.Add(new ValidatePage(_allComponents, _mainConfig));

            // 10. InstallStart
            _pages.Add(new InstallStartPage(_allComponents));

            // 11. Installing (progress page)
            _pages.Add(new InstallingPage(_allComponents, _mainConfig, _cancellationTokenSource));

            // 12. BaseInstallComplete
            _pages.Add(new BaseInstallCompletePage(0, TimeSpan.Zero, 0, 0));

            // Note: Widescreen-specific pages will be added dynamically after base install if needed

            // Finished
            _pages.Add(new FinishedPage());

            if ((MainConfig.AllComponents?.Count ?? 0) > 0)
            {
                loadInstructionPage.InstructionFileLoaded();
            }
        }

        private void AddWidescreenPages()
        {
            // Detect widescreen mods
            _widescreenMods = _allComponents.Where(c => c.WidescreenOnly).ToList();
            _hasWidescreenMods = _widescreenMods.Any();

            if (!_hasWidescreenMods)
            {
                return;
            }

            // Find the index to insert before FinishedPage
            int finishedPageIndex = _pages.Count - 1;
            int insertionStartIndex = finishedPageIndex;

            // 11. WidescreenNotice
            if (!string.IsNullOrWhiteSpace(_mainConfig.widescreenWarningContent))
            {
                _pages.Insert(finishedPageIndex, new WidescreenNoticePage(_mainConfig.widescreenWarningContent));
                _pageInfos.Insert(finishedPageIndex, new WizardPageInfo(_pages[finishedPageIndex], finishedPageIndex + 1));
                finishedPageIndex++;
            }

            // 12. WidescreenModSelection
            _pages.Insert(finishedPageIndex, new WidescreenModSelectionPage(_widescreenMods));
            _pageInfos.Insert(finishedPageIndex, new WizardPageInfo(_pages[finishedPageIndex], finishedPageIndex + 1));
            finishedPageIndex++;

            // 13. WidescreenInstalling
            _pages.Insert(finishedPageIndex, new WidescreenInstallingPage(_widescreenMods, _mainConfig, _cancellationTokenSource));
            _pageInfos.Insert(finishedPageIndex, new WizardPageInfo(_pages[finishedPageIndex], finishedPageIndex + 1));
            finishedPageIndex++;

            // 14. WidescreenComplete
            _pages.Insert(finishedPageIndex, new WidescreenCompletePage());
            _pageInfos.Insert(finishedPageIndex, new WizardPageInfo(_pages[finishedPageIndex], finishedPageIndex + 1));

            // Update page numbers for all pages after the insertion point
            for (int i = insertionStartIndex; i < _pageInfos.Count; i++)
            {
                // We need to recreate the page info with the correct page number
                WizardPageInfo oldInfo = _pageInfos[i];
                _pageInfos[i] = new WizardPageInfo(oldInfo.Page, i + 1)
                {
                    IsCompleted = oldInfo.IsCompleted,
                    IsCurrent = oldInfo.IsCurrent,
                    IsAccessible = oldInfo.IsAccessible,
                };
            }
        }

        private void NavigateToPage(int pageIndex) => _ = NavigateToPageInternalAsync(pageIndex);

        private async Task NavigateToPageInternalAsync(int pageIndex)
        {
            await _navigationSemaphore.WaitAsync();
            try
            {
                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] START: pageIndex={pageIndex}, _pages.Count={_pages.Count}");

                if (pageIndex < 0 || pageIndex >= _pages.Count)
                {
                    await Logger.LogErrorAsync($"[InstallWizardDialog.NavigateToPage] ABORT: Invalid page index {pageIndex}");
                    return;
                }

                _currentPageIndex = pageIndex;
                IWizardPage page = _pages[pageIndex];

                if (page is null)
                {
                    await Logger.LogErrorAsync($"[InstallWizardDialog.NavigateToPage] Page at index {pageIndex} is null. Aborting navigation.");
                    return;
                }

                IWizardPage activePage = page;

                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Page type: {activePage.GetType().Name}, Title: {activePage.Title}");
                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Page.Content type: {activePage.Content?.GetType().Name}, HasParent: {activePage.Content?.Parent != null}");
                if (activePage.Content?.Parent != null)
                {
                    await Logger.LogWarningAsync($"[InstallWizardDialog.NavigateToPage] WARNING: Page content already has parent: {activePage.Content.Parent.GetType().Name}");
                }

                // Update page states in navigation list
                UpdatePageStates();

                // Update header
                PageTitleText.Text = activePage.Title;
                PageSubtitleText.Text = activePage.Subtitle;
                ProgressStepText.Text = $"Step {pageIndex + 1} of {_pages.Count}";
                WizardProgress.Maximum = _pages.Count;
                WizardProgress.Value = pageIndex + 1;

                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Clearing PageContent. Current content type: {PageContent?.Content?.GetType().Name}");

                // CRITICAL: Clear existing content first to ensure clean detachment
                ClearPageContent();

                // Wait for Avalonia to process visual tree detachment of nested controls
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] PageContent cleared. Page.Content parent now: {activePage.Content?.Parent?.GetType().Name ?? "null"}");

                // Detach the page content from any existing parent
                if (activePage.Content?.Parent != null)
                {
                    await Logger.LogWarningAsync($"[InstallWizardDialog.NavigateToPage] Page content still has parent after clear: {activePage.Content.Parent.GetType().Name}. Attempting detach...");
                    DetachControlFromParent(activePage.Content);
                    await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] After DetachControlFromParent, parent is: {activePage.Content?.Parent?.GetType().Name ?? "null"}");
                }

                // If the page content is a ScrollViewer, recursively detach its nested content
                if (activePage.Content is ScrollViewer scrollViewer)
                {
                    await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Page content is ScrollViewer, detaching nested content...");
                    DetachScrollViewerContent(scrollViewer);
                    await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] ScrollViewer nested content detached");
                }

                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Creating fresh ContentControl wrapper...");

                // Create a fresh wrapper container each time to avoid visual tree conflicts
                var pageContainer = new ContentControl
                {
                    Content = activePage.Content,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                };

                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Wrapper created. Page.Content parent now: {activePage.Content?.Parent?.GetType().Name}");

                // Update content
                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Setting PageContent.Content to wrapper...");
                PageContent.Content = pageContainer;
                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] PageContent.Content set. Type: {PageContent?.Content?.GetType().Name}");

                // Update navigation buttons
                BackButton.IsEnabled = pageIndex > 0 && activePage.CanNavigateBack;
                NextButton.IsEnabled = activePage.CanNavigateForward;
                NextButton.IsVisible = pageIndex < _pages.Count - 1;
                FinishButton.IsVisible = pageIndex == _pages.Count - 1;
                CancelButton.IsEnabled = activePage.CanCancel;

                // Update button text
                if (activePage is InstallingPage || activePage is WidescreenInstallingPage)
                {
                    NextButton.Content = "Continue";
                    BackButton.IsEnabled = false;
                    CancelButton.Content = "Stop Install";
                }
                else
                {
                    NextButton.Content = "Next →";
                    CancelButton.Content = "Cancel";
                }

                // Call page activation
                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] Calling OnNavigatedToAsync...");
                try
                {
                    await activePage.OnNavigatedToAsync(_cancellationTokenSource.Token);
                    await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] OnNavigatedToAsync completed successfully");
                }
                catch (Exception ex)
                {
                    await Logger.LogExceptionAsync(ex, "[InstallWizardDialog.NavigateToPage] Error in OnNavigatedToAsync");
                }

                await Logger.LogVerboseAsync($"[InstallWizardDialog.NavigateToPage] COMPLETE: Successfully navigated to page {pageIndex}");
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, $"[InstallWizardDialog.NavigateToPage] FATAL ERROR during navigation to page {pageIndex}");
                throw;
            }
            finally
            {
                _navigationSemaphore.Release();
            }
        }

        private void UpdatePageStates()
        {
            for (int i = 0; i < _pageInfos.Count; i++)
            {
                WizardPageInfo info = _pageInfos[i];

                // Update current state
                info.IsCurrent = (i == _currentPageIndex);

                // Update completed state - all pages before current are completed
                info.IsCompleted = (i < _currentPageIndex);

                // Update accessible state
                // Can access: current page, completed pages, or next page after a completed sequence
                info.IsAccessible = (i <= _currentPageIndex);
            }
        }

        private void NavigationItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WizardPageInfo pageInfo)
            {
                // Convert display page number (1-indexed) back to page index (0-indexed)
                int targetPageIndex = pageInfo.PageIndex - 1;

                // Only allow navigation if the page is accessible
                if (pageInfo.IsAccessible && targetPageIndex != _currentPageIndex)
                {
                    NavigateToPage(targetPageIndex);
                }
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IWizardPage currentPage = _pages[_currentPageIndex];

                // Validate current page before proceeding
                (bool isValid, string errorMessage) = await currentPage.ValidateAsync(_cancellationTokenSource.Token);

                if (!isValid)
                {
                    await InformationDialog.ShowInformationDialogAsync(
                        this,
                        errorMessage ?? "Please complete all required fields before continuing."
                    );
                    return;
                }

                // Call page deactivation
                await currentPage.OnNavigatingFromAsync(_cancellationTokenSource.Token);

                // Special handling for certain pages
                if (currentPage is BaseInstallCompletePage && !_hasWidescreenMods)
                {
                    // Check if widescreen pages need to be added
                    AddWidescreenPages();
                }

                // Navigate to next page
                if (_currentPageIndex < _pages.Count - 1)
                {
                    NavigateToPage(_currentPageIndex + 1);
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Error navigating to next page");
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IWizardPage currentPage = _pages[_currentPageIndex];
                await currentPage.OnNavigatingFromAsync(_cancellationTokenSource.Token);

                if (_currentPageIndex > 0)
                {
                    NavigateToPage(_currentPageIndex - 1);
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Error navigating to previous page");
            }
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IWizardPage currentPage = _pages[_currentPageIndex];

                // If on installing page, confirm cancellation
                if (currentPage is InstallingPage || currentPage is WidescreenInstallingPage)
                {
                    bool? result = await ConfirmationDialog.ShowConfirmationDialogAsync(
                        this,
                        "Are you sure you want to stop the installation?\n\nThe current mod will finish installing, but no further mods will be installed."
                    );

                    if (result != true)
                    {
                        return;
                    }

                    await _cancellationTokenSource.CancelAsync();
                    InstallationCancelled = true;
                    return;
                }

                // Regular cancel
                bool? confirmCancel = await ConfirmationDialog.ShowConfirmationDialogAsync(
                    this,
                    "Are you sure you want to cancel the installation wizard?"
                );

                if (confirmCancel == true)
                {
                    InstallationCancelled = true;
                    await _cancellationTokenSource.CancelAsync();
                    Close(dialogResult: false);
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Error cancelling wizard");
            }
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            InstallationCompleted = true;
            Close(dialogResult: true);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CancelButton_Click(sender, e);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clear page content
            ClearPageContent();

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }

        private void ClearPageContent()
        {
            if (PageContent?.Content is Control existingContent)
            {
                if (existingContent is ContentControl existingContentControl)
                {
                    if (existingContentControl.Content is Control innerControl)
                    {
                        existingContentControl.Content = null;

                        if (innerControl.Parent != null)
                        {
                            DetachControlFromParent(innerControl);
                        }
                    }
                    else if (existingContentControl.Content != null)
                    {
                        existingContentControl.Content = null;
                    }
                }

                DetachControlFromParent(existingContent);
            }

            if (PageContent != null)
            {
                PageContent.Content = null;
            }
        }

        private static void DetachScrollViewerContent(ScrollViewer scrollViewer)
        {
            if (scrollViewer?.Content is Control innerContent)
            {
                Logger.LogVerbose($"[InstallWizardDialog.DetachScrollViewerContent] Detaching ScrollViewer.Content: {innerContent.GetType().Name}");

                // Recursively detach nested controls if it's a panel
                if (innerContent.Parent != null && innerContent.Parent != scrollViewer)
                {
                    Logger.LogVerbose($"[InstallWizardDialog.DetachScrollViewerContent] Detaching nested content {innerContent.GetType().Name} from parent {innerContent.Parent.GetType().Name}");
                    DetachControlFromParent(innerContent);
                    scrollViewer.Content = innerContent;
                    Logger.LogVerbose("[InstallWizardDialog.DetachScrollViewerContent] ScrollViewer.Content reassigned after detach");
                }
            }
        }

        /// <summary>
        /// Detaches a control from its current parent in the visual tree.
        /// This is critical to avoid "control already has a visual parent" exceptions.
        /// </summary>
        private static void DetachControlFromParent(Control control)
        {
            if (control is null)
            {
                Logger.LogVerbose("[InstallWizardDialog.DetachControlFromParent] Control is null, nothing to detach");
                return;
            }

            Avalonia.StyledElement parent = control.Parent;

            if (parent is null)
            {
                Logger.LogVerbose($"[InstallWizardDialog.DetachControlFromParent] Control {control.GetType().Name} has no parent, nothing to detach");
                return;
            }

            Logger.LogVerbose($"[InstallWizardDialog.DetachControlFromParent] Detaching {control.GetType().Name} from parent {parent.GetType().Name}");

            switch (parent)
            {
                case ContentControl contentControl when contentControl.Content == control:
                    contentControl.Content = null;
                    Logger.LogVerbose($"[InstallWizardDialog.DetachControlFromParent] Detached from ContentControl");
                    break;
                case ContentPresenter contentPresenter when contentPresenter.Content == control:
                    contentPresenter.Content = null;
                    Logger.LogVerbose($"[InstallWizardDialog.DetachControlFromParent] Detached from ContentPresenter");
                    break;
                case Decorator decorator when decorator.Child == control:
                    decorator.Child = null;
                    Logger.LogVerbose($"[InstallWizardDialog.DetachControlFromParent] Detached from Decorator");
                    break;
                case Panel panel:
                    if (panel.Children.Contains(control))
                    {
                        panel.Children.Remove(control);
                        Logger.LogVerbose($"[InstallWizardDialog.DetachControlFromParent] Removed from Panel with {panel.Children.Count} remaining children");
                    }
                    else
                    {
                        Logger.LogWarning($"[InstallWizardDialog.DetachControlFromParent] Control claims Panel parent but is not in Panel's children collection");
                    }
                    break;
                default:
                    Logger.LogWarning($"[InstallWizardDialog.DetachControlFromParent] Unknown parent type: {parent.GetType().Name}, cannot detach");
                    break;
            }

            if (control.Parent != null)
            {
                Logger.LogError($"[InstallWizardDialog.DetachControlFromParent] FAILED: Control still has parent {control.Parent.GetType().Name} after detach attempt");
            }
            else
            {
                Logger.LogVerbose($"[InstallWizardDialog.DetachControlFromParent] SUCCESS: Control parent is now null");
            }
        }
    }
}

