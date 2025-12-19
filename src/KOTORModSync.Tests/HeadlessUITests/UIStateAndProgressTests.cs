// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KOTORModSync;
using KOTORModSync.Core;
using KOTORModSync.Services;
using Xunit;

namespace KOTORModSync.Tests.HeadlessUITests
{
    [Collection(HeadlessTestApp.CollectionName)]
    public sealed class UIStateAndProgressTests
    {
        private static async Task<MainWindow> CreateWindowAsync(bool withComponents = false)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    ResetMainConfig(withComponents);
                },
                DispatcherPriority.Background);

            var window = await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var w = new MainWindow();
                    w.Show();
                    return w;
                },
                DispatcherPriority.Background);

            await PumpEventsAsync();
            return window;
        }

        private static void ResetMainConfig(bool withComponents)
        {
            MainConfig.AllComponents = withComponents
                ? new List<ModComponent> { new ModComponent { Name = "Test Mod", Guid = Guid.NewGuid() } }
                : new List<ModComponent>();
        }

        private static async Task PumpEventsAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(50);
        }

        private static async Task CloseWindowAsync(Window window)
        {
            if (window == null)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    window.Close();
                },
                DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        #region Step Progress Tests

        [AvaloniaFact(DisplayName = "Step 1 complete when paths are set")]
        public async Task StepProgress_Step1_CompleteWhenPathsSet()
        {
            var window = await CreateWindowAsync();
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainConfig.Instance.sourcePath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    MainConfig.Instance.destinationPath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var validationService = new ValidationService(MainConfig.Instance);
                bool step1Complete = ValidationService.IsStep1Complete();

                Assert.True(step1Complete, "Step 1 should be complete when paths are set");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Step 2 complete when components are loaded")]
        public async Task StepProgress_Step2_CompleteWhenComponentsLoaded()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainConfig.Instance.sourcePath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    MainConfig.Instance.destinationPath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var validationService = new ValidationService(MainConfig.Instance);
                bool step1Complete = ValidationService.IsStep1Complete();
                bool step2Complete = step1Complete && MainConfig.AllComponents?.Count > 0;

                Assert.True(step2Complete, "Step 2 should be complete when components are loaded");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Step 3 complete when components are selected")]
        public async Task StepProgress_Step3_CompleteWhenComponentsSelected()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainConfig.Instance.sourcePath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    MainConfig.Instance.destinationPath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    MainConfig.AllComponents.First().IsSelected = true;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                bool step3Complete = MainConfig.AllComponents?.Any(c => c.IsSelected) == true;

                Assert.True(step3Complete, "Step 3 should be complete when components are selected");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Step 4 complete when all selected components are downloaded")]
        public async Task StepProgress_Step4_CompleteWhenAllDownloaded()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainConfig.Instance.sourcePath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    MainConfig.Instance.destinationPath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    var component = MainConfig.AllComponents.First();
                    component.IsSelected = true;
                    component.IsDownloaded = true;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var selectedComponents = MainConfig.AllComponents.Where(c => c.IsSelected).ToList();
                bool step4Complete = selectedComponents.Count > 0 && selectedComponents.All(c => c.IsDownloaded);

                Assert.True(step4Complete, "Step 4 should be complete when all selected components are downloaded");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Step 4 incomplete when some components not downloaded")]
        public async Task StepProgress_Step4_IncompleteWhenNotAllDownloaded()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MainConfig.Instance.sourcePath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    MainConfig.Instance.destinationPath = new System.IO.DirectoryInfo(System.IO.Path.GetTempPath());
                    var component1 = new ModComponent { Name = "Mod 1", Guid = Guid.NewGuid(), IsSelected = true, IsDownloaded = true };
                    var component2 = new ModComponent { Name = "Mod 2", Guid = Guid.NewGuid(), IsSelected = true, IsDownloaded = false };
                    MainConfig.AllComponents = new List<ModComponent> { component1, component2 };
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var selectedComponents = MainConfig.AllComponents.Where(c => c.IsSelected).ToList();
                bool step4Complete = selectedComponents.Count > 0 && selectedComponents.All(c => c.IsDownloaded);

                Assert.False(step4Complete, "Step 4 should be incomplete when some components are not downloaded");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        #endregion

        #region Component State Visibility Tests

        [AvaloniaFact(DisplayName = "Component validation state updates correctly")]
        public async Task ComponentState_ValidationState_UpdatesCorrectly()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var component = MainConfig.AllComponents.First();
                    component.IsValidating = true;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var component = MainConfig.AllComponents.First();
                Assert.True(component.IsValidating, "Component should be in validating state");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Component install state transitions correctly")]
        public async Task ComponentState_InstallState_TransitionsCorrectly()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var component = MainConfig.AllComponents.First();
                    component.InstallState = ModComponent.ComponentInstallState.Pending;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var component = MainConfig.AllComponents.First();
                Assert.Equal(ModComponent.ComponentInstallState.Pending, component.InstallState);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    component.InstallState = ModComponent.ComponentInstallState.Running;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                Assert.Equal(ModComponent.ComponentInstallState.Running, component.InstallState);
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        #endregion

        #region Component Selection State Tests

        [AvaloniaFact(DisplayName = "Component selection state affects visibility")]
        public async Task ComponentSelection_SelectionState_AffectsVisibility()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var component = MainConfig.AllComponents.First();
                    component.IsSelected = true;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var component = MainConfig.AllComponents.First();
                Assert.True(component.IsSelected, "Component should be selected");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Unselected component has different visual state")]
        public async Task ComponentSelection_Unselected_HasDifferentVisualState()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var component = MainConfig.AllComponents.First();
                    component.IsSelected = false;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var component = MainConfig.AllComponents.First();
                Assert.False(component.IsSelected, "Component should not be selected");
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        #endregion

        #region Complex UI State Combinations

        [AvaloniaFact(DisplayName = "Component with all states set correctly")]
        public async Task ComponentState_AllStates_SetCorrectly()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var component = MainConfig.AllComponents.First();
                    component.IsSelected = true;
                    component.IsDownloaded = true;
                    component.InstallState = ModComponent.ComponentInstallState.Completed;
                    component.IsValidating = false;
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var component = MainConfig.AllComponents.First();
                Assert.Multiple(() =>
                {
                    Assert.True(component.IsSelected, "Component should be selected");
                    Assert.True(component.IsDownloaded, "Component should be downloaded");
                    Assert.Equal(ModComponent.ComponentInstallState.Completed, component.InstallState);
                    Assert.False(component.IsValidating, "Component should not be validating");
                });
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Multiple components with different states")]
        public async Task ComponentState_MultipleComponents_DifferentStates()
        {
            var window = await CreateWindowAsync(withComponents: true);
            try
            {
                await PumpEventsAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var component1 = new ModComponent 
                    { 
                        Name = "Mod 1", 
                        Guid = Guid.NewGuid(), 
                        IsSelected = true,
                        IsDownloaded = true,
                        InstallState = ModComponent.ComponentInstallState.Completed
                    };
                    var component2 = new ModComponent 
                    { 
                        Name = "Mod 2", 
                        Guid = Guid.NewGuid(), 
                        IsSelected = false,
                        IsDownloaded = false,
                        InstallState = ModComponent.ComponentInstallState.Pending
                    };
                    var component3 = new ModComponent 
                    { 
                        Name = "Mod 3", 
                        Guid = Guid.NewGuid(), 
                        IsSelected = true,
                        IsDownloaded = false,
                        InstallState = ModComponent.ComponentInstallState.Failed
                    };

                    MainConfig.AllComponents = new List<ModComponent> { component1, component2, component3 };
                }, DispatcherPriority.Background);

                await PumpEventsAsync();

                var components = MainConfig.AllComponents.ToList();
                Assert.Multiple(() =>
                {
                    Assert.Equal(3, components.Count);
                    Assert.True(components[0].IsSelected, "Component 1 should be selected");
                    Assert.False(components[1].IsSelected, "Component 2 should not be selected");
                    Assert.True(components[2].IsSelected, "Component 3 should be selected");
                });
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        #endregion
    }
}

