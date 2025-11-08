// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Controls;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public partial class LoadInstructionPage : WizardPageBase
    {
        private readonly MainConfig _mainConfig;
        private LandingPageView _landingPageView;

        public LoadInstructionPage([NotNull] MainConfig mainConfig)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));

            InitializeComponent();
            InitializeLandingPage();
            UpdateStatus();
        }

        public override string Title => "Load Instruction File";

        public override string Subtitle => "Load a .toml file to preconfigure the wizard";

        public override Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            UpdateStatus();
            return Task.CompletedTask;
        }

        public void InstructionFileLoaded()
        {
            UpdateStatus();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _landingPageView = this.FindControl<LandingPageView>("LandingPageHost");
        }

        private void InitializeLandingPage()
        {
            if (_landingPageView is null)
            {
                return;
            }

            _landingPageView.LoadInstructionsRequested += OnLoadInstructionsRequested;
            _landingPageView.CreateInstructionsRequested += OnCreateInstructionsRequested;
            _landingPageView.ConfigureDirectoriesRequested += OnConfigureDirectoriesRequested;
        }

        private void UpdateStatus()
        {
            if (_landingPageView is null)
            {
                return;
            }

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(UpdateStatus);
                return;
            }

            bool instructionsLoaded = (_mainConfig.allComponents?.Count ?? 0) > 0;
            string instructionFileName = null;
            bool editorModeEnabled = MainConfig.EditorMode;
            string modDirectory = _mainConfig.sourcePathFullName;
            string gameDirectory = _mainConfig.destinationPathFullName;

            if (
                Application.Current?.ApplicationLifetime is ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow
            )
            {
                instructionFileName = mainWindow.LastLoadedInstructionFileName;
            }

            _landingPageView.UpdateState(
                instructionsLoaded,
                instructionFileName,
                editorModeEnabled,
                modDirectory,
                gameDirectory
            );
        }

        private void OnLoadInstructionsRequested(object sender, EventArgs e)
        {
            if (
                Application.Current?.ApplicationLifetime is ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow
            )
            {
                mainWindow.LoadFile_Click(sender ?? mainWindow, new RoutedEventArgs());
            }
        }

        private void OnCreateInstructionsRequested(object sender, EventArgs e)
        {
            if (
                Application.Current?.ApplicationLifetime is ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow
            )
            {
                mainWindow.EnterEditorFromLandingPage();
            }
        }

        private void OnConfigureDirectoriesRequested(object sender, EventArgs e)
        {
            if (
                Application.Current?.ApplicationLifetime is ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is MainWindow mainWindow
            )
            {
                mainWindow.OpenDirectorySetupFromLandingPage();
            }
        }
    }
}


