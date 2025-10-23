// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;
using Newtonsoft.Json;

namespace KOTORModSync.Core.CLI
{
	public class ErrorCollector
	{
		private readonly List<ErrorInfo> _errors = new List<ErrorInfo>();
		private readonly object _errorLock = new object();

		public enum ErrorCategory
		{
			Download,
			FileOperation,
			Extraction,
			Validation,
			Installation,
			TslPatcher,
			General
		}

		public class ErrorInfo
		{
			public ErrorCategory Category { get; set; }
			public string ComponentName { get; set; }
			public string Message { get; set; }
			public string Details { get; set; }
			public Exception Exception { get; set; }
			public DateTime Timestamp { get; set; }
			public List<string> LogContext { get; set; }
		}

		public void RecordError(
			ErrorCategory category,
			string componentName,
			string message,
			string details = null,
			Exception exception = null)
		{
			lock ( _errorLock )
			{
				// Capture recent log messages for context (last 30 messages)
				List<string> logContext = Logger.GetRecentLogMessages(30);

				_errors.Add(new ErrorInfo
				{
					Category = category,
					ComponentName = componentName,
					Message = message,
					Details = details,
					Exception = exception,
					Timestamp = DateTime.Now,
					LogContext = logContext
				});
			}
		}

		public List<ErrorInfo> GetErrors()
		{
			lock ( _errorLock )
			{
				return _errors.ToList();
			}
		}

		public int GetErrorCount()
		{
			lock ( _errorLock )
			{
				return _errors.Count;
			}
		}

		public void Clear()
		{
			lock ( _errorLock )
			{
				_errors.Clear();
			}
		}

		public Dictionary<ErrorCategory, List<ErrorInfo>> GetErrorsByCategory()
		{
			lock ( _errorLock )
			{
				return _errors.GroupBy(e => e.Category)
					.ToDictionary(g => g.Key, g => g.ToList());
			}
		}
	}

	public static class ModBuildConverter
	{
		private static MainConfig _config;
		private static ConsoleProgressDisplay _progressDisplay;
		private static DownloadCacheService _globalDownloadCache;
		private static ErrorCollector _errorCollector;

		private static void EnsureConfigInitialized()
		{
			if ( _config == null )
			{
				_config = new MainConfig();
				Logger.LogVerbose("MainConfig initialized");

				try
				{
					string settingsPath = Path.Combine(
						Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
						"KOTORModSync",
						"settings.json"
					);

					if ( File.Exists(settingsPath) )
					{
						Logger.LogVerbose($"Loading settings from: {settingsPath}");
						string json = File.ReadAllText(settingsPath);
						var settings = JsonConvert.DeserializeObject<SettingsData>(json);

						if ( settings != null )
						{
							if ( !string.IsNullOrWhiteSpace(settings.NexusModsApiKey) )
							{
								_config.nexusModsApiKey = settings.NexusModsApiKey;
								Logger.LogVerbose("Loaded Nexus Mods API key from settings.json");
							}

							if ( !string.IsNullOrEmpty(settings.SourcePath) && Directory.Exists(settings.SourcePath) )
							{
								_config.sourcePath = new DirectoryInfo(settings.SourcePath);
								Logger.LogVerbose($"Loaded source path from settings: {settings.SourcePath}");
							}

							if ( !string.IsNullOrEmpty(settings.DestinationPath) && Directory.Exists(settings.DestinationPath) )
							{
								_config.destinationPath = new DirectoryInfo(settings.DestinationPath);
								Logger.LogVerbose($"Loaded destination path from settings: {settings.DestinationPath}");
							}

							_config.debugLogging = settings.DebugLogging;
							_config.attemptFixes = settings.AttemptFixes;
							_config.noAdmin = settings.NoAdmin;
							_config.caseInsensitivePathing = settings.CaseInsensitivePathing;
							_config.archiveDeepCheck = settings.ArchiveDeepCheck;
							_config.useMultiThreadedIO = settings.UseMultiThreadedIO;
							_config.useCopyForMoveActions = settings.UseCopyForMoveActions;
							_config.validateAndReplaceInvalidArchives = settings.ValidateAndReplaceInvalidArchives;
							_config.filterDownloadsByResolution = settings.FilterDownloadsByResolution;

							Logger.LogVerbose("Settings loaded successfully from settings.json");
						}
					}
					else
					{
						Logger.LogVerbose("No settings.json found, using defaults");
					}
				}
				catch ( Exception ex )
				{
					Logger.LogWarning($"Failed to load settings.json: {ex.Message}");
				}

				if ( string.IsNullOrWhiteSpace(_config.nexusModsApiKey) )
				{
					try
					{
						string configDir = Path.Combine(
							Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
							"KOTORModSync"
						);
						string legacyConfigFile = Path.Combine(configDir, "nexusmods.config");

						if ( File.Exists(legacyConfigFile) )
						{
							string apiKey = File.ReadAllText(legacyConfigFile).Trim();
							if ( !string.IsNullOrWhiteSpace(apiKey) )
							{
								_config.nexusModsApiKey = apiKey;
								Logger.LogVerbose($"Loaded Nexus Mods API key from legacy config: {legacyConfigFile}");

								SaveSettings();
								Logger.LogVerbose("Migrated API key to settings.json");
							}
						}
					}
					catch ( Exception ex )
					{
						Logger.LogWarning($"Failed to load legacy nexusmods.config: {ex.Message}");
					}
				}
			}
		}

		private static void SaveSettings()
		{
			try
			{
				string configDir = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"KOTORModSync"
				);
				Directory.CreateDirectory(configDir);
				string settingsPath = Path.Combine(configDir, "settings.json");

				SettingsData settings;
				if ( File.Exists(settingsPath) )
				{
					try
					{
						string existingJson = File.ReadAllText(settingsPath);
						settings = JsonConvert.DeserializeObject<SettingsData>(existingJson) ?? new SettingsData();
						Logger.LogVerbose("Loaded existing settings for merge");
					}
					catch
					{
						settings = new SettingsData();
						Logger.LogVerbose("Failed to load existing settings, creating new");
					}
				}
				else
				{
					settings = new SettingsData();
				}

				if ( string.IsNullOrEmpty(settings.Theme) )
					settings.Theme = "/Styles/KotorStyle.axaml";

				settings.SourcePath = _config.sourcePathFullName;
				settings.DestinationPath = _config.destinationPathFullName;
				settings.DebugLogging = _config.debugLogging;
				settings.AttemptFixes = _config.attemptFixes;
				settings.NoAdmin = _config.noAdmin;
				settings.CaseInsensitivePathing = _config.caseInsensitivePathing;
				settings.ArchiveDeepCheck = _config.archiveDeepCheck;
				settings.UseMultiThreadedIO = _config.useMultiThreadedIO;
				settings.UseCopyForMoveActions = _config.useCopyForMoveActions;
				settings.LastOutputDirectory = _config.lastOutputDirectory?.FullName;
				settings.ValidateAndReplaceInvalidArchives = _config.validateAndReplaceInvalidArchives;
				settings.FilterDownloadsByResolution = _config.filterDownloadsByResolution;
				settings.NexusModsApiKey = _config.nexusModsApiKey;

				string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
				File.WriteAllText(settingsPath, json);

				Logger.LogVerbose($"Settings saved to: {settingsPath}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to save settings");
			}
		}

		private class SettingsData
		{
			[JsonProperty("theme")]
			public string Theme { get; set; }

			[JsonProperty("sourcePath")]
			public string SourcePath { get; set; }

			[JsonProperty("destinationPath")]
			public string DestinationPath { get; set; }

			[JsonProperty("debugLogging")]
			public bool DebugLogging { get; set; }

			[JsonProperty("attemptFixes")]
			public bool AttemptFixes { get; set; } = true;

			[JsonProperty("noAdmin")]
			public bool NoAdmin { get; set; }

			[JsonProperty("caseInsensitivePathing")]
			public bool CaseInsensitivePathing { get; set; } = true;

			[JsonProperty("archiveDeepCheck")]
			public bool ArchiveDeepCheck { get; set; }

			[JsonProperty("useMultiThreadedIO")]
			public bool UseMultiThreadedIO { get; set; }

			[JsonProperty("useCopyForMoveActions")]
			public bool UseCopyForMoveActions { get; set; }

			[JsonProperty("lastOutputDirectory")]
			public string LastOutputDirectory { get; set; }

			[JsonProperty("validateAndReplaceInvalidArchives")]
			public bool ValidateAndReplaceInvalidArchives { get; set; } = true;

			[JsonProperty("filterDownloadsByResolution")]
			public bool FilterDownloadsByResolution { get; set; } = true;

			[JsonProperty("nexusModsApiKey")]
			public string NexusModsApiKey { get; set; }
		}

		public class BaseOptions
		{
			[Option('v', "verbose", Required = false, HelpText = "Enable verbose output for debugging.")]
			public bool Verbose { get; set; }

			[Option("plaintext", Required = false, HelpText = "Use plaintext output instead of fancy ANSI progress display.")]
			public bool PlainText { get; set; }
		}

		[Verb("convert", HelpText = "Convert between formats or merge instruction sets, output to stdout or file")]
		public class ConvertOptions : BaseOptions
		{
			[Option('i', "input", Required = false, HelpText = "Input file path (for single file conversion)")]
			public string InputPath { get; set; }

			[Option('o', "output", Required = false, HelpText = "Output file path (if not specified, writes to stdout)")]
			public string OutputPath { get; set; }

			[Option('f', "format", Required = false, Default = "toml", HelpText = "Output format (toml, yaml, json, xml, ini, markdown)")]
			public string Format { get; set; }

			[Option('a', "auto", Required = false, HelpText = "Autogenerate instructions by pre-resolving URLs (does not download files)")]
			public bool AutoGenerate { get; set; }

			[Option('d', "download", Required = false, HelpText = "Download all mod files to source-path before processing (requires --source-path)")]
			public bool Download { get; set; }

			[Option('s', "select", Required = false, HelpText = "Select components by category or tier (format: 'category:Name' or 'tier:Name'). Can be specified multiple times.")]
			public IEnumerable<string> Select { get; set; }

			[Option("source-path", Required = false, HelpText = "Path to source directory containing downloaded mod files")]
			public string SourcePath { get; set; }

			[Option("nexus-mods-api-key", Required = false, HelpText = "Nexus Mods API key (overrides stored key from settings.json)")]
			public string NexusModsApiKey { get; set; }

			[Option('m', "merge", Required = false, HelpText = "Merge mode: merge two instruction sets (requires --existing and --incoming)")]
			public bool Merge { get; set; }

			[Option('e', "existing", Required = false, HelpText = "Existing instruction set file path (for merge mode)")]
			public string ExistingPath { get; set; }

			[Option('n', "incoming", Required = false, HelpText = "Incoming instruction set file path (for merge mode)")]
			public string IncomingPath { get; set; }

