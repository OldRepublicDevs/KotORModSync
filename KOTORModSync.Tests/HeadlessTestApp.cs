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


