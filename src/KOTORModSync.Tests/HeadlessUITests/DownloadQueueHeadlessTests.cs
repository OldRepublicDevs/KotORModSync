// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.HeadlessUITests
{
    [Collection(HeadlessTestApp.CollectionName)]
    public sealed class DownloadQueueHeadlessTests
    {
        [AvaloniaFact(DisplayName = "Download window keeps separate rows for shared URLs across components")]
        public async Task DownloadWindow_SharedUrlAcrossComponents_KeepsDistinctRows()
        {
            DownloadProgressWindow window = await CreateWindowAsync();

            try
            {
                var firstComponentId = Guid.NewGuid();
                var secondComponentId = Guid.NewGuid();
                const string sharedUrl = "https://example.com/shared.zip";

                window.AddDownload(new DownloadProgress
                {
                    ModName = "Shared Url Mod A",
                    Url = sharedUrl,
                    ComponentGuid = firstComponentId,
                    Status = DownloadStatus.Pending,
                    StatusMessage = "Waiting to start..."
                });

                window.AddDownload(new DownloadProgress
                {
                    ModName = "Shared Url Mod B",
                    Url = sharedUrl,
                    ComponentGuid = secondComponentId,
                    Status = DownloadStatus.Pending,
                    StatusMessage = "Waiting to start..."
                });

                await PumpEventsAsync();

                Assert.Equal(2, GetItemsCount(window, "PendingDownloadsControl"));

                window.UpdateDownloadProgress(new DownloadProgress
                {
                    ModName = "Shared Url Mod A",
                    Url = sharedUrl,
                    ComponentGuid = firstComponentId,
                    Status = DownloadStatus.InProgress,
                    StatusMessage = "Downloading..."
                });

                await PumpEventsAsync();

                Assert.Equal(1, GetItemsCount(window, "ActiveDownloadsControl"));
                Assert.Equal(1, GetItemsCount(window, "PendingDownloadsControl"));
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Download window updates existing row for same component and URL")]
        public async Task DownloadWindow_SameComponentAndUrl_UpdatesExistingRow()
        {
            DownloadProgressWindow window = await CreateWindowAsync();

            try
            {
                var componentId = Guid.NewGuid();
                const string url = "https://example.com/mod.zip";

                window.AddDownload(new DownloadProgress
                {
                    ModName = "Retryable Mod",
                    Url = url,
                    ComponentGuid = componentId,
                    Status = DownloadStatus.Pending,
                    StatusMessage = "Waiting to start..."
                });

                await PumpEventsAsync();

                window.UpdateDownloadProgress(new DownloadProgress
                {
                    ModName = "Retryable Mod",
                    Url = url,
                    ComponentGuid = componentId,
                    Status = DownloadStatus.InProgress,
                    StatusMessage = "Downloading..."
                });

                await PumpEventsAsync();

                Assert.Equal(1, GetItemsCount(window, "ActiveDownloadsControl"));
                Assert.Equal(0, GetItemsCount(window, "PendingDownloadsControl"));
                Assert.Equal(1, GetTotalTrackedItems(window));
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        [AvaloniaFact(DisplayName = "Download window can reset a completed session for a new fetch")]
        public async Task DownloadWindow_ResetForNewSession_ClearsCompletedStateAndItems()
        {
            DownloadProgressWindow window = await CreateWindowAsync();

            try
            {
                window.AddDownload(new DownloadProgress
                {
                    ModName = "Completed Mod",
                    Url = "https://example.com/completed.zip",
                    ComponentGuid = Guid.NewGuid(),
                    Status = DownloadStatus.Completed,
                    StatusMessage = "Already cached"
                });

                window.MarkCompleted();
                await PumpEventsAsync();

                Assert.True(window.IsSessionCompleted);
                Assert.False(window.HasPendingOrActiveDownloads);
                Assert.Equal(1, GetItemsCount(window, "CompletedDownloadsControl"));

                window.ResetForNewSession();
                await PumpEventsAsync();

                Assert.False(window.IsSessionCompleted);
                Assert.False(window.HasPendingOrActiveDownloads);
                Assert.Equal(0, GetItemsCount(window, "ActiveDownloadsControl"));
                Assert.Equal(0, GetItemsCount(window, "PendingDownloadsControl"));
                Assert.Equal(0, GetItemsCount(window, "CompletedDownloadsControl"));
                Assert.Equal(0, GetTotalTrackedItems(window));
            }
            finally
            {
                await CloseWindowAsync(window);
            }
        }

        private static async Task<DownloadProgressWindow> CreateWindowAsync()
        {
            DownloadProgressWindow window = await Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    var progressWindow = new DownloadProgressWindow();
                    progressWindow.Show();
                    return progressWindow;
                },
                DispatcherPriority.Background);

            await PumpEventsAsync();
            return window;
        }

        private static int GetItemsCount(Control root, string controlName)
        {
            ItemsControl itemsControl = root.FindControl<ItemsControl>(controlName);
            Assert.NotNull(itemsControl);

            IEnumerable items = itemsControl.ItemsSource as IEnumerable;
            Assert.NotNull(items);

            return items.Cast<object>().Count();
        }

        private static int GetTotalTrackedItems(DownloadProgressWindow window)
        {
            return GetItemsCount(window, "ActiveDownloadsControl")
                + GetItemsCount(window, "PendingDownloadsControl")
                + GetItemsCount(window, "CompletedDownloadsControl");
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