			[Option("exclude-existing-only", Required = false, HelpText = "[Merge] Remove components that exist only in EXISTING")]
			public bool ExcludeExistingOnly { get; set; }

			[Option("exclude-incoming-only", Required = false, HelpText = "[Merge] Remove components that exist only in INCOMING")]
			public bool ExcludeIncomingOnly { get; set; }

			[Option("use-existing-order", Required = false, HelpText = "[Merge] Use EXISTING component order (default: INCOMING order)")]
			public bool UseExistingOrder { get; set; }

			[Option("prefer-existing-fields", Required = false, HelpText = "[Merge] Prefer EXISTING values for ALL fields when both exist (default: prefer INCOMING)")]
			public bool PreferExistingFields { get; set; }

			[Option("prefer-incoming-fields", Required = false, HelpText = "[Merge] Prefer INCOMING values for ALL fields when both exist (default behavior)")]
			public bool PreferIncomingFields { get; set; }

			[Option("prefer-existing-name", Required = false, HelpText = "[Merge] Prefer EXISTING name when both exist")]
			public bool PreferExistingName { get; set; }

			[Option("prefer-existing-author", Required = false, HelpText = "[Merge] Prefer EXISTING author when both exist")]
			public bool PreferExistingAuthor { get; set; }

			[Option("prefer-existing-description", Required = false, HelpText = "[Merge] Prefer EXISTING description when both exist")]
			public bool PreferExistingDescription { get; set; }

			[Option("prefer-existing-directions", Required = false, HelpText = "[Merge] Prefer EXISTING directions when both exist")]
			public bool PreferExistingDirections { get; set; }

			[Option("prefer-existing-category", Required = false, HelpText = "[Merge] Prefer EXISTING category when both exist")]
			public bool PreferExistingCategory { get; set; }

			[Option("prefer-existing-tier", Required = false, HelpText = "[Merge] Prefer EXISTING tier when both exist")]
			public bool PreferExistingTier { get; set; }

			[Option("prefer-existing-installation-method", Required = false, HelpText = "[Merge] Prefer EXISTING installation method when both exist")]
			public bool PreferExistingInstallationMethod { get; set; }

			[Option("prefer-existing-instructions", Required = false, HelpText = "[Merge] Prefer EXISTING instructions when both exist")]
			public bool PreferExistingInstructions { get; set; }

			[Option("prefer-existing-options", Required = false, HelpText = "[Merge] Prefer EXISTING options when both exist")]
			public bool PreferExistingOptions { get; set; }

			[Option("prefer-existing-modlinks", Required = false, HelpText = "[Merge] Prefer EXISTING mod link filenames when both exist")]
			public bool PreferExistingModLinks { get; set; }

			[Option("concurrent", Required = false, HelpText = "Process downloads concurrently/in parallel instead of sequentially (faster but harder to debug) (default: false, sequential)")]
			public bool Concurrent { get; set; }

			[Option("ignore-errors", Required = false, HelpText = "Ignore dependency resolution errors and attempt to load components in the best possible order")]
			public bool IgnoreErrors { get; set; }
		}

		[Verb("merge", HelpText = "Merge two instruction sets together")]
		public class MergeOptions : BaseOptions
		{
			[Option('e', "existing", Required = true, HelpText = "Existing instruction set file path")]
			public string ExistingPath { get; set; }

			[Option('n', "incoming", Required = true, HelpText = "Incoming instruction set file path")]
			public string IncomingPath { get; set; }

			[Option('o', "output", Required = false, HelpText = "Output file path (if not specified, writes to stdout)")]
			public string OutputPath { get; set; }

			[Option('f', "format", Required = false, Default = "toml", HelpText = "Output format (toml, yaml, json, xml, ini, markdown)")]
			public string Format { get; set; }

			[Option('d', "download", Required = false, HelpText = "Download all mod files to source-path before processing (requires --source-path)")]
			public bool Download { get; set; }

			[Option('s', "select", Required = false, HelpText = "Select components by category or tier (format: 'category:Name' or 'tier:Name'). Can be specified multiple times.")]
			public IEnumerable<string> Select { get; set; }

			[Option("source-path", Required = false, HelpText = "Path to source directory containing downloaded mod files")]
			public string SourcePath { get; set; }

			[Option("nexus-mods-api-key", Required = false, HelpText = "Nexus Mods API key (overrides stored key from settings.json)")]
			public string NexusModsApiKey { get; set; }

			[Option("exclude-existing-only", Required = false, HelpText = "Remove components that exist only in EXISTING")]
			public bool ExcludeExistingOnly { get; set; }

			[Option("exclude-incoming-only", Required = false, HelpText = "Remove components that exist only in INCOMING")]
			public bool ExcludeIncomingOnly { get; set; }

			[Option("use-existing-order", Required = false, HelpText = "Use EXISTING component order (default: INCOMING order)")]
			public bool UseExistingOrder { get; set; }

			[Option("prefer-existing-fields", Required = false, HelpText = "Prefer EXISTING values for ALL fields when both exist (default: prefer INCOMING)")]
			public bool PreferExistingFields { get; set; }

			[Option("prefer-incoming-fields", Required = false, HelpText = "Prefer INCOMING values for ALL fields when both exist (default behavior)")]
			public bool PreferIncomingFields { get; set; }

			[Option("prefer-existing-name", Required = false, HelpText = "Prefer EXISTING name when both exist")]
			public bool PreferExistingName { get; set; }

			[Option("prefer-existing-author", Required = false, HelpText = "Prefer EXISTING author when both exist")]
			public bool PreferExistingAuthor { get; set; }

			[Option("prefer-existing-description", Required = false, HelpText = "Prefer EXISTING description when both exist")]
			public bool PreferExistingDescription { get; set; }

			[Option("prefer-existing-directions", Required = false, HelpText = "Prefer EXISTING directions when both exist")]
			public bool PreferExistingDirections { get; set; }

			[Option("prefer-existing-category", Required = false, HelpText = "Prefer EXISTING category when both exist")]
			public bool PreferExistingCategory { get; set; }

			[Option("prefer-existing-tier", Required = false, HelpText = "Prefer EXISTING tier when both exist")]
			public bool PreferExistingTier { get; set; }

			[Option("prefer-existing-installation-method", Required = false, HelpText = "Prefer EXISTING installation method when both exist")]
			public bool PreferExistingInstallationMethod { get; set; }

			[Option("prefer-existing-instructions", Required = false, HelpText = "Prefer EXISTING instructions when both exist")]
			public bool PreferExistingInstructions { get; set; }

			[Option("prefer-existing-options", Required = false, HelpText = "Prefer EXISTING options when both exist")]
			public bool PreferExistingOptions { get; set; }

			[Option("prefer-existing-modlinks", Required = false, HelpText = "Prefer EXISTING mod link filenames when both exist")]
			public bool PreferExistingModLinks { get; set; }

			[Option("concurrent", Required = false, HelpText = "Process downloads concurrently/in parallel instead of sequentially (faster but harder to debug) (default: false, sequential)")]
			public bool Concurrent { get; set; }

			[Option("ignore-errors", Required = false, HelpText = "Ignore dependency resolution errors and attempt to load components in the best possible order")]
			public bool IgnoreErrors { get; set; }
		}

		[Verb("validate", HelpText = "Validate instruction files for errors")]
		public class ValidateOptions : BaseOptions
		{
			[Option('i', "input", Required = true, HelpText = "Input file path to validate")]
			public string InputPath { get; set; }

			[Option('g', "game-dir", Required = false, HelpText = "Game installation directory (for full validation)")]
			public string GameDirectory { get; set; }

			[Option('s', "source-dir", Required = false, HelpText = "Source directory containing mod files (for file existence checks)")]
			public string SourceDirectory { get; set; }

			[Option("select", Required = false, HelpText = "Select components to validate (format: 'category:Name' or 'tier:Name'). Can be specified multiple times.")]
			public IEnumerable<string> Select { get; set; }

			[Option("full", Required = false, Default = false, HelpText = "Perform full validation including environment checks (requires --game-dir and --source-dir)")]
			public bool FullValidation { get; set; }

			[Option("errors-only", Required = false, Default = false, HelpText = "Only show errors, suppress warnings and info messages")]
			public bool ErrorsOnly { get; set; }

			[Option("ignore-errors", Required = false, HelpText = "Ignore dependency resolution errors and attempt to load components in the best possible order")]
			public bool IgnoreErrors { get; set; }
		}

		[Verb("install", HelpText = "Install mods from an instruction file")]
		public class InstallOptions : BaseOptions
		{
			[Option('i', "input", Required = true, HelpText = "Instruction file path")]
			public string InputPath { get; set; }

			[Option('g', "game-dir", Required = true, HelpText = "Game installation directory")]
			public string GameDirectory { get; set; }

			[Option('s', "source-dir", Required = false, HelpText = "Source directory containing mod files (defaults to input file directory)")]
			public string SourceDirectory { get; set; }

			[Option("select", Required = false, HelpText = "Select components to install (format: 'category:Name' or 'tier:Name'). Can be specified multiple times. If not specified, all selected mods in the file will be installed.")]
			public IEnumerable<string> Select { get; set; }

			[Option("no-checkpoint", Required = false, Default = false, HelpText = "Disable checkpoint system (not recommended)")]
			public bool NoCheckpoint { get; set; }

			[Option("skip-validation", Required = false, Default = false, HelpText = "Skip pre-installation validation (not recommended)")]
			public bool SkipValidation { get; set; }

			[Option('y', "yes", Required = false, Default = false, HelpText = "Automatically answer 'yes' to all prompts")]
			public bool AutoConfirm { get; set; }

			[Option("ignore-errors", Required = false, Default = false, HelpText = "Ignore dependency resolution errors and attempt to load components in the best possible order")]
			public bool IgnoreErrors { get; set; }
		}

		[Verb("set-nexus-api-key", HelpText = "Set and validate your Nexus Mods API key")]
		public class SetNexusApiKeyOptions : BaseOptions
		{
			[Value(0, Required = true, MetaName = "api-key", HelpText = "Your Nexus Mods API key")]
			public string ApiKey { get; set; }

			[Option("skip-validation", Required = false, Default = false, HelpText = "Skip API key validation")]
			public bool SkipValidation { get; set; }
		}

		[Verb("install-python-deps", HelpText = "Install Python dependencies for HoloPatcher at build time")]
		public class InstallPythonDepsOptions : BaseOptions
		{
			[Option("force", Required = false, Default = false, HelpText = "Force reinstall even if dependencies are already installed")]
			public bool Force { get; set; }
		}

		[Verb("holopatcher", HelpText = "Run HoloPatcher with optional arguments")]
		public class HolopatcherOptions : BaseOptions
		{
			[Option('a', "args", Required = false, Default = "", HelpText = "Arguments to pass to HoloPatcher")]
			public string Arguments { get; set; }
		}

