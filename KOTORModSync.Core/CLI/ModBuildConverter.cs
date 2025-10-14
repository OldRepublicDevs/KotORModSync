// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using JetBrains.Annotations;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;

namespace KOTORModSync.Core.CLI
{
	public static class ModBuildConverter
	{
		private static MainConfig _config;
		private static ConsoleProgressDisplay _progressDisplay;

		private static void EnsureConfigInitialized()
		{
			if ( _config == null )
			{
				_config = new MainConfig();
				Logger.LogVerbose("MainConfig initialized");

				// Load settings from persistent storage (settings.json)
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
							// Apply settings to MainConfig
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

				// Fallback: Try to load from legacy nexusmods.config if no API key in settings.json
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

								// Migrate to settings.json
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

				// Load existing settings to preserve values we haven't modified
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

				// Update with current MainConfig values
				// Preserve Theme if it exists, otherwise use default
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

		// Internal class for JSON serialization (matches AppSettings structure)
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

		// Base options shared by all commands
		public class BaseOptions
		{
			[Option('v', "verbose", Required = false, HelpText = "Enable verbose output for debugging.")]
			public bool Verbose { get; set; }
		}

		// Convert/Merge command options
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

			// Merge-specific options
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
		}

		// Validate command options
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
		}

		// Install command options
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
		}

		// Set Nexus Mods API key command options
		[Verb("set-nexus-api-key", HelpText = "Set and validate your Nexus Mods API key")]
		public class SetNexusApiKeyOptions : BaseOptions
		{
			[Value(0, Required = true, MetaName = "api-key", HelpText = "Your Nexus Mods API key")]
			public string ApiKey { get; set; }

			[Option("skip-validation", Required = false, Default = false, HelpText = "Skip API key validation")]
			public bool SkipValidation { get; set; }
		}

		public static int Run(string[] args)
		{
			Logger.Initialize();

			var parser = new Parser(with => with.HelpWriter = Console.Out);

			return parser.ParseArguments<ConvertOptions, ValidateOptions, InstallOptions, SetNexusApiKeyOptions>(args)
			.MapResult(
				(ConvertOptions opts) => RunConvertAsync(opts).GetAwaiter().GetResult(),
				(ValidateOptions opts) => RunValidateAsync(opts).GetAwaiter().GetResult(),
				(InstallOptions opts) => RunInstallAsync(opts).GetAwaiter().GetResult(),
				(SetNexusApiKeyOptions opts) => RunSetNexusApiKeyAsync(opts).GetAwaiter().GetResult(),
				errs => 1);
		}

		private static void SetVerboseMode(bool verbose)
		{
			// Set MainConfig.DebugLogging via the instance property
			var config = new MainConfig { debugLogging = verbose };
		}

