using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.ReactiveUI;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Utility;

namespace KOTORModSync
{
	internal static class Program
	{
		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.
		[STAThread]
		public static void Main(string[] args) =>
			//var consoleThread = new Thread(ConsoleLoop);
			//consoleThread.Start();

			_ = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

		// Avalonia configuration, don't remove; also used by visual designer.
		public static AppBuilder BuildAvaloniaApp() =>
			AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().UseReactiveUI();
	}
}
