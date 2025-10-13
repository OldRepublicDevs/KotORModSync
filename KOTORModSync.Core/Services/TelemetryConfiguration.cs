// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Configuration for telemetry service with privacy controls.
	/// </summary>
	public class TelemetryConfiguration
	{
		private static readonly string ConfigFilePath = Path.Combine(
			System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
			"KOTORModSync",
			"telemetry_config.json"
		);

		/// <summary>
		/// Master switch for all telemetry. If false, no data is collected or sent.
		/// Enabled by default - users can opt-out in Settings.
		/// </summary>
		[JsonPropertyName("enabled")]
		public bool IsEnabled { get; set; } = true;

		/// <summary>
		/// User has explicitly provided consent for telemetry.
		/// Not required - telemetry is opt-out, not opt-in.
		/// </summary>
		[JsonPropertyName("user_consented")]
		public bool UserConsented { get; set; } = true;

		/// <summary>
		/// Date and time when user provided consent.
		/// </summary>
		[JsonPropertyName("consent_date")]
		public DateTime? ConsentDate { get; set; }

		/// <summary>
		/// Anonymous user identifier (GUID) - never includes personal information.
		/// </summary>
		[JsonPropertyName("anonymous_user_id")]
		public string AnonymousUserId { get; set; }

		/// <summary>
		/// Unique session identifier for this application session.
		/// </summary>
		[JsonPropertyName("session_id")]
		public string SessionId { get; set; }

		/// <summary>
		/// Environment name (e.g., "production", "development").
		/// </summary>
		[JsonPropertyName("environment")]
		public string Environment { get; set; } = "production";

		/// <summary>
		/// Collect usage data (which features are used, button clicks, etc.).
		/// </summary>
		[JsonPropertyName("collect_usage_data")]
		public bool CollectUsageData { get; set; } = true;

		/// <summary>
		/// Collect performance metrics (render times, operation durations, etc.).
		/// </summary>
		[JsonPropertyName("collect_performance_metrics")]
		public bool CollectPerformanceMetrics { get; set; } = true;

		/// <summary>
		/// Collect crash and error reports.
		/// </summary>
		[JsonPropertyName("collect_crash_reports")]
		public bool CollectCrashReports { get; set; } = true;

		/// <summary>
		/// Include machine name in telemetry. False for better privacy.
		/// </summary>
		[JsonPropertyName("collect_machine_info")]
		public bool CollectMachineInfo { get; set; } = false;

		/// <summary>
		/// Enable console exporter for debugging (logs telemetry to console).
		/// </summary>
		[JsonPropertyName("enable_console_exporter")]
		public bool EnableConsoleExporter { get; set; } = false;

		/// <summary>
		/// Enable file exporter (saves telemetry to local files for debugging).
		/// </summary>
		[JsonPropertyName("enable_file_exporter")]
		public bool EnableFileExporter { get; set; } = true;

		/// <summary>
		/// Enable OTLP exporter (sends to OpenTelemetry Collector).
		/// </summary>
		[JsonPropertyName("enable_otlp_exporter")]
		public bool EnableOtlpExporter { get; set; } = false;

		/// <summary>
		/// Enable Prometheus exporter for metrics.
		/// </summary>
		[JsonPropertyName("enable_prometheus_exporter")]
		public bool EnablePrometheusExporter { get; set; } = false;

		/// <summary>
		/// OTLP endpoint URL (e.g., "http://localhost:4317" for local collector).
		/// </summary>
		[JsonPropertyName("otlp_endpoint")]
		public string OtlpEndpoint { get; set; } = "http://localhost:4317";

		/// <summary>
		/// API key or authentication token for remote telemetry endpoint (if needed).
		/// </summary>
		[JsonPropertyName("api_key")]
		public string ApiKey { get; set; }

		/// <summary>
		/// Indicates if this is the first run of the application.
		/// </summary>
		[JsonPropertyName("is_first_run")]
		public bool IsFirstRun { get; set; } = true;

		/// <summary>
		/// Last time telemetry data was sent successfully.
		/// </summary>
		[JsonPropertyName("last_send_time")]
		public DateTime? LastSendTime { get; set; }

		/// <summary>
		/// File path where telemetry logs are stored locally.
		/// </summary>
		[JsonPropertyName("local_log_path")]
		public string LocalLogPath { get; set; }

		/// <summary>
		/// Maximum size of local telemetry log file in MB before rotation.
		/// </summary>
		[JsonPropertyName("max_log_file_size_mb")]
		public int MaxLogFileSizeMB { get; set; } = 50;

		/// <summary>
		/// Number of days to retain local telemetry data.
		/// </summary>
		[JsonPropertyName("retention_days")]
		public int RetentionDays { get; set; } = 30;

		public TelemetryConfiguration()
		{
			// Generate anonymous user ID on first creation
			AnonymousUserId = Guid.NewGuid().ToString();
			SessionId = Guid.NewGuid().ToString();

			// Set default local log path
			LocalLogPath = Path.Combine(
				System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
				"KOTORModSync",
				"telemetry",
				"telemetry.log"
			);
		}

		/// <summary>
		/// Loads telemetry configuration from disk or creates default.
		/// </summary>
		public static TelemetryConfiguration Load()
		{
			try
			{
				if ( File.Exists(ConfigFilePath) )
				{
					string json = File.ReadAllText(ConfigFilePath);
					var config = JsonSerializer.Deserialize<TelemetryConfiguration>(json);

					// Generate new session ID for each application launch
					config.SessionId = Guid.NewGuid().ToString();
					config.IsFirstRun = false;

					return config;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "[Telemetry] Failed to load configuration");
			}

			// Return default configuration (telemetry enabled by default - opt-out model)
			return new TelemetryConfiguration
			{
				IsEnabled = true,
				UserConsented = true,
				IsFirstRun = true,
			};
		}

		/// <summary>
		/// Saves telemetry configuration to disk.
		/// </summary>
		public void Save()
		{
			try
			{
				string directory = Path.GetDirectoryName(ConfigFilePath);
				if ( !Directory.Exists(directory) )
				{
					Directory.CreateDirectory(directory);
				}

				var options = new JsonSerializerOptions
				{
					WriteIndented = true,
					DefaultIgnoreCondition = JsonIgnoreCondition.Never,
				};

				string json = JsonSerializer.Serialize(this, options);
				File.WriteAllText(ConfigFilePath, json);

				Logger.LogVerbose("[Telemetry] Configuration saved");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "[Telemetry] Failed to save configuration");
			}
		}

		/// <summary>
		/// Updates user preference and enables/disables telemetry accordingly.
		/// In opt-out model, user can disable telemetry at any time.
		/// </summary>
		public void SetUserConsent(bool enabled)
		{
			UserConsented = enabled;
			IsEnabled = enabled;
			Save();
		}

		/// <summary>
		/// Creates a privacy-friendly summary of configuration for display to user.
		/// </summary>
		public string GetPrivacySummary()
		{
			if ( !IsEnabled )
			{
				return "Telemetry is disabled. No data is being collected.";
			}

			var summary = "Telemetry is enabled (opt-out). The following data is being collected:\n\n";

			if ( CollectUsageData )
			{
				summary += "✓ Usage data (which features you use)\n";
			}

			if ( CollectPerformanceMetrics )
			{
				summary += "✓ Performance metrics (app speed and responsiveness)\n";
			}

			if ( CollectCrashReports )
			{
				summary += "✓ Crash and error reports\n";
			}

			summary += "\nThe following data is NOT collected:\n";
			summary += "✗ Personal information (name, email, etc.)\n";
			summary += "✗ File contents or mod names\n";
			summary += "✗ Passwords or authentication tokens\n";

			if ( !CollectMachineInfo )
			{
				summary += "✗ Machine name or hostname\n";
			}

			summary += $"\nAnonymous User ID: {AnonymousUserId}\n";
			summary += $"Session ID: {SessionId}\n";
			summary += "\nNote: You can disable telemetry at any time in Settings.\n";

			if ( EnableOtlpExporter && !string.IsNullOrEmpty(OtlpEndpoint) )
			{
				summary += $"\nData is sent to: {OtlpEndpoint}\n";
			}
			else if ( EnableFileExporter )
			{
				summary += $"\nData is stored locally at: {LocalLogPath}\n";
			}

			return summary;
		}

		/// <summary>
		/// Cleans up old telemetry data based on retention policy.
		/// </summary>
		public void CleanupOldData()
		{
			try
			{
				if ( string.IsNullOrEmpty(LocalLogPath) )
					return;

				string directory = Path.GetDirectoryName(LocalLogPath);
				if ( !Directory.Exists(directory) )
					return;

				var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);

				foreach ( string file in Directory.GetFiles(directory, "*.log") )
				{
					var fileInfo = new FileInfo(file);
					if ( fileInfo.LastWriteTimeUtc < cutoffDate )
					{
						fileInfo.Delete();
						Logger.LogVerbose($"[Telemetry] Deleted old telemetry file: {fileInfo.Name}");
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "[Telemetry] Failed to cleanup old telemetry data");
			}
		}
	}
}