		public static int Run(string[] args)
		{
			// Disable keyring BEFORE any Python initialization to prevent pip hanging
			// This must be set at the process level before Python.Included initializes
			Environment.SetEnvironmentVariable("PYTHON_KEYRING_BACKEND", "keyring.backends.null.Keyring");
			Environment.SetEnvironmentVariable("DISPLAY", "");  // Also disable X11 display waiting

			Logger.Initialize();

			var parser = new Parser(with => with.HelpWriter = Console.Out);

			return parser.ParseArguments<ConvertOptions, MergeOptions, ValidateOptions, InstallOptions, SetNexusApiKeyOptions, InstallPythonDepsOptions, HolopatcherOptions>(args)
			.MapResult(
				(ConvertOptions opts) => RunConvertAsync(opts).GetAwaiter().GetResult(),
				(MergeOptions opts) => RunMergeAsync(opts).GetAwaiter().GetResult(),
				(ValidateOptions opts) => RunValidateAsync(opts).GetAwaiter().GetResult(),
				(InstallOptions opts) => RunInstallAsync(opts).GetAwaiter().GetResult(),
				(SetNexusApiKeyOptions opts) => RunSetNexusApiKeyAsync(opts).GetAwaiter().GetResult(),
				(InstallPythonDepsOptions opts) => RunInstallPythonDepsAsync(opts).GetAwaiter().GetResult(),
				(HolopatcherOptions opts) => RunHolopatcherAsync(opts).GetAwaiter().GetResult(),
				errs => 1);
		}

		private static void SetVerboseMode(bool verbose)
		{
			var config = new MainConfig { debugLogging = verbose };
		}

		/// <summary>
		/// Handles dependency resolution errors in CLI mode.
		/// If ignoreErrors is true, attempts to resolve with errors ignored.
		/// Otherwise, prints comprehensive error information and fails.
		/// </summary>
		private static List<ModComponent> HandleDependencyResolutionErrors(
			List<ModComponent> components,
			bool ignoreErrors,
			string operationContext)
		{
			try
			{
				var resolutionResult = Core.Services.DependencyResolverService.ResolveDependencies(components, ignoreErrors);

				if ( resolutionResult.Success )
				{
					Logger.LogVerbose($"Successfully resolved dependencies for {resolutionResult.OrderedComponents.Count} components");
					return resolutionResult.OrderedComponents;
				}
				else
				{
					if ( ignoreErrors )
					{
						Logger.LogWarning($"Dependency resolution failed with {resolutionResult.Errors.Count} errors, but --ignore-errors flag was specified. Attempting to load in best possible order.");
						return resolutionResult.OrderedComponents;
					}
					else
					{
						Logger.LogError($"Dependency resolution failed with {resolutionResult.Errors.Count} errors:");
						Logger.LogError("");

						foreach ( var error in resolutionResult.Errors )
						{
							Logger.LogError($"❌ {error.ComponentName}: {error.Message}");
							if ( error.AffectedComponents.Count > 0 )
							{
								Logger.LogError($"   Affected components: {string.Join(", ", error.AffectedComponents)}");
							}
						}

						Logger.LogError("");
						Logger.LogError("To resolve these issues, you can:");
						Logger.LogError("1. Fix the dependency relationships manually in your instruction file");
						Logger.LogError("2. Use the --ignore-errors flag to attempt loading in the best possible order");
						Logger.LogError("3. Use the GUI to auto-fix dependencies or remove all dependencies");
						Logger.LogError("");
						Logger.LogError($"Operation '{operationContext}' failed due to dependency resolution errors.");

						throw new InvalidOperationException($"Dependency resolution failed with {resolutionResult.Errors.Count} errors. Use --ignore-errors flag to attempt loading in best possible order.");
					}
				}
			}
			catch ( Exception ex )
			{
				if ( ignoreErrors )
				{
					Logger.LogWarning($"Dependency resolution failed with exception: {ex.Message}. Continuing with original order due to --ignore-errors flag.");
					return components;
				}
				else
				{
					Logger.LogError($"Dependency resolution failed with exception: {ex.Message}");
					throw;
				}
			}
		}

		private static void LogAllErrors(DownloadCacheService downloadCache, bool forceConsoleOutput = false)
		{
			bool hasDownloadFailures = downloadCache?.GetFailures()?.Count > 0;
			bool hasOtherErrors = _errorCollector?.GetErrorCount() > 0;

			if ( !hasDownloadFailures && !hasOtherErrors )
			{
				if ( forceConsoleOutput )
				{
					Console.WriteLine("No errors to report.");
					Console.Out.Flush();
				}
				return;
			}

			void WriteOutput(string message)
			{
				if ( forceConsoleOutput )
				{
					Console.WriteLine(message);
					Console.Out.Flush();
				}
				else if ( _progressDisplay != null )
				{
					_progressDisplay.WriteScrollingLog(message);
				}
				else
				{
					Logger.Log(message);
				}
			}

			WriteOutput("");
			WriteOutput(new string('=', 80));
			WriteOutput("ERROR AND FAILURE SUMMARY");
			WriteOutput(new string('=', 80));

			// Display errors by category
			if ( hasOtherErrors )
			{
				var errorsByCategory = _errorCollector.GetErrorsByCategory();

				foreach ( var categoryGroup in errorsByCategory.OrderBy(kvp => kvp.Key.ToString()) )
				{
					WriteOutput("");
					WriteOutput($"▼ {categoryGroup.Key} Errors ({categoryGroup.Value.Count}):");
					WriteOutput(new string('-', 80));

					foreach ( var error in categoryGroup.Value )
					{
						string componentPrefix = !string.IsNullOrWhiteSpace(error.ComponentName)
							? $"[{error.ComponentName}] "
							: "";

						WriteOutput($"  ✗ {componentPrefix}{error.Message}");

						if ( !string.IsNullOrWhiteSpace(error.Details) )
						{
							WriteOutput($"    Details: {error.Details}");
						}

						// Display full log context leading up to the error
						if ( error.LogContext != null && error.LogContext.Count > 0 )
						{
							WriteOutput("");
							WriteOutput("    ═══ Log Context (leading up to error) ═══");
							foreach ( string logLine in error.LogContext )
							{
								WriteOutput($"    {logLine}");
							}
							WriteOutput("    ═══════════════════════════════════════");
						}

						if ( error.Exception != null )
						{
							WriteOutput("");
							WriteOutput($"    Exception: {error.Exception.GetType().Name} - {error.Exception.Message}");
							WriteOutput($"    Stack trace:");
							if ( !string.IsNullOrWhiteSpace(error.Exception.StackTrace) )
							{
								// Split stack trace by lines and indent each line
								string[] stackLines = error.Exception.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
								foreach ( string line in stackLines )
								{
									WriteOutput($"      {line}");
								}
							}
						}

						WriteOutput("");
					}
				}
			}

			// Display download failures
			if ( hasDownloadFailures )
			{
				var failures = downloadCache.GetFailures();
				var failuresWithBoth = new List<DownloadCacheService.DownloadFailureInfo>();
				var failuresUrlOnly = new List<DownloadCacheService.DownloadFailureInfo>();
				var failuresFileOnly = new List<DownloadCacheService.DownloadFailureInfo>();

				foreach ( var failure in failures )
				{
					bool hasUrl = !string.IsNullOrWhiteSpace(failure.Url);
					bool hasFile = !string.IsNullOrWhiteSpace(failure.ExpectedFileName);

					if ( hasUrl && hasFile )
						failuresWithBoth.Add(failure);
					else if ( hasUrl )
						failuresUrlOnly.Add(failure);
					else if ( hasFile )
						failuresFileOnly.Add(failure);
				}

				WriteOutput("");
				WriteOutput($"▼ Download and File Failures ({failures.Count}):");
				WriteOutput(new string('-', 80));

				if ( failuresWithBoth.Count > 0 )
				{
					WriteOutput("");
					WriteOutput($"  Failed Downloads with Expected Filenames ({failuresWithBoth.Count}):");

					foreach ( var failure in failuresWithBoth )
					{
						WriteOutput($"    [{failure.ComponentName}] {failure.Url} → {failure.ExpectedFileName}");
						if ( !string.IsNullOrWhiteSpace(failure.ErrorMessage) )
							WriteOutput($"      Error: {failure.ErrorMessage}");

						// Display full log context leading up to the failure
						if ( failure.LogContext != null && failure.LogContext.Count > 0 )
						{
							WriteOutput("");
							WriteOutput("      ═══ Log Context (leading up to failure) ═══");
							foreach ( string logLine in failure.LogContext )
							{
								WriteOutput($"      {logLine}");
							}
							WriteOutput("      ════════════════════════════════════════════");
							WriteOutput("");
						}
					}
				}

				if ( failuresUrlOnly.Count > 0 )
				{
					WriteOutput("");
					WriteOutput($"  Failed URLs (No Filename Resolved) ({failuresUrlOnly.Count}):");

					foreach ( var failure in failuresUrlOnly )
					{
						WriteOutput($"    [{failure.ComponentName}] {failure.Url}");
						if ( !string.IsNullOrWhiteSpace(failure.ErrorMessage) )
							WriteOutput($"      Error: {failure.ErrorMessage}");

						// Display full log context leading up to the failure
						if ( failure.LogContext != null && failure.LogContext.Count > 0 )
						{
							WriteOutput("");
							WriteOutput("      ═══ Log Context (leading up to failure) ═══");
							foreach ( string logLine in failure.LogContext )
							{
								WriteOutput($"      {logLine}");
							}
							WriteOutput("      ════════════════════════════════════════════");
							WriteOutput("");
						}
					}
				}

				if ( failuresFileOnly.Count > 0 )
				{
					WriteOutput("");
					WriteOutput($"  Missing Files (No URL) ({failuresFileOnly.Count}):");

					foreach ( var failure in failuresFileOnly )
					{
						WriteOutput($"    [{failure.ComponentName}] {failure.ExpectedFileName}");
						if ( !string.IsNullOrWhiteSpace(failure.ErrorMessage) )
							WriteOutput($"      Error: {failure.ErrorMessage}");

						// Display full log context leading up to the failure
						if ( failure.LogContext != null && failure.LogContext.Count > 0 )
						{
							WriteOutput("");
							WriteOutput("      ═══ Log Context (leading up to failure) ═══");
							foreach ( string logLine in failure.LogContext )
							{
								WriteOutput($"      {logLine}");
							}
							WriteOutput("      ════════════════════════════════════════════");
							WriteOutput("");
						}
					}
				}
			}

			WriteOutput("");
			WriteOutput(new string('=', 80));
			int totalErrors = (_errorCollector?.GetErrorCount() ?? 0) + (downloadCache?.GetFailures()?.Count ?? 0);
			WriteOutput($"TOTAL ERRORS/FAILURES: {totalErrors}");
			WriteOutput(new string('=', 80));
		}

