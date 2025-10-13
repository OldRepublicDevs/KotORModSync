// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync
{
	public class App : Application
	{
		public override void Initialize() => AvaloniaXamlLoader.Load(this);

		public override void OnFrameworkInitializationCompleted()
		{
			if ( ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop )
			{
				try
				{

					TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

					desktop.MainWindow = new MainWindow();
					Logger.Log("Started main window");
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex);
				}
			}

			base.OnFrameworkInitializationCompleted();
		}

		private void HandleUnobservedTaskException([CanBeNull] object sender, UnobservedTaskExceptionEventArgs e)
		{

			Logger.LogException(e.Exception);
			e.SetObserved();
		}
	}
}
