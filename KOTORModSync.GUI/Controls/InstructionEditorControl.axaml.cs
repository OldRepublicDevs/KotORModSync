// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
    public partial class InstructionEditorControl : UserControl
    {
        public InstructionEditorControl()
        {

            AvaloniaXamlLoader.Load(this);

            Loaded += (sender, e) => UpdateFileExtensionsControl();
        }

        private static readonly char[] separator = new[] { ' ', ',', ';', '\n', '\r' };

        public event EventHandler<RoutedEventArgs> AddNewInstruction;
        public event EventHandler<RoutedEventArgs> DeleteInstruction;
        public event EventHandler<RoutedEventArgs> MoveInstructionUp;
        public event EventHandler<RoutedEventArgs> MoveInstructionDown;
        public event EventHandler<RoutedEventArgs> BrowseSourceFiles;
        public event EventHandler<RoutedEventArgs> BrowseModFiles;
        public event EventHandler<RoutedEventArgs> BrowseDestination;
        public event EventHandler<Core.Services.Validation.PathValidationResult> JumpToBlockingInstruction;

        private void AddNewInstruction_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => AddNewInstruction?.Invoke(this, e);

        private void DeleteInstruction_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => DeleteInstruction?.Invoke(this, e);

        private void MoveInstructionUp_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => MoveInstructionUp?.Invoke(this, e);

        private void MoveInstructionDown_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => MoveInstructionDown?.Invoke(this, e);

        private void BrowseSourceFiles_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            Core.Logger.LogVerbose("InstructionEditorControl.BrowseSourceFiles_Click: Event triggered");
            BrowseSourceFiles?.Invoke(this, e);
        }

        private void BrowseModFiles_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            Core.Logger.LogVerbose("InstructionEditorControl.BrowseModFiles_Click: Event triggered");
            BrowseModFiles?.Invoke(this, e);
        }

        private void BrowseDestination_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => BrowseDestination?.Invoke(this, e);

        private void JumpToBlockingInstruction_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Core.Services.Validation.PathValidationResult validationResult)
            {
                JumpToBlockingInstruction?.Invoke(this, validationResult);
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            Dispatcher.UIThread.Post(() => UpdateFileExtensionsControl(), DispatcherPriority.Loaded);
        }

        private void UpdateFileExtensionsControl()
        {
            if (DataContext is Instruction instruction && instruction.Action == Instruction.ActionType.DelDuplicate)
            {

                List<string> extensions = ParseExtensionsFromArguments(instruction.Arguments);
                Logger.LogVerbose($"InstructionEditorControl.UpdateFileExtensionsControl: Arguments='{instruction.Arguments}', Parsed extensions: [{string.Join(", ", extensions)}]");
                var fileExtensionsControl = this.FindControl<FileExtensionsControl>("FileExtensionsControl");
                if (fileExtensionsControl != null)
                {
                    Logger.LogVerbose("Found FileExtensionsControl, calling SetExtensions");
                    fileExtensionsControl.SetExtensions(extensions);
                }
                else
                {
                    Logger.LogVerbose("FileExtensionsControl not found!");
                }
            }
        }

        private static List<string> ParseExtensionsFromArguments([NotNull] string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return new List<string>();
            }

            var extensions = arguments.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .Select(ext => ext.Trim())
                .ToList();

            return extensions;
        }

        public void SyncExtensionsToArguments()
        {
            if (DataContext is Instruction instruction && instruction.Action == Instruction.ActionType.DelDuplicate)
            {
                var fileExtensionsControl = this.FindControl<FileExtensionsControl>("FileExtensionsControl");
                var extensions = fileExtensionsControl?.GetValidExtensions() ?? new List<string>();
                instruction.Arguments = string.Join(" ", extensions.Where(ext => !string.IsNullOrWhiteSpace(ext)));
            }
        }

        private async void RefreshPathValidation_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(DataContext is Instruction instruction))
            {
                return;
            }

            string path = button.Tag as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                // Handle list paths - take first path from the list
                if (button.Tag is System.Collections.Generic.List<string> pathList && pathList.Count > 0)
                {
                    path = pathList[0];
                }
                else
                {
                    return;
                }
            }

            ModComponent component = MainConfig.CurrentComponent;
            if (component is null)
            {
                return;
            }

            string originalContent = button.Content?.ToString() ?? "⟳";
            button.IsEnabled = false;
            try
            {
                button.Content = "⏳";
                // Validate and cache the result using VFS
                await Core.Services.Validation.PathValidationCache.ValidateAndCacheAsync(path, instruction, component).ConfigureAwait(false);

                // Force UI update - trigger a fake property change to refresh bindings
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Trigger property changed notification to refresh bindings using reflection
                    if (instruction is System.ComponentModel.INotifyPropertyChanged)
                    {
                        // Use reflection to call OnPropertyChanged if it exists
                        string propertyName = string.Equals(button.Name, "SourceRefreshButton", StringComparison.Ordinal) ? "Source" : "Destination";
                        MethodInfo onPropertyChangedMethod = instruction.GetType().GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null)
                            ?? throw new InvalidOperationException("OnPropertyChanged method not found");
                        onPropertyChangedMethod.Invoke(instruction, new object[] { propertyName });
                    }
                    button.Content = originalContent;
                    button.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Error refreshing path validation");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    button.Content = originalContent;
                    button.IsEnabled = true;
                });
            }
        }
    }
}