		private static async Task<DownloadCacheService> DownloadAllModFilesAsync(List<ModComponent> components, string destinationDirectory, bool verbose, bool sequential = true, CancellationToken cancellationToken = default)
		{
			int componentCount = components.Count(c => c.ModLinkFilenames != null && c.ModLinkFilenames.Count > 0);
			if ( componentCount == 0 )
			{
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog("No components with URLs found to download");
				else
					Logger.LogVerbose("No components with URLs found to download");
				return null;
			}

			string message = $"Processing {componentCount} component(s) for download...";
			if ( _progressDisplay != null )
				_progressDisplay.WriteScrollingLog(message);
			else
				Logger.Log(message);

			var downloadCache = new DownloadCacheService();
			_globalDownloadCache = downloadCache;

			var downloadManager = Services.Download.DownloadHandlerFactory.CreateDownloadManager(
				nexusModsApiKey: _config.nexusModsApiKey);

			downloadCache.SetDownloadManager(downloadManager);

			var lastLoggedProgress = new Dictionary<string, double>();
			var progressLock = new object();

			var progressReporter = new Progress<DownloadProgress>(progress =>
			{
				string fileName = Path.GetFileName(progress.FilePath ?? progress.Url);
				string progressKey = $"{progress.ModName}:{fileName}";

				if ( progress.Status == DownloadStatus.InProgress )
				{
					if ( _progressDisplay != null )
					{
						string displayText = $"{progress.ModName}: {fileName}";
						_progressDisplay.UpdateProgress(progressKey, displayText, progress.ProgressPercentage, "downloading");
					}
					else if ( verbose )
					{
						bool shouldLog = false;
						lock ( progressLock )
						{
							if ( !lastLoggedProgress.TryGetValue(progressKey, out double lastProgress) )
							{
								shouldLog = true;
							}
							else if ( progress.ProgressPercentage - lastProgress >= 10.0 )
							{
								shouldLog = true;
							}

							if ( shouldLog )
							{
								lastLoggedProgress[progressKey] = progress.ProgressPercentage;
							}
						}

						if ( shouldLog )
						{
							Logger.LogVerbose($"[Download] {progress.ModName}: {progress.ProgressPercentage:F1}% - {fileName}");
						}
					}
				}
				else if ( progress.Status == DownloadStatus.Completed )
				{
					if ( _progressDisplay != null )
					{
						_progressDisplay.RemoveProgress(progressKey);
						_progressDisplay.WriteScrollingLog($"✓ Downloaded: {fileName}");
					}
					else
					{
						Logger.Log($"[Download] Completed: {fileName}");
					}
					lock ( progressLock )
					{
						lastLoggedProgress.Remove(progressKey);
					}
				}
				else if ( progress.Status == DownloadStatus.Failed )
				{
					if ( _progressDisplay != null )
					{
						_progressDisplay.RemoveProgress(progressKey);
						_progressDisplay.AddFailedItem(progress.Url, progress.ErrorMessage);
						_progressDisplay.WriteScrollingLog($"✗ Failed: {fileName}");
					}
					else
					{
						Logger.LogError($"[Download] Failed: {fileName} - {progress.ErrorMessage}");
					}
					lock ( progressLock )
					{
						lastLoggedProgress.Remove(progressKey);
					}
				}
				else if ( progress.Status == DownloadStatus.Skipped )
				{
					if ( _progressDisplay != null )
					{
						_progressDisplay.WriteScrollingLog($"⊙ Skipped (exists): {fileName}");
					}
					else
					{
						Logger.Log($"[Download] Skipped (already exists): {fileName}");
					}
				}
			});

			try
			{
				var componentsToProcess = components.Where(c => c.ModLinkFilenames != null && c.ModLinkFilenames.Count > 0).ToList();
				Logger.LogVerbose($"[Download] Processing {componentsToProcess.Count} components with concurrency limit of 10");

				using ( var semaphore = new SemaphoreSlim(10) )
				{
					var downloadTasks = componentsToProcess.Select(async component =>
				{
					await semaphore.WaitAsync();
					try
					{
						Logger.LogVerbose($"[Download] Processing component: {component.Name} ({component.ModLinkFilenames.Count} URL(s))");

						try
						{
							var results = await downloadCache.ResolveOrDownloadAsync(
								component,
								destinationDirectory,
								progressReporter,
								sequential: sequential,
								cancellationToken);

							int successCount = results.Count(entry =>
							{
								string filePath = MainConfig.SourcePath != null
									? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
									: Path.Combine(destinationDirectory, entry.FileName);
								return File.Exists(filePath);
							});

							return (component, results, successCount, error: (string)null);
						}
						catch ( Exception ex )
						{
							string errorMsg = $"Error processing component {component.Name}: {ex.Message}";
							if ( _progressDisplay != null )
								_progressDisplay.WriteScrollingLog($"✗ {errorMsg}");
							else
								Logger.LogError(errorMsg);

							if ( verbose )
							{
								Logger.LogException(ex);
							}

							_errorCollector?.RecordError(
								ErrorCollector.ErrorCategory.Download,
								component.Name,
								"Failed to process component for download",
								errorMsg,
								ex);

							return (component, results: new List<KOTORModSync.Core.Services.DownloadCacheService.DownloadCacheEntry>(), successCount: 0, error: errorMsg);
						}
					}
					finally
					{
						semaphore.Release();
					}
				}).ToList();

					var downloadResults = await Task.WhenAll(downloadTasks);

					int totalSuccessCount = downloadResults.Sum(r => r.successCount);
					int totalFailCount = downloadResults.Count(r => r.error != null);

					string summaryMsg = $"Download results: {totalSuccessCount} files available, {totalFailCount} failed";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(summaryMsg);
					else
						Logger.Log(summaryMsg);

					if ( totalFailCount > 0 )
					{
						string warningMsg = "Some downloads failed. Check logs for details.";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog($"⚠ {warningMsg}");
						else
							Logger.LogWarning(warningMsg);
					}
				}
			}
			catch ( Exception ex )
			{
				string errorMsg = $"Error during download: {ex.Message}";
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog($"✗ {errorMsg}");
				else
					Logger.LogError(errorMsg);

				if ( verbose )
				{
					Logger.LogException(ex);
				}

				_errorCollector?.RecordError(
					ErrorCollector.ErrorCategory.Download,
					null,
					"Critical error during download process",
					errorMsg,
					ex);

				throw;
			}

			return downloadCache;
		}

		private static void ApplySelectionFilters(
			List<ModComponent> components,
			IEnumerable<string> selections)
		{
			if ( components == null )
				return;

			if ( selections == null || !selections.Any() )
			{
				foreach ( var component in components )
				{
					component.IsSelected = true;
				}
				return;
			}

			var selectedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var selectedTiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach ( string selection in selections )
			{
				if ( string.IsNullOrWhiteSpace(selection) )
					continue;

				string[] parts = selection.Split(new[] { ':' }, 2);
				if ( parts.Length != 2 )
				{
					Logger.LogWarning($"Invalid selection format: '{selection}'. Expected format: 'category:Name' or 'tier:Name'");
					continue;
				}

				string type = parts[0].Trim().ToLowerInvariant();
				string value = parts[1].Trim();

				if ( type == "category" )
				{
					selectedCategories.Add(value);
					Logger.LogVerbose($"Added category filter: {value}");
				}
				else if ( type == "tier" )
				{
					selectedTiers.Add(value);
					Logger.LogVerbose($"Added tier filter: {value}");
				}
				else
				{
					Logger.LogWarning($"Unknown selection type: '{type}'. Use 'category' or 'tier'");
				}
			}

			var tierPriorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
			{
				{ "Essential", 1 },
				{ "Recommended", 2 },
				{ "Suggested", 3 },
				{ "Optional", 4 }
			};

			int selectedCount = 0;

			foreach ( var component in components )
			{
				bool includeByCategory = false;
				bool includeByTier = false;

				if ( selectedCategories.Count > 0 )
				{
					if ( component.Category != null && component.Category.Count > 0 )
					{
						includeByCategory = component.Category.Any(cat => selectedCategories.Contains(cat));
					}
				}
				else
				{
					includeByCategory = true;
				}

				if ( selectedTiers.Count > 0 )
				{
					if ( !string.IsNullOrEmpty(component.Tier) )
					{
						foreach ( string selectedTier in selectedTiers )
						{
							if ( tierPriorities.TryGetValue(selectedTier, out int selectedPriority) &&
								tierPriorities.TryGetValue(component.Tier, out int componentPriority) )
							{
								if ( componentPriority <= selectedPriority )
								{
									includeByTier = true;
									break;
								}
							}
							else if ( component.Tier.Equals(selectedTier, StringComparison.OrdinalIgnoreCase) )
							{
								includeByTier = true;
								break;
							}
						}
					}
				}
				else
				{
					includeByTier = true;
				}

				if ( includeByCategory && includeByTier )
				{
					component.IsSelected = true;
					selectedCount++;
				}
				else
				{
					component.IsSelected = false;
				}
			}

			Logger.LogVerbose($"Selection filters applied: {selectedCount}/{components.Count} components selected");
			if ( selectedCategories.Count > 0 )
			{
				Logger.LogVerbose($"Categories: {string.Join(", ", selectedCategories)}");
			}
			if ( selectedTiers.Count > 0 )
			{
				Logger.LogVerbose($"Tiers: {string.Join(", ", selectedTiers)}");
			}
		}

