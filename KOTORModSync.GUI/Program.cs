// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

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
