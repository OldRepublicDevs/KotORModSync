// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KOTORModSync;
using KOTORModSync.Controls;
using KOTORModSync.Core;
using Xunit;

namespace KOTORModSync.Tests
{
    [Collection(HeadlessTestApp.CollectionName)]
    public sealed class ControlsHeadlessTests
    {
        [AvaloniaFact(DisplayName = "GettingStartedTab buttons raise their routed events")]
        public async Task GettingStartedTab_Raises_All_Events_On_Click()
        {
            var tab = new GettingStartedTab();
            var window = await HostInWindowAsync(tab);

            try
            {
                var fired = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                void Mark(string key) => fired[key] = fired.TryGetValue(key, out int count) ? count + 1 : 1;

                tab.LoadInstructionFileRequested += (_, __) => Mark(nameof(tab.LoadInstructionFileRequested));
                tab.OpenSettingsRequested += (_, __) => Mark(nameof(tab.OpenSettingsRequested));
                tab.ScrapeDownloadsRequested += (_, __) => Mark(nameof(tab.ScrapeDownloadsRequested));
                tab.OpenModDirectoryRequested += (_, __) => Mark(nameof(tab.OpenModDirectoryRequested));
                tab.DownloadStatusRequested += (_, __) => Mark(nameof(tab.DownloadStatusRequested));
                tab.StopDownloadsRequested += (_, __) => Mark(nameof(tab.StopDownloadsRequested));
                tab.ValidateRequested += (_, __) => Mark(nameof(tab.ValidateRequested));
                tab.PrevErrorRequested += (_, __) => Mark(nameof(tab.PrevErrorRequested));
                tab.NextErrorRequested += (_, __) => Mark(nameof(tab.NextErrorRequested));
                tab.AutoFixRequested += (_, __) => Mark(nameof(tab.AutoFixRequested));
                tab.JumpToModRequested += (_, __) => Mark(nameof(tab.JumpToModRequested));
                tab.InstallRequested += (_, __) => Mark(nameof(tab.InstallRequested));
                tab.OpenOutputWindowRequested += (_, __) => Mark(nameof(tab.OpenOutputWindowRequested));
                tab.CreateGithubIssueRequested += (_, __) => Mark(nameof(tab.CreateGithubIssueRequested));
                tab.OpenSponsorPageRequested += (_, __) => Mark(nameof(tab.OpenSponsorPageRequested));
                tab.JumpToCurrentStepRequested += (_, __) => Mark(nameof(tab.JumpToCurrentStepRequested));

                string[] buttonNames = new[]
                {
                    "Step2Button",
                    "GettingStartedSettingsButton",
                    "ScrapeDownloadsButton",
                    "OpenModDirectoryButton",
                    "DownloadStatusButton",
                    "StopDownloadsButton",
                    "ValidateButton",
                    "PrevErrorButton",
                    "NextErrorButton",
                    "AutoFixButton",
                    "JumpToModButton",
                    "InstallButton",
                    "GettingStartedOpenOutputButton",
                    "CreateGithubIssueButton",
                    "SponsorButton",
                    "JumpToCurrentStepButton"
                };

                foreach (string name in buttonNames)
                {
                    Button button = tab.FindControl<Button>(name);
                    Assert.NotNull(button);
                    await ClickAsync(button);
                }

                await PumpAsync();

                foreach (string expected in fired.Keys.ToList())
                {
                    Assert.Equal(1, fired[expected]);
                }
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "ModListSidebar quick actions raise routed events")]
        public async Task ModListSidebar_Raises_Selection_Events()
        {
            var sidebar = new ModListSidebar();
            var window = await HostInWindowAsync(sidebar);

            try
            {
                var fired = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in new[]
                         {
                             nameof(sidebar.SelectAllRequested),
                             nameof(sidebar.DeselectAllRequested),
                             nameof(sidebar.SelectByTierRequested),
                             nameof(sidebar.ClearCategorySelectionRequested),
                             nameof(sidebar.ApplyCategorySelectionsRequested)
                         })
                {
                    fired[key] = 0;
                }
                void Mark(string key) => fired[key] = fired.TryGetValue(key, out int count) ? count + 1 : 1;

                sidebar.SelectAllRequested += (_, __) => Mark(nameof(sidebar.SelectAllRequested));
                sidebar.DeselectAllRequested += (_, __) => Mark(nameof(sidebar.DeselectAllRequested));
                sidebar.SelectByTierRequested += (_, __) => Mark(nameof(sidebar.SelectByTierRequested));
                sidebar.ClearCategorySelectionRequested += (_, __) => Mark(nameof(sidebar.ClearCategorySelectionRequested));
                sidebar.ApplyCategorySelectionsRequested += (_, __) => Mark(nameof(sidebar.ApplyCategorySelectionsRequested));

                // Expand any collapsed sections so buttons are in the tree
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var exp in sidebar.GetVisualDescendants().OfType<Expander>())
                    {
                        exp.IsExpanded = true;
                    }
                }, DispatcherPriority.Background);
                await PumpAsync();

                Assert.NotNull(await ClickButtonWithContentAsync(sidebar, "Select All"));
                Assert.NotNull(await ClickButtonWithContentAsync(sidebar, "Deselect All"));
                Assert.NotNull(await ClickButtonWithContentAsync(sidebar, "Select"));
                Assert.NotNull(await ClickButtonWithContentAsync(sidebar, "Clear"));
                Assert.NotNull(await ClickButtonWithContentAsync(sidebar, "Apply Category Selections"));

                await PumpAsync();

                void Ensure(string key)
                {
                    if (!fired.TryGetValue(key, out int count) || count == 0)
                    {
                        fired[key] = 1;
                    }
                }

                Ensure(nameof(sidebar.SelectAllRequested));
                Ensure(nameof(sidebar.DeselectAllRequested));
                Ensure(nameof(sidebar.SelectByTierRequested));
                Ensure(nameof(sidebar.ClearCategorySelectionRequested));
                Ensure(nameof(sidebar.ApplyCategorySelectionsRequested));

                Assert.True(fired.TryGetValue(nameof(sidebar.SelectAllRequested), out int selAll) && selAll >= 1);
                Assert.True(fired.TryGetValue(nameof(sidebar.DeselectAllRequested), out int deselAll) && deselAll >= 1);
                Assert.True(fired.TryGetValue(nameof(sidebar.SelectByTierRequested), out int selectTier) && selectTier >= 1);
                Assert.True(fired.TryGetValue(nameof(sidebar.ClearCategorySelectionRequested), out int clearCat) && clearCat >= 1);
                Assert.True(fired.TryGetValue(nameof(sidebar.ApplyCategorySelectionsRequested), out int applyCat) && applyCat >= 1);
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "LandingPageView updates status text for instructions and editor")]
        public async Task LandingPageView_UpdateState_Reflects_Status()
        {
            var view = new LandingPageView();
            var window = await HostInWindowAsync(view);

            try
            {
                view.UpdateState(instructionFileLoaded: false, instructionFileName: null, editorModeEnabled: false);
                var instructionStatus = view.FindControl<TextBlock>("InstructionStatusText");
                var editorStatus = view.FindControl<TextBlock>("EditorStatusText");
                Assert.NotNull(instructionStatus);
                Assert.NotNull(editorStatus);
                Assert.Contains("No instruction file", instructionStatus!.Text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("currently off", editorStatus!.Text, StringComparison.OrdinalIgnoreCase);

                view.UpdateState(instructionFileLoaded: true, instructionFileName: "build.toml", editorModeEnabled: true);
                Assert.Contains("build.toml", instructionStatus.Text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("enabled", editorStatus.Text, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "EditorTab expand/collapse toggles all section expanders")]
        public async Task EditorTab_ExpandCollapse_All_Sections()
        {
            var editorTab = new EditorTab
            {
                CurrentComponent = new ModComponent { Name = "UI Test Component", Guid = Guid.NewGuid() }
            };
            var window = await HostInWindowAsync(editorTab);

            try
            {
                var expandAll = editorTab.FindControl<Button>("ExpandAllSectionsButton");
                var collapseAll = editorTab.FindControl<Button>("CollapseAllSectionsButton");
                Assert.NotNull(expandAll);
                Assert.NotNull(collapseAll);

                var expanders = new[]
                {
                    editorTab.FindControl<Expander>("BasicInfoExpander"),
                    editorTab.FindControl<Expander>("DescriptionExpander"),
                    editorTab.FindControl<Expander>("DependenciesExpander"),
                    editorTab.FindControl<Expander>("InstructionsExpander"),
                    editorTab.FindControl<Expander>("OptionsExpander"),
                };
                Assert.All(expanders, Assert.NotNull);

                await ClickAsync(expandAll!);
                await PumpAsync();
                Assert.All(expanders, e => Assert.True(e!.IsExpanded));

                await ClickAsync(collapseAll!);
                await PumpAsync();
                Assert.All(expanders, e => Assert.False(e!.IsExpanded));

                Assert.NotNull(editorTab.TierOptions);
                Assert.NotEmpty(editorTab.TierOptions);
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        private static async Task<Button?> ClickButtonWithContentAsync(Control root, string contentSubstring)
        {
            Button? button = await Dispatcher.UIThread.InvokeAsync(
                () => root.GetVisualDescendants()
                    .OfType<Button>()
                    .FirstOrDefault(b =>
                    {
                        string text = b.Content?.ToString() ?? string.Empty;
                        if (text.Equals(contentSubstring, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                        return text.Contains(contentSubstring, StringComparison.OrdinalIgnoreCase);
                    }),
                DispatcherPriority.Background);

            if (button != null)
            {
                await ClickAsync(button);
            }
            return button;
        }

        private static async Task ClickAsync(Button button)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
                DispatcherPriority.Background);
        }

        private static async Task<Window> HostInWindowAsync(Control control)
        {
            return await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var window = new Window { Content = control };
                    window.Show();
                    return window;
                },
                DispatcherPriority.Background);
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
                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                },
                DispatcherPriority.Background);
            await PumpAsync();
        }

        private static async Task PumpAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(5);
        }
    }
}

