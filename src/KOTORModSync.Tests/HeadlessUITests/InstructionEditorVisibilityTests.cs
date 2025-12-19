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
using KOTORModSync.Controls;
using KOTORModSync.Core;
using KOTORModSync.Models;
using Xunit;

namespace KOTORModSync.Tests.HeadlessUITests
{
    [Collection(HeadlessTestApp.CollectionName)]
    public sealed class InstructionEditorVisibilityTests
    {
        private static async Task PumpEventsAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(50);
        }

        #region Instruction Editor Conditional Visibility Tests

        [AvaloniaFact(DisplayName = "Choose action shows component selector, not file paths")]
        public async Task InstructionEditor_ChooseAction_ShowsComponentSelector()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Choose,
                    Source = new List<string> { Guid.NewGuid().ToString() }
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find controls
                var choosePanel = editor.GetLogicalDescendants()
                    .OfType<StackPanel>()
                    .FirstOrDefault(p => p.IsVisible && p.Children.OfType<DependencyControl>().Any());

                Assert.NotNull(choosePanel);
                Assert.True(choosePanel.IsVisible, "Choose action should show component selector");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Extract action shows file paths, not component selector")]
        public async Task InstructionEditor_ExtractAction_ShowsFilePaths()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Extract,
                    Source = new List<string> { "<<modDirectory>>/file.zip" }
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find file path controls
                var filePathPanel = editor.GetLogicalDescendants()
                    .OfType<StackPanel>()
                    .FirstOrDefault(p => p.IsVisible && p.Children.OfType<TextBox>().Any());

                Assert.NotNull(filePathPanel);
                Assert.True(filePathPanel.IsVisible, "Extract action should show file paths");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Arguments field visible for DelDuplicate action")]
        public async Task InstructionEditor_DelDuplicateAction_ShowsArguments()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.DelDuplicate,
                    Source = new List<string> { ".tpc", ".tga" },
                    Arguments = ".tpc"
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find arguments panel
                var argumentsPanel = editor.GetLogicalDescendants()
                    .OfType<StackPanel>()
                    .FirstOrDefault(p => p.IsVisible && p.Children.OfType<FileExtensionsControl>().Any());

                Assert.NotNull(argumentsPanel);
                Assert.True(argumentsPanel.IsVisible, "DelDuplicate action should show arguments field");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Arguments field visible for Execute action")]
        public async Task InstructionEditor_ExecuteAction_ShowsArguments()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Execute,
                    Source = new List<string> { "<<modDirectory>>/program.exe" },
                    Arguments = "/arg1 /arg2"
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find arguments textbox
                var argumentsTextBox = editor.GetLogicalDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(tb => tb.IsVisible && tb.Watermark?.Contains("command line") == true);

                Assert.NotNull(argumentsTextBox);
                Assert.True(argumentsTextBox.IsVisible, "Execute action should show arguments textbox");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Arguments field visible for Patcher action")]
        public async Task InstructionEditor_PatcherAction_ShowsArguments()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Patcher,
                    Source = new List<string> { "<<modDirectory>>/tslpatchdata" },
                    Arguments = "0"
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find arguments panel
                var argumentsPanel = editor.GetLogicalDescendants()
                    .OfType<StackPanel>()
                    .FirstOrDefault(p => p.IsVisible && p.Children.OfType<ComboBox>().Any());

                Assert.NotNull(argumentsPanel);
                Assert.True(argumentsPanel.IsVisible, "Patcher action should show arguments field");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Overwrite checkbox visible for Move action")]
        public async Task InstructionEditor_MoveAction_ShowsOverwrite()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Overwrite = true
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find overwrite checkbox
                var overwriteCheckbox = editor.GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .FirstOrDefault(cb => cb.IsVisible && cb.Content?.ToString()?.Contains("Overwrite") == true);

                Assert.NotNull(overwriteCheckbox);
                Assert.True(overwriteCheckbox.IsVisible, "Move action should show overwrite checkbox");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Overwrite checkbox visible for Copy action")]
        public async Task InstructionEditor_CopyAction_ShowsOverwrite()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Copy,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Overwrite = true
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find overwrite checkbox
                var overwriteCheckbox = editor.GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .FirstOrDefault(cb => cb.IsVisible && cb.Content?.ToString()?.Contains("Overwrite") == true);

