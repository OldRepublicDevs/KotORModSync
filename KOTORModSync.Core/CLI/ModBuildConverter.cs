// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using JetBrains.Annotations;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;

namespace KOTORModSync.Core.CLI
{
	public static class ModBuildConverter
	{
		// Base options shared by all commands
		public class BaseOptions
		{
			[Option('v', "verbose", Required = false, HelpText = "Enable verbose output for debugging.")]
			public bool Verbose { get; set; }
		}

		// Convert command options
		[Verb("convert", HelpText = "Convert between formats, output to stdout or file")]
		public class ConvertOptions : BaseOptions
		{
			[Option('i', "input", Required = true, HelpText = "Input file path")]
			public string InputPath { get; set; }

			[Option('o', "output", Required = false, HelpText = "Output file path (if not specified, writes to stdout)")]
			public string OutputPath { get; set; }

			[Option('f', "format", Required = false, Default = "toml", HelpText = "Output format (toml, yaml, json, xml, ini, markdown)")]
			public string Format { get; set; }

			[Option('a', "auto", Required = false, HelpText = "Autogenerate instructions by pre-resolving URLs (does not download files)")]
			public bool AutoGenerate { get; set; }

			[Option('s', "select", Required = false, HelpText = "Select components by category or tier (format: 'category:Name' or 'tier:Name'). Can be specified multiple times.")]
			public IEnumerable<string> Select { get; set; }
		}

		// Merge command options
		[Verb("merge", HelpText = "Merge two instruction sets, output to stdout")]
		public class MergeOptions : BaseOptions
		{
			[Value(0, MetaName = "existing", Required = false, HelpText = "Existing instruction set file path")]
			public string ExistingPath { get; set; }

			[Value(1, MetaName = "incoming", Required = false, HelpText = "Incoming instruction set file path")]
			public string IncomingPath { get; set; }
			[Option('o', "output", Required = false, HelpText = "Output file path (if not specified, writes to stdout)")]
			public string OutputPath { get; set; }

			[Option('e', "existing", Required = false, HelpText = "Existing instruction set file path (alternative to positional arg)")]
			public string ExistingPathNamed { get; set; }

			[Option('n', "incoming", Required = false, HelpText = "Incoming instruction set file path (alternative to positional arg)")]
			public string IncomingPathNamed { get; set; }

			[Option('f', "format", Required = false, Default = "toml", HelpText = "Output format (toml, yaml, json, xml, ini, markdown)")]
			public string Format { get; set; }

			[Option("exclude-existing-only", Required = false, HelpText = "Remove components that exist only in EXISTING")]
			public bool ExcludeExistingOnly { get; set; }

			[Option("exclude-incoming-only", Required = false, HelpText = "Remove components that exist only in INCOMING")]
			public bool ExcludeIncomingOnly { get; set; }

			[Option("use-incoming-order", Required = false, HelpText = "Use INCOMING component order (default: EXISTING order)")]
			public bool UseIncomingOrder { get; set; }

			[Option('s', "select", Required = false, HelpText = "Select components by category or tier (format: 'category:Name' or 'tier:Name'). Can be specified multiple times.")]
			public IEnumerable<string> Select { get; set; }
		}

		// Validate command options
		[Verb("validate", HelpText = "Validate instruction files for errors (not yet implemented)")]
		public class ValidateOptions : BaseOptions
		{
			[Option('i', "input", Required = true, HelpText = "Input file path to validate")]
			public string InputPath { get; set; }
		}

		// Install command options
		[Verb("install", HelpText = "Install mods from an instruction file (not yet implemented)")]
		public class InstallOptions : BaseOptions
		{
			[Option('i', "input", Required = true, HelpText = "Instruction file path")]
			public string InputPath { get; set; }

			[Option('g', "game-dir", Required = true, HelpText = "Game installation directory")]
			public string GameDirectory { get; set; }
		}

		// Download command options
		[Verb("download", HelpText = "Download mods specified in an instruction file (not yet implemented)")]
		public class DownloadOptions : BaseOptions
		{
			[Option('i', "input", Required = true, HelpText = "Instruction file path")]
			public string InputPath { get; set; }

			[Option('o', "output", Required = false, HelpText = "Output directory for downloads")]
			public string OutputDirectory { get; set; }
		}

