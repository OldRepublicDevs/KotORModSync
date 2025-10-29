// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
	public partial class TelemetryConsentDialog : Window
	{
		public TelemetryConfiguration Configuration { get; private set; }
		public bool UserAccepted { get; private set; }

		public TelemetryConsentDialog()
		{
			InitializeComponent();
			Configuration = TelemetryConfiguration.Load();
		}

		private void EnableButton_Click( object sender, RoutedEventArgs e )
		{
			try
			{

				Configuration.SetUserConsent( true );
				Configuration.CollectUsageData = CollectUsageCheckBox?.IsChecked ?? true;
				Configuration.CollectPerformanceMetrics = CollectPerformanceCheckBox?.IsChecked ?? true;
				Configuration.CollectCrashReports = CollectCrashReportsCheckBox?.IsChecked ?? true;
				Configuration.CollectMachineInfo = CollectMachineInfoCheckBox?.IsChecked ?? false;

				bool localOnly = LocalOnlyRadio?.IsChecked ?? true;
				Configuration.EnableFileExporter = localOnly;
				Configuration.EnableOtlpExporter = !localOnly;

				if (!localOnly)
				{

					Configuration.OtlpEndpoint = "https://telemetry.kotormodsync.com/v1/traces";
				}

				Configuration.Save();

				UserAccepted = true;
				Logger.Log( "[Telemetry] User consented to telemetry collection" );
				Close( true );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "[Telemetry] Error saving telemetry consent" );
				Close( false );
			}
		}

		private void DeclineButton_Click( object sender, RoutedEventArgs e )
		{
			try
			{
				Configuration.SetUserConsent( false );
				Configuration.Save();

				UserAccepted = false;
				Logger.Log( "[Telemetry] User declined telemetry collection" );
				Close( false );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "[Telemetry] Error saving telemetry decline" );
				Close( false );
			}
		}

		public static bool? ShowConsentDialog( Window parent )
		{
			bool? result = null;
			Dispatcher.UIThread.InvokeAsync( () =>
			{
				var dialog = new TelemetryConsentDialog();
				dialog.ShowDialog( parent );
				result = dialog.UserAccepted;
			} ).Wait();
			return result;
		}
	}
}