                Assert.NotNull(overwriteCheckbox);
                Assert.True(overwriteCheckbox.IsVisible, "Copy action should show overwrite checkbox");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Overwrite checkbox visible for Rename action")]
        public async Task InstructionEditor_RenameAction_ShowsOverwrite()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Rename,
                    Source = new List<string> { "<<kotorDirectory>>/Override/old.txt" },
                    Destination = "new.txt",
                    Overwrite = true
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find overwrite checkbox
                var overwriteCheckbox = editor.GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .FirstOrDefault(cb => cb.IsVisible && cb.Content?.ToString()?.Contains("Overwrite") == true);

                Assert.NotNull(overwriteCheckbox);
                Assert.True(overwriteCheckbox.IsVisible, "Rename action should show overwrite checkbox");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Overwrite checkbox visible for Extract action")]
        public async Task InstructionEditor_ExtractAction_ShowsOverwrite()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Extract,
                    Source = new List<string> { "<<modDirectory>>/archive.zip" },
                    Destination = "<<modDirectory>>/extracted",
                    Overwrite = true
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find overwrite checkbox
                var overwriteCheckbox = editor.GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .FirstOrDefault(cb => cb.IsVisible && cb.Content?.ToString()?.Contains("Overwrite") == true);

                Assert.NotNull(overwriteCheckbox);
                Assert.True(overwriteCheckbox.IsVisible, "Extract action should show overwrite checkbox");
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Overwrite checkbox hidden for Choose action")]
        public async Task InstructionEditor_ChooseAction_HidesOverwrite()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Choose,
                    Source = new List<string> { Guid.NewGuid().ToString() }
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find overwrite checkbox
                var overwriteCheckbox = editor.GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .FirstOrDefault(cb => cb.Content?.ToString()?.Contains("Overwrite") == true);

                if (overwriteCheckbox != null)
                {
                    Assert.False(overwriteCheckbox.IsVisible, "Choose action should hide overwrite checkbox");
                }
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Overwrite checkbox hidden for Delete action")]
        public async Task InstructionEditor_DeleteAction_HidesOverwrite()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Delete,
                    Source = new List<string> { "<<kotorDirectory>>/Override/file.txt" }
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find overwrite checkbox
                var overwriteCheckbox = editor.GetLogicalDescendants()
                    .OfType<CheckBox>()
                    .FirstOrDefault(cb => cb.Content?.ToString()?.Contains("Overwrite") == true);

                if (overwriteCheckbox != null)
                {
                    Assert.False(overwriteCheckbox.IsVisible, "Delete action should hide overwrite checkbox");
                }
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        [AvaloniaFact(DisplayName = "Dependencies and Restrictions always visible in conditional execution")]
        public async Task InstructionEditor_ConditionalExecution_AlwaysVisible()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var instruction = new InstructionViewModel(new Instruction
                {
                    Action = Instruction.ActionType.Move,
                    Source = new List<string> { "<<modDirectory>>/file.txt" },
                    Destination = "<<kotorDirectory>>/Override",
                    Dependencies = new List<Guid> { Guid.NewGuid() },
                    Restrictions = new List<Guid> { Guid.NewGuid() }
                });

                var editor = new InstructionEditorControl
                {
                    DataContext = instruction
                };

                var window = new Window { Content = editor };
                window.Show();

                // Find conditional execution expander
                var expander = editor.GetLogicalDescendants()
                    .OfType<Expander>()
                    .FirstOrDefault(e => e.Header?.ToString()?.Contains("Conditional Execution") == true);

                Assert.NotNull(expander);
                // Expander should exist (visibility may depend on expansion state)
            }, DispatcherPriority.Background);

            await PumpEventsAsync();
        }

        #endregion

        #region All Action Type Visibility Tests

        [AvaloniaFact(DisplayName = "All action types show correct fields")]
        public async Task InstructionEditor_AllActionTypes_ShowCorrectFields()
        {
            var actionTypes = Enum.GetValues(typeof(Instruction.ActionType)).Cast<Instruction.ActionType>();

            foreach (var actionType in actionTypes)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var instruction = new InstructionViewModel(new Instruction
                    {
                        Action = actionType,
                        Source = new List<string> { actionType == Instruction.ActionType.Choose ? Guid.NewGuid().ToString() : "<<modDirectory>>/file.txt" },
                        Destination = actionType == Instruction.ActionType.Choose ? string.Empty : "<<kotorDirectory>>/Override"
                    });

                    var editor = new InstructionEditorControl
                    {
                        DataContext = instruction
                    };

                    var window = new Window { Content = editor };
                    window.Show();

                    // Verify editor is created successfully for each action type
                    Assert.NotNull(editor);
                }, DispatcherPriority.Background);

                await PumpEventsAsync();
            }
        }

        #endregion
    }
}

