// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KOTORModSync.Core;
using KOTORModSync.Dialogs.WizardPages;
using KOTORModSync.Tests.TestHelpers;
using Xunit;

namespace KOTORModSync.Tests.HeadlessUITests
{
    [Collection(HeadlessTestApp.CollectionName)]
    public sealed class InstallingPageHeadlessTests
    {
        [AvaloniaFact(DisplayName = "Installing page completes shared pipeline install")]
        public async Task InstallingPage_CompletesSharedPipelineInstall()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_InstallingPageTests", Guid.NewGuid().ToString("N"));
            DirectoryInfo workingDirectory = Directory.CreateDirectory(tempRoot);
            try
            {
                ModComponent component = TestComponentFactory.CreateComponent("InstallingPageComponent", workingDirectory);
                var components = new List<ModComponent> { component };
                var mainConfig = new MainConfig
                {
                    destinationPath = workingDirectory,
                    sourcePath = workingDirectory,
                    allComponents = components,
                };

                InstallingPage page = await Dispatcher.UIThread.InvokeAsync(
                    () => new InstallingPage(components, mainConfig, new CancellationTokenSource()),
                    DispatcherPriority.Background);

                Window window = await HostInWindowAsync(page);
                try
                {
                    await page.OnNavigatedToAsync(CancellationToken.None);

                    await WaitForAsync(async () =>
                    {
                        (bool isValid, string _) = await page.ValidateAsync(CancellationToken.None);
                        return isValid;
                    }, timeout: TimeSpan.FromSeconds(15));

                    TextBlock currentModText = page.FindControl<TextBlock>("CurrentModText");
                    TextBlock countText = page.FindControl<TextBlock>("CountText");

                    Assert.NotNull(currentModText);
                    Assert.NotNull(countText);
                    Assert.Contains("complete", currentModText.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("1/1", countText.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);

                    string extractedDirectory = Path.Combine(workingDirectory.FullName, "extracted", "InstallingPageComponent");
                    Assert.True(Directory.Exists(extractedDirectory), "Installing page should execute the shared installation pipeline.");
                }
                finally
                {
                    await CloseWindowAsync(window);
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
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
        }

        private static async Task<Window> HostInWindowAsync(Control control)
        {
            Window window = await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var host = new Window { Content = control };
                    host.Show();
                    return host;
                },
                DispatcherPriority.Background);

            await PumpEventsAsync();
            return window;
        }

        private static async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                {
                    return;
                }

                await PumpEventsAsync();
                await Task.Delay(50);
            }

            throw new TimeoutException("Condition was not satisfied before timeout.");
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
