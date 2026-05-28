// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;

using KOTORModSync.Core;
using KOTORModSync.Dialogs.WizardPages;
using KOTORModSync.Tests.TestHelpers;

using Xunit;

namespace KOTORModSync.Tests.HeadlessUITests
{
    [Collection(HeadlessTestApp.CollectionName)]
    public sealed class WizardFlowHeadlessTests
    {
        [AvaloniaFact(DisplayName = "Mod selection page select all chooses visible components")]
        public async Task ModSelectionPage_SelectAllButton_SelectsAllComponents()
        {
            List<ModComponent> components = new List<ModComponent>
            {
                new ModComponent { Guid = Guid.NewGuid(), Name = "Alpha", IsSelected = false, Category = new List<string> { "Test" }, Tier = "1 - Essential" },
                new ModComponent { Guid = Guid.NewGuid(), Name = "Beta", IsSelected = false, Category = new List<string> { "Test" }, Tier = "2 - Recommended" },
            };

            var page = new ModSelectionPage(components);
            Window window = await HostControlAsync(page);

            try
            {
                Button selectAllButton = page.FindControl<Button>("SelectAllButton");
                Assert.NotNull(selectAllButton);

                await Dispatcher.UIThread.InvokeAsync(
                    () => selectAllButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
                    DispatcherPriority.Background);
                await PumpEventsAsync();

                Assert.All(components, component => Assert.True(component.IsSelected));
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Validate page runs validation for selected components")]
        public async Task ValidatePage_RunValidation_ProducesSummary()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_ValidatePageTests", Guid.NewGuid().ToString("N"));
            DirectoryInfo destination = Directory.CreateDirectory(Path.Combine(tempRoot, "game"));
            DirectoryInfo source = Directory.CreateDirectory(Path.Combine(tempRoot, "mods"));
            Directory.CreateDirectory(Path.Combine(destination.FullName, "Override"));

            var config = new MainConfig
            {
                sourcePath = source,
                destinationPath = destination,
            };

            List<ModComponent> components = new List<ModComponent>
            {
                TestComponentFactory.CreateComponent("Validation Component", source),
            };

            var page = new ValidatePage(components, new MainConfig
            {
                sourcePath = source,
                destinationPath = destination,
                allComponents = components,
            });
            Window window = await HostControlAsync(page);

            try
            {
                Button validateButton = page.FindControl<Button>("ValidateButton");
                Assert.NotNull(validateButton);

                await Dispatcher.UIThread.InvokeAsync(
                    () => validateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
                    DispatcherPriority.Background);

                await WaitForAsync(async () =>
                {
                    TextBlock summary = page.FindControl<TextBlock>("SummaryText");
                    return summary != null && !string.IsNullOrWhiteSpace(summary.Text);
                }, TimeSpan.FromSeconds(15));

                TextBlock summaryText = page.FindControl<TextBlock>("SummaryText");
                Assert.NotNull(summaryText);
                Assert.False(string.IsNullOrWhiteSpace(summaryText.Text));

                Assert.Contains("validation", summaryText.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                await CloseWindowAsync(window);
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }

        private static async Task<Window> HostControlAsync(Control control)
        {
            Window window = await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var hostWindow = new Window
                    {
                        Content = control,
                        Width = 1200,
                        Height = 900,
                    };
                    hostWindow.Show();
                    return hostWindow;
                },
                DispatcherPriority.Background);

            await PumpEventsAsync();
            return window;
        }

        private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (await predicate().ConfigureAwait(false))
                {
                    return;
                }

                await PumpEventsAsync();
                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Condition was not met before timeout.");
        }

        private static async Task PumpEventsAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
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

            await PumpEventsAsync();
        }
    }
}