		public static int Run(string[] args)
		{
			Logger.Initialize();

			var parser = new Parser(with => with.HelpWriter = Console.Out);

			return parser.ParseArguments<ConvertOptions, MergeOptions, ValidateOptions, InstallOptions, DownloadOptions>(args)
			.MapResult(
				(ConvertOptions opts) => RunConvertAsync(opts).GetAwaiter().GetResult(),
				(MergeOptions opts) => RunMerge(opts),
				(ValidateOptions opts) => RunValidate(opts),
				(InstallOptions opts) => RunInstall(opts),
				(DownloadOptions opts) => RunDownload(opts),
				errs => 1);
		}

		private static void SetVerboseMode(bool verbose)
		{
			// Set MainConfig.DebugLogging via the instance property
			var config = new MainConfig { debugLogging = verbose };
		}

		private static List<ModComponent> ApplySelectionFilters(List<ModComponent> components, IEnumerable<string> selections)
		{
			if ( selections == null || !selections.Any() )
				return components;

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

			var filteredComponents = new List<ModComponent>();

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

				// Include if matches both filters (AND logic)
				if ( includeByCategory && includeByTier )
				{
					filteredComponents.Add(component);
				}
			}

			Logger.LogVerbose($"Selection filters applied: {filteredComponents.Count}/{components.Count} components selected");
			if ( selectedCategories.Count > 0 )
			{
				Logger.LogVerbose($"Categories: {string.Join(", ", selectedCategories)}");
			}
			if ( selectedTiers.Count > 0 )
			{
				Logger.LogVerbose($"Tiers: {string.Join(", ", selectedTiers)}");
			}

			return filteredComponents;
		}

