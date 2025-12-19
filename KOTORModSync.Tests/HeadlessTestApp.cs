// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.ReactiveUI;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(KOTORModSync.Tests.HeadlessTestApp))]

namespace KOTORModSync.Tests
{

    /// <summary>
    /// Centralized Avalonia headless bootstrap used by all GUI tests.
    /// Ensures a single, deterministic application instance with the same
    /// setup as the real app (ReactiveUI + theme pipeline) while keeping
    /// rendering fully headless for CI performance.
    /// </summary>
    public static class HeadlessTestApp
    {
        public const string CollectionName = "AvaloniaHeadlessCollection";

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UseReactiveUI()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = true,
                });
    }

    [CollectionDefinition(HeadlessTestApp.CollectionName, DisableParallelization = true)]
    public class AvaloniaHeadlessCollection : ICollectionFixture<object>
    {
    }
}
