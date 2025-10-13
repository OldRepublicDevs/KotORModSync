// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(KOTORModSync.Tests.TestAppBuilder))]

namespace KOTORModSync.Tests
{
	public class TestApp : Application
	{
		public override void Initialize() => Styles.Add(new FluentTheme());

	}

	public static class TestAppBuilder
	{
		public static AppBuilder BuildAvaloniaApp() =>
			AppBuilder.Configure<TestApp>()
				.UseHeadless(new AvaloniaHeadlessPlatformOptions());
	}
}

