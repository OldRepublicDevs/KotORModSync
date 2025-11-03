// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;
using KOTORModSync.Services;

namespace KOTORModSync.Controls
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public partial class EditorTab : UserControl
    {
        private readonly ComponentValidationWatcherService _validationWatcher;

        public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
            AvaloniaProperty.Register<EditorTab, ModComponent>(nameof(CurrentComponent));

        public static readonly StyledProperty<List<string>> TierOptionsProperty =
            AvaloniaProperty.Register<EditorTab, List<string>>(nameof(TierOptions));

        public static readonly StyledProperty<DownloadCacheService> DownloadCacheServiceProperty =
            AvaloniaProperty.Register<EditorTab, DownloadCacheService>(nameof(DownloadCacheService));

        public static readonly StyledProperty<ModManagementService> ModManagementServiceProperty =
            AvaloniaProperty.Register<EditorTab, ModManagementService>(nameof(ModManagementService));

        [CanBeNull]
        public ModComponent CurrentComponent
        {
            get => MainConfig.CurrentComponent;
            set
            {
                MainConfig.CurrentComponent = value;
                SetValue(CurrentComponentProperty, value);
                
                // Update the validation watcher to monitor this component's files
                _validationWatcher?.SetCurrentComponent(value);
                
                // Trigger initial validation when component is loaded
                if (value != null)
                {
                    _ = ValidateCurrentComponentAsync();
                }
            }
        }

        private async Task ValidateCurrentComponentAsync()
        {
            if (CurrentComponent == null)
            {
                return;
            }

            try
            {
                // Validate all instructions in the component
                foreach (Instruction instruction in CurrentComponent.Instructions)
                {
                    if (instruction.Source != null)
                    {
                        foreach (string sourcePath in instruction.Source)
                        {
                            if (!string.IsNullOrWhiteSpace(sourcePath))
                            {
                                await Core.Services.Validation.PathValidationCache.ValidateAndCacheAsync(
                                    sourcePath, instruction, CurrentComponent).ConfigureAwait(false);
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(instruction.Destination))
                    {
                        await Core.Services.Validation.PathValidationCache.ValidateAndCacheAsync(
                            instruction.Destination, instruction, CurrentComponent).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Error validating component");
            }
        }

        [CanBeNull]
        public List<string> TierOptions
        {
            get => GetValue(TierOptionsProperty);
            set => SetValue(TierOptionsProperty, value);
        }

        [CanBeNull]
        public DownloadCacheService DownloadCacheService
        {
            get => GetValue(DownloadCacheServiceProperty);
            set => SetValue(DownloadCacheServiceProperty, value);
        }

        [CanBeNull]
        public ModManagementService ModManagementService
        {
            get => GetValue(ModManagementServiceProperty);
            set => SetValue(ModManagementServiceProperty, value);
        }

        public event EventHandler<RoutedEventArgs> ExpandAllSectionsRequested;
        public event EventHandler<RoutedEventArgs> CollapseAllSectionsRequested;
        public event EventHandler<RoutedEventArgs> AutoGenerateInstructionsRequested;
        public event EventHandler<RoutedEventArgs> AddNewInstructionRequested;
        public event EventHandler<RoutedEventArgs> DeleteInstructionRequested;
        public event EventHandler<RoutedEventArgs> BrowseDestinationRequested;
        public event EventHandler<RoutedEventArgs> BrowseSourceFilesRequested;
        public event EventHandler<RoutedEventArgs> BrowseModFilesRequested;

        public event EventHandler<RoutedEventArgs> MoveInstructionUpRequested;
        public event EventHandler<RoutedEventArgs> MoveInstructionDownRequested;
        public event EventHandler<RoutedEventArgs> AddNewOptionRequested;
        public event EventHandler<RoutedEventArgs> DeleteOptionRequested;
        public event EventHandler<RoutedEventArgs> MoveOptionUpRequested;
        public event EventHandler<RoutedEventArgs> MoveOptionDownRequested;

        [SuppressMessage("Design", "MA0046:Use EventHandler<T> to declare events", Justification = "<Pending>")]
        public event EventHandler<Core.Services.Validation.PathValidationResult> JumpToBlockingInstructionRequested;

        public EditorTab()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize validation watcher
            _validationWatcher = new ComponentValidationWatcherService();
            _validationWatcher.ValidationStateChanged += OnValidationStateChanged;

            // Initialize Tier options so the ComboBox has selectable items
            try
            {
                var tierKeys = new List<string>(CategoryTierDefinitions.TierDefinitions.Keys);
                // Keep a stable, user-friendly order: by leading number if present
                tierKeys.Sort((a, b) =>
                {
                    int ParseLeadingNumber(string s)
                    {
                        if (string.IsNullOrWhiteSpace(s))
                        {
                            return int.MaxValue;
                        }

                        for (int i = 0; i < s.Length; i++)
                        {
                            if (char.IsDigit(s[i]))
                            {
                                int j = i;
                                while (j < s.Length && char.IsDigit(s[j]))
                                {
                                    j++;
                                }

                                if (int.TryParse(s.Substring(i, j - i), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                                {
                                    return n;
                                }

                                break;
                            }
                        }
                        return int.MaxValue;
                    }

                    int na = ParseLeadingNumber(a);
                    int nb = ParseLeadingNumber(b);
                    int cmp = na.CompareTo(nb);
                    return cmp != 0 ? cmp : string.Compare(a, b, StringComparison.Ordinal);
                });

                TierOptions = tierKeys;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Failed to initialize Tier options");
            }
        }

        private void OnValidationStateChanged(object sender, ModComponent component)
        {
            // Force UI refresh to show updated validation state
            if (component != null && component == CurrentComponent)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        // Trigger property change to update bindings
                        var temp = CurrentComponent;
                        CurrentComponent = null;
                        CurrentComponent = temp;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "Error refreshing validation state");
                    }
                }, DispatcherPriority.Background);
            }
        }

        private void ExpandAllSections_Click(object sender, RoutedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => ExpandAllSections_Click(sender, e), DispatcherPriority.Normal);
                return;
            }
            try
            {
                BasicInfoExpander.IsExpanded = true;
                DescriptionExpander.IsExpanded = true;
                DependenciesExpander.IsExpanded = true;
                InstructionsExpander.IsExpanded = true;
                OptionsExpander.IsExpanded = true;
                ExpandAllSectionsRequested?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error expanding all sections");
            }
        }

        private void CollapseAllSections_Click(object sender, RoutedEventArgs e)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => CollapseAllSections_Click(sender, e), DispatcherPriority.Normal);
                return;
            }
            try
            {
                BasicInfoExpander.IsExpanded = false;
                DescriptionExpander.IsExpanded = false;
                DependenciesExpander.IsExpanded = false;
                InstructionsExpander.IsExpanded = false;
                OptionsExpander.IsExpanded = false;
                CollapseAllSectionsRequested?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error collapsing all sections");
            }
        }

        private void AutoGenerateInstructions_Click(object sender, RoutedEventArgs e)
        {
            AutoGenerateInstructionsRequested?.Invoke(this, e);
        }

        private void AddNewInstruction_Click(object sender, RoutedEventArgs e)
        {
            AddNewInstructionRequested?.Invoke(this, e);
        }

        private void DeleteInstruction_Click(object sender, RoutedEventArgs e)
        {
            DeleteInstructionRequested?.Invoke(this, e);
        }

        private void BrowseDestination_Click(object sender, RoutedEventArgs e)
        {
            BrowseDestinationRequested?.Invoke(this, e);
        }

        private void BrowseSourceFiles_Click(object sender, RoutedEventArgs e)
        {
            BrowseSourceFilesRequested?.Invoke(this, e);
        }

        private void BrowseModFiles_Click(object sender, RoutedEventArgs e)
        {
            BrowseModFilesRequested?.Invoke(this, e);
        }

        private void MoveInstructionUp_Click(object sender, RoutedEventArgs e)
        {
            MoveInstructionUpRequested?.Invoke(this, e);
        }

        private void MoveInstructionDown_Click(object sender, RoutedEventArgs e)
        {
            MoveInstructionDownRequested?.Invoke(this, e);
        }

        private void AddNewOption_Click(object sender, RoutedEventArgs e)
        {
            AddNewOptionRequested?.Invoke(this, e);
        }

        private void DeleteOption_Click(object sender, RoutedEventArgs e)
        {
            DeleteOptionRequested?.Invoke(this, e);
        }

        private void MoveOptionUp_Click(object sender, RoutedEventArgs e)
        {
            MoveOptionUpRequested?.Invoke(this, e);
        }

        private void MoveOptionDown_Click(object sender, RoutedEventArgs e)
        {
            MoveOptionDownRequested?.Invoke(this, e);
        }
    }
}