		private static async Task DownloadAllModFilesAsync(List<ModComponent> components, string destinationDirectory, bool verbose)
		{
			// Count components with URLs
			int componentCount = components.Count(c => c.ModLink != null && c.ModLink.Count > 0);
			if ( componentCount == 0 )
			{
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog("No components with URLs found to download");
				else
					Logger.LogVerbose("No components with URLs found to download");
				return;
			}

			string message = $"Processing {componentCount} component(s) for download...";
			if ( _progressDisplay != null )
				_progressDisplay.WriteScrollingLog(message);
			else
				Logger.Log(message);

			// Create download cache service
			var downloadCache = new DownloadCacheService();

			// Create download handlers with API key
			var httpClient = new HttpClient();
			var handlers = new List<IDownloadHandler>
			{
				new DeadlyStreamDownloadHandler(httpClient),
				new MegaDownloadHandler(),
				new NexusModsDownloadHandler(httpClient, _config.nexusModsApiKey),
				new DirectDownloadHandler(httpClient)
			};
			var downloadManager = new DownloadManager(handlers);

			// Set download manager in cache service
			downloadCache.SetDownloadManager(downloadManager);

			// Track last logged progress for each download to throttle updates
			var lastLoggedProgress = new Dictionary<string, double>();

			// Create progress reporter for console output
			var progressReporter = new Progress<DownloadProgress>(progress =>
			{
				string fileName = Path.GetFileName(progress.FilePath ?? progress.Url);
				string progressKey = $"{progress.ModName}:{fileName}";

				if ( progress.Status == DownloadStatus.InProgress )
				{
					if ( _progressDisplay != null )
					{
						// Update dynamic progress display
						string displayText = $"{progress.ModName}: {fileName}";
						_progressDisplay.UpdateProgress(progressKey, displayText, progress.ProgressPercentage, "downloading");
					}
					else if ( verbose )
					{
						// Fallback to traditional logging
						bool shouldLog = false;
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
							Logger.LogVerbose($"[Download] {progress.ModName}: {progress.ProgressPercentage:F1}% - {fileName}");
							lastLoggedProgress[progressKey] = progress.ProgressPercentage;
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
					lastLoggedProgress.Remove(progressKey);
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
					lastLoggedProgress.Remove(progressKey);
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
				int successCount = 0;
				int failCount = 0;

				// Process each component using the cache service
				foreach ( var component in components.Where(c => c.ModLink != null && c.ModLink.Count > 0) )
				{
					Logger.LogVerbose($"[Download] Processing component: {component.Name} ({component.ModLink.Count} URL(s))");

					try
					{
						// Use DownloadCacheService which handles caching and file existence checks
						var results = await downloadCache.ResolveOrDownloadAsync(
							component,
							destinationDirectory,
							progressReporter,
							CancellationToken.None);

						// Count results based on cache entries
						foreach ( var entry in results )
						{
							string filePath = MainConfig.SourcePath != null
								? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
								: Path.Combine(destinationDirectory, entry.FileName);

							if ( File.Exists(filePath) )
							{
								successCount++;
							}
						}
					}
					catch ( Exception ex )
					{
						string errorMsg = $"[Download] Error processing component {component.Name}: {ex.Message}";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog($"✗ {errorMsg}");
						else
							Logger.LogError(errorMsg);

						if ( verbose )
						{
							Logger.LogException(ex);
						}
						failCount++;
					}
				}

				string summaryMsg = $"Download results: {successCount} files available, {failCount} failed";
				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog(summaryMsg);
				else
					Logger.Log(summaryMsg);

				if ( failCount > 0 )
				{
					string warningMsg = "Some downloads failed. Check logs for details.";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog($"⚠ {warningMsg}");
					else
						Logger.LogWarning(warningMsg);
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
				throw;
			}
		}

		private static void ApplySelectionFilters(
			List<ModComponent> components,
			IEnumerable<string> selections)
		{
			if ( components == null )
				return;

			if ( selections == null || !selections.Any() )
			{
				// Select all if no selections
				foreach ( var component in components )
				{
					component.IsSelected = true;
				}
				return;
			}

			var selectedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var selectedTiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Parse selections
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

			// Define tier priority mapping (lower number = higher priority)
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

				// Check category match
				if ( selectedCategories.Count > 0 )
				{
					if ( component.Category != null && component.Category.Count > 0 )
					{
						includeByCategory = component.Category.Any(cat => selectedCategories.Contains(cat));
					}
				}
				else
				{
					// No category filter specified, include all
					includeByCategory = true;
				}

				// Check tier match
				if ( selectedTiers.Count > 0 )
				{
					if ( !string.IsNullOrEmpty(component.Tier) )
					{
						// Include this tier and all higher priority tiers
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
								// Direct tier match for non-standard tiers
								includeByTier = true;
								break;
							}
						}
					}
				}
				else
				{
					// No tier filter specified, include all
					includeByTier = true;
				}

				// Set isSelected according to filters (AND logic)
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

			// Initialize progress display for dynamic terminal output
			_progressDisplay = new ConsoleProgressDisplay();

			try
			{
				// Initialize config first
				EnsureConfigInitialized();

				// Override API key if provided via command line
				if ( !string.IsNullOrWhiteSpace(opts.NexusModsApiKey) )
				{
					_config.nexusModsApiKey = opts.NexusModsApiKey;
					Logger.LogVerbose("Using Nexus Mods API key from command line argument");
				}

				// Validate merge vs convert mode
				if ( opts.Merge )
				{
					// Merge mode validation
					if ( string.IsNullOrEmpty(opts.ExistingPath) || string.IsNullOrEmpty(opts.IncomingPath) )
					{
						Logger.LogError("--merge requires both --existing and --incoming to be specified");
						Console.WriteLine("Usage: convert --merge --existing <file> --incoming <file> [options]");
						return 1;
					}

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
				}
				else
				{
					// Convert mode validation
					if ( string.IsNullOrEmpty(opts.InputPath) )
					{
						Logger.LogError("--input is required for convert mode (or use --merge for merge mode)");
						Console.WriteLine("Usage: convert --input <file> [options]");
						Console.WriteLine("   OR: convert --merge --existing <file> --incoming <file> [options]");
						return 1;
					}

					if ( !File.Exists(opts.InputPath) )
					{
						Logger.LogError($"Input file not found: {opts.InputPath}");
						return 1;
					}

					Logger.LogVerbose($"Convert mode: {opts.InputPath}");
				}

				Logger.LogVerbose($"Output format: {opts.Format}");

				// Validate download requirements
				if ( opts.Download && string.IsNullOrEmpty(opts.SourcePath) )
				{
					Logger.LogError("--download requires --source-path to be specified");
					return 1;
				}

				// Set source path if provided
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

				// Load components based on mode
				List<ModComponent> components;
				if ( opts.Merge )
				{
					// If download is enabled, download files BEFORE merging
					if ( opts.Download )
					{
						string msg = "Download mode enabled - loading instruction sets separately for download...";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.LogVerbose(msg);

						// Load existing components
						msg = "Loading existing instruction set...";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.LogVerbose(msg);

						List<ModComponent> existingComponents = FileLoadingService.LoadFromFile(opts.ExistingPath);
						
						msg = $"Loaded {existingComponents.Count} components from existing";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.LogVerbose(msg);

						foreach ( var component in existingComponents )
						{
							component.IsSelected = true;
						}

						// Load incoming components
						msg = "Loading incoming instruction set...";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.LogVerbose(msg);

						List<ModComponent> incomingComponents = FileLoadingService.LoadFromFile(opts.IncomingPath);
						
						msg = $"Loaded {incomingComponents.Count} components from incoming";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.LogVerbose(msg);

						foreach ( var component in incomingComponents )
						{
							component.IsSelected = true;
						}

						// Download files for existing components
						msg = "Downloading files for EXISTING instruction set...";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.Log(msg);

						await DownloadAllModFilesAsync(existingComponents, opts.SourcePath, opts.Verbose);
						
						msg = "Download complete for existing components";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.Log(msg);

						// Download files for incoming components
						msg = "Downloading files for INCOMING instruction set...";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.Log(msg);

						await DownloadAllModFilesAsync(incomingComponents, opts.SourcePath, opts.Verbose);
						
						msg = "Download complete for incoming components";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.Log(msg);
					}

					// Now perform the merge
					Logger.LogVerbose("Merging instruction sets...");
					var mergeOptions = new Services.MergeOptions
					{
						ExcludeExistingOnly = opts.ExcludeExistingOnly,
						ExcludeIncomingOnly = opts.ExcludeIncomingOnly,
						UseExistingOrder = opts.UseExistingOrder, // Inverted: default is now incoming order
						HeuristicsOptions = MergeHeuristicsOptions.CreateDefault()
					};

					components = ComponentMergeService.MergeInstructionSets(
						opts.ExistingPath,
						opts.IncomingPath,
						MergeStrategy.ByNameAndAuthor,
						mergeOptions);

					Logger.LogVerbose($"Merged result contains {components.Count} components");
				}
				else
				{
					string msg = "Loading components from input file...";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(msg);
					else
						Logger.LogVerbose(msg);

					components = FileLoadingService.LoadFromFile(opts.InputPath);
					
					msg = $"Loaded {components.Count} components";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(msg);
					else
						Logger.LogVerbose(msg);

					// Download files if requested (non-merge mode)
					if ( opts.Download )
					{
						msg = "Starting download of mod files...";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.Log(msg);

						await DownloadAllModFilesAsync(components, opts.SourcePath, opts.Verbose);
						
						msg = "Download complete";
						if ( _progressDisplay != null )
							_progressDisplay.WriteScrollingLog(msg);
						else
							Logger.Log(msg);
					}
				}

				if ( opts.AutoGenerate )
				{
					string message = "Auto-generating instructions from URLs...";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(message);
					else
						Logger.LogVerbose(message);

					var downloadCache = new Services.DownloadCacheService();
					downloadCache.SetDownloadManager();

					int totalComponents = components.Count(c => c.ModLink != null && c.ModLink.Count > 0);
					message = $"Processing {totalComponents} components sequentially...";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(message);
					else
						Logger.LogVerbose(message);

					// Process all components sequentially (no concurrency)
					int successCount = 0;
					int currentIndex = 0;
					foreach ( var component in components.Where(c => c.ModLink != null && c.ModLink.Count > 0) )
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

						bool success = await Services.AutoInstructionGenerator.GenerateInstructionsFromUrlsAsync(
							component, downloadCache);

						if ( _progressDisplay != null )
						{
							_progressDisplay.RemoveProgress(progressKey);
						}

						if ( success )
						{
							if ( _progressDisplay != null )
								_progressDisplay.WriteScrollingLog($"✓ {component.Name}");
							else
								Logger.LogVerbose($"Auto-generation successful for component: {component.Name}");
							successCount++;
						}
						else
						{
							if ( _progressDisplay != null )
								_progressDisplay.WriteScrollingLog($"✗ Failed: {component.Name}");
						}
					}

					message = $"Auto-generation complete: {successCount}/{totalComponents} components processed successfully";
					if ( _progressDisplay != null )
						_progressDisplay.WriteScrollingLog(message);
					else
						Logger.LogVerbose(message);
				}

				// Apply selection filters
				ApplySelectionFilters(components, opts.Select);

				if ( _progressDisplay != null )
					_progressDisplay.WriteScrollingLog("Serializing to output format...");
				else
					Logger.LogVerbose("Serializing to output format...");

				string output = ModComponentSerializationService.SaveToString(components, opts.Format);

				if ( !string.IsNullOrEmpty(opts.OutputPath) )
				{
					// Write to file
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
					// Clean up progress display before writing to stdout
					_progressDisplay?.Dispose();
					_progressDisplay = null;

					// Write to stdout
					Console.WriteLine(output);
					Logger.LogVerbose("Conversion completed successfully (output to stdout)");
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
				return 1;
			}
			finally
			{
				// Clean up progress display
				_progressDisplay?.Dispose();
				_progressDisplay = null;
			}
		}

		private static async Task<int> RunValidateAsync(ValidateOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			try
			{
				// Validate input file exists
				if ( !File.Exists(opts.InputPath) )
				{
					Console.Error.WriteLine($"Error: Input file not found: {opts.InputPath}");
					return 1;
				}

				// Check for full validation requirements
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

				Console.WriteLine($"Loading instruction file: {opts.InputPath}");

				// Load instruction file
				List<ModComponent> components;
				try
				{
					components = await Core.Services.FileLoadingService.LoadFromFileAsync(opts.InputPath);
				}
				catch ( Exception ex )
				{
					Console.Error.WriteLine($"Error loading instruction file: {ex.Message}");
					if ( opts.Verbose )
					{
						Console.Error.WriteLine("Stack trace:");
						Console.Error.WriteLine(ex.StackTrace);
					}
					return 1;
				}

				if ( components == null || components.Count == 0 )
				{
					Console.Error.WriteLine("Error: No components loaded from instruction file.");
					return 1;
				}

				Console.WriteLine($"Loaded {components.Count} component(s) from instruction file.");
				Console.WriteLine();

				// Initialize configuration if needed for full validation
				if ( opts.FullValidation )
				{
					EnsureConfigInitialized();
					_config.sourcePath = new DirectoryInfo(opts.SourceDirectory);
					_config.destinationPath = new DirectoryInfo(opts.GameDirectory);
					_config.allComponents = components;
				}

				// Apply selection filters if specified
				List<ModComponent> componentsToValidate = components;
				if ( opts.Select != null && opts.Select.Any() )
				{
					if ( !opts.ErrorsOnly )
						Console.WriteLine("Applying selection filters...");

					// Clone the list and apply filters
					componentsToValidate = new List<ModComponent>(components);
					ApplySelectionFilters(componentsToValidate, opts.Select);
					componentsToValidate = componentsToValidate.Where(c => c.IsSelected).ToList();

					if ( componentsToValidate.Count == 0 )
					{
						Console.Error.WriteLine("Error: No components match the selection criteria.");
						return 1;
					}

					if ( !opts.ErrorsOnly )
						Console.WriteLine($"{componentsToValidate.Count} component(s) selected for validation.");
				}

				// Perform full environment validation if requested
				if ( opts.FullValidation )
				{
					if ( !opts.ErrorsOnly )
					{
						Console.WriteLine("Performing full environment validation...");
						Console.WriteLine(new string('-', 50));
					}

					(bool success, string message) = await Core.Services.InstallationService.ValidateInstallationEnvironmentAsync(_config);

					if ( !success )
					{
						Console.Error.WriteLine("Environment validation failed:");
						Console.Error.WriteLine(message);
						if ( !opts.ErrorsOnly )
							Console.WriteLine(new string('-', 50));
						return 1;
					}

					if ( !opts.ErrorsOnly )
					{
						Console.WriteLine("✓ Environment validation passed");
						Console.WriteLine(new string('-', 50));
						Console.WriteLine();
					}
				}

				// Validate individual components
				if ( !opts.ErrorsOnly )
				{
					Console.WriteLine("Validating components...");
					Console.WriteLine(new string('=', 50));
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

					// Display component validation results
					if ( !opts.ErrorsOnly || errors.Count > 0 )
					{
						if ( isValid && errors.Count == 0 && warnings.Count == 0 )
						{
							if ( !opts.ErrorsOnly )
								Console.WriteLine($"✓ {component.Name}");
						}
						else
						{
							if ( errors.Count > 0 )
							{
								Console.WriteLine($"✗ {component.Name}");
								foreach ( string error in errors )
									Console.WriteLine($"    ERROR: {error}");
							}
							else if ( warnings.Count > 0 && !opts.ErrorsOnly )
							{
								Console.WriteLine($"⚠ {component.Name}");
								foreach ( string warning in warnings )
									Console.WriteLine($"    WARNING: {warning}");
							}
						}
					}
				}

				// Summary
				if ( !opts.ErrorsOnly )
				{
					Console.WriteLine(new string('=', 50));
					Console.WriteLine();
					Console.WriteLine("Validation Summary:");
					Console.WriteLine($"  Total components validated: {totalComponents}");
					Console.WriteLine($"  ✓ Valid: {validComponents}");
					if ( componentsWithWarnings > 0 )
						Console.WriteLine($"  ⚠ With warnings: {componentsWithWarnings}");
					if ( componentsWithErrors > 0 )
						Console.WriteLine($"  ✗ With errors: {componentsWithErrors}");
					Console.WriteLine();
				}

				// Return appropriate exit code
				if ( componentsWithErrors > 0 )
				{
					if ( opts.ErrorsOnly )
					{
						// In errors-only mode, just show the count
						Console.WriteLine($"{componentsWithErrors} component(s) with errors");
					}
					else
					{
						Console.WriteLine("❌ Validation failed - errors found");
					}
					return 1;
				}
				else if ( componentsWithWarnings > 0 )
				{
					if ( !opts.ErrorsOnly )
						Console.WriteLine("⚠️ Validation passed with warnings");
					return 0;
				}
				else
				{
					if ( !opts.ErrorsOnly )
						Console.WriteLine("✅ All validations passed!");
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

			try
			{
				// Validate input file exists
				if ( !File.Exists(opts.InputPath) )
				{
					Console.Error.WriteLine($"Error: Input file not found: {opts.InputPath}");
					return 1;
				}

				// Validate game directory exists
				if ( !Directory.Exists(opts.GameDirectory) )
				{
					Console.Error.WriteLine($"Error: Game directory not found: {opts.GameDirectory}");
					return 1;
				}

				// Set source directory (default to input file directory if not specified)
				string sourceDir = opts.SourceDirectory;
				if ( string.IsNullOrEmpty(sourceDir) )
				{
					sourceDir = Path.GetDirectoryName(Path.GetFullPath(opts.InputPath));
					Console.WriteLine($"Using source directory: {sourceDir}");
				}

				if ( !Directory.Exists(sourceDir) )
				{
					Console.Error.WriteLine($"Error: Source directory not found: {sourceDir}");
					return 1;
				}

				// Initialize configuration
				EnsureConfigInitialized();
				_config.sourcePath = new DirectoryInfo(sourceDir);
				_config.destinationPath = new DirectoryInfo(opts.GameDirectory);

				Console.WriteLine($"Loading instruction file: {opts.InputPath}");

				// Load instruction file (auto-detects format based on extension)
				List<ModComponent> components = await Core.Services.FileLoadingService.LoadFromFileAsync(opts.InputPath);

				if ( components == null || components.Count == 0 )
				{
					Console.Error.WriteLine("Error: No components loaded from instruction file.");
					return 1;
				}

				_config.allComponents = components;
				Console.WriteLine($"Loaded {components.Count} component(s) from instruction file.");

				// Apply selection filters if specified
				if ( opts.Select != null && opts.Select.Any() )
				{
					Console.WriteLine("Applying selection filters...");
					ApplySelectionFilters(components, opts.Select);
				}

				// Count selected components
				int selectedCount = components.Count(c => c.IsSelected);
				if ( selectedCount == 0 )
				{
					Console.Error.WriteLine("Error: No components selected for installation.");
					Console.Error.WriteLine("Use --select to specify components, or ensure components are marked as selected in the instruction file.");
					return 1;
				}

				Console.WriteLine($"{selectedCount} component(s) selected for installation.");
				Console.WriteLine();

				// List selected components
				Console.WriteLine("Components to install:");
				int index = 1;
				foreach ( ModComponent component in components.Where(c => c.IsSelected) )
				{
					Console.WriteLine($"  {index}. {component.Name}");
					if ( !string.IsNullOrEmpty(component.Description) )
					{
						string desc = component.Description.Length > 80
							? component.Description.Substring(0, 77) + "..."
							: component.Description;
						Console.WriteLine($"     {desc}");
					}
					index++;
				}
				Console.WriteLine();

				// Confirm installation unless auto-confirm is enabled
				if ( !opts.AutoConfirm )
				{
					Console.Write("Proceed with installation? [y/N]: ");
					string response = Console.ReadLine()?.Trim().ToLowerInvariant();
					if ( response != "y" && response != "yes" )
					{
						Console.WriteLine("Installation cancelled by user.");
						return 0;
					}
				}

				// Validate installation environment
				if ( !opts.SkipValidation )
				{
					Console.WriteLine("Validating installation environment...");
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
					Console.WriteLine("Validation passed.");
					Console.WriteLine();
				}

				// Install components
				Console.WriteLine("Starting installation...");
				Console.WriteLine(new string('=', 50));

				ModComponent.InstallExitCode exitCode = await Core.Services.InstallationService.InstallAllSelectedComponentsAsync(
					components,
					(currentIndex, total, componentName) =>
					{
						Console.WriteLine($"[{currentIndex + 1}/{total}] Installing: {componentName}");
					}
				);

				Console.WriteLine(new string('=', 50));

				if ( exitCode == ModComponent.InstallExitCode.Success )
				{
					Console.WriteLine("Installation completed successfully!");
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
				Console.WriteLine($"API Key: {opts.ApiKey.Substring(0, Math.Min(10, opts.ApiKey.Length))}...");

				// Validate the API key if not skipped
				if ( !opts.SkipValidation )
				{
					Console.WriteLine("\nValidating API key with Nexus Mods...");
					(bool isValid, string message) = await Services.Download.NexusModsDownloadHandler.ValidateApiKeyAsync(opts.ApiKey);

					if ( !isValid )
					{
						Logger.LogError($"API key validation failed: {message}");
						Console.WriteLine($"\n❌ Validation failed: {message}");
						return 1;
					}

					Console.WriteLine($"\n✓ {message}");
				}
				else
				{
					Logger.LogWarning("Skipping API key validation");
					Console.WriteLine("Skipping validation (--skip-validation specified)");
				}

				// Store the API key in MainConfig
				_config.nexusModsApiKey = opts.ApiKey;
				Logger.Log("API key stored in MainConfig");

				// Save to persistent storage (settings.json)
				SaveSettings();

				string settingsPath = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"KOTORModSync",
					"settings.json"
				);

				Logger.Log($"API key saved to: {settingsPath}");

				Console.WriteLine($"\n✓ Nexus Mods API key set successfully!");
				Console.WriteLine($"Settings file: {settingsPath}");
				Console.WriteLine("\nYou can now use the download command to automatically download mods from Nexus Mods.");
				Console.WriteLine("This setting is shared with the KOTORModSync GUI application.");

				return 0;
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Error setting Nexus Mods API key: {ex.Message}");
				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}
				Console.WriteLine($"\n❌ Error: {ex.Message}");
				return 1;
			}
		}
	}
}
