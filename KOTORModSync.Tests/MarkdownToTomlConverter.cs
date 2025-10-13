// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Utility class for converting markdown mod builds to TOML format
	/// </summary>
	public static class MarkdownToTomlConverter
	{
		/// <summary>
		/// Converts a markdown file to TOML format
		/// </summary>
		/// <param name="inputMarkdownPath">Path to input markdown file</param>
		/// <param name="outputTomlPath">Path to output TOML file</param>
		/// <returns>True if conversion was successful</returns>
		public static bool ConvertMarkdownToToml(string inputMarkdownPath, string outputTomlPath)
		{
			try
			{
				// Read markdown file
				if ( !File.Exists(inputMarkdownPath) )
				{
					Console.Error.WriteLine($"Error: Input file not found: {inputMarkdownPath}");
					return false;
				}

				string markdown = File.ReadAllText(inputMarkdownPath);
				Console.WriteLine($"Read {markdown.Length} characters from {inputMarkdownPath}");

				// Parse markdown
				var profile = MarkdownImportProfile.CreateDefault();
				var parser = new MarkdownParser(profile);
				MarkdownParserResult parseResult = parser.Parse(markdown);

				Console.WriteLine($"Parsed {parseResult.Components.Count} components");

				if ( parseResult.Warnings.Count > 0 )
				{
					Console.WriteLine($"Warnings: {parseResult.Warnings.Count}");
					foreach ( string warning in parseResult.Warnings.Take(10) )
					{
						Console.WriteLine($"  - {warning}");
					}
					if ( parseResult.Warnings.Count > 10 )
					{
						Console.WriteLine($"  ... and {parseResult.Warnings.Count - 10} more warnings");
					}
				}

				if ( parseResult.Components.Count == 0 )
				{
					Console.Error.WriteLine("Error: No components parsed from markdown");
					return false;
				}

				// Convert to TOML
				var tomlData = new Dictionary<string, object>();
				var componentsList = new List<object>();

				foreach ( ModComponent component in parseResult.Components )
				{
					var componentData = new Dictionary<string, object>();

					if ( !string.IsNullOrWhiteSpace(component.Name) )
						componentData["name"] = component.Name;

					if ( !string.IsNullOrWhiteSpace(component.Author) )
						componentData["author"] = component.Author;

					if ( !string.IsNullOrWhiteSpace(component.Description) )
						componentData["description"] = component.Description;

					if ( component.Category?.Count > 0 )
						componentData["category"] = component.Category.Cast<object>().ToList();

					if ( !string.IsNullOrWhiteSpace(component.Tier) )
						componentData["tier"] = component.Tier;

					if ( component.Language?.Count > 0 )
						componentData["language"] = component.Language.Cast<object>().ToList();

					if ( !string.IsNullOrWhiteSpace(component.InstallationMethod) )
						componentData["installation_method"] = component.InstallationMethod;

					if ( !string.IsNullOrWhiteSpace(component.Directions) )
						componentData["installation_instructions"] = component.Directions;

					if ( component.ModLink?.Count > 0 )
						componentData["mod_links"] = component.ModLink.Cast<object>().ToList();

					if ( component.Guid != Guid.Empty )
						componentData["guid"] = component.Guid.ToString();

					componentsList.Add(componentData);
				}

				tomlData["components"] = componentsList;

				// Write TOML
				string tomlContent = TomlWriter.WriteString(tomlData);

				// Ensure output directory exists
				string? outputDir = Path.GetDirectoryName(outputTomlPath);
				if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
				{
					Directory.CreateDirectory(outputDir);
				}

				File.WriteAllText(outputTomlPath, tomlContent);
				Console.WriteLine($"Successfully wrote {tomlContent.Length} characters to {outputTomlPath}");

				return true;
			}
			catch ( Exception ex )
			{
				Console.Error.WriteLine($"Error during conversion: {ex.Message}");
				Console.Error.WriteLine(ex.StackTrace);
				return false;
			}
		}

		/// <summary>
		/// Extracts mod sections from markdown content
		/// </summary>
		/// <param name="markdown">Markdown content to extract sections from</param>
		/// <returns>List of mod sections</returns>
		public static List<string> ExtractModSections(string markdown)
		{
			return MarkdownUtilities.ExtractModSections(markdown);
		}

		/// <summary>
		/// Extracts the mod list section from markdown
		/// </summary>
		/// <param name="markdown">Full markdown content</param>
		/// <returns>Content starting from ## Mod List marker</returns>
		public static string ExtractModListSection(string markdown)
		{
			return MarkdownUtilities.ExtractModListSection(markdown);
		}

		/// <summary>
		/// Extracts all field values from text using a regex pattern
		/// </summary>
		/// <param name="text">Text to search</param>
		/// <param name="pattern">Regex pattern to use</param>
		/// <returns>List of extracted values</returns>
		public static List<string> ExtractAllFieldValues(string text, string pattern)
		{
			return MarkdownUtilities.ExtractAllFieldValues(text, pattern);
		}

		/// <summary>
		/// Normalizes category format for comparison
		/// </summary>
		/// <param name="category">Category string to normalize</param>
		/// <returns>Normalized category string</returns>
		public static string NormalizeCategoryFormat(string category)
		{
			return MarkdownUtilities.NormalizeCategoryFormat(category);
		}

		/// <summary>
		/// Regenerates markdown documentation from a markdown file
		/// </summary>
		/// <param name="inputMarkdownPath">Path to input markdown file</param>
		/// <param name="outputMarkdownPath">Path to output regenerated markdown file</param>
		/// <returns>True if generation was successful</returns>
		public static bool RegenerateMarkdownDocumentation(string inputMarkdownPath, string outputMarkdownPath)
		{
			try
			{
				// Read markdown file
				if ( !File.Exists(inputMarkdownPath) )
				{
					Console.Error.WriteLine($"Error: Input file not found: {inputMarkdownPath}");
					return false;
				}

				string markdown = File.ReadAllText(inputMarkdownPath);
				Console.WriteLine($"Read {markdown.Length} characters from {inputMarkdownPath}");

				// Parse markdown
				var profile = MarkdownImportProfile.CreateDefault();
				var parser = new MarkdownParser(profile);
				MarkdownParserResult parseResult = parser.Parse(markdown);

				Console.WriteLine($"Parsed {parseResult.Components.Count} components");

				if ( parseResult.Warnings.Count > 0 )
				{
					Console.WriteLine($"Warnings: {parseResult.Warnings.Count}");
					foreach ( string warning in parseResult.Warnings.Take(10) )
					{
						Console.WriteLine($"  - {warning}");
					}
					if ( parseResult.Warnings.Count > 10 )
					{
						Console.WriteLine($"  ... and {parseResult.Warnings.Count - 10} more warnings");
					}
				}

				if ( parseResult.Components.Count == 0 )
				{
					Console.Error.WriteLine("Error: No components parsed from markdown");
					return false;
				}

				// Generate documentation
				string generatedDocs = ModComponent.GenerateModDocumentation(parseResult.Components.ToList());

				// Ensure output directory exists
				string? outputDir = Path.GetDirectoryName(outputMarkdownPath);
				if ( !string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir) )
				{
					Directory.CreateDirectory(outputDir);
				}

				File.WriteAllText(outputMarkdownPath, generatedDocs);
				Console.WriteLine($"Successfully wrote {generatedDocs.Length} characters to {outputMarkdownPath}");

				return true;
			}
			catch ( Exception ex )
			{
				Console.Error.WriteLine($"Error during documentation generation: {ex.Message}");
				Console.Error.WriteLine(ex.StackTrace);
				return false;
			}
		}

		/// <summary>
		/// CLI entry point for the converter
		/// </summary>
		public static int Run(string[] args)
		{
			if ( args.Length == 0 )
			{
				Console.WriteLine("Usage:");
				Console.WriteLine("  MarkdownToTomlConverter convert-to-toml --input <file> --output <file>");
				Console.WriteLine("  MarkdownToTomlConverter generate-docs --input <file> --output <file>");
				return 1;
			}

			string command = args[0];
			string? inputPath = null;
			string? outputPath = null;

			// Parse arguments
			for ( int i = 1; i < args.Length; i++ )
			{
				if ( args[i] == "--input" && i + 1 < args.Length )
				{
					inputPath = args[++i];
				}
				else if ( args[i] == "--output" && i + 1 < args.Length )
				{
					outputPath = args[++i];
				}
			}

			if ( string.IsNullOrEmpty(inputPath) || string.IsNullOrEmpty(outputPath) )
			{
				Console.Error.WriteLine("Error: Both --input and --output are required");
				return 1;
			}

			bool success;
			switch ( command.ToLowerInvariant() )
			{
				case "convert-to-toml":
					success = ConvertMarkdownToToml(inputPath, outputPath);
					break;
				case "generate-docs":
					success = RegenerateMarkdownDocumentation(inputPath, outputPath);
					break;
				default:
					Console.Error.WriteLine($"Error: Unknown command '{command}'");
					Console.WriteLine("Valid commands: convert-to-toml, generate-docs");
					return 1;
			}

			return success ? 0 : 1;
		}
	}
}