		private static async Task<int> RunConvertAsync(ConvertOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			try
			{
				Logger.LogVerbose($"Converting file: {opts.InputPath}");
				Logger.LogVerbose($"Output format: {opts.Format}");

				if ( !File.Exists(opts.InputPath) )
				{
					Logger.LogError($"Input file not found: {opts.InputPath}");
					return 1;
				}

				Logger.LogVerbose("Loading components from input file...");
				List<ModComponent> components = FileLoadingService.LoadFromFile(opts.InputPath);
				Logger.LogVerbose($"Loaded {components.Count} components");

				// Apply selection filters
				components = ApplySelectionFilters(components, opts.Select);

				if ( opts.AutoGenerate )
				{
					Logger.LogVerbose("Auto-generating instructions from URLs...");

					var downloadCache = new Services.DownloadCacheService();
					downloadCache.SetDownloadManager();

					int totalComponents = components.Count(c => c.ModLink != null && c.ModLink.Count > 0);
					Logger.LogVerbose($"Processing {totalComponents} components in parallel...");

					// Process all components in parallel for 10x+ speed improvement
					var componentTasks = components
						.Where(c => c.ModLink != null && c.ModLink.Count > 0)
						.Select(async component =>
						{
							Logger.LogVerbose($"Processing component: {component.Name}");
							return await Services.AutoInstructionGenerator.GenerateInstructionsFromUrlsAsync(
								component, downloadCache);
						})
						.ToList();

					bool[] results = await Task.WhenAll(componentTasks);
					int successCount = results.Count(r => r);

					Logger.LogVerbose($"Auto-generation complete: {successCount}/{totalComponents} components processed successfully");
				}

				Logger.LogVerbose("Serializing to output format...");
				string output = ModComponentSerializationService.SaveToString(components, opts.Format);

				if ( !string.IsNullOrEmpty(opts.OutputPath) )
				{
					// Write to file
					string outputDir = Path.GetDirectoryName(opts.OutputPath);
					if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
					{
						Directory.CreateDirectory(outputDir);
						Logger.LogVerbose($"Created output directory: {outputDir}");
					}

					File.WriteAllText(opts.OutputPath, output);
					Logger.LogVerbose($"Conversion completed successfully, saved to: {opts.OutputPath}");
				}
				else
				{
					// Write to stdout
					Console.WriteLine(output);
					Logger.LogVerbose("Conversion completed successfully (output to stdout)");
				}

				return 0;
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Error during conversion: {ex.Message}");
				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}
				return 1;
			}
		}

		private static int RunMerge(MergeOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			try
			{
				// Use named parameters if provided, otherwise use positional
				string existingPath = !string.IsNullOrEmpty(opts.ExistingPathNamed)
									 ? opts.ExistingPathNamed
									 : !string.IsNullOrEmpty(opts.ExistingPath)
									 ? opts.ExistingPath
									 : null;
				string incomingPath = !string.IsNullOrEmpty(opts.IncomingPathNamed)
									 ? opts.IncomingPathNamed
									 : !string.IsNullOrEmpty(opts.IncomingPath)
									 ? opts.IncomingPath
									 : null;

				if ( string.IsNullOrEmpty(existingPath) || string.IsNullOrEmpty(incomingPath) )
				{
					Logger.LogError("Both existing and incoming file paths are required");
					Console.WriteLine("Usage: merge <existing> <incoming> --format <format> [options] --output <file>");
					Console.WriteLine("   OR: merge --existing <file> --incoming <file> --format <format> [options] --output <file>");
					return 1;
				}

				Logger.LogVerbose($"Existing file: {existingPath}");
				Logger.LogVerbose($"Incoming file: {incomingPath}");
				Logger.LogVerbose($"Output format: {opts.Format}");
				Logger.LogVerbose($"Exclude existing-only: {opts.ExcludeExistingOnly}");
				Logger.LogVerbose($"Exclude incoming-only: {opts.ExcludeIncomingOnly}");
				Logger.LogVerbose($"Use incoming order: {opts.UseIncomingOrder}");

				if ( !File.Exists(existingPath) )
				{
					Logger.LogError($"Existing file not found: {existingPath}");
					return 1;
				}

				if ( !File.Exists(incomingPath) )
				{
					Logger.LogError($"Incoming file not found: {incomingPath}");
					return 1;
				}

				var mergeOptions = new Services.MergeOptions
				{
					ExcludeExistingOnly = opts.ExcludeExistingOnly,
					ExcludeIncomingOnly = opts.ExcludeIncomingOnly,
					UseIncomingOrder = opts.UseIncomingOrder,
					HeuristicsOptions = MergeHeuristicsOptions.CreateDefault()
				};

				Logger.LogVerbose("Loading and merging instruction sets...");
				List<ModComponent> mergedComponents = ComponentMergeService.MergeInstructionSets(
					existingPath,
					incomingPath,
					MergeStrategy.ByNameAndAuthor,
					mergeOptions);

				Logger.LogVerbose($"Merged result contains {mergedComponents.Count} components");

				// Apply selection filters
				mergedComponents = ApplySelectionFilters(mergedComponents, opts.Select);

				Logger.LogVerbose("Serializing to output format...");
				string output = ModComponentSerializationService.SaveToString(mergedComponents, opts.Format);

				if ( !string.IsNullOrEmpty(opts.OutputPath) )
				{
					// Write to file
					string outputDir = Path.GetDirectoryName(opts.OutputPath);
					if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
					{
						Directory.CreateDirectory(outputDir);
						Logger.LogVerbose($"Created output directory: {outputDir}");
					}

					File.WriteAllText(opts.OutputPath, output);
					Logger.LogVerbose($"Merge completed successfully, saved to: {opts.OutputPath}");
				}
				else
				{
					Console.WriteLine(output);
					Logger.LogVerbose("Merge completed successfully (output to stdout)");
				}
				Logger.LogVerbose("Merge completed successfully");
				return 0;
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Error during merge: {ex.Message}");
				if ( opts.Verbose )
				{
					Logger.LogException(ex);
				}
				return 1;
			}
		}

		private static int RunValidate(ValidateOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			Logger.LogError("validate command not yet implemented");
			Console.WriteLine("This command will validate instruction files for errors");
			return 1;
		}

		private static int RunInstall(InstallOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			Logger.LogError("install command not yet implemented");
			Console.WriteLine("This command will install mods from an instruction file");
			return 1;
		}

		private static int RunDownload(DownloadOptions opts)
		{
			SetVerboseMode(opts.Verbose);

			Logger.LogError("download command not yet implemented");
			Console.WriteLine("This command will download mods specified in an instruction file");
			return 1;
		}
	}
}
