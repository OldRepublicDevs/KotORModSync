// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KOTORModSync.Core.Services
{

	public class TelemetryConfiguration
	{
		private static readonly string ConfigFilePath = Path.Combine(
			System.Environment.GetFolderPath( System.Environment.SpecialFolder.ApplicationData ),
			"KOTORModSync",
			"telemetry_config.json"
		);

		private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
		};

		[JsonPropertyName( "enabled" )]
		public bool IsEnabled { get; set; } = true;

		[JsonPropertyName( "user_consented" )]
		public bool UserConsented { get; set; } = true;

		[JsonPropertyName( "consent_date" )]
		public DateTime? ConsentDate { get; set; }

		[JsonPropertyName( "anonymous_user_id" )]
		public string AnonymousUserId { get; set; }

		[JsonPropertyName( "session_id" )]
		public string SessionId { get; set; }

		[JsonPropertyName( "environment" )]
		public string Environment { get; set; } = "production";

		[JsonPropertyName( "collect_usage_data" )]
		public bool CollectUsageData { get; set; } = true;

		[JsonPropertyName( "collect_performance_metrics" )]
		public bool CollectPerformanceMetrics { get; set; } = true;

		[JsonPropertyName( "collect_crash_reports" )]
		public bool CollectCrashReports { get; set; } = true;

		[JsonPropertyName( "collect_machine_info" )]
		public bool CollectMachineInfo { get; set; } = false;

		[JsonPropertyName( "enable_console_exporter" )]
		public bool EnableConsoleExporter { get; set; } = false;

		[JsonPropertyName( "enable_file_exporter" )]
		public bool EnableFileExporter { get; set; } = false;

		[JsonPropertyName( "enable_otlp_exporter" )]
		public bool EnableOtlpExporter { get; set; } = true;

		[JsonPropertyName( "enable_prometheus_exporter" )]
		public bool EnablePrometheusExporter { get; set; } = false;

		[JsonPropertyName( "prometheus_pushgateway_endpoint" )]
		public string PrometheusPushgatewayEndpoint { get; set; } = "https://prometheus.bolabaden.org/metrics";

		[JsonPropertyName( "otlp_endpoint" )]
		public string OtlpEndpoint { get; set; } = "https://otlp.bolabaden.org";

		[JsonPropertyName( "prometheus_port" )]
		public int PrometheusPort { get; set; } = 9464;

		[JsonPropertyName( "api_key" )]
		public string ApiKey { get; set; }

		[JsonPropertyName( "is_first_run" )]
		public bool IsFirstRun { get; set; } = true;

		[JsonPropertyName( "last_send_time" )]
		public DateTime? LastSendTime { get; set; }

		[JsonPropertyName( "local_log_path" )]
		public string LocalLogPath { get; set; }

		[JsonPropertyName( "max_log_file_size_mb" )]
		public int MaxLogFileSizeMB { get; set; } = 50;

		[JsonPropertyName( "retention_days" )]
		public int RetentionDays { get; set; } = 30;

		[JsonIgnore]
		public string SigningSecret { get; private set; }

		public TelemetryConfiguration()
		{

			AnonymousUserId = Guid.NewGuid().ToString();
			SessionId = Guid.NewGuid().ToString();

			LocalLogPath = Path.Combine(
				System.Environment.GetFolderPath( System.Environment.SpecialFolder.ApplicationData ),
				"KOTORModSync",
				"telemetry",
				"telemetry.log"
			);
		}

		public static TelemetryConfiguration Load()
		{
			try
			{
				if (File.Exists( ConfigFilePath ))
				{
					string json = File.ReadAllText( ConfigFilePath );
					TelemetryConfiguration config = JsonSerializer.Deserialize<TelemetryConfiguration>( json );

					config.SessionId = Guid.NewGuid().ToString();
					config.IsFirstRun = false;
					config.SigningSecret = LoadSigningSecret();

					return config;
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "[Telemetry] Failed to load configuration" );
			}

			TelemetryConfiguration newConfig = new TelemetryConfiguration
			{
				IsEnabled = true,
				UserConsented = true,
				IsFirstRun = true,
				EnableOtlpExporter = true,
				EnablePrometheusExporter = false,
				EnableFileExporter = false,
			};
			newConfig.SigningSecret = LoadSigningSecret();
			return newConfig;
		}







		private static string LoadSigningSecret()
		{
			string secret = System.Environment.GetEnvironmentVariable( "KOTORMODSYNC_SIGNING_SECRET" );
			if (!string.IsNullOrEmpty( secret ))
			{
				Logger.LogVerbose( "[Telemetry] Signing secret loaded from environment variable" );
				return secret.Trim();
			}

			string configPath = Path.Combine(
				System.Environment.GetFolderPath( System.Environment.SpecialFolder.ApplicationData ),
				"KOTORModSync",
				"telemetry.key"
			);

			if (File.Exists( configPath ))
			{
				try
				{
					secret = File.ReadAllText( configPath ).Trim();
					if (!string.IsNullOrEmpty( secret ))
					{
						Logger.LogVerbose( "[Telemetry] Signing secret loaded from config file" );
						return secret;
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning( $"[Telemetry] Could not read signing secret from {configPath}: {ex.Message}" );
				}
			}

#if OFFICIAL_BUILD
		if ( !string.IsNullOrEmpty(EmbeddedSecrets.TELEMETRY_SIGNING_KEY) )
		{
			Logger.LogVerbose("[Telemetry] Signing secret loaded from embedded source");
			return EmbeddedSecrets.TELEMETRY_SIGNING_KEY.Trim();
		}
#endif

			Logger.LogWarning( "[Telemetry] No signing secret found - telemetry will be disabled" );
			return null;
		}

		public void Save()
		{
			try
			{
				string directory = Path.GetDirectoryName( ConfigFilePath );
				if (!Directory.Exists( directory ))
				{
					Directory.CreateDirectory( directory );
				}

				string json = JsonSerializer.Serialize( this, SerializerOptions );
				File.WriteAllText( ConfigFilePath, json );

				Logger.LogVerbose( "[Telemetry] Configuration saved" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "[Telemetry] Failed to save configuration" );
			}
		}

		public void SetUserConsent( bool enabled )
		{
			UserConsented = enabled;
			IsEnabled = enabled;
			Save();
		}

		public string GetPrivacySummary()
		{
			if (!IsEnabled)
			{
				return "Telemetry is disabled. No data is being collected.";
			}

			string summary = "Telemetry is enabled (opt-out). The following data is being collected:\n\n";

			if (CollectUsageData)
			{
				summary += "✓ Usage data (which features you use)\n";
			}

			if (CollectPerformanceMetrics)
			{
				summary += "✓ Performance metrics (app speed and responsiveness)\n";
			}

			if (CollectCrashReports)
			{
				summary += "✓ Crash and error reports\n";
			}

			summary += "\nThe following data is NOT collected:\n";
			summary += "✗ Personal information (name, email, etc.)\n";
			summary += "✗ File contents or mod names\n";
			summary += "✗ Passwords or authentication tokens\n";

			if (!CollectMachineInfo)
			{
				summary += "✗ Machine name or hostname\n";
			}

			summary += $"\nAnonymous User ID: {AnonymousUserId}\n";
			summary += $"Session ID: {SessionId}\n";
			summary += "\nNote: You can disable telemetry at any time in Settings.\n";

			if (EnableOtlpExporter && !string.IsNullOrEmpty( OtlpEndpoint ))
			{
				summary += $"\nData is sent via OTLP to: {OtlpEndpoint}\n";
			}

			if (EnablePrometheusExporter)
			{
				summary += $"Prometheus metrics exposed on port: {PrometheusPort}\n";
			}

			if (EnableFileExporter)
			{
				summary += $"Data is stored locally at: {LocalLogPath}\n";
			}

			return summary;
		}

		public void CleanupOldData()
		{
			try
			{
				if (string.IsNullOrEmpty( LocalLogPath ))
					return;

				string directory = Path.GetDirectoryName( LocalLogPath );
				if (!Directory.Exists( directory ))
					return;

				DateTime cutoffDate = DateTime.UtcNow.AddDays( -RetentionDays );

				foreach (string file in Directory.GetFiles( directory, "*.log" ))
				{
					FileInfo fileInfo = new FileInfo( file );
					if (fileInfo.LastWriteTimeUtc < cutoffDate)
					{
						fileInfo.Delete();
						Logger.LogVerbose( $"[Telemetry] Deleted old telemetry file: {fileInfo.Name}" );
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "[Telemetry] Failed to cleanup old telemetry data" );
			}
		}
	}
}