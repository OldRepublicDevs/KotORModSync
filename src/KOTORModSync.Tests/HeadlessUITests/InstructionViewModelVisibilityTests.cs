// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KOTORModSync;
using KOTORModSync.Core;
using KOTORModSync.Models;
using Xunit;

namespace KOTORModSync.Tests.HeadlessUITests
{
    [Collection(HeadlessTestApp.CollectionName)]
    public sealed class InstructionViewModelVisibilityTests
    {
        private static async Task PumpEventsAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(50);
        }

        #region InstructionViewModel WillExecute Tests

        [AvaloniaFact(DisplayName = "InstructionViewModel WillExecute true sets opacity to 1.0")]
        public async Task InstructionViewModel_WillExecuteTrue_SetsOpacityToOne()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override"
                };

                var viewModel = new InstructionViewModel(instruction, component, willExecute: true);

                Assert.Equal(1.0, viewModel.Opacity);
                Assert.Equal("SemiBold", viewModel.FontWeight);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel WillExecute false sets opacity to 0.5")]
        public async Task InstructionViewModel_WillExecuteFalse_SetsOpacityToHalf()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override"
                };

                var viewModel = new InstructionViewModel(instruction, component, willExecute: false);

                Assert.Equal(0.5, viewModel.Opacity);
                Assert.Equal("Normal", viewModel.FontWeight);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel updates visual state when WillExecute changes")]
        public async Task InstructionViewModel_WillExecuteChange_UpdatesVisualState()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override"
                };

                var viewModel = new InstructionViewModel(instruction, component, willExecute: false);

                Assert.Equal(0.5, viewModel.Opacity);
                Assert.Equal("Normal", viewModel.FontWeight);

                viewModel.WillExecute = true;

                Assert.Equal(1.0, viewModel.Opacity);
                Assert.Equal("SemiBold", viewModel.FontWeight);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel resolves dependency names correctly")]
        public async Task InstructionViewModel_DependencyNames_ResolvesCorrectly()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var depComponent = new ModComponent { Name = "Dependency Mod", Guid = Guid.NewGuid() };
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Dependencies = new List<Guid> { depComponent.Guid }
                };

                MainConfig.AllComponents = new List<ModComponent> { depComponent, component };

                var viewModel = new InstructionViewModel(instruction, component, willExecute: true);

                Assert.Contains("[ModComponent] Dependency Mod", viewModel.DependencyNames);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel resolves option dependency names correctly")]
        public async Task InstructionViewModel_OptionDependencyNames_ResolvesCorrectly()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var parentComponent = new ModComponent { Name = "Parent", Guid = Guid.NewGuid() };
                var option = new Option { Name = "Option", Guid = Guid.NewGuid() };
                parentComponent.Options.Add(option);

                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Dependencies = new List<Guid> { option.Guid }
                };

                MainConfig.AllComponents = new List<ModComponent> { parentComponent, component };

                var viewModel = new InstructionViewModel(instruction, component, willExecute: true);

                Assert.Contains("[Option] Parent → Option", viewModel.DependencyNames);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel resolves restriction names correctly")]
        public async Task InstructionViewModel_RestrictionNames_ResolvesCorrectly()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var restrictedComponent = new ModComponent { Name = "Restricted Mod", Guid = Guid.NewGuid() };
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Restrictions = new List<Guid> { restrictedComponent.Guid }
                };

                MainConfig.AllComponents = new List<ModComponent> { restrictedComponent, component };

                var viewModel = new InstructionViewModel(instruction, component, willExecute: true);

                Assert.Contains("[ModComponent] Restricted Mod", viewModel.RestrictionNames);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel with empty dependencies has empty dependency names")]
        public async Task InstructionViewModel_EmptyDependencies_HasEmptyDependencyNames()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Dependencies = new List<Guid>()
                };

                var viewModel = new InstructionViewModel(instruction, component, willExecute: true);

                Assert.Empty(viewModel.DependencyNames);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel with null dependencies has empty dependency names")]
        public async Task InstructionViewModel_NullDependencies_HasEmptyDependencyNames()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override"
                };
                instruction.Dependencies = null;

                var viewModel = new InstructionViewModel(instruction, component, willExecute: true);

                Assert.Empty(viewModel.DependencyNames);
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        #endregion

        #region InstructionViewModel Conditional Execution Tests

        [AvaloniaFact(DisplayName = "InstructionViewModel WillExecute true when no dependencies")]
        public async Task InstructionViewModel_NoDependencies_WillExecuteTrue()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override"
                };

                var components = new List<ModComponent> { component };
                bool shouldRun = ModComponent.ShouldRunInstruction(instruction, components);

                var viewModel = new InstructionViewModel(instruction, component, willExecute: shouldRun);

                Assert.True(viewModel.WillExecute, "Instruction should execute when no dependencies");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel WillExecute false when dependency not met")]
        public async Task InstructionViewModel_UnmetDependency_WillExecuteFalse()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var depComponent = new ModComponent { Name = "Dependency", Guid = Guid.NewGuid(), IsSelected = false };
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Dependencies = new List<Guid> { depComponent.Guid }
                };

                var components = new List<ModComponent> { depComponent, component };
                bool shouldRun = ModComponent.ShouldRunInstruction(instruction, components);

                var viewModel = new InstructionViewModel(instruction, component, willExecute: shouldRun);

                Assert.False(viewModel.WillExecute, "Instruction should not execute when dependency not met");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "InstructionViewModel WillExecute false when restriction selected")]
        public async Task InstructionViewModel_RestrictionSelected_WillExecuteFalse()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var restrictedComponent = new ModComponent { Name = "Restricted", Guid = Guid.NewGuid(), IsSelected = true };
                var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
                var instruction = new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Restrictions = new List<Guid> { restrictedComponent.Guid }
                };

                var components = new List<ModComponent> { restrictedComponent, component };
                bool shouldRun = ModComponent.ShouldRunInstruction(instruction, components);

                var viewModel = new InstructionViewModel(instruction, component, willExecute: shouldRun);

                Assert.False(viewModel.WillExecute, "Instruction should not execute when restriction selected");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        #endregion
    }
}

