// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync;
using KOTORModSync.Core;
using KOTORModSync.Dialogs;
using KOTORModSync.Dialogs.WizardPages;

namespace KOTORModSync.Controls
{
    public partial class WizardHostControl : UserControl
    {
        private readonly List<IWizardPage> _pages = new List<IWizardPage>();
        private int _currentPageIndex = 0;
        private MainConfig _mainConfig;
        private List<ModComponent> _allComponents;
        private CancellationTokenSource _cancellationTokenSource;
        private Window _parentWindow;
        private MainWindow _mainWindow;
        private ModListSidebar _modListSidebar;

        // Installation state
        public bool InstallationCompleted { get; private set; }
        public bool InstallationCancelled { get; private set; }

        // Widescreen state
        private bool _hasWidescreenMods;
        private List<ModComponent> _widescreenMods;

        // Events
        public event EventHandler WizardCompleted;
        public event EventHandler WizardCancelled;

        public WizardHostControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Initializes the wizard with the required data
        /// </summary>
        public void Initialize([NotNull] MainConfig mainConfig, [NotNull] List<ModComponent> allComponents, [NotNull] Window parentWindow, [CanBeNull] ModListSidebar modListSidebar = null)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));
            _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
            _mainWindow = parentWindow as MainWindow;
            _modListSidebar = modListSidebar;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            InitializePages();
            NavigateToPage(0);
        }

        /// <summary>
        /// Resets the wizard state for a fresh start
        /// </summary>
        public void Reset()
        {
            _pages.Clear();
            _currentPageIndex = 0;
            InstallationCompleted = false;
            InstallationCancelled = false;
            _hasWidescreenMods = false;
            _widescreenMods?.Clear();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private void InitializePages()
        {
            _pages.Clear();

            // 1. Welcome
            _pages.Add(new WelcomePage());

            // 2. BeforeContent (conditional)
            if (!string.IsNullOrWhiteSpace(_mainConfig.preambleContent))
            {
                _pages.Add(new BeforeContentPage(_mainConfig.preambleContent));
            }

            // 3. Setup (Directories & Load File)
            _pages.Add(new SetupPage(_mainConfig));

            // 4. AspyrNotice (conditional)
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

            // 5. ModSelection
            _pages.Add(new ModSelectionPage(_allComponents));

            // 6. DownloadsExplain
            _pages.Add(new DownloadsExplainPage());

            // 7. Validate
            _pages.Add(new ValidatePage(_allComponents, _mainConfig));

            // 8. InstallStart
            _pages.Add(new InstallStartPage(_allComponents));

            // 9. Installing (progress page)
            _pages.Add(new InstallingPage(_allComponents, _mainConfig, _cancellationTokenSource));

            // 10. BaseInstallComplete
            _pages.Add(new BaseInstallCompletePage());

            // Note: Widescreen pages (11-14) will be added dynamically after base install if needed

            // 15. Finished
            _pages.Add(new FinishedPage());
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

            // 11. WidescreenNotice
            if (!string.IsNullOrWhiteSpace(_mainConfig.widescreenWarningContent))
            {
                _pages.Insert(finishedPageIndex, new WidescreenNoticePage(_mainConfig.widescreenWarningContent));
                finishedPageIndex++;
            }

            // 12. WidescreenModSelection
            _pages.Insert(finishedPageIndex, new WidescreenModSelectionPage(_widescreenMods));
            finishedPageIndex++;

            // 13. WidescreenInstalling
            _pages.Insert(finishedPageIndex, new WidescreenInstallingPage(_widescreenMods, _mainConfig, _cancellationTokenSource));
            finishedPageIndex++;

            // 14. WidescreenComplete
            _pages.Insert(finishedPageIndex, new WidescreenCompletePage());
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Bug", "S3168:\"async\" methods should not return \"void\"", Justification = "<Pending>")]
        private async void NavigateToPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pages.Count)
            {
                return;
            }

            _currentPageIndex = pageIndex;
            IWizardPage page = _pages[pageIndex];

            // Update header
            PageTitleText.Text = page.Title;
            PageSubtitleText.Text = page.Subtitle;
            ProgressStepText.Text = $"Step {pageIndex + 1} of {_pages.Count}";
            WizardProgress.Maximum = _pages.Count;
            WizardProgress.Value = pageIndex + 1;

            // Update content
            PageContent.Content = page.Content;

            // Update navigation buttons
            BackButton.IsEnabled = pageIndex > 0 && page.CanNavigateBack;
            NextButton.IsEnabled = page.CanNavigateForward;
            NextButton.IsVisible = pageIndex < _pages.Count - 1;
            FinishButton.IsVisible = pageIndex == _pages.Count - 1;
            CancelButton.IsEnabled = page.CanCancel;

            // Update button text
            if (page is InstallingPage || page is WidescreenInstallingPage)
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
            try
            {
                await page.OnNavigatedToAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Error activating wizard page");
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
                        _parentWindow,
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
                        _parentWindow,
                        "Are you sure you want to stop the installation?\n\nThe current mod will finish installing, but no further mods will be installed."
                    );

                    if (result != true)
                    {
                        return;
                    }

                    await _cancellationTokenSource.CancelAsync();
                    InstallationCancelled = true;
                    WizardCancelled?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // Regular cancel
                bool? confirmCancel = await ConfirmationDialog.ShowConfirmationDialogAsync(
                    _parentWindow,
                    "Are you sure you want to exit the installation wizard?"
                );

                if (confirmCancel == true)
                {
                    InstallationCancelled = true;
                    await _cancellationTokenSource.CancelAsync();
                    WizardCancelled?.Invoke(this, EventArgs.Empty);
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
            WizardCompleted?.Invoke(this, EventArgs.Empty);
        }

        [UsedImplicitly]
        private void SwitchToLightTheme_Click(object sender, RoutedEventArgs e) => ThemeManager.UpdateStyle("/Styles/FluentLightStyle.axaml");
        [UsedImplicitly]
        private void SwitchToK1Theme_Click(object sender, RoutedEventArgs e) => ThemeManager.UpdateStyle("/Styles/KotorStyle.axaml");
        [UsedImplicitly]
        private void SwitchToTslTheme_Click(object sender, RoutedEventArgs e) => ThemeManager.UpdateStyle("/Styles/Kotor2Style.axaml");

        public void Cleanup()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }
}

