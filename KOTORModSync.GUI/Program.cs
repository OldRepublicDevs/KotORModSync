


using System;
using Avalonia;
using Avalonia.ReactiveUI;

namespace KOTORModSync
{
	internal static class Program
	{
		
		
		
		[STAThread]
		public static void Main(string[] args) =>
			
			

			_ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

		
		public static AppBuilder BuildAvaloniaApp() =>
			AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().UseReactiveUI();
	}
}