		private static async Task<int> RunConvertAsync(ConvertOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			_progressDisplay = new ConsoleProgressDisplay(usePlainText: opts.PlainText);
			_errorCollector = new ErrorCollector();

			DownloadCacheService downloadCache = null;

			ConsoleCancelEventHandler cancelHandler = (sender, e) =>
			{
				e.Cancel = true;

				try
				{
					Console.Error.WriteLine();
					Console.Error.WriteLine();
					Console.Error.WriteLine("====================================================================");
					Console.Error.WriteLine("CTRL+C DETECTED - Cancellation in progress...");
					Console.Error.WriteLine("====================================================================");
					Console.Error.Flush();

					try
					{
						_progressDisplay?.Dispose();
						_progressDisplay = null;
					}
					catch ( Exception disposeEx )
					{
						Console.Error.WriteLine($"Warning: Error disposing progress display: {disposeEx.Message}");
					}

					if ( _globalDownloadCache != null )
					{
						try
						{
							Console.Error.WriteLine();
							Console.Error.WriteLine("Logging all errors and failures...");
							Console.Error.Flush();

							LogAllErrors(_globalDownloadCache, forceConsoleOutput: true);

							Console.Error.WriteLine();
							Console.Error.WriteLine("Error logging complete.");
							Console.Error.Flush();
						}
						catch ( Exception logEx )
						{
							Console.Error.WriteLine($"Error logging failures: {logEx.Message}");
							Console.Error.Flush();
						}
					}
					else
					{
						Console.Error.WriteLine("No download cache to log (no downloads were performed).");
						Console.Error.Flush();
					}

					Console.Error.WriteLine();
					Console.Error.WriteLine("Exiting...");
					Console.Error.Flush();

					System.Threading.Thread.Sleep(500);
				}
				catch ( Exception ex )
				{
					Console.Error.WriteLine($"Critical error in CTRL+C handler: {ex.Message}");
					Console.Error.Flush();
					System.Threading.Thread.Sleep(100);
				}
				finally
				{
					Environment.Exit(1);
				}
			};

			Console.CancelKeyPress += cancelHandler;

			try
			{
				EnsureConfigInitialized();

				if ( !string.IsNullOrWhiteSpace(opts.NexusModsApiKey) )
				{
					_config.nexusModsApiKey = opts.NexusModsApiKey;
					Logger.LogVerbose("Using Nexus Mods API key from command line argument");
				}

				// Backward compatibility: redirect to RunMergeAsync if using --merge flag
				if ( opts.Merge )
				{
					_progressDisplay?.Dispose();
					_progressDisplay = null;

					var mergeOpts = new MergeOptions
					{
						ExistingPath = opts.ExistingPath,
						IncomingPath = opts.IncomingPath,
						OutputPath = opts.OutputPath,
						Format = opts.Format,
						Download = opts.Download,
						Select = opts.Select,
						SourcePath = opts.SourcePath,
						NexusModsApiKey = opts.NexusModsApiKey,
						ExcludeExistingOnly = opts.ExcludeExistingOnly,
						ExcludeIncomingOnly = opts.ExcludeIncomingOnly,
						UseExistingOrder = opts.UseExistingOrder,
						PreferExistingFields = opts.PreferExistingFields,
						PreferIncomingFields = opts.PreferIncomingFields,
						PreferExistingName = opts.PreferExistingName,
						PreferExistingAuthor = opts.PreferExistingAuthor,
						PreferExistingDescription = opts.PreferExistingDescription,
						PreferExistingDirections = opts.PreferExistingDirections,
						PreferExistingCategory = opts.PreferExistingCategory,
						PreferExistingTier = opts.PreferExistingTier,
						PreferExistingInstallationMethod = opts.PreferExistingInstallationMethod,
						PreferExistingInstructions = opts.PreferExistingInstructions,
						PreferExistingOptions = opts.PreferExistingOptions,
						PreferExistingModLinks = opts.PreferExistingModLinks,
						Verbose = opts.Verbose,
						PlainText = opts.PlainText
					};

					return await RunMergeAsync(mergeOpts);
				}

				// Convert mode validation
				if ( string.IsNullOrEmpty(opts.InputPath) )
				{
					Logger.LogError("--input is required for convert mode");
					Logger.Log("Usage: convert --input <file> [options]");
					Logger.Log("   OR: Use the 'merge' command to merge two instruction sets");
					Logger.Log("       merge --existing <file> --incoming <file> [options]");
					Logger.Log("   OR: convert --merge --existing <file> --incoming <file> [options] (backward compatible)");
					return 1;
				}

				if ( !File.Exists(opts.InputPath) )
				{
					Logger.LogError($"Input file not found: {opts.InputPath}");
					return 1;
				}

				Logger.LogVerbose($"Convert mode: {opts.InputPath}");
				Logger.LogVerbose($"Output format: {opts.Format}");

				if ( opts.Download && string.IsNullOrEmpty(opts.SourcePath) )
				{
					Logger.LogError("--download requires --source-path to be specified");
					return 1;
				}

				if ( !string.IsNullOrEmpty(opts.SourcePath) )
				{
					Logger.LogVerbose($"Source path provided: {opts.SourcePath}");
					if ( !Directory.Exists(opts.SourcePath) )
					{
						Logger.LogVerbose($"Source path does not exist, creating: {opts.SourcePath}");
						Directory.CreateDirectory(opts.SourcePath);
					}

					_config.sourcePath = new DirectoryInfo(opts.SourcePath);
					_config.debugLogging = opts.Verbose;
					Logger.LogVerbose($"Source path set to: {opts.SourcePath}");
				}

				// Convert mode only (merge is now handled in RunMergeAsync)
				string msg = "Loading components from input file...";
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog(msg);
				else
					Logger.LogVerbose(msg);

				List<ModComponent> components;
				try
				{
					components = FileLoadingService.LoadFromFile(opts.InputPath);

					// Handle dependency resolution
					components = HandleDependencyResolutionErrors(components, opts.IgnoreErrors, "Convert");

					msg = $"Loaded {components.Count} components";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(msg);
					else
						Logger.LogVerbose(msg);
				}
				catch ( Exception ex )
				{
					_errorCollector?.RecordError(
						ErrorCollector.ErrorCategory.FileOperation,
						null,
						"Failed to load components from file",
						$"Input file: {opts.InputPath}",
						ex);
					throw;
				}

				if ( opts.Download )
				{
					msg = "Starting download of mod files...";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(msg);
					else
						Logger.Log(msg);

					using ( var downloadCts = new CancellationTokenSource(TimeSpan.FromHours(2)) )
					{
						downloadCache = await DownloadAllModFilesAsync(components, opts.SourcePath, opts.Verbose, sequential: !opts.Concurrent, downloadCts.Token);
					}

					msg = "Download complete";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(msg);
					else
						Logger.Log(msg);
				}

				if ( opts.AutoGenerate )
				{
					string message = "Auto-generating instructions from URLs...";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(message);
					else
						Logger.LogVerbose(message);

					if ( downloadCache == null )
					{
						downloadCache = new Services.DownloadCacheService();
						downloadCache.SetDownloadManager();
						_globalDownloadCache = downloadCache;
					}

					int totalComponents = components.Count(c => c.ModLinkFilenames != null && c.ModLinkFilenames.Count > 0);
					message = $"Processing {totalComponents} components sequentially...";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(message);
					else
						Logger.LogVerbose(message);

					int successCount = 0;
					int currentIndex = 0;
					foreach ( var component in components.Where(c => c.ModLinkFilenames != null && c.ModLinkFilenames.Count > 0) )
					{
						component.IsSelected = true;
						currentIndex++;

						string progressKey = $"autogen:{component.Name}";
						double progressPercent = (double)currentIndex / totalComponents * 100.0;

						if ( _progressDisplay != null )
						{
							_progressDisplay.UpdateProgress(progressKey, component.Name, progressPercent, "processing");
							_progressDisplay.WriteScrollingLog($"[{currentIndex}/{totalComponents}] Processing: {component.Name}");
						}
						else
						{
							Logger.Log($"[Auto-Generate] Processing component: {component.Name}");
						}

						bool success = false;
						try
						{
							success = await Services.AutoInstructionGenerator.GenerateInstructionsFromUrlsAsync(
								component, downloadCache);
						}
						catch ( Exception ex )
						{
							_errorCollector?.RecordError(
								ErrorCollector.ErrorCategory.General,
								component.Name,
								"Auto-instruction generation failed",
								$"Failed to generate instructions from URLs",
								ex);
							success = false;
						}

						if ( _progressDisplay != null )
						{
							_progressDisplay.RemoveProgress(progressKey);
						}

						if ( success )
						{
							if ( _progressDisplay != null )
								_progressDisplay.WriteScrollingLog($"✓ {component.Name}");
							else
								await Logger.LogVerboseAsync($"Auto-generation successful for component: {component.Name}");
							successCount++;
						}
						else
						{
							if ( _progressDisplay != null )
								_progressDisplay.WriteScrollingLog($"✗ Failed: {component.Name}");

							// Record as error if not already recorded
							if ( success == false )
							{
								_errorCollector?.RecordError(
									ErrorCollector.ErrorCategory.General,
									component.Name,
									"Auto-instruction generation returned false",
									"Failed to generate instructions from URLs");
							}
						}
					}

					message = $"Auto-generation complete: {successCount}/{totalComponents} components processed successfully";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(message);
					else
						Logger.LogVerbose(message);
				}

				ApplySelectionFilters(components, opts.Select);

				// Create validation context to track issues for serialization
				var validationContext = new ComponentValidationContext();

				// Collect download failures from cache
				if ( downloadCache != null )
				{
					var failures = downloadCache.GetFailures();
					foreach ( var failure in failures )
					{
						validationContext.AddUrlFailure(failure.Url, failure.ErrorMessage);
					}
				}

				// Collect validation issues from error collector
				if ( _errorCollector != null )
				{
					foreach ( var error in _errorCollector.GetErrors() )
					{
						// Try to find the component by name
						var component = components.FirstOrDefault(c => c.Name == error.ComponentName);
						if ( component != null )
						{
							validationContext.AddModComponentIssue(component.Guid, error.Message);
						}
					}
				}

				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog("Serializing to output format...");
				else
					Logger.LogVerbose("Serializing to output format...");

				string output = ModComponentSerializationService.SerializeModComponentAsString(components, opts.Format, validationContext);

				if ( !string.IsNullOrEmpty(opts.OutputPath) )
				{
					string outputDir = Path.GetDirectoryName(opts.OutputPath);
					if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
					{
						Directory.CreateDirectory(outputDir);
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog($"Created output directory: {outputDir}");
						else
							Logger.LogVerbose($"Created output directory: {outputDir}");
					}

					File.WriteAllText(opts.OutputPath, output);

					string successMsg = $"✓ Conversion completed successfully, saved to: {opts.OutputPath}";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(successMsg);
					else
						Logger.LogVerbose($"Conversion completed successfully, saved to: {opts.OutputPath}");
				}
				else
				{
					_progressDisplay?.Dispose();
					_progressDisplay = null;

					Logger.Log(output);
					Logger.LogVerbose("Conversion completed successfully (output to stdout)");
				}

				if ( downloadCache != null || _errorCollector != null )
				{
					LogAllErrors(downloadCache);
				}

				return 0;
			}
			catch ( Exception ex )
			{
				string errorMsg = $"Error during conversion: {ex.Message}";
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog($"✗ {errorMsg}");
				else
					Logger.LogError(errorMsg);

				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}

				if ( downloadCache != null || _errorCollector != null )
				{
					LogAllErrors(downloadCache);
				}

				return 1;
			}
			finally
			{
				Console.CancelKeyPress -= cancelHandler;

				_progressDisplay?.Dispose();
				_progressDisplay = null;

				_globalDownloadCache = null;
				_errorCollector = null;
			}
		}

		private static async Task<int> RunMergeAsync(MergeOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			_progressDisplay = new ConsoleProgressDisplay(usePlainText: opts.PlainText);
			_errorCollector = new ErrorCollector();

			DownloadCacheService downloadCache = null;

			ConsoleCancelEventHandler cancelHandler = (sender, e) =>
			{
				e.Cancel = true;

				try
				{
					Console.Error.WriteLine();
					Console.Error.WriteLine();
					Console.Error.WriteLine("====================================================================");
					Console.Error.WriteLine("CTRL+C DETECTED - Cancellation in progress...");
					Console.Error.WriteLine("====================================================================");
					Console.Error.Flush();

					try
					{
						_progressDisplay?.Dispose();
						_progressDisplay = null;
					}
					catch ( Exception disposeEx )
					{
						Console.Error.WriteLine($"Warning: Error disposing progress display: {disposeEx.Message}");
					}

					if ( _globalDownloadCache != null )
					{
						try
						{
							Console.Error.WriteLine();
							Console.Error.WriteLine("Logging all errors and failures...");
							Console.Error.Flush();

							LogAllErrors(_globalDownloadCache, forceConsoleOutput: true);

							Console.Error.WriteLine();
							Console.Error.WriteLine("Error logging complete.");
							Console.Error.Flush();
						}
						catch ( Exception logEx )
						{
							Console.Error.WriteLine($"Error logging failures: {logEx.Message}");
							Console.Error.Flush();
						}
					}
					else
					{
						Console.Error.WriteLine("No download cache to log (no downloads were performed).");
						Console.Error.Flush();
					}

					Console.Error.WriteLine();
					Console.Error.WriteLine("Exiting...");
					Console.Error.Flush();

					System.Threading.Thread.Sleep(500);
				}
				catch ( Exception ex )
				{
					Console.Error.WriteLine($"Critical error in CTRL+C handler: {ex.Message}");
					Console.Error.Flush();
					System.Threading.Thread.Sleep(100);
				}
				finally
				{
					Environment.Exit(1);
				}
			};

			Console.CancelKeyPress += cancelHandler;

			try
			{
				EnsureConfigInitialized();

				if ( !string.IsNullOrWhiteSpace(opts.NexusModsApiKey) )
				{
					_config.nexusModsApiKey = opts.NexusModsApiKey;
					Logger.LogVerbose("Using Nexus Mods API key from command line argument");
				}

				// Validate inputs
				if ( !File.Exists(opts.ExistingPath) )
				{
					Logger.LogError($"Existing file not found: {opts.ExistingPath}");
					return 1;
				}

				if ( !File.Exists(opts.IncomingPath) )
				{
					Logger.LogError($"Incoming file not found: {opts.IncomingPath}");
					return 1;
				}

				Logger.LogVerbose($"Merge mode: {opts.ExistingPath} + {opts.IncomingPath}");
				Logger.LogVerbose($"Output format: {opts.Format}");

				if ( opts.Download && string.IsNullOrEmpty(opts.SourcePath) )
				{
					Logger.LogError("--download requires --source-path to be specified");
					return 1;
				}

				if ( !string.IsNullOrEmpty(opts.SourcePath) )
				{
					Logger.LogVerbose($"Source path provided: {opts.SourcePath}");
					if ( !Directory.Exists(opts.SourcePath) )
					{
						Logger.LogVerbose($"Source path does not exist, creating: {opts.SourcePath}");
						Directory.CreateDirectory(opts.SourcePath);
					}

					_config.sourcePath = new DirectoryInfo(opts.SourcePath);
					_config.debugLogging = opts.Verbose;
					Logger.LogVerbose($"Source path set to: {opts.SourcePath}");
				}

				// Merge instruction sets
				string msg = "Merging instruction sets...";
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog(msg);
				else
					Logger.LogVerbose(msg);

				List<ModComponent> components;
				try
				{
					// Initialize download cache if we need to download or validate URLs
					if ( opts.Download && downloadCache == null )
					{
						downloadCache = new DownloadCacheService();
						downloadCache.SetDownloadManager();
						_globalDownloadCache = downloadCache;
					}

					var mergeOptions = new Services.MergeOptions
					{
						ExcludeExistingOnly = opts.ExcludeExistingOnly,
						ExcludeIncomingOnly = opts.ExcludeIncomingOnly,
						UseExistingOrder = opts.UseExistingOrder,
						HeuristicsOptions = MergeHeuristicsOptions.CreateDefault()
					};

					// Apply field-level preferences
					if ( opts.PreferExistingFields )
					{
						mergeOptions.PreferAllExistingFields = true;
					}
					else if ( opts.PreferIncomingFields )
					{
						mergeOptions.PreferAllIncomingFields = true;
					}

					// Individual field preferences override global settings
					if ( opts.PreferExistingName )
						mergeOptions.PreferExistingName = true;
					if ( opts.PreferExistingAuthor )
						mergeOptions.PreferExistingAuthor = true;
					if ( opts.PreferExistingDescription )
						mergeOptions.PreferExistingDescription = true;
					if ( opts.PreferExistingDirections )
						mergeOptions.PreferExistingDirections = true;
					if ( opts.PreferExistingCategory )
						mergeOptions.PreferExistingCategory = true;
					if ( opts.PreferExistingTier )
						mergeOptions.PreferExistingTier = true;
					if ( opts.PreferExistingInstallationMethod )
						mergeOptions.PreferExistingInstallationMethod = true;
					if ( opts.PreferExistingInstructions )
						mergeOptions.PreferExistingInstructions = true;
					if ( opts.PreferExistingOptions )
						mergeOptions.PreferExistingOptions = true;
					if ( opts.PreferExistingModLinks )
						mergeOptions.PreferExistingModLinkFilenames = true;

					// Use async merge to support URL validation with sequential flag
					using ( var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)) )
					{
						components = await ComponentMergeService.MergeInstructionSetsAsync(
							opts.ExistingPath,
							opts.IncomingPath,
							mergeOptions,
							downloadCache,
							sequential: !opts.Concurrent,
							cancellationToken: cts.Token);

						msg = $"Merged result contains {components.Count} unique components";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.LogVerbose(msg);
					}
				}
				catch ( Exception ex )
				{
					_errorCollector?.RecordError(
						ErrorCollector.ErrorCategory.General,
						null,
						"Failed to merge instruction sets",
						$"Existing: {opts.ExistingPath}, Incoming: {opts.IncomingPath}",
						ex);
					throw;
				}

				if ( opts.Download )
				{
					foreach ( var component in components )
					{
						component.IsSelected = true;
					}

					msg = "Downloading files for merged components...";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(msg);
					else
						Logger.Log(msg);

					using ( var downloadCts = new CancellationTokenSource(TimeSpan.FromHours(2)) )
					{
						downloadCache = await DownloadAllModFilesAsync(components, opts.SourcePath, opts.Verbose, sequential: !opts.Concurrent, downloadCts.Token);

						msg = "Download complete for all components";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.Log(msg);
					}
				}

				ApplySelectionFilters(components, opts.Select);

				// Create validation context to track issues for serialization
				var validationContext = new ComponentValidationContext();

				// Collect download failures from cache
				if ( downloadCache != null )
				{
					var failures = downloadCache.GetFailures();
					foreach ( var failure in failures )
					{
						validationContext.AddUrlFailure(failure.Url, failure.ErrorMessage);
					}
				}

				// Collect validation issues from error collector
				if ( _errorCollector != null )
				{
					foreach ( ErrorCollector.ErrorInfo error in _errorCollector.GetErrors() )
					{
						// Try to find the component by name
						var component = components.FirstOrDefault(c => c.Name == error.ComponentName);
						if ( component != null )
						{
							validationContext.AddModComponentIssue(component.Guid, error.Message);
						}
					}
				}

				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog("Serializing to output format...");
				else
					await Logger.LogVerboseAsync("Serializing to output format...");

				string output = ModComponentSerializationService.SerializeModComponentAsString(components, opts.Format, validationContext);

				if ( !string.IsNullOrEmpty(opts.OutputPath) )
				{
					string outputDir = Path.GetDirectoryName(opts.OutputPath);
					if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
					{
						Directory.CreateDirectory(outputDir);
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog($"Created output directory: {outputDir}");
						else
							await Logger.LogVerboseAsync($"Created output directory: {outputDir}");
					}

					File.WriteAllText(opts.OutputPath, output);

					string successMsg = $"✓ Merge completed successfully, saved to: {opts.OutputPath}";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(successMsg);
					else
						await Logger.LogVerboseAsync($"Merge completed successfully, saved to: {opts.OutputPath}");
				}
				else
				{
					_progressDisplay?.Dispose();
					_progressDisplay = null;

					await Logger.LogAsync(output);
					await Logger.LogVerboseAsync("Merge completed successfully (output to stdout)");
				}

				if ( downloadCache != null || _errorCollector != null )
				{
					LogAllErrors(downloadCache);
				}

				return 0;
			}
			catch ( Exception ex )
			{
				string errorMsg = $"Error during merge: {ex.Message}";
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog($"✗ {errorMsg}");
				else
					Logger.LogError(errorMsg);

				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}

				if ( downloadCache != null || _errorCollector != null )
				{
					LogAllErrors(downloadCache);
				}

				return 1;
			}
			finally
			{
				Console.CancelKeyPress -= cancelHandler;

				_progressDisplay?.Dispose();
				_progressDisplay = null;

				_globalDownloadCache = null;
				_errorCollector = null;
			}
		}

		private static async Task<int> RunValidateAsync(ValidateOptions opts)
		{
			SetVerboseMode(opts.Verbose);
			_errorCollector = new ErrorCollector();

			try
			{
				if ( !File.Exists(opts.InputPath) )
				{
					Console.Error.WriteLine($"Error: Input file not found: {opts.InputPath}");
					return 1;
				}

				if ( opts.FullValidation )
				{
					if ( string.IsNullOrEmpty(opts.GameDirectory) || string.IsNullOrEmpty(opts.SourceDirectory) )
					{
						Console.Error.WriteLine("Error: Full validation requires both --game-dir and --source-dir");
						return 1;
					}

					if ( !Directory.Exists(opts.GameDirectory) )
					{
						Console.Error.WriteLine($"Error: Game directory not found: {opts.GameDirectory}");
						return 1;
					}

					if ( !Directory.Exists(opts.SourceDirectory) )
					{
						Console.Error.WriteLine($"Error: Source directory not found: {opts.SourceDirectory}");
						return 1;
					}
				}

				Logger.Log($"Loading instruction file: {opts.InputPath}");

				List<ModComponent> components;
				try
				{
					components = await Core.Services.FileLoadingService.LoadFromFileAsync(opts.InputPath);

					// Handle dependency resolution
					components = HandleDependencyResolutionErrors(components, opts.IgnoreErrors, "Validate");
				}
				catch ( Exception ex )
				{
					Console.Error.WriteLine($"Error loading instruction file: {ex.Message}");
					if ( opts.Verbose )
					{
						Console.Error.WriteLine("Stack trace:");
						Console.Error.WriteLine(ex.StackTrace);
					}

					_errorCollector?.RecordError(
						ErrorCollector.ErrorCategory.FileOperation,
						null,
						"Failed to load instruction file",
						$"File: {opts.InputPath}",
						ex);

					return 1;
				}

				if ( components == null || components.Count == 0 )
				{
					Console.Error.WriteLine("Error: No components loaded from instruction file.");
					return 1;
				}

				Logger.Log($"Loaded {components.Count} component(s) from instruction file.");
				Logger.Log();

				if ( opts.FullValidation )
				{
					EnsureConfigInitialized();
					_config.sourcePath = new DirectoryInfo(opts.SourceDirectory);
					_config.destinationPath = new DirectoryInfo(opts.GameDirectory);
					_config.allComponents = components;
				}

				List<ModComponent> componentsToValidate = components;
				if ( opts.Select != null && opts.Select.Any() )
				{
					if ( !opts.ErrorsOnly )
						Logger.Log("Applying selection filters...");

					componentsToValidate = new List<ModComponent>(components);
					ApplySelectionFilters(componentsToValidate, opts.Select);
					componentsToValidate = componentsToValidate.Where(c => c.IsSelected).ToList();

					if ( componentsToValidate.Count == 0 )
					{
						Console.Error.WriteLine("Error: No components match the selection criteria.");
						return 1;
					}

					if ( !opts.ErrorsOnly )
						Logger.Log($"{componentsToValidate.Count} component(s) selected for validation.");
				}

				if ( opts.FullValidation )
				{
					if ( !opts.ErrorsOnly )
					{
						Logger.Log("Performing full environment validation...");
						Logger.Log(new string('-', 50));
					}

					(bool success, string message) = await Core.Services.InstallationService.ValidateInstallationEnvironmentAsync(_config);

					if ( !success )
					{
						Console.Error.WriteLine("Environment validation failed:");
						Console.Error.WriteLine(message);
						if ( !opts.ErrorsOnly )
							Logger.Log(new string('-', 50));
						return 1;
					}

					if ( !opts.ErrorsOnly )
					{
						Logger.Log("✓ Environment validation passed");
						Logger.Log(new string('-', 50));
						Logger.Log();
					}
				}

				if ( !opts.ErrorsOnly )
				{
					Logger.Log("Validating components...");
					Logger.Log(new string('=', 50));
				}

				int totalComponents = componentsToValidate.Count;
				int validComponents = 0;
				int componentsWithErrors = 0;
				int componentsWithWarnings = 0;
				var allErrors = new List<(ModComponent component, List<string> errors)>();
				var allWarnings = new List<(ModComponent component, List<string> warnings)>();

				foreach ( ModComponent component in componentsToValidate )
				{
					var validator = new ComponentValidation(component, components);
					bool isValid = validator.Run();

					List<string> errors = validator.GetErrors();
					List<string> warnings = validator.GetWarnings();

					if ( errors.Count > 0 )
					{
						componentsWithErrors++;
						allErrors.Add((component, errors));

						// Record validation errors in error collector
						foreach ( string error in errors )
						{
							_errorCollector?.RecordError(
								ErrorCollector.ErrorCategory.Validation,
								component.Name,
								error,
								null,
								null);
						}
					}
					else if ( warnings.Count > 0 )
					{
						componentsWithWarnings++;
						allWarnings.Add((component, warnings));
					}
					else
					{
						validComponents++;
					}

					if ( !opts.ErrorsOnly || errors.Count > 0 )
					{
						if ( isValid && errors.Count == 0 && warnings.Count == 0 )
						{
							if ( !opts.ErrorsOnly )
								Logger.Log($"✓ {component.Name}");
						}
						else
						{
							if ( errors.Count > 0 )
							{
								Logger.Log($"✗ {component.Name}");
								foreach ( string error in errors )
									Logger.Log($"    ERROR: {error}");
							}
							else if ( warnings.Count > 0 && !opts.ErrorsOnly )
							{
								Logger.Log($"⚠ {component.Name}");
								foreach ( string warning in warnings )
									Logger.Log($"    WARNING: {warning}");
							}
						}
					}
				}

				if ( !opts.ErrorsOnly )
				{
					Logger.Log(new string('=', 50));
					Logger.Log();
					Logger.Log("Validation Summary:");
					Logger.Log($"  Total components validated: {totalComponents}");
					Logger.Log($"  ✓ Valid: {validComponents}");
					if ( componentsWithWarnings > 0 )
						Logger.Log($"  ⚠ With warnings: {componentsWithWarnings}");
					if ( componentsWithErrors > 0 )
						Logger.Log($"  ✗ With errors: {componentsWithErrors}");
					Logger.Log();
				}

				if ( componentsWithErrors > 0 )
				{
					if ( opts.ErrorsOnly )
					{
						Logger.Log($"{componentsWithErrors} component(s) with errors");
					}
					else
					{
						Logger.Log("❌ Validation failed - errors found");
					}
					return 1;
				}
				else if ( componentsWithWarnings > 0 )
				{
					if ( !opts.ErrorsOnly )
						Logger.Log("⚠️ Validation passed with warnings");
					return 0;
				}
				else
				{
					if ( !opts.ErrorsOnly )
						Logger.Log("✅ All validations passed!");
					return 0;
				}
			}
			catch ( Exception ex )
			{
				Console.Error.WriteLine($"Error during validation: {ex.Message}");
				if ( opts.Verbose )
				{
					Console.Error.WriteLine("Stack trace:");
					Console.Error.WriteLine(ex.StackTrace);
				}
				return 1;
			}
		}

		private static async Task<int> RunInstallAsync(InstallOptions opts)
		{
			SetVerboseMode(opts.Verbose);
			_errorCollector = new ErrorCollector();

			try
			{
				if ( !File.Exists(opts.InputPath) )
				{
					Console.Error.WriteLine($"Error: Input file not found: {opts.InputPath}");
					return 1;
				}

				if ( !Directory.Exists(opts.GameDirectory) )
				{
					Console.Error.WriteLine($"Error: Game directory not found: {opts.GameDirectory}");
					return 1;
				}

				string sourceDir = opts.SourceDirectory;
				if ( string.IsNullOrEmpty(sourceDir) )
				{
					sourceDir = Path.GetDirectoryName(Path.GetFullPath(opts.InputPath));
					Logger.Log($"Using source directory: {sourceDir}");
				}

				if ( !Directory.Exists(sourceDir) )
				{
					Console.Error.WriteLine($"Error: Source directory not found: {sourceDir}");
					return 1;
				}

				EnsureConfigInitialized();
				_config.sourcePath = new DirectoryInfo(sourceDir);
				_config.destinationPath = new DirectoryInfo(opts.GameDirectory);

				Logger.Log($"Loading instruction file: {opts.InputPath}");

				List<ModComponent> components = await Core.Services.FileLoadingService.LoadFromFileAsync(opts.InputPath);

				// Handle dependency resolution
				components = HandleDependencyResolutionErrors(components, opts.IgnoreErrors, "Install");

				if ( components == null || components.Count == 0 )
				{
					Console.Error.WriteLine("Error: No components loaded from instruction file.");
					return 1;
				}

				_config.allComponents = components;
				Logger.Log($"Loaded {components.Count} component(s) from instruction file.");

				if ( opts.Select != null && opts.Select.Any() )
				{
					Logger.Log("Applying selection filters...");
					ApplySelectionFilters(components, opts.Select);
				}

				int selectedCount = components.Count(c => c.IsSelected);
				if ( selectedCount == 0 )
				{
					Console.Error.WriteLine("Error: No components selected for installation.");
					Console.Error.WriteLine("Use --select to specify components, or ensure components are marked as selected in the instruction file.");
					return 1;
				}

				Logger.Log($"{selectedCount} component(s) selected for installation.");
				Logger.Log();

				Logger.Log("Components to install:");
				int index = 1;
				foreach ( ModComponent component in components.Where(c => c.IsSelected) )
				{
					Logger.Log($"  {index}. {component.Name}");
					if ( !string.IsNullOrEmpty(component.Description) )
					{
						string desc = component.Description.Length > 80
							? component.Description.Substring(0, 77) + "..."
							: component.Description;
						Logger.Log($"     {desc}");
					}
					index++;
				}
				Logger.Log();

				if ( !opts.AutoConfirm )
				{
					Console.Write("Proceed with installation? [y/N]: ");
					string response = Console.ReadLine()?.Trim().ToLowerInvariant();
					if ( response != "y" && response != "yes" )
					{
						Logger.Log("Installation cancelled by user.");
						return 0;
					}
				}

				if ( !opts.SkipValidation )
				{
					Logger.Log("Validating installation environment...");
					(bool success, string message) = await Core.Services.InstallationService.ValidateInstallationEnvironmentAsync(
						_config,
						(confirmMessage) =>
						{
							if ( opts.AutoConfirm )
								return Task.FromResult<bool?>(true);
							Console.Write($"{confirmMessage} [y/N]: ");
							string response = Console.ReadLine()?.Trim().ToLowerInvariant();
							bool? result = response == "y" || response == "yes";
							return Task.FromResult(result);
						}
					);

					if ( !success )
					{
						Console.Error.WriteLine("Validation failed:");
						Console.Error.WriteLine(message);
						return 1;
					}
					Logger.Log("Validation passed.");
					Logger.Log();
				}

				Logger.Log("Starting installation...");
				Logger.Log(new string('=', 50));

				ModComponent.InstallExitCode exitCode = await Core.Services.InstallationService.InstallAllSelectedComponentsAsync(
					components,
					(currentIndex, total, componentName) =>
					{
						Logger.Log($"[{currentIndex + 1}/{total}] Installing: {componentName}");
					}
				);

				Logger.Log(new string('=', 50));

				if ( exitCode == ModComponent.InstallExitCode.Success )
				{
					Logger.Log("Installation completed successfully!");
					return 0;
				}
				else
				{
					Console.Error.WriteLine($"Installation failed with exit code: {exitCode}");
					Console.Error.WriteLine("Check the logs above for more details.");
					return 1;
				}
			}
			catch ( Exception ex )
			{
				Console.Error.WriteLine($"Error during installation: {ex.Message}");
				if ( opts.Verbose )
				{
					Console.Error.WriteLine("Stack trace:");
					Console.Error.WriteLine(ex.StackTrace);
				}

				_errorCollector?.RecordError(
					ErrorCollector.ErrorCategory.Installation,
					null,
					"Installation failed with exception",
					$"Input: {opts.InputPath}, Game Dir: {opts.GameDirectory}",
					ex);

				LogAllErrors(null, forceConsoleOutput: true);

				return 1;
			}
		}

		private static async Task<int> RunSetNexusApiKeyAsync(SetNexusApiKeyOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			try
			{
				EnsureConfigInitialized();

				Logger.Log("Setting Nexus Mods API key...");
				Logger.Log($"API Key: {opts.ApiKey.Substring(0, Math.Min(10, opts.ApiKey.Length))}...");

				if ( !opts.SkipValidation )
				{
					Logger.Log("\nValidating API key with Nexus Mods...");
					(bool isValid, string message) = await Services.Download.NexusModsDownloadHandler.ValidateApiKeyAsync(opts.ApiKey);

					if ( !isValid )
					{
						Logger.LogError($"API key validation failed: {message}");
						Logger.Log($"\n❌ Validation failed: {message}");
						return 1;
					}

					Logger.Log($"\n✓ {message}");
				}
				else
				{
					Logger.LogWarning("Skipping API key validation");
					Logger.Log("Skipping validation (--skip-validation specified)");
				}

				_config.nexusModsApiKey = opts.ApiKey;
				Logger.Log("API key stored in MainConfig");

				SaveSettings();

				string settingsPath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"KOTORModSync",
					"settings.json"
				);

				Logger.Log($"API key saved to: {settingsPath}");

				Logger.Log($"\n✓ Nexus Mods API key set successfully!");
				Logger.Log($"Settings file: {settingsPath}");
				Logger.Log("\nYou can now use the download command to automatically download mods from Nexus Mods.");
				Logger.Log("This setting is shared with the KOTORModSync GUI application.");

				return 0;
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Error setting Nexus Mods API key: {ex.Message}");
				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}
				Logger.Log($"\n❌ Error: {ex.Message}");
				return 1;
			}
		}

		private static async Task<int> RunInstallPythonDepsAsync(InstallPythonDepsOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			try
			{
				// Disable keyring to prevent pip from hanging
				Environment.SetEnvironmentVariable("PYTHON_KEYRING_BACKEND", "keyring.backends.null.Keyring");
				Environment.SetEnvironmentVariable("PIP_NO_INPUT", "1");
				Environment.SetEnvironmentVariable("PIP_DISABLE_PIP_VERSION_CHECK", "1");

				Logger.Log("Installing Python dependencies for HoloPatcher...");
				Logger.Log("This may take several minutes on first run...");

				var startTime = DateTime.Now;

				// Setup Python environment
				Logger.Log("Setting up Python environment...");
				await Python.Included.Installer.SetupPython();

				Logger.Log("Initializing Python engine...");
				Logger.Log("[DEBUG] About to call PythonEngine.Initialize()");
				Python.Runtime.PythonEngine.Initialize();
				Logger.Log("[DEBUG] PythonEngine.Initialize() completed");

				// Check if dependencies are already installed
				bool dependenciesInstalled = false;
				if ( !opts.Force )
				{
					try
					{
						Logger.Log("Checking if dependencies are already installed...");
						Logger.Log("[DEBUG] Acquiring GIL...");
						using ( Python.Runtime.Py.GIL() )
						{
							Logger.Log("[DEBUG] GIL acquired, importing loggerplus...");
							Python.Runtime.Py.Import("loggerplus");
							Logger.Log("[DEBUG] loggerplus imported, importing ply...");
							Python.Runtime.Py.Import("ply");
							Logger.Log("[DEBUG] ply imported");
							dependenciesInstalled = true;
							Logger.Log("✓ Python dependencies already installed.");
						}
						Logger.Log("[DEBUG] GIL released");
					}
					catch ( Python.Runtime.PythonException ex )
					{
						Logger.Log($"[DEBUG] Import failed: {ex.Message}");
						Logger.Log("Dependencies not found, will install...");
						dependenciesInstalled = false;
					}
				}

				if ( !dependenciesInstalled )
				{
					Logger.Log("[DEBUG] ===== STARTING DEPENDENCY INSTALLATION =====");
					Logger.Log("Installing dependencies using Python.NET (bypassing Python.Included's buggy RunCommand)...");

					Logger.Log("[DEBUG] About to acquire GIL for installation");
					using ( Python.Runtime.Py.GIL() )
					{
						Logger.Log("[DEBUG] GIL acquired for installation");

						// First ensure pip is available
						try
						{
							Logger.Log("[DEBUG] About to import ensurepip...");
							dynamic ensurepip = Python.Runtime.Py.Import("ensurepip");
							Logger.Log("[DEBUG] ensurepip imported, calling _bootstrap()...");
							ensurepip._bootstrap(upgrade: true);
							Logger.Log("✓ Pip bootstrapped");
						}
						catch ( Python.Runtime.PythonException ex )
						{
							Logger.Log($"[DEBUG] ensurepip exception: {ex.Message}");
							Logger.Log($"ensurepip note: {ex.Message} (pip may already exist)");
						}

						// Install loggerplus using pip's internal API
						Logger.Log("[DEBUG] ===== INSTALLING LOGGERPLUS =====");
						Logger.Log("Installing loggerplus using pip._internal.main()...");
						try
						{
							Logger.Log("[DEBUG] Importing pip._internal...");
							dynamic pip_internal = Python.Runtime.Py.Import("pip._internal");
							Logger.Log("[DEBUG] pip._internal imported, getting main function...");
							dynamic pipMain = pip_internal.main;
							Logger.Log("[DEBUG] Got pipMain, creating args list...");

							using ( dynamic args = new Python.Runtime.PyList() )
							{
								Logger.Log("[DEBUG] Created PyList, appending 'install'...");
								args.Append(new Python.Runtime.PyString("install"));
								Logger.Log("[DEBUG] Appending 'loggerplus'...");
								args.Append(new Python.Runtime.PyString("loggerplus"));

								Logger.Log("[DEBUG] About to call pipMain(args)...");
								Logger.Log("[DEBUG] THIS IS WHERE IT MIGHT HANG...");
								pipMain(args);
								Logger.Log("[DEBUG] pipMain() returned!");
							}
							Logger.Log("✓ loggerplus installed");
						}
						catch ( Python.Runtime.PythonException ex )
						{
							Logger.Log($"[DEBUG] PythonException caught: {ex.Message}");
							// pip._internal.main() raises SystemExit on success
							if ( ex.Message.Contains("SystemExit") && ex.Message.Contains("0") )
							{
								Logger.Log("✓ loggerplus installed (exit 0)");
							}
							else
							{
								Logger.LogError($"Failed to install loggerplus: {ex.Message}");
								return 1;
							}
						}

						// Install ply
						Logger.Log("[DEBUG] ===== INSTALLING PLY =====");
						Logger.Log("Installing ply using pip._internal.main()...");
						try
						{
							Logger.Log("[DEBUG] Importing pip._internal for ply...");
							dynamic pip_internal = Python.Runtime.Py.Import("pip._internal");
							dynamic pipMain = pip_internal.main;
							Logger.Log("[DEBUG] Creating args for ply...");

							using ( dynamic args = new Python.Runtime.PyList() )
							{
								args.Append(new Python.Runtime.PyString("install"));
								args.Append(new Python.Runtime.PyString("ply"));

								Logger.Log("[DEBUG] Calling pipMain for ply...");
								pipMain(args);
								Logger.Log("[DEBUG] pipMain for ply returned!");
							}
							Logger.Log("✓ ply installed");
						}
						catch ( Python.Runtime.PythonException ex )
						{
							Logger.Log($"[DEBUG] ply PythonException: {ex.Message}");
							if ( ex.Message.Contains("SystemExit") && ex.Message.Contains("0") )
							{
								Logger.Log("✓ ply installed (exit 0)");
							}
							else
							{
								Logger.LogError($"Failed to install ply: {ex.Message}");
								return 1;
							}
						}
					}
					Logger.Log("[DEBUG] GIL released after installation");
					Logger.Log("[DEBUG] ===== INSTALLATION COMPLETE =====");

					Logger.Log("Python dependencies installation completed.");
					Logger.Log("Skipping verification to avoid potential GIL issues.");
				}

				var elapsed = DateTime.Now - startTime;
				Logger.Log($"Python dependencies setup completed in {elapsed.TotalSeconds:F1} seconds.");
				Logger.Log("[DEBUG] ===== SHUTTING DOWN PYTHON ENGINE =====");

				// Shutdown Python engine to prevent hanging
				try
				{
					if ( Python.Runtime.PythonEngine.IsInitialized )
					{
						Logger.Log("[DEBUG] Calling PythonEngine.Shutdown()...");
						Python.Runtime.PythonEngine.Shutdown();
						Logger.Log("[DEBUG] PythonEngine.Shutdown() completed");
					}
				}
				catch ( Exception shutdownEx )
				{
					Logger.Log($"[DEBUG] Shutdown warning: {shutdownEx.Message}");
				}

				Logger.Log("[DEBUG] ===== EXITING SUCCESSFULLY =====");

				return 0;
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Error installing Python dependencies: {ex.Message}");
				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}
				return 1;
			}
		}

		private static async Task<int> RunHolopatcherAsync(HolopatcherOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			try
			{
				Logger.Log("Launching HoloPatcher...");
				Logger.Log($"Arguments: {(string.IsNullOrEmpty(opts.Arguments) ? "(none)" : opts.Arguments)}");

				string baseDir = Core.Utility.Utility.GetBaseDirectory();
				string resourcesDir = Core.Utility.Utility.GetResourcesDirectory(baseDir);

				Logger.Log($"[DEBUG] Base directory: {baseDir}");
				Logger.Log($"[DEBUG] Resources directory: {resourcesDir}");

				// Find holopatcher
				var (holopatcherPath, usePythonVersion, found) = await Services.InstallationService.FindHolopatcherAsync(resourcesDir, baseDir);

				if ( !found )
				{
					Logger.LogError("HoloPatcher not found in Resources directory.");
					Logger.Log("Please ensure PyKotor/HoloPatcher is installed correctly.");
					return 1;
				}

				Logger.Log($"Found HoloPatcher at: {holopatcherPath}");
				Logger.Log($"Using Python version: {usePythonVersion}");

				// Run holopatcher
				int exitCode;
				string stdout;
				string stderr;

				if ( usePythonVersion )
				{
					Logger.Log("Running HoloPatcher via Python.NET...");
					(exitCode, stdout, stderr) = await Services.InstallationService.RunHolopatcherPyAsync(holopatcherPath, opts.Arguments ?? "");
				}
				else
				{
					Logger.Log("Running HoloPatcher executable...");
					(exitCode, stdout, stderr) = await Core.Utility.PlatformAgnosticMethods.ExecuteProcessAsync(holopatcherPath, opts.Arguments ?? "");
				}

				if ( !string.IsNullOrEmpty(stdout) )
				{
					Logger.Log("=== STDOUT ===");
					Logger.Log(stdout);
				}

				if ( !string.IsNullOrEmpty(stderr) )
				{
					Logger.LogError("=== STDERR ===");
					Logger.LogError(stderr);
				}

				if ( exitCode == 0 )
				{
					Logger.Log("✓ HoloPatcher completed successfully");
					return 0;
				}
				else
				{
					Logger.LogError($"HoloPatcher exited with code {exitCode}");
					return exitCode;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Error running HoloPatcher: {ex.Message}");
				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}
				return 1;
			}
		}
	}
}
