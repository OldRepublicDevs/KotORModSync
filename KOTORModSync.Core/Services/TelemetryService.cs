// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Provides telemetry services using OpenTelemetry for traces, metrics, and structured logging.
	/// Singleton instance that can be accessed throughout the application.
	/// </summary>
	public sealed class TelemetryService : IDisposable
	{
		private static readonly Lazy<TelemetryService> _instance = new Lazy<TelemetryService>(() => new TelemetryService());
		
		private TelemetryConfiguration _config;
		private TracerProvider _tracerProvider;
		private MeterProvider _meterProvider;
		private ActivitySource _activitySource;
		private Meter _meter;
		
		// Metrics
		private Counter<long> _eventCounter;
		private Counter<long> _errorCounter;
		private Histogram<double> _operationDuration;
		private Counter<long> _modInstallCounter;
		private Counter<long> _modValidationCounter;
		private Counter<long> _downloadCounter;
		private Histogram<long> _downloadSize;
		
		private bool _isInitialized;
		private bool _disposed;
		
		/// <summary>
		/// Gets the singleton instance of the TelemetryService.
		/// </summary>
		public static TelemetryService Instance => _instance.Value;
		
		/// <summary>
		/// Gets whether telemetry is currently enabled and operational.
		/// </summary>
		public bool IsEnabled => _config?.IsEnabled ?? false;
		
		private TelemetryService()
		{
			_config = TelemetryConfiguration.Load();
		}
		
		/// <summary>
		/// Initializes the telemetry service with OpenTelemetry providers.
		/// Should be called once at application startup.
		/// </summary>
		public void Initialize()
		{
			if (_isInitialized || !_config.IsEnabled)
				return;
				
			try
			{
				// Create resource with service information
				var resourceBuilder = ResourceBuilder.CreateDefault()
					.AddService(
						serviceName: "KOTORModSync",
						serviceVersion: typeof(TelemetryService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
						serviceInstanceId: _config.SessionId)
					.AddAttributes(new Dictionary<string, object>
					{
						["user.id"] = _config.AnonymousUserId,
						["session.id"] = _config.SessionId,
						["environment"] = _config.Environment,
						["platform"] = System.Environment.OSVersion.Platform.ToString()
					});
				
				// Initialize ActivitySource for tracing
				_activitySource = new ActivitySource("KOTORModSync", "1.0.0");
				
				// Initialize Meter for metrics
				_meter = new Meter("KOTORModSync", "1.0.0");
				
				// Create metrics
				_eventCounter = _meter.CreateCounter<long>("kotormodsync.events", "events", "Number of events recorded");
				_errorCounter = _meter.CreateCounter<long>("kotormodsync.errors", "errors", "Number of errors recorded");
				_operationDuration = _meter.CreateHistogram<double>("kotormodsync.operation.duration", "ms", "Duration of operations");
				_modInstallCounter = _meter.CreateCounter<long>("kotormodsync.mods.installed", "mods", "Number of mods installed");
				_modValidationCounter = _meter.CreateCounter<long>("kotormodsync.mods.validated", "mods", "Number of mods validated");
				_downloadCounter = _meter.CreateCounter<long>("kotormodsync.downloads", "downloads", "Number of downloads");
				_downloadSize = _meter.CreateHistogram<long>("kotormodsync.download.size", "bytes", "Size of downloads");
				
				// Configure TracerProvider
				var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
					.SetResourceBuilder(resourceBuilder)
					.AddSource("KOTORModSync");
				
				// Add exporters based on configuration
				if (_config.EnableConsoleExporter)
				{
					tracerProviderBuilder.AddConsoleExporter();
				}
				
				if (_config.EnableFileExporter && !string.IsNullOrEmpty(_config.LocalLogPath))
				{
					// File exporter would need custom implementation
					Logger.LogVerbose("[Telemetry] File exporter requested but not yet implemented");
				}
				
				if (_config.EnableOtlpExporter && !string.IsNullOrEmpty(_config.OtlpEndpoint))
				{
					tracerProviderBuilder.AddOtlpExporter(options =>
					{
						options.Endpoint = new Uri(_config.OtlpEndpoint);
					});
				}
				
				_tracerProvider = tracerProviderBuilder.Build();
				
				// Configure MeterProvider
				var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
					.SetResourceBuilder(resourceBuilder)
					.AddMeter("KOTORModSync");
				
				// Add exporters based on configuration
				if (_config.EnableConsoleExporter)
				{
					meterProviderBuilder.AddConsoleExporter();
				}
				
				if (_config.EnableOtlpExporter && !string.IsNullOrEmpty(_config.OtlpEndpoint))
				{
					meterProviderBuilder.AddOtlpExporter(options =>
					{
						options.Endpoint = new Uri(_config.OtlpEndpoint);
					});
				}
				
				_meterProvider = meterProviderBuilder.Build();
				
				_isInitialized = true;
				Logger.Log("[Telemetry] Telemetry service initialized successfully");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to initialize telemetry service");
			}
		}
		
		/// <summary>
		/// Updates the telemetry configuration and reinitializes if necessary.
		/// </summary>
		public void UpdateConfiguration(TelemetryConfiguration newConfig)
		{
			if (newConfig == null)
				return;
				
			bool wasEnabled = _config?.IsEnabled ?? false;
			bool isNowEnabled = newConfig.IsEnabled;
			
			_config = newConfig;
			
			// If telemetry was disabled and is now enabled, initialize
			if (!wasEnabled && isNowEnabled && !_isInitialized)
			{
				Initialize();
			}
			// If telemetry was enabled and is now disabled, dispose providers
			else if (wasEnabled && !isNowEnabled && _isInitialized)
			{
				Dispose();
			}
		}
		
		/// <summary>
		/// Records a generic telemetry event with optional tags.
		/// </summary>
		public void RecordEvent(string eventName, Dictionary<string, object> tags = null)
		{
			if (!IsEnabled || !_config.CollectUsageData)
				return;
				
			try
			{
				_eventCounter?.Add(1, CreateTagList(eventName, tags));
				Logger.LogVerbose($"[Telemetry] Event: {eventName}");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, $"[Telemetry] Failed to record event: {eventName}");
			}
		}
		
		/// <summary>
		/// Starts a new activity (span) for tracing operations.
		/// </summary>
		public Activity StartActivity(string activityName, Dictionary<string, object> tags = null)
		{
			if (!IsEnabled || !_config.CollectPerformanceMetrics)
				return null;
				
			try
			{
				var activity = _activitySource?.StartActivity(activityName);
				if (activity != null && tags != null)
				{
					foreach (var tag in tags)
					{
						activity.SetTag(tag.Key, tag.Value);
					}
				}
				return activity;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, $"[Telemetry] Failed to start activity: {activityName}");
				return null;
			}
		}
		
		/// <summary>
		/// Records a mod installation event.
		/// </summary>
		public void RecordModInstallation(string modName, bool success, double durationMs, string errorMessage = null)
		{
			if (!IsEnabled || !_config.CollectUsageData)
				return;
				
			try
			{
				var tags = new Dictionary<string, object>
				{
					["mod.name.hash"] = HashString(modName), // Hash for privacy
					["success"] = success
				};
				
				if (!success && !string.IsNullOrEmpty(errorMessage))
				{
					tags["error.type"] = HashString(errorMessage);
				}
				
				_modInstallCounter?.Add(1, CreateTagList("mod.install", tags));
				
				if (_config.CollectPerformanceMetrics)
				{
					_operationDuration?.Record(durationMs, CreateTagList("mod.install", tags));
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to record mod installation");
			}
		}
		
		/// <summary>
		/// Records a mod validation event.
		/// </summary>
		public void RecordModValidation(int componentCount, bool success, double durationMs)
		{
			if (!IsEnabled || !_config.CollectUsageData)
				return;
				
			try
			{
				var tags = new Dictionary<string, object>
				{
					["component.count"] = componentCount,
					["success"] = success
				};
				
				_modValidationCounter?.Add(1, CreateTagList("mod.validation", tags));
				
				if (_config.CollectPerformanceMetrics)
				{
					_operationDuration?.Record(durationMs, CreateTagList("mod.validation", tags));
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to record mod validation");
			}
		}
		
		/// <summary>
		/// Records a download event.
		/// </summary>
		public void RecordDownload(string url, bool success, long sizeBytes, double durationMs)
		{
			if (!IsEnabled || !_config.CollectUsageData)
				return;
				
			try
			{
				var tags = new Dictionary<string, object>
				{
					["url.host"] = new Uri(url).Host, // Only record host, not full URL
					["success"] = success
				};
				
				_downloadCounter?.Add(1, CreateTagList("download", tags));
				
				if (success)
				{
					_downloadSize?.Record(sizeBytes, CreateTagList("download", tags));
				}
				
				if (_config.CollectPerformanceMetrics)
				{
					_operationDuration?.Record(durationMs, CreateTagList("download", tags));
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to record download");
			}
		}
		
		/// <summary>
		/// Records a UI interaction event.
		/// </summary>
		public void RecordUIInteraction(string elementName, string action)
		{
			if (!IsEnabled || !_config.CollectUsageData)
				return;
				
			try
			{
				var tags = new Dictionary<string, object>
				{
					["element"] = elementName,
					["action"] = action
				};
				
				_eventCounter?.Add(1, CreateTagList("ui.interaction", tags));
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to record UI interaction");
			}
		}
		
		/// <summary>
		/// Records an error event.
		/// </summary>
		public void RecordError(string errorType, string errorMessage, string stackTrace = null)
		{
			if (!IsEnabled || !_config.CollectCrashReports)
				return;
				
			try
			{
				var tags = new Dictionary<string, object>
				{
					["error.type"] = errorType,
					["error.message.hash"] = HashString(errorMessage) // Hash for privacy
				};
				
				if (!string.IsNullOrEmpty(stackTrace))
				{
					tags["error.stacktrace.hash"] = HashString(stackTrace);
				}
				
				_errorCounter?.Add(1, CreateTagList("error", tags));
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to record error");
			}
		}
		
		/// <summary>
		/// Flushes all telemetry data to configured exporters.
		/// Should be called before application exit.
		/// </summary>
		public void Flush()
		{
			try
			{
				_tracerProvider?.ForceFlush();
				_meterProvider?.ForceFlush();
				Logger.LogVerbose("[Telemetry] Telemetry data flushed");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to flush telemetry data");
			}
		}
		
		private KeyValuePair<string, object>[] CreateTagList(string eventName, Dictionary<string, object> tags)
		{
			var tagList = new List<KeyValuePair<string, object>>
			{
				new KeyValuePair<string, object>("event.name", eventName)
			};
			
			if (tags != null)
			{
				foreach (var tag in tags)
				{
					tagList.Add(new KeyValuePair<string, object>(tag.Key, tag.Value));
				}
			}
			
			return tagList.ToArray();
		}
		
		private string HashString(string input)
		{
			if (string.IsNullOrEmpty(input))
				return "empty";
				
			// Simple hash for privacy - not cryptographically secure
			unchecked
			{
				int hash = 17;
				foreach (char c in input)
				{
					hash = hash * 31 + c;
				}
				return Math.Abs(hash).ToString("X8");
			}
		}
		
		public void Dispose()
		{
			if (_disposed)
				return;
				
			try
			{
				Flush();
				_tracerProvider?.Dispose();
				_meterProvider?.Dispose();
				_activitySource?.Dispose();
				_meter?.Dispose();
				_isInitialized = false;
				_disposed = true;
				Logger.LogVerbose("[Telemetry] Telemetry service disposed");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "[Telemetry] Failed to dispose telemetry service");
			}
		}
	}
}

