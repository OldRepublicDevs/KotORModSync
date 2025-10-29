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
using KOTORModSync.Services;

namespace KOTORModSync
{
	public class App : Application
	{
		private AutoUpdateService _autoUpdateService;

		public override void Initialize() => AvaloniaXamlLoader.Load(this);

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				try
				{
					// Load default theme BEFORE creating MainWindow to ensure control templates are available
					ThemeManager.UpdateStyle("/Styles/FluentLightStyle.axaml");

					TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;

					desktop.MainWindow = new MainWindow();
					Logger.Log("Started main window");

					// Initialize auto-update service
					InitializeAutoUpdates();
				}
				catch (Exception ex)
				{
					Logger.LogException(ex);
				}
			}

			base.OnFrameworkInitializationCompleted();
		}

		private void InitializeAutoUpdates()
		{
			try
			{
				_autoUpdateService = new AutoUpdateService();
				_autoUpdateService.Initialize();
				_autoUpdateService.StartUpdateCheckLoop();
				Logger.Log("Auto-update service started successfully.");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Failed to initialize auto-update service");
			}
		}

		private void HandleUnobservedTaskException([CanBeNull] object sender, UnobservedTaskExceptionEventArgs e)
		{

			Logger.LogException(e.Exception);
			e.SetObserved();
		}
	}
}