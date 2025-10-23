// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json.Linq;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;
using static KOTORModSync.Core.Instruction;
using YamlSerialization = YamlDotNet.Serialization;

namespace KOTORModSync.Core.Services
{
	public class ComponentValidationContext
	{
		public Dictionary<Guid, List<string>> ComponentIssues { get; set; } = new Dictionary<Guid, List<string>>();
		public Dictionary<Guid, List<string>> InstructionIssues { get; set; } = new Dictionary<Guid, List<string>>();
		public Dictionary<string, List<string>> UrlFailures { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

		public void AddModComponentIssue(Guid componentGuid, string issue)
		{
			if ( !ComponentIssues.ContainsKey(componentGuid) )
				ComponentIssues[componentGuid] = new List<string>();
			ComponentIssues[componentGuid].Add(issue);
		}

		public void AddInstructionIssue(Guid instructionGuid, string issue)
		{
			if ( !InstructionIssues.ContainsKey(instructionGuid) )
				InstructionIssues[instructionGuid] = new List<string>();
			InstructionIssues[instructionGuid].Add(issue);
		}

		public void AddUrlFailure(string url, string error)
		{
			if ( !UrlFailures.ContainsKey(url) )
				UrlFailures[url] = new List<string>();
			UrlFailures[url].Add(error);
		}

		public List<string> GetComponentIssues(Guid componentGuid)
		{
			return ComponentIssues.TryGetValue(componentGuid, out List<string> issues) ? issues : new List<string>();
		}

		public List<string> GetInstructionIssues(Guid instructionGuid)
		{
			return InstructionIssues.TryGetValue(instructionGuid, out List<string> issues) ? issues : new List<string>();
		}

		public List<string> GetUrlFailures(string url)
		{
			return UrlFailures.TryGetValue(url, out List<string> failures) ? failures : new List<string>();
		}

		public bool HasIssues(Guid componentGuid)
		{
			return ComponentIssues.ContainsKey(componentGuid);
		}

		public bool HasInstructionIssues(Guid instructionGuid)
		{
			return InstructionIssues.ContainsKey(instructionGuid);
		}

		public bool HasUrlFailures(string url)
		{
			return UrlFailures.ContainsKey(url);
		}
	}

	public static class ModComponentSerializationService
	{
		#region Encoding Sanitization
		/// <summary>
		/// Sanitizes string content to handle problematic characters that break parsers.
		/// Uses the encoding specified in MainConfig.FileEncoding (default: utf-8)
		/// </summary>
		private static string SanitizeUtf8(string input)
		{
			if ( string.IsNullOrEmpty(input) )
				return input;

			try
			{
				// Get the configured encoding (default to UTF-8)
				string encodingName = MainConfig.FileEncoding
									  ?? "utf-8";
				Encoding targetEncoding;

				// Map encoding name to .NET Encoding
				if ( encodingName.Equals("windows-1252", StringComparison.OrdinalIgnoreCase) ||
					encodingName.Equals("cp-1252", StringComparison.OrdinalIgnoreCase) ||
					encodingName.Equals("cp1252", StringComparison.OrdinalIgnoreCase) )
				{
					targetEncoding = Encoding.GetEncoding(1252);
				}
				else if ( encodingName.Equals("utf-8", StringComparison.OrdinalIgnoreCase) ||
						 encodingName.Equals("utf8", StringComparison.OrdinalIgnoreCase) )
				{
					targetEncoding = new UTF8Encoding(false, false);
				}
				else
				{
					// Try to get encoding by name, fallback to UTF-8
					try
					{
						targetEncoding = Encoding.GetEncoding(encodingName);
					}
					catch
					{
						Logger.LogWarning($"Unknown encoding '{encodingName}', using UTF-8");
						targetEncoding = new UTF8Encoding(false, false);
					}
				}

				var result = new StringBuilder(input.Length);

				foreach ( char c in input )
				{
					try
					{
						// Try to encode this character to target encoding
						byte[] unused = targetEncoding.GetBytes(new[] { c });
						// If successful, add it to result
						result.Append(c);
					}
					catch
					{
						// If encoding fails, ignore/skip this character
						int lineNumber = input.Substring(0, input.IndexOf(c)).Count(x => x == '\n') + 1;
						int columnNumber = input.Substring(0, input.IndexOf(c)).Length - input.Substring(0, input.IndexOf(c)).LastIndexOf('\n') + 1;
						Logger.LogVerbose($"Failed to encode character `{c}` with encoding '{encodingName}' at line {lineNumber} column {columnNumber}, ignoring");
					}
				}

				return result.ToString();
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"Failed to sanitize content with encoding (using original): {ex.Message}");
				return input;
			}
		}
		#endregion

		#region Loading Functions
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> DeserializeModComponentFromTomlString([NotNull] string tomlContent)
		{
			Logger.LogVerbose("Loading from TOML string");
			if ( tomlContent == null )
				throw new ArgumentNullException(nameof(tomlContent));
			tomlContent = SanitizeUtf8(tomlContent);
			tomlContent = tomlContent
				.Replace(oldValue: "Instructions = []", string.Empty)
				.Replace(oldValue: "Options = []", string.Empty);
			if ( string.IsNullOrWhiteSpace(tomlContent) )
				throw new InvalidDataException("TOML content is empty.");
			tomlContent = Utility.Serializer.FixWhitespaceIssues(tomlContent);

			DocumentSyntax tomlDocument = Toml.Parse(tomlContent);
			if ( tomlDocument.HasErrors )
			{
				foreach ( var message in tomlDocument.Diagnostics )
				{
					if ( message != null )
						Logger.LogError(message.Message);
				}
			}

			TomlTable tomlTable = tomlDocument.ToModel();
			ParseMetadataSection(tomlTable);

			if ( !tomlTable.TryGetValue("thisMod", out object thisModObj) )
				throw new InvalidDataException("TOML content does not contain 'thisMod' array.");

			IEnumerable<object> componentTables;

			if ( thisModObj is TomlTableArray tomlTableArray )
			{
				componentTables = tomlTableArray;
			}
			else if ( thisModObj is System.Collections.IList list )
			{
				componentTables = list.Cast<object>();
			}
			else
			{
				throw new InvalidDataException($"TOML 'thisMod' is not a valid array type. Got: {thisModObj.GetType().Name}");
			}


			// Collect all [[thisMod.Instructions]] entries from the root level
			var allInstructions = new List<object>();
			foreach ( string key in tomlTable.Keys )
			{
				if ( key.Contains("Instructions") && !key.Contains("Options") )
				{
					if ( tomlTable.TryGetValue(key, out object instructionsObj) && instructionsObj is IList<object> instructionsList )
					{
						Logger.LogVerbose($"Found {instructionsList.Count} instructions at root level");
						allInstructions.AddRange(instructionsList);
					}
				}
			}

			// Collect all [[thisMod.Options]] entries from the root level
			var allOptions = new List<object>();
			Logger.LogVerbose($"TOML table keys: {string.Join(", ", tomlTable.Keys)}");
			foreach ( string key in tomlTable.Keys )
			{
				Logger.LogVerbose($"Checking key: '{key}' - Contains Options: {key.Contains("Options")}, Contains Instructions: {key.Contains("Instructions")}");
				if ( key.Contains("Options") && !key.Contains("Instructions") )
				{
					Logger.LogVerbose($"Found Options key: '{key}'");
					if ( tomlTable.TryGetValue(key, out object optionsObj) )
					{
						Logger.LogVerbose($"Options object type: {optionsObj?.GetType().Name ?? "null"}");
						if ( optionsObj is IList<object> optionsList )
						{
							Logger.LogVerbose($"Found {optionsList.Count} options at root level");
							allOptions.AddRange(optionsList);
						}
					}
				}
			}

			// Collect all [[thisMod.Options.Instructions]] entries from the root level
			var allOptionsInstructions = new List<object>();
			foreach ( string key in tomlTable.Keys )
			{
				Logger.LogVerbose($"Checking key for Options Instructions: '{key}' - Contains Options: {key.Contains("Options")}, Contains Instructions: {key.Contains("Instructions")}");
				if ( key.Contains("Options") && key.Contains("Instructions") )
				{
					Logger.LogVerbose($"Found Options Instructions key: '{key}'");
					if ( tomlTable.TryGetValue(key, out object optionsInstructionsObj) )
					{
						Logger.LogVerbose($"Options Instructions object type: {optionsInstructionsObj?.GetType().Name ?? "null"}");
						if ( optionsInstructionsObj is IList<object> optionsInstructionsList )
						{
							Logger.LogVerbose($"Found {optionsInstructionsList.Count} options instructions at root level");
							allOptionsInstructions.AddRange(optionsInstructionsList);
						}
					}
				}
			}

			var components = new List<ModComponent>();

			foreach ( object tomlComponent in componentTables )
			{
				if ( tomlComponent == null )
					continue;

				try
				{
					var thisComponent = new ModComponent();
					var componentDict = tomlComponent as IDictionary<string, object>
						?? throw new InvalidCastException("Failed to cast TOML component to IDictionary<string, object>");

					Logger.LogVerbose($"=== Processing TOML component ===");
					Logger.LogVerbose($"tomlComponent type: {tomlComponent.GetType().Name}");
					Logger.LogVerbose($"componentDict type: {componentDict.GetType().Name}");
					Logger.LogVerbose($"componentDict keys: {string.Join(", ", componentDict.Keys)}");

					// Check if this component has Instructions at the TOML level
					if ( componentDict.ContainsKey("Instructions") )
					{
						object tomlInstructions = componentDict["Instructions"];
						Logger.LogVerbose($"TOML component has Instructions field: type={tomlInstructions?.GetType().Name ?? "null"}, value: {tomlInstructions}");
						if ( tomlInstructions is IList<object> instructionsList )
						{
							Logger.LogVerbose($"Instructions is IList with {instructionsList.Count} items");
							for ( int i = 0; i < Math.Min(instructionsList.Count, 3); i++ )
							{
								Logger.LogVerbose($"  Instructions[{i}] type: {instructionsList[i]?.GetType().Name ?? "null"}, value: {instructionsList[i]}");
							}
						}
					}
					else
					{
						Logger.LogVerbose($"TOML component does NOT have Instructions field. Available keys: {string.Join(", ", componentDict.Keys)}");
					}

					thisComponent = DeserializeComponent(componentDict);

					// Assign collected instructions to this component
					if ( allInstructions.Count > 0 )
					{
						Logger.LogVerbose($"Assigning {allInstructions.Count} instructions to component '{thisComponent.Name}'");
						thisComponent.Instructions = DeserializeInstructions(allInstructions, thisComponent);
					}

					// Assign collected options to this component
					if ( allOptions.Count > 0 )
					{
						Logger.LogVerbose($"Assigning {allOptions.Count} options to component '{thisComponent.Name}'");
						thisComponent.Options = DeserializeOptions(allOptions);
					}

					// Assign collected options instructions to the appropriate options
					if ( allOptionsInstructions.Count > 0 )
					{
						Logger.LogVerbose($"Assigning {allOptionsInstructions.Count} options instructions to component '{thisComponent.Name}'");
						var instructionsByParent = new Dictionary<string, List<object>>();
						foreach ( object instrObj in allOptionsInstructions )
						{
							if ( instrObj is IDictionary<string, object> instrDict &&
								 instrDict.TryGetValue("Parent", out object parentObj) )
							{
								string parentGuid = parentObj?.ToString();
								if ( !string.IsNullOrEmpty(parentGuid) )
								{
									if ( !instructionsByParent.ContainsKey(parentGuid) )
										instructionsByParent[parentGuid] = new List<object>();
									instructionsByParent[parentGuid].Add(instrObj);
								}
							}
						}

						foreach ( Option option in thisComponent.Options )
						{
							string optionGuidStr = option.Guid.ToString();
							if ( instructionsByParent.TryGetValue(optionGuidStr, out List<object> instructions) )
							{
								if ( option.Instructions.Count == 0 )
								{
									option.Instructions = DeserializeInstructions(instructions, option);
								}
							}
						}
					}

					components.Add(thisComponent);
				}
				catch ( Exception ex )
				{
					Logger.LogError($"Failed to deserialize component: {ex.Message}");
					Logger.LogError($"Exception type: {ex.GetType().Name}");
					Logger.LogError($"Stack trace: {ex.StackTrace}");
				}
			}

			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in TOML content.");

			return components;
		}

		private static readonly string[] s_yamlSeparator = new[] { "---" };

		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> DeserializeModComponentFromYamlString([NotNull] string yamlContent)
		{
			Logger.LogVerbose("Loading from YAML string");
			if ( yamlContent == null )
				throw new ArgumentNullException(nameof(yamlContent));
			yamlContent = SanitizeUtf8(yamlContent);
			var components = new List<ModComponent>();
			string[] yamlDocs = yamlContent.Split(s_yamlSeparator, StringSplitOptions.RemoveEmptyEntries);
			int docIndex = 0;
			foreach ( string yamlDoc in yamlDocs )
			{
				docIndex++;
				if ( string.IsNullOrWhiteSpace(yamlDoc) )
					continue;
				try
				{
					// Check if this is a metadata document
					if ( IsYamlMetadataDocument(yamlDoc) )
					{
						ParseYamlMetadataSection(yamlDoc.Trim());
						Logger.LogVerbose($"Parsed YAML metadata document #{docIndex}");
						continue;
					}

					ModComponent component = DeserializeYamlComponent(yamlDoc.Trim());
					if ( component != null )
						components.Add(component);
				}
				catch ( Exception ex )
				{
					Logger.LogWarning($"Failed to deserialize YAML document #{docIndex}: {ex.Message} - skipping this document");
					Logger.LogVerbose($"YAML deserialization error details: {ex}");
				}
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in YAML content.");

			return components;
		}

		private static bool IsYamlMetadataDocument(string yamlDoc)
		{
			if ( string.IsNullOrWhiteSpace(yamlDoc) )
				return false;

			// Metadata documents have "fileFormatVersion" or start with "# Metadata"
			return yamlDoc.Contains("fileFormatVersion:") ||
				   yamlDoc.TrimStart().StartsWith("# Metadata", StringComparison.OrdinalIgnoreCase);
		}

		private static void ParseYamlMetadataSection(string yamlDoc)
		{
			try
			{
				YamlSerialization.IDeserializer deserializer = new YamlSerialization.DeserializerBuilder()
					.WithNamingConvention(YamlSerialization.NamingConventions.PascalCaseNamingConvention.Instance)
					.IgnoreUnmatchedProperties()
					.Build();
				Dictionary<string, object> metadataDict = deserializer.Deserialize<Dictionary<string, object>>(yamlDoc);

				if ( metadataDict == null )
					return;

				if ( metadataDict.TryGetValue("FileFormatVersion", out object versionObj) || metadataDict.TryGetValue("fileFormatVersion", out versionObj) )
					MainConfig.FileFormatVersion = versionObj?.ToString() ?? "2.0";
				if ( metadataDict.TryGetValue("TargetGame", out object gameObj) || metadataDict.TryGetValue("targetGame", out gameObj) )
					MainConfig.TargetGame = gameObj?.ToString() ?? string.Empty;
				if ( metadataDict.TryGetValue("BuildName", out object nameObj) || metadataDict.TryGetValue("buildName", out nameObj) )
					MainConfig.BuildName = nameObj?.ToString() ?? string.Empty;
				if ( metadataDict.TryGetValue("BuildAuthor", out object authorObj) || metadataDict.TryGetValue("buildAuthor", out authorObj) )
					MainConfig.BuildAuthor = authorObj?.ToString() ?? string.Empty;
				if ( metadataDict.TryGetValue("BuildDescription", out object descObj) || metadataDict.TryGetValue("buildDescription", out descObj) )
					MainConfig.BuildDescription = descObj?.ToString() ?? string.Empty;
				if ( metadataDict.TryGetValue("LastModified", out object modifiedObj) || metadataDict.TryGetValue("lastModified", out modifiedObj) )
				{
					if ( DateTime.TryParse(modifiedObj?.ToString(), out DateTime parsedDate) )
						MainConfig.LastModified = parsedDate;
				}
				if ( metadataDict.TryGetValue("BeforeModListContent", out object beforeObj) || metadataDict.TryGetValue("beforeModListContent", out beforeObj) )
					MainConfig.BeforeModListContent = beforeObj?.ToString() ?? string.Empty;
				if ( metadataDict.TryGetValue("AfterModListContent", out object afterObj) || metadataDict.TryGetValue("afterModListContent", out afterObj) )
					MainConfig.AfterModListContent = afterObj?.ToString() ?? string.Empty;
				if ( metadataDict.TryGetValue("WidescreenSectionContent", out object widescreenObj) || metadataDict.TryGetValue("widescreenSectionContent", out widescreenObj) )
					MainConfig.WidescreenSectionContent = widescreenObj?.ToString() ?? "Please install manually the widescreen implementations, e.g. uniws, before continuing.";
				if ( metadataDict.TryGetValue("AspyrSectionContent", out object aspyrObj) || metadataDict.TryGetValue("aspyrSectionContent", out aspyrObj) )
					MainConfig.AspyrSectionContent = aspyrObj?.ToString() ?? string.Empty;

				Logger.LogVerbose($"Loaded YAML metadata: Game={MainConfig.TargetGame}, Version={MainConfig.FileFormatVersion}, Build={MainConfig.BuildName}");
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"Failed to parse YAML metadata section (non-fatal): {ex.Message}");
			}
		}

		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> DeserializeModComponentFromMarkdownString([NotNull] string markdownContent)
		{
			Logger.LogVerbose("Loading from Markdown string");
			if ( markdownContent == null )
				throw new ArgumentNullException(nameof(markdownContent));
			markdownContent = SanitizeUtf8(markdownContent);
			try
			{
				var profile = MarkdownImportProfile.CreateDefault();
				var parser = new MarkdownParser(profile);
				MarkdownParserResult result = parser.Parse(markdownContent);
				if ( result.Components == null || result.Components.Count == 0 )
					throw new InvalidDataException("No valid components found in Markdown content.");

				return result.Components.ToList();
			}
			catch ( InvalidDataException )
			{
				throw;
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"Failed to parse Markdown content: {ex.Message}");
				Logger.LogVerbose($"Markdown parsing error details: {ex}");
				throw new InvalidDataException("Failed to parse Markdown content.", ex);
			}
		}

		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> DeserializeModComponentFromJsonString([NotNull] string jsonContent)
		{
			Logger.LogVerbose("Loading from JSON string");
			if ( jsonContent == null )
				throw new ArgumentNullException(nameof(jsonContent));
			jsonContent = SanitizeUtf8(jsonContent);
			var jsonObject = JObject.Parse(jsonContent);
			if ( jsonObject["metadata"] is JObject metadataObj )
			{
				MainConfig.FileFormatVersion = metadataObj["fileFormatVersion"]?.ToString() ?? "2.0";
				MainConfig.TargetGame = metadataObj["targetGame"]?.ToString() ?? string.Empty;
				MainConfig.BuildName = metadataObj["buildName"]?.ToString() ?? string.Empty;
				MainConfig.BuildAuthor = metadataObj["buildAuthor"]?.ToString() ?? string.Empty;
				MainConfig.BuildDescription = metadataObj["buildDescription"]?.ToString() ?? string.Empty;
				if ( metadataObj["lastModified"] != null )
					MainConfig.LastModified = metadataObj["lastModified"].ToObject<DateTime?>();
				MainConfig.BeforeModListContent = metadataObj["beforeModListContent"]?.ToString() ?? string.Empty;
				MainConfig.AfterModListContent = metadataObj["afterModListContent"]?.ToString() ?? string.Empty;
				MainConfig.WidescreenSectionContent = metadataObj["widescreenSectionContent"]?.ToString() ?? "Please install manually the widescreen implementations, e.g. uniws, before continuing.";
				MainConfig.AspyrSectionContent = metadataObj["aspyrSectionContent"]?.ToString() ?? string.Empty;
			}
			var components = new List<ModComponent>();
			if ( jsonObject["components"] is JArray componentsArray )
			{
				int componentIndex = 0;
				foreach ( JToken compToken in componentsArray )
				{
					componentIndex++;
					try
					{
						// Convert JToken to Dictionary recursively to handle nested structures
						Dictionary<string, object> compDict = JTokenToDictionary(compToken);

						// Pre-process the component dictionary to handle duplicate fields
						compDict = PreprocessComponentDictionary(compDict);

						var component = DeserializeComponent(compDict);
						components.Add(component);
					}
					catch ( Exception ex )
					{
						Logger.LogWarning($"Failed to deserialize JSON component #{componentIndex}: {ex.Message} - skipping this component");
						Logger.LogVerbose($"Component deserialization error details: {ex}");
					}
				}
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in JSON content.");

			return components;
		}
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> DeserializeModComponentFromXmlString([NotNull] string xmlContent)
		{
			Logger.LogVerbose("Loading from XML string");
			if ( xmlContent == null )
				throw new ArgumentNullException(nameof(xmlContent));
			xmlContent = SanitizeUtf8(xmlContent);
			var doc = XDocument.Parse(xmlContent);
			XElement root = doc.Root;
			XElement metadataElem = root?.Element("Metadata");
			if ( metadataElem != null )
			{
				MainConfig.FileFormatVersion = metadataElem.Element("FileFormatVersion")?.Value
					?? "2.0";
				MainConfig.TargetGame = metadataElem.Element("TargetGame")?.Value
					?? string.Empty;
				MainConfig.BuildName = metadataElem.Element("BuildName")?.Value
					?? string.Empty;
				MainConfig.BuildAuthor = metadataElem.Element("BuildAuthor")?.Value ?? string.Empty;
				MainConfig.BuildDescription = metadataElem.Element("BuildDescription")?.Value ?? string.Empty;
				if ( DateTime.TryParse(metadataElem.Element("LastModified")?.Value, out DateTime lastMod) )
					MainConfig.LastModified = lastMod;
				MainConfig.BeforeModListContent = metadataElem.Element("BeforeModListContent")?.Value ?? string.Empty;
				MainConfig.AfterModListContent = metadataElem.Element("AfterModListContent")?.Value ?? string.Empty;
				MainConfig.WidescreenSectionContent = metadataElem.Element("WidescreenSectionContent")?.Value ?? "Please install manually the widescreen implementations, e.g. uniws, before continuing.";
				MainConfig.AspyrSectionContent = metadataElem.Element("AspyrSectionContent")?.Value ?? string.Empty;
			}
			var components = new List<ModComponent>();
			XElement componentsElem = root?.Element("Components");
			if ( componentsElem != null )
			{
				int componentIndex = 0;
				foreach ( XElement compElem in componentsElem.Elements("Component") )
				{
					componentIndex++;
					try
					{
						Dictionary<string, object> compDict = XmlElementToDictionary(compElem);

						// Pre-process the component dictionary to handle duplicate fields
						compDict = PreprocessComponentDictionary(compDict);

						var component = DeserializeComponent(compDict);
						components.Add(component);
					}
					catch ( Exception ex )
					{
						Logger.LogWarning($"Failed to deserialize XML component #{componentIndex}: {ex.Message} - skipping this component");
						Logger.LogVerbose($"Component deserialization error details: {ex}");
					}
				}
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in XML content.");

			return components;
		}
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> DeserializeModComponentFromString(
			[NotNull] string content,
			[CanBeNull] string format = null)
		{
			Logger.LogVerbose($"Loading from string with format: {format ?? "auto-detect"}");
			if ( content == null )
				throw new ArgumentNullException(nameof(content));

			List<ModComponent> components;

			if ( !string.IsNullOrWhiteSpace(format) )
			{
				string fmt = format.Trim().ToLowerInvariant();
				switch ( fmt )
				{
					case "toml":
					case "tml":
						components = DeserializeModComponentFromTomlString(content);
						break;
					case "json":
						components = DeserializeModComponentFromJsonString(content);
						break;
					case "yaml":
					case "yml":
						components = DeserializeModComponentFromYamlString(content);
						break;
					case "xml":
						components = DeserializeModComponentFromXmlString(content);
						break;
					case "md":
					case "markdown":
					case "mdown":
					case "mkdn":
					case "mkd":
					case "mdtxt":
					case "mdtext":
					case "text":
						components = DeserializeModComponentFromMarkdownString(content);
						break;
					default:
						throw new ArgumentException($"Unknown format \"{format}\" passed to DeserializeModComponentFromString.");
				}
			}
			else
			{
				try
				{
					components = DeserializeModComponentFromTomlString(content);
				}
				catch ( Exception tomlEx )
				{
					Logger.LogVerbose($"TOML parsing failed: {tomlEx.Message}");

					try
					{
						components = DeserializeModComponentFromMarkdownString(content);
					}
					catch ( Exception mdEx )
					{
						Logger.LogVerbose($"Markdown parsing failed: {mdEx.Message}");

						try
						{
							components = DeserializeModComponentFromYamlString(content);
						}
						catch ( Exception yamlEx )
						{
							Logger.LogVerbose($"YAML parsing failed: {yamlEx.Message}");

							try
							{
								components = DeserializeModComponentFromTomlString(content);
							}
							catch ( Exception tomlSecondEx )
							{
								Logger.LogVerbose($"TOML (second attempt) parsing failed: {tomlSecondEx.Message}");

								try
								{
									components = DeserializeModComponentFromJsonString(content);
								}
								catch ( Exception jsonEx )
								{
									Logger.LogVerbose($"JSON parsing failed: {jsonEx.Message}");

									components = DeserializeModComponentFromXmlString(content);
								}
							}
						}
					}
				}
			}

			// Remove duplicate options from all loaded components
			foreach ( ModComponent component in components )
			{
				RemoveDuplicateOptions(component);
			}

			// Resolve dependencies and reorder components
			try
			{
				var resolutionResult = DependencyResolverService.ResolveDependencies(components, ignoreErrors: false);
				if ( resolutionResult.Success )
				{
					components = resolutionResult.OrderedComponents;
					Logger.LogVerbose($"Successfully resolved dependencies and reordered {components.Count} components");
				}
				else
				{
					// Log dependency resolution errors but don't fail the load
					Logger.LogWarning($"Dependency resolution failed with {resolutionResult.Errors.Count} errors:");
					foreach ( var error in resolutionResult.Errors )
					{
						Logger.LogWarning($"  - {error.ComponentName}: {error.Message}");
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"Failed to resolve dependencies during loading: {ex.Message}");
			}

			return components;
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> DeserializeModComponentFromStringAsync(
			[NotNull] string content,
			[CanBeNull] string format = null)
		{
			return Task.Run(() => DeserializeModComponentFromString(content, format));
		}
		#endregion
		#region Saving Functions
		[NotNull]
		public static string SerializeModComponentAsString(
			[NotNull] List<ModComponent> components,
			[NotNull] string format = "toml",
			[CanBeNull] ComponentValidationContext validationContext = null
		)
		{
			Logger.LogVerbose($"Saving to string with format: {format}");
			if ( components == null )
				throw new ArgumentNullException(nameof(components));
			if ( format == null )
				throw new ArgumentNullException(nameof(format));
			switch ( format.ToLowerInvariant() )
			{
				case "toml":
				case "tml":
					return SerializeModComponentAsTomlString(components, validationContext);
				case "yaml":
				case "yml":
					return SerializeModComponentAsYamlString(components, validationContext);
				case "md":
				case "markdown":
				case "mdown":
				case "mkdn":
				case "mkd":
				case "mdtxt":
				case "mdtext":
				case "text":
					return SerializeModComponentAsMarkdownString(components, validationContext);
				case "json":
					return SerializeModComponentAsJsonString(components, validationContext);
				case "xml":
					return SerializeModComponentAsXmlString(components, validationContext);
				default:
					throw new NotSupportedException($"Unsupported format: {format}");
			}
		}
		[NotNull]
		public static Task<string> SerializeModComponentAsStringAsync(
			[NotNull] List<ModComponent> components,
			[NotNull] string format = "toml")
		{
			return Task.Run(() => SerializeModComponentAsString(components, format));
		}
		#endregion
		#region Public Helpers

		/// <summary>
		/// Preprocesses a component dictionary to handle duplicate fields by flattening nested collections.
		/// This handles cases where TOML/JSON/YAML/XML might have duplicate field definitions.
		/// </summary>
		private static Dictionary<string, object> PreprocessComponentDictionary([NotNull] IDictionary<string, object> componentDict)
		{
			var processedDict = new Dictionary<string, object>(componentDict, StringComparer.OrdinalIgnoreCase);

			// Handle any potential duplicate field issues by ensuring consistent structure
			foreach ( var kvp in componentDict )
			{
				if ( kvp.Value is System.Collections.IEnumerable enumerable && !(kvp.Value is string) )
				{
					// Flatten any nested collections that might result from duplicate fields
					var flattenedList = new List<object>();
					foreach ( object item in enumerable )
					{
						if ( item is System.Collections.IEnumerable nestedEnumerable && !(item is string) )
						{
							// Flatten nested collections (handles duplicate Instructions, etc.)
							foreach ( object nestedItem in nestedEnumerable )
							{
								flattenedList.Add(nestedItem);
							}
						}
						else
						{
							flattenedList.Add(item);
						}
					}
					processedDict[kvp.Key] = flattenedList;
				}
				else
				{
					processedDict[kvp.Key] = kvp.Value;
				}
			}

			// Handle YAML deserialization issues where Instructions/Options are created as KeyValuePair objects
			// instead of proper dictionaries
			ProcessInstructionsAndOptions(processedDict);

			return processedDict;
		}

		/// <summary>
		/// Processes Instructions and Options to handle YAML deserialization issues where
		/// they might be created as KeyValuePair objects instead of proper dictionaries.
		/// Also handles component-level properties that might be converted to KeyValuePair objects.
		/// </summary>
		private static void ProcessInstructionsAndOptions(Dictionary<string, object> processedDict)
		{
			// First, handle component-level properties that might be KeyValuePair objects
			ProcessComponentLevelProperties(processedDict);

			// Process Instructions
			if ( processedDict.TryGetValue("Instructions", out object instructionsValue) && instructionsValue is List<object> instructionsList )
			{
				Logger.LogVerbose($"ProcessInstructionsAndOptions: Found {instructionsList.Count} instruction items");

				// Check if we have KeyValuePair objects (YAML deserialization issue)
				var keyValuePairs = instructionsList.Where(item => item.GetType().Name.StartsWith("KeyValuePair")).ToList();
				bool hasKeyValuePairs = keyValuePairs.Count > 0;

				if ( hasKeyValuePairs )
				{
					Logger.LogVerbose($"ProcessInstructionsAndOptions: Found {keyValuePairs.Count} KeyValuePair instruction items, grouping them");
					var processedInstructions = GroupKeyValuePairsIntoInstructions(keyValuePairs);
					processedDict["Instructions"] = processedInstructions;
				}
				else
				{
					Logger.LogVerbose("ProcessInstructionsAndOptions: No KeyValuePair instruction items, processing individually");
					var processedInstructions = new List<object>();
					var currentInstruction = new Dictionary<string, object>();

					foreach ( object item in instructionsList )
					{
						Logger.LogVerbose($"ProcessInstructionsAndOptions: Processing instruction item of type {item.GetType().Name}");

						if ( item is KeyValuePair<string, object> kvp )
						{
							Logger.LogVerbose($"ProcessInstructionsAndOptions: KeyValuePair - {kvp.Key} = {kvp.Value}");
							currentInstruction[kvp.Key] = kvp.Value;

							// Check if this completes an instruction (has Action field)
							if ( kvp.Key.Equals("Action", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kvp.Value?.ToString()) )
							{
								if ( currentInstruction.Count > 0 )
								{
									processedInstructions.Add(new Dictionary<string, object>(currentInstruction));
									currentInstruction.Clear();
								}
							}
						}
						else if ( item is Dictionary<string, object> dict )
						{
							Logger.LogVerbose($"ProcessInstructionsAndOptions: Dictionary with {dict.Count} keys: {string.Join(", ", dict.Keys)}");
							if ( currentInstruction.Count > 0 )
							{
								processedInstructions.Add(new Dictionary<string, object>(currentInstruction));
								currentInstruction.Clear();
							}
							processedInstructions.Add(dict);
						}
						else
						{
							Logger.LogVerbose($"ProcessInstructionsAndOptions: Unknown item type {item.GetType().Name}: {item}");
						}
					}

					if ( currentInstruction.Count > 0 )
					{
						processedInstructions.Add(new Dictionary<string, object>(currentInstruction));
					}

					Logger.LogVerbose($"ProcessInstructionsAndOptions: Processed {processedInstructions.Count} instructions");
					processedDict["Instructions"] = processedInstructions;
				}
			}

			// Process Options
			if ( processedDict.TryGetValue("Options", out object optionsValue) && optionsValue is List<object> optionsList )
			{
				Logger.LogVerbose($"ProcessInstructionsAndOptions: Found {optionsList.Count} option items");

				// Check if we have KeyValuePair objects (YAML deserialization issue)
				var keyValuePairs = optionsList.Where(item => item.GetType().Name.StartsWith("KeyValuePair")).ToList();
				bool hasKeyValuePairs = keyValuePairs.Count > 0;

				if ( hasKeyValuePairs )
				{
					Logger.LogVerbose($"ProcessInstructionsAndOptions: Found {keyValuePairs.Count} KeyValuePair option items, grouping them");
					var processedOptions = GroupKeyValuePairsIntoOptions(keyValuePairs);
					processedDict["Options"] = processedOptions;
				}
				else
				{
					Logger.LogVerbose("ProcessInstructionsAndOptions: No KeyValuePair option items, processing individually");
					var processedOptions = new List<object>();
					var currentOption = new Dictionary<string, object>();

					foreach ( object item in optionsList )
					{
						Logger.LogVerbose($"ProcessInstructionsAndOptions: Processing option item of type {item.GetType().Name}");

						if ( item is KeyValuePair<string, object> kvp )
						{
							Logger.LogVerbose($"ProcessInstructionsAndOptions: KeyValuePair - {kvp.Key} = {kvp.Value}");
							currentOption[kvp.Key] = kvp.Value;

							// Check if this completes an option (has Name field)
							if ( kvp.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kvp.Value?.ToString()) )
							{
								if ( currentOption.Count > 0 )
								{
									processedOptions.Add(new Dictionary<string, object>(currentOption));
									currentOption.Clear();
								}
							}
						}
						else if ( item is Dictionary<string, object> dict )
						{
							Logger.LogVerbose($"ProcessInstructionsAndOptions: Dictionary with {dict.Count} keys: {string.Join(", ", dict.Keys)}");
							if ( currentOption.Count > 0 )
							{
								processedOptions.Add(new Dictionary<string, object>(currentOption));
								currentOption.Clear();
							}
							processedOptions.Add(dict);
						}
						else
						{
							Logger.LogVerbose($"ProcessInstructionsAndOptions: Unknown item type {item.GetType().Name}: {item}");
						}
					}

					if ( currentOption.Count > 0 )
					{
						processedOptions.Add(new Dictionary<string, object>(currentOption));
					}

					Logger.LogVerbose($"ProcessInstructionsAndOptions: Processed {processedOptions.Count} options");
					processedDict["Options"] = processedOptions;
				}
			}
		}

		/// <summary>
		/// Processes component-level properties that might be converted to KeyValuePair objects during YAML deserialization.
		/// </summary>
		private static void ProcessComponentLevelProperties(Dictionary<string, object> processedDict)
		{
			// Check if any component-level properties are KeyValuePair objects
			var keyValuePairKeys = new List<string>();
			foreach ( var kvp in processedDict )
			{
				if ( kvp.Value != null && kvp.Value.GetType().Name.StartsWith("KeyValuePair") )
				{
					keyValuePairKeys.Add(kvp.Key);
				}
			}

			if ( keyValuePairKeys.Count > 0 )
			{
				Logger.LogVerbose($"ProcessComponentLevelProperties: Found {keyValuePairKeys.Count} component-level KeyValuePair properties: {string.Join(", ", keyValuePairKeys)}");

				foreach ( var key in keyValuePairKeys )
				{
					var kvp = processedDict[key];
					var kvpType = kvp.GetType();
					var keyProperty = kvpType.GetProperty("Key");
					var valueProperty = kvpType.GetProperty("Value");

					if ( keyProperty != null && valueProperty != null )
					{
						var kvpKey = keyProperty.GetValue(kvp)?.ToString();
						var kvpValue = valueProperty.GetValue(kvp);

						// Convert string values back to appropriate types
						if ( kvpValue is string stringValue )
						{
							if ( bool.TryParse(stringValue, out bool boolValue) )
							{
								kvpValue = boolValue;
							}
							else if ( int.TryParse(stringValue, out int intValue) )
							{
								kvpValue = intValue;
							}
							else if ( Guid.TryParse(stringValue, out Guid guidValue) )
							{
								kvpValue = guidValue;
							}
						}

						Logger.LogVerbose($"ProcessComponentLevelProperties: Processing {kvpKey} = {kvpValue}");
						processedDict[kvpKey] = kvpValue;
					}
				}
			}
		}

		/// <summary>
		/// Groups KeyValuePair objects into complete instruction dictionaries.
		/// </summary>
		private static List<object> GroupKeyValuePairsIntoInstructions(List<object> kvpList)
		{
			var instructions = new List<object>();
			var instructionGroups = new Dictionary<string, Dictionary<string, object>>();
			var currentInstructionIndex = 0;
			string currentGroupKey = null;

			foreach ( var kvp in kvpList )
			{
				// Use reflection to get Key and Value from KeyValuePair
				var kvpType = kvp.GetType();
				var keyProperty = kvpType.GetProperty("Key");
				var valueProperty = kvpType.GetProperty("Value");

				if ( keyProperty != null && valueProperty != null )
				{
					var key = keyProperty.GetValue(kvp)?.ToString();
					var value = valueProperty.GetValue(kvp);

					// Convert string values back to appropriate types
					if ( value is string stringValue )
					{
						if ( bool.TryParse(stringValue, out bool boolValue) )
						{
							value = boolValue;
						}
						else if ( int.TryParse(stringValue, out int intValue) )
						{
							value = intValue;
						}
						else if ( Guid.TryParse(stringValue, out Guid guidValue) )
						{
							value = guidValue;
						}
					}

					Logger.LogVerbose($"GroupKeyValuePairsIntoInstructions: Processing {key} = {value}");

					// Group by GUID if available (for TOML), otherwise use position-based grouping (for YAML/XML/JSON)
					if ( key.Equals("Guid", StringComparison.OrdinalIgnoreCase) && value != null )
					{
						currentGroupKey = value.ToString();
					}
					else if ( key.Equals("Action", StringComparison.OrdinalIgnoreCase) && value != null )
					{
						// For formats without GUIDs, use position-based grouping
						currentGroupKey = $"instruction_{currentInstructionIndex}";
						currentInstructionIndex++;
					}

					if ( currentGroupKey != null )
					{
						if ( !instructionGroups.ContainsKey(currentGroupKey) )
						{
							instructionGroups[currentGroupKey] = new Dictionary<string, object>();
						}
						instructionGroups[currentGroupKey][key] = value;
					}
				}
			}

			// Convert grouped instructions to list
			foreach ( var instruction in instructionGroups.Values )
			{
				if ( instruction.Count > 0 )
				{
					Logger.LogVerbose($"GroupKeyValuePairsIntoInstructions: Completed instruction with {instruction.Count} fields");
					instructions.Add(new Dictionary<string, object>(instruction));
				}
			}

			Logger.LogVerbose($"GroupKeyValuePairsIntoInstructions: Grouped {kvpList.Count} KeyValuePairs into {instructions.Count} instructions");
			return instructions;
		}

		/// <summary>
		/// Groups KeyValuePair objects into complete option dictionaries.
		/// </summary>
		private static List<object> GroupKeyValuePairsIntoOptions(List<object> kvpList)
		{
			var options = new List<object>();
			var currentOption = new Dictionary<string, object>();

			foreach ( var kvp in kvpList )
			{
				// Use reflection to get Key and Value from KeyValuePair
				var kvpType = kvp.GetType();
				var keyProperty = kvpType.GetProperty("Key");
				var valueProperty = kvpType.GetProperty("Value");

				if ( keyProperty != null && valueProperty != null )
				{
					var key = keyProperty.GetValue(kvp)?.ToString();
					var value = valueProperty.GetValue(kvp);

					// Convert string values back to appropriate types
					if ( value is string stringValue )
					{
						if ( bool.TryParse(stringValue, out bool boolValue) )
						{
							value = boolValue;
						}
						else if ( int.TryParse(stringValue, out int intValue) )
						{
							value = intValue;
						}
						else if ( Guid.TryParse(stringValue, out Guid guidValue) )
						{
							value = guidValue;
						}
					}

					Logger.LogVerbose($"GroupKeyValuePairsIntoOptions: Processing {key} = {value}");
					currentOption[key] = value;

					// Check if this completes an option (has Name field and we've seen a Guid)
					if ( key.Equals("Name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value?.ToString()) && currentOption.ContainsKey("Guid") )
					{
						if ( currentOption.Count > 0 )
						{
							Logger.LogVerbose($"GroupKeyValuePairsIntoOptions: Completed option with {currentOption.Count} fields");
							options.Add(new Dictionary<string, object>(currentOption));
							currentOption.Clear();
						}
					}
				}
			}

			// Add any remaining option
			if ( currentOption.Count > 0 )
			{
				Logger.LogVerbose($"GroupKeyValuePairsIntoOptions: Adding final option with {currentOption.Count} fields");
				options.Add(new Dictionary<string, object>(currentOption));
			}

			Logger.LogVerbose($"GroupKeyValuePairsIntoOptions: Grouped {kvpList.Count} KeyValuePairs into {options.Count} options");
			return options;
		}

		/// <summary>
		/// Deserializes a component from a dictionary with all conditional logic unified.
		/// This is the migrated version from ModComponent.DeserializeComponent.
		/// </summary>
		public static ModComponent DeserializeComponent([NotNull] IDictionary<string, object> componentDict)
		{
			var component = new ModComponent();

			component.Guid = GetRequiredValue<Guid>(componentDict, key: "Guid");
			component.Name = GetRequiredValue<string>(componentDict, key: "Name");
			_ = Logger.LogVerboseAsync($" == Deserialize next component '{component.Name}' ==");
			component.Author = GetValueOrDefault<string>(componentDict, key: "Author") ?? string.Empty;
			component.Category = GetValueOrDefault<List<string>>(componentDict, key: "Category") ?? new List<string>();
			if ( component.Category.Count == 0 )
			{
				string categoryStr = GetValueOrDefault<string>(componentDict, key: "Category") ?? string.Empty;
				if ( !string.IsNullOrEmpty(categoryStr) )
				{
					component.Category = categoryStr.Split(
						new[] { ",", ";" },
						StringSplitOptions.RemoveEmptyEntries
					).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
				}
			}
			else if ( component.Category.Count == 1 )
			{
				string singleCategory = component.Category[0];
				if ( !string.IsNullOrEmpty(singleCategory) &&
					 (singleCategory.Contains(',') || singleCategory.Contains(';')) )
				{
					component.Category = singleCategory.Split(
						new[] { ",", ";" },
						StringSplitOptions.RemoveEmptyEntries
					).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
				}
				else if ( string.IsNullOrWhiteSpace(singleCategory) )
				{
					component.Category = new List<string>();
				}
			}
			component.Tier = GetValueOrDefault<string>(componentDict, key: "Tier") ?? string.Empty;
			component.Description = GetValueOrDefault<string>(componentDict, key: "Description") ?? string.Empty;
			component.DescriptionSpoilerFree = GetValueOrDefault<string>(componentDict, key: "DescriptionSpoilerFree") ?? string.Empty;
			component.InstallationMethod = GetValueOrDefault<string>(componentDict, key: "InstallationMethod") ?? string.Empty;
			component.Directions = GetValueOrDefault<string>(componentDict, key: "Directions") ?? string.Empty;
			component.DirectionsSpoilerFree = GetValueOrDefault<string>(componentDict, key: "DirectionsSpoilerFree") ?? string.Empty;
			component.DownloadInstructions = GetValueOrDefault<string>(componentDict, key: "DownloadInstructions") ?? string.Empty;
			component.DownloadInstructionsSpoilerFree = GetValueOrDefault<string>(componentDict, key: "DownloadInstructionsSpoilerFree") ?? string.Empty;
			component.UsageWarning = GetValueOrDefault<string>(componentDict, key: "UsageWarning") ?? string.Empty;
			component.UsageWarningSpoilerFree = GetValueOrDefault<string>(componentDict, key: "UsageWarningSpoilerFree") ?? string.Empty;
			component.Screenshots = GetValueOrDefault<string>(componentDict, key: "Screenshots") ?? string.Empty;
			component.ScreenshotsSpoilerFree = GetValueOrDefault<string>(componentDict, key: "ScreenshotsSpoilerFree") ?? string.Empty;
			component.Language = GetValueOrDefault<List<string>>(componentDict, key: "Language") ?? new List<string>();
			component.ExcludedDownloads = GetValueOrDefault<List<string>>(componentDict, key: "ExcludedDownloads") ?? new List<string>();

			// Load legacy ModLink format first (if present)
			List<string> legacyModLink = GetValueOrDefault<List<string>>(componentDict, key: "ModLink") ?? new List<string>();
			if ( legacyModLink == null || legacyModLink.Count == 0 )
			{
				string modLink = GetValueOrDefault<string>(componentDict, key: "ModLink") ?? string.Empty;
				if ( !string.IsNullOrEmpty(modLink) )
					legacyModLink = modLink.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
			}

			// Initialize ModLinkFilenames with legacy URLs (with null values = auto-discover)
			component.ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>(StringComparer.OrdinalIgnoreCase);
			if ( legacyModLink.Count > 0 )
			{
				_ = Logger.LogVerboseAsync($"Migrating legacy ModLink to ModLinkFilenames for component '{component.Name}' ({legacyModLink.Count} URLs)");
				foreach ( string url in legacyModLink )
				{
					if ( !string.IsNullOrWhiteSpace(url) )
					{
						// Create an entry with an empty filename dictionary - files will be discovered during download
						component.ModLinkFilenames[url] = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
					}
				}
			}

			// Load ModLinkFilenames (will merge/overwrite legacy entries if present)
			Dictionary<string, Dictionary<string, bool?>> deserializedFilenames = DeserializeModLinkFilenames(componentDict);
			if ( deserializedFilenames.Count > 0 )
			{
				foreach ( KeyValuePair<string, Dictionary<string, bool?>> kvp in deserializedFilenames )
				{
					component.ModLinkFilenames[kvp.Key] = kvp.Value;
				}
			}

			component.Dependencies = GetValueOrDefault<List<Guid>>(componentDict, key: "Dependencies") ?? new List<Guid>();
			component.Restrictions = GetValueOrDefault<List<Guid>>(componentDict, key: "Restrictions") ?? new List<Guid>();
			component.InstallBefore = GetValueOrDefault<List<Guid>>(componentDict, key: "InstallBefore") ?? new List<Guid>();
			component.InstallAfter = GetValueOrDefault<List<Guid>>(componentDict, key: "InstallAfter") ?? new List<Guid>();
			component.IsSelected = GetValueOrDefault<bool>(componentDict, key: "IsSelected");

			Logger.LogVerbose($"=== Processing Instructions for component '{component.Name}' ===");
			Logger.LogVerbose($"componentDict contains 'Instructions' key: {componentDict.ContainsKey("Instructions")}");
			if ( componentDict.ContainsKey("Instructions") )
			{
				object instructionsObj = componentDict["Instructions"];
				Logger.LogVerbose($"Instructions object type: {instructionsObj?.GetType().Name ?? "null"}, value: {instructionsObj}");
				if ( instructionsObj is IList<object> instructionsList )
				{
					Logger.LogVerbose($"Instructions is IList<object> with {instructionsList.Count} items");
					for ( int i = 0; i < Math.Min(instructionsList.Count, 3); i++ )
					{
						Logger.LogVerbose($"  Instruction {i}: type={instructionsList[i]?.GetType().Name ?? "null"}, value={instructionsList[i]}");
					}
				}
				else
				{
					Logger.LogVerbose($"Instructions is NOT IList<object>, actual type: {instructionsObj?.GetType().Name ?? "null"}, actual value: {instructionsObj}");
				}
			}
			else
			{
				Logger.LogVerbose($"componentDict does NOT contain 'Instructions' key. Available keys: {string.Join(", ", componentDict.Keys)}");
			}

			component.Instructions = DeserializeInstructions(
				GetValueOrDefault<IList<object>>(componentDict, key: "Instructions"), component
			);
			component.Options = DeserializeOptions(GetValueOrDefault<IList<object>>(componentDict, key: "Options"));
			_ = Logger.LogVerboseAsync($"Successfully deserialized component '{component.Name}'");

			return component;
		}

		/// <summary>
		/// Removes duplicate options from a component. Options are considered duplicates if they have identical instructions.
		/// Keeps the first occurrence and removes subsequent duplicates.
		/// </summary>
		private static void RemoveDuplicateOptions([NotNull] ModComponent component)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			if ( component.Options.Count <= 1 )
				return;

			var optionsToRemove = new List<int>();
			var guidsToRemove = new HashSet<Guid>();

			// Compare each option with all subsequent options
			for ( int i = 0; i < component.Options.Count; i++ )
			{
				if ( optionsToRemove.Contains(i) )
					continue;

				Option option1 = component.Options[i];

				for ( int j = i + 1; j < component.Options.Count; j++ )
				{
					if ( optionsToRemove.Contains(j) )
						continue;

					Option option2 = component.Options[j];

					// Compare instructions
					if ( AreInstructionsIdentical(option1.Instructions, option2.Instructions) )
					{
						// Mark option2 for removal (keep the earlier one)
						optionsToRemove.Add(j);
						guidsToRemove.Add(option2.Guid);
						Logger.LogWarning($"Component '{component.Name}': Duplicate option detected - '{option2.Name}' (GUID: {option2.Guid}) has identical instructions to '{option1.Name}' (GUID: {option1.Guid}). Removing duplicate.");
					}
				}
			}

			// Remove options in reverse order to maintain indices
			if ( optionsToRemove.Count > 0 )
			{
				foreach ( int index in optionsToRemove.OrderByDescending(x => x) )
				{
					component.Options.RemoveAt(index);
				}

				// Remove GUIDs from Choose instructions
				RemoveGuidsFromChooseInstructions(component, guidsToRemove);
			}
		}

		/// <summary>
		/// Compares two instruction collections to determine if they are identical.
		/// </summary>
		private static bool AreInstructionsIdentical(
			[NotNull][ItemNotNull] System.Collections.ObjectModel.ObservableCollection<Instruction> instructions1,
			[NotNull][ItemNotNull] System.Collections.ObjectModel.ObservableCollection<Instruction> instructions2)
		{
			if ( instructions1 == null || instructions2 == null )
				return false;

			if ( instructions1.Count != instructions2.Count )
				return false;

			for ( int i = 0; i < instructions1.Count; i++ )
			{
				Instruction instr1 = instructions1[i];
				Instruction instr2 = instructions2[i];

				if ( instr1.Action != instr2.Action )
					return false;
				if ( instr1.Destination != instr2.Destination )
					return false;
				if ( instr1.Arguments != instr2.Arguments )
					return false;
				if ( instr1.Overwrite != instr2.Overwrite )
					return false;

				// Compare Source lists
				if ( instr1.Source.Count != instr2.Source.Count )
					return false;
				for ( int s = 0; s < instr1.Source.Count; s++ )
				{
					if ( !string.Equals(instr1.Source[s], instr2.Source[s], StringComparison.Ordinal) )
						return false;
				}

				// Compare Dependencies
				if ( instr1.Dependencies.Count != instr2.Dependencies.Count )
					return false;
				if ( !instr1.Dependencies.SequenceEqual(instr2.Dependencies) )
					return false;

				// Compare Restrictions
				if ( instr1.Restrictions.Count != instr2.Restrictions.Count )
					return false;
				if ( !instr1.Restrictions.SequenceEqual(instr2.Restrictions) )
					return false;
			}

			return true;
		}

		/// <summary>
		/// Removes specified GUIDs from all Choose instruction Source lists in a component.
		/// </summary>
		private static void RemoveGuidsFromChooseInstructions([NotNull] ModComponent component, [NotNull] HashSet<Guid> guidsToRemove)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( guidsToRemove == null )
				throw new ArgumentNullException(nameof(guidsToRemove));

			foreach ( Instruction instruction in component.Instructions )
			{
				if ( instruction.Action == ActionType.Choose )
				{
					// Source contains GUIDs as strings
					List<string> originalSource = instruction.Source.ToList();
					instruction.Source.Clear();

					foreach ( string guidStr in originalSource )
					{
						if ( Guid.TryParse(guidStr, out Guid guid) )
						{
							if ( !guidsToRemove.Contains(guid) )
							{
								instruction.Source.Add(guidStr);
							}
							else
							{
								Logger.LogVerbose($"Removed GUID {guid} from Choose instruction {instruction.Guid}");
							}
						}
						else
						{
							// Keep non-GUID strings as-is
							instruction.Source.Add(guidStr);
						}
					}
				}
			}
		}

		public static void ParseMetadataSection(TomlTable tomlTable)
		{
			if ( tomlTable == null )
				return;
			MainConfig.FileFormatVersion = "2.0";
			MainConfig.TargetGame = string.Empty;
			MainConfig.BuildName = string.Empty;
			MainConfig.BuildAuthor = string.Empty;
			MainConfig.BuildDescription = string.Empty;
			MainConfig.LastModified = null;
			MainConfig.BeforeModListContent = string.Empty;
			MainConfig.AfterModListContent = string.Empty;
			MainConfig.WidescreenSectionContent = "Please install manually the widescreen implementations, e.g. uniws, before continuing.";
			MainConfig.AspyrSectionContent = string.Empty;
			try
			{
				if ( !tomlTable.TryGetValue("metadata", out object metadataObj) || !(metadataObj is TomlTable metadataTable) )
					return;
				if ( metadataTable.TryGetValue("fileFormatVersion", out object versionObj) )
					MainConfig.FileFormatVersion = versionObj.ToString() ?? "2.0";
				if ( metadataTable.TryGetValue("targetGame", out object gameObj) )
					MainConfig.TargetGame = gameObj.ToString() ?? string.Empty;
				if ( metadataTable.TryGetValue("buildName", out object nameObj) )
					MainConfig.BuildName = nameObj.ToString() ?? string.Empty;
				if ( metadataTable.TryGetValue("buildAuthor", out object authorObj) )
					MainConfig.BuildAuthor = authorObj.ToString() ?? string.Empty;
				if ( metadataTable.TryGetValue("buildDescription", out object descObj) )
					MainConfig.BuildDescription = descObj.ToString() ?? string.Empty;
				if ( metadataTable.TryGetValue("lastModified", out object modifiedObj) )
				{
					if ( DateTime.TryParse(modifiedObj.ToString(), out DateTime parsedDate) )
						MainConfig.LastModified = parsedDate;
				}
				if ( metadataTable.TryGetValue("beforeModListContent", out object beforeObj) )
					MainConfig.BeforeModListContent = beforeObj.ToString() ?? string.Empty;
				if ( metadataTable.TryGetValue("afterModListContent", out object afterObj) )
					MainConfig.AfterModListContent = afterObj.ToString() ?? string.Empty;
				if ( metadataTable.TryGetValue("widescreenSectionContent", out object widescreenObj) )
					MainConfig.WidescreenSectionContent = widescreenObj.ToString()
														  ?? "Please install manually the widescreen implementations, e.g. uniws, before continuing.";
				if ( metadataTable.TryGetValue("aspyrSectionContent", out object aspyrObj) )
					MainConfig.AspyrSectionContent = aspyrObj.ToString() ?? string.Empty;
				Logger.LogVerbose($"Loaded metadata: Game={MainConfig.TargetGame}, Version={MainConfig.FileFormatVersion}, Build={MainConfig.BuildName}");
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"Failed to parse metadata section (non-fatal): {ex.Message}");
			}
		}

		[ItemNotNull]
		[NotNull]
		internal static System.Collections.ObjectModel.ObservableCollection<Instruction> DeserializeInstructions(
				[CanBeNull][ItemCanBeNull] IList<object> instructionsSerializedList,
				object parentComponent
			)
		{
			string componentName = parentComponent is ModComponent mc ? mc.Name : parentComponent is Option opt ? opt.Name : "Unknown";

			if ( instructionsSerializedList == null || instructionsSerializedList.Count == 0 )
			{
				_ = Logger.LogWarningAsync($"No instructions found for component '{componentName}'");
				return new System.Collections.ObjectModel.ObservableCollection<Instruction>();
			}

			Logger.LogVerbose($"DeserializeInstructions called for '{componentName}' with {instructionsSerializedList.Count} items");
			for ( int i = 0; i < Math.Min(instructionsSerializedList.Count, 3); i++ )
			{
				Logger.LogVerbose($"  instructionsSerializedList[{i}] type: {instructionsSerializedList[i]?.GetType().Name ?? "null"}, value: {instructionsSerializedList[i]}");
			}

			// Check if we're dealing with individual instruction fields (KeyValuePair objects) that need to be grouped
			bool needsGrouping = instructionsSerializedList.Count > 0 && instructionsSerializedList[0] is KeyValuePair<string, object>;

			if ( needsGrouping )
			{
				Logger.LogVerbose($"Detected individual instruction fields, using GroupInstructionFieldsIntoInstructions");
				return GroupInstructionFieldsIntoInstructions(instructionsSerializedList, parentComponent);
			}

			var instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>();
			for ( int index = 0; index < instructionsSerializedList.Count; index++ )
			{
				Logger.LogVerbose($"Processing instruction {index + 1} for '{componentName}': {instructionsSerializedList[index]}");
				Dictionary<string, object> instructionDict =
					Utility.Serializer.SerializeIntoDictionary(instructionsSerializedList[index]);
				Logger.LogVerbose($"Serialized instruction dict: {string.Join(", ", instructionDict.Keys)}");

				// Only deserialize paths for non-Choose instructions (Choose instructions have GUIDs as sources)
				string strAction = GetValueOrDefault<string>(instructionDict, key: "Action");
				if ( !string.Equals(strAction, "Choose", StringComparison.OrdinalIgnoreCase) )
				{
					Utility.Serializer.DeserializePathInDictionary(instructionDict, key: "Source");
				}

				Utility.Serializer.DeserializeGuidDictionary(instructionDict, key: "Restrictions");
				Utility.Serializer.DeserializeGuidDictionary(instructionDict, key: "Dependencies");
				var instruction = new Instruction();

				// Set the GUID from the TOML data
				if ( instructionDict.TryGetValue("Guid", out object guidObj) && guidObj != null )
				{
					if ( Guid.TryParse(guidObj.ToString(), out Guid guid) )
					{
						instruction.Guid = guid;
					}
				}
				if ( string.Equals(strAction, "TSLPatcher", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(strAction, "HoloPatcher", StringComparison.OrdinalIgnoreCase) )
				{
					instruction.Action = ActionType.Patcher;
					_ = Logger.LogVerboseAsync($" -- Deserialize instruction #{index + 1} action '{strAction}' -> Patcher (backward compatibility)");
				}
				else if ( Enum.TryParse(strAction, ignoreCase: true, out ActionType action) )
				{
					instruction.Action = action;
					_ = Logger.LogVerboseAsync($" -- Deserialize instruction #{index + 1} action '{action}'");
				}
				else
				{
					_ = Logger.LogErrorAsync(
						$"{Environment.NewLine} -- Missing/invalid action for instruction #{index}"
					);
					instruction.Action = ActionType.Unset;
				}
				instruction.Arguments = GetValueOrDefault<string>(instructionDict, key: "Arguments") ?? string.Empty;
				// Default Overwrite behavior: Delete defaults to false (lenient), others default to true
				if ( instructionDict.ContainsKey("Overwrite") )
				{
					instruction.Overwrite = GetValueOrDefault<bool>(instructionDict, key: "Overwrite");
				}
				else
				{
					instruction.Overwrite = instruction.Action != ActionType.Delete;
				}
				instruction.Restrictions = GetValueOrDefault<List<Guid>>(instructionDict, key: "Restrictions")
					?? new List<Guid>();
				instruction.Dependencies = GetValueOrDefault<List<Guid>>(instructionDict, key: "Dependencies")
					?? new List<Guid>();
				instruction.Source = GetValueOrDefault<List<string>>(instructionDict, key: "Source")
					?? new List<string>();
				instruction.Destination = GetValueOrDefault<string>(instructionDict, key: "Destination") ?? string.Empty;
				instructions.Add(instruction);
				if ( parentComponent is ModComponent parentMc )
					instruction.SetParentComponent(parentMc);
			}
			return instructions;
		}

		/// <summary>
		/// Groups individual KeyValuePair fields into complete instruction dictionaries.
		/// This handles the case where Tomlyn parses [[thisMod.Instructions]] as separate field entries.
		/// </summary>
		[ItemNotNull]
		[NotNull]
		private static System.Collections.ObjectModel.ObservableCollection<Instruction> GroupInstructionFieldsIntoInstructions(
			[NotNull] IList<object> instructionFields,
			object parentComponent)
		{
			string componentName;
			if ( parentComponent is ModComponent mc )
				componentName = mc.Name;
			else
				componentName = "Unknown";

			Logger.LogVerbose($"=== GroupInstructionFieldsIntoInstructions for '{componentName}' ===");
			Logger.LogVerbose($"Processing {instructionFields.Count} individual instruction fields");

			var instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>();
			var currentInstruction = new Dictionary<string, object>();

			foreach ( object fieldObj in instructionFields )
			{
				if ( fieldObj is KeyValuePair<string, object> kvp )
				{
					string key = kvp.Key;
					object value = kvp.Value;

					Logger.LogVerbose($"Processing field: {key} = {value} (type: {value?.GetType().Name ?? "null"})");

					// Convert Tomlyn types to standard .NET types
					object convertedValue = ConvertTomlynValue(value);

					// If this is a Guid field and we already have fields in the current instruction, it's a new instruction
					if ( key.Equals("Guid", StringComparison.OrdinalIgnoreCase) && currentInstruction.Count > 0 )
					{
						// We have a complete instruction, process it
						Logger.LogVerbose($"Found complete instruction with Guid: {(currentInstruction.ContainsKey("Guid") ? currentInstruction["Guid"] : "unknown")}");
						ProcessCompleteInstruction(currentInstruction, instructions, parentComponent);
						currentInstruction = new Dictionary<string, object>();
					}

					currentInstruction[key] = convertedValue;
				}
			}

			// Process the last instruction if there is one
			if ( currentInstruction.Count > 0 )
			{
				Logger.LogVerbose($"Processing final instruction with {currentInstruction.Count} fields");
				ProcessCompleteInstruction(currentInstruction, instructions, parentComponent);
			}

			Logger.LogVerbose($"Grouped {instructionFields.Count} fields into {instructions.Count} complete instructions");
			return instructions;
		}

		/// <summary>
		/// Converts Tomlyn-specific types to standard .NET types that can be processed by the instruction deserializer.
		/// </summary>
		private static object ConvertTomlynValue(object value)
		{
			if ( value == null )
				return null;

			// Handle TomlTableArray - preserve it for further processing (e.g., Instructions)
			if ( value is Tomlyn.Model.TomlTableArray )
			{
				return value;
			}

			// Handle TomlArray by converting to List<string>
			if ( value is Tomlyn.Model.TomlArray tomlArray )
			{
				var list = new List<string>();
				foreach ( object item in tomlArray )
				{
					list.Add(item?.ToString() ?? string.Empty);
				}
				return list;
			}

			// Handle other Tomlyn types by converting to string
			if ( value.GetType().Namespace?.StartsWith("Tomlyn") == true )
			{
				return value.ToString();
			}

			return value;
		}

		/// <summary>
		/// Processes a complete instruction dictionary and adds it to the instructions collection.
		/// </summary>
		private static void ProcessCompleteInstruction(
			Dictionary<string, object> instructionDict,
			System.Collections.ObjectModel.ObservableCollection<Instruction> instructions,
			object parentComponent)
		{
			Logger.LogVerbose($"Processing complete instruction with keys: {string.Join(", ", instructionDict.Keys)}");

			// Only deserialize paths for non-Choose instructions (Choose instructions have GUIDs as sources)
			string strAction = GetValueOrDefault<string>(instructionDict, key: "Action");
			if ( !string.Equals(strAction, "Choose", StringComparison.OrdinalIgnoreCase) )
			{
				Utility.Serializer.DeserializePathInDictionary(instructionDict, key: "Source");
			}

			Utility.Serializer.DeserializeGuidDictionary(instructionDict, key: "Restrictions");
			Utility.Serializer.DeserializeGuidDictionary(instructionDict, key: "Dependencies");

			var instruction = new Instruction();

			// Set the GUID from the TOML data
			if ( instructionDict.TryGetValue("Guid", out object guidObj) && guidObj != null )
			{
				if ( Guid.TryParse(guidObj.ToString(), out Guid guid) )
				{
					instruction.Guid = guid;
				}
			}
			if ( string.Equals(strAction, "TSLPatcher", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(strAction, "HoloPatcher", StringComparison.OrdinalIgnoreCase) )
			{
				instruction.Action = ActionType.Patcher;
				Logger.LogVerbose($"Instruction action '{strAction}' -> Patcher (backward compatibility)");
			}
			else if ( Enum.TryParse(strAction, ignoreCase: true, out ActionType action) )
			{
				instruction.Action = action;
				Logger.LogVerbose($"Instruction action: '{action}'");
			}
			else
			{
				Logger.LogError($"Missing/invalid action for instruction: {strAction}");
				instruction.Action = ActionType.Unset;
			}

			instruction.Arguments = GetValueOrDefault<string>(instructionDict, key: "Arguments") ?? string.Empty;
			if ( instructionDict.ContainsKey("Overwrite") )
				instruction.Overwrite = GetValueOrDefault<bool>(instructionDict, key: "Overwrite");
			else
				instruction.Overwrite = instruction.Action != ActionType.Delete;

			instruction.Restrictions = GetValueOrDefault<List<Guid>>(instructionDict, key: "Restrictions") ?? new List<Guid>();
			instruction.Dependencies = GetValueOrDefault<List<Guid>>(instructionDict, key: "Dependencies") ?? new List<Guid>();
			instruction.Source = GetValueOrDefault<List<string>>(instructionDict, key: "Source") ?? new List<string>();
			instruction.Destination = GetValueOrDefault<string>(instructionDict, key: "Destination") ?? string.Empty;

			instructions.Add(instruction);
			if ( parentComponent is ModComponent parentMc )
				instruction.SetParentComponent(parentMc);

			Logger.LogVerbose($"Successfully created instruction: Action={instruction.Action}, Source.Count={instruction.Source.Count}, Destination='{instruction.Destination}'");
		}

		[ItemNotNull]
		[NotNull]
		internal static System.Collections.ObjectModel.ObservableCollection<Option> DeserializeOptions(
			[CanBeNull][ItemCanBeNull] IList<object> optionsSerializedList
		)
		{
			if ( optionsSerializedList == null || optionsSerializedList.Count == 0 )
				return new System.Collections.ObjectModel.ObservableCollection<Option>();

			Logger.LogVerbose($"DeserializeOptions called with {optionsSerializedList.Count} items");
			for ( int i = 0; i < Math.Min(optionsSerializedList.Count, 3); i++ )
			{
				Logger.LogVerbose($"  optionsSerializedList[{i}] type: {optionsSerializedList[i]?.GetType().Name ?? "null"}, value: {optionsSerializedList[i]}");
			}

			// Check if we're dealing with individual option fields (KeyValuePair objects) that need to be grouped
			bool needsGrouping = optionsSerializedList.Count > 0 && optionsSerializedList[0] is KeyValuePair<string, object>;

			if ( needsGrouping )
			{
				Logger.LogVerbose($"Detected individual option fields, using GroupOptionFieldsIntoOptions");
				return GroupOptionFieldsIntoOptions(optionsSerializedList);
			}

			var options = new System.Collections.ObjectModel.ObservableCollection<Option>();
			for ( int index = 0; index < optionsSerializedList.Count; index++ )
			{
				// Handle both KeyValuePair<string, object> (from TOML array of tables) and direct IDictionary
				IDictionary<string, object> optionsDict;
				if ( optionsSerializedList[index] is KeyValuePair<string, object> kvp )
				{
					optionsDict = kvp.Value as IDictionary<string, object>;
				}
				else
				{
					optionsDict = optionsSerializedList[index] as IDictionary<string, object>;
				}

				if ( optionsDict is null )
					continue;
				Utility.Serializer.DeserializeGuidDictionary(optionsDict, key: "Restrictions");
				Utility.Serializer.DeserializeGuidDictionary(optionsDict, key: "Dependencies");
				var option = new Option();
				_ = Logger.LogVerboseAsync($"-- Deserialize option #{index + 1}");
				option.Name = GetRequiredValue<string>(optionsDict, key: "Name");
				option.Description = GetValueOrDefault<string>(optionsDict, key: "Description") ?? string.Empty;
				_ = Logger.LogVerboseAsync($" == Deserialize next option '{option.Name}' ==");
				option.Guid = GetRequiredValue<Guid>(optionsDict, key: "Guid");
				option.Restrictions =
					GetValueOrDefault<List<Guid>>(optionsDict, key: "Restrictions") ?? new List<Guid>();
				option.Dependencies =
					GetValueOrDefault<List<Guid>>(optionsDict, key: "Dependencies") ?? new List<Guid>();
				option.Instructions = DeserializeInstructions(
					GetValueOrDefault<IList<object>>(optionsDict, key: "Instructions"), option
				);
				option.IsSelected = GetValueOrDefault<bool>(optionsDict, key: "IsSelected");
				options.Add(option);
			}
			return options;
		}

		/// <summary>
		/// Groups individual KeyValuePair fields into complete option dictionaries.
		/// This handles the case where Tomlyn parses [[thisMod.Options]] as separate field entries.
		/// </summary>
		[ItemNotNull]
		[NotNull]
		private static System.Collections.ObjectModel.ObservableCollection<Option> GroupOptionFieldsIntoOptions(
			[NotNull] IList<object> optionFields)
		{
			Logger.LogVerbose($"=== GroupOptionFieldsIntoOptions ===");
			Logger.LogVerbose($"Processing {optionFields.Count} individual option fields");

			var options = new System.Collections.ObjectModel.ObservableCollection<Option>();
			var currentOption = new Dictionary<string, object>();

			foreach ( object fieldObj in optionFields )
			{
				if ( fieldObj is KeyValuePair<string, object> kvp )
				{
					string key = kvp.Key;
					object value = kvp.Value;

					Logger.LogVerbose($"Processing option field: {key} = {value} (type: {value?.GetType().Name ?? "null"})");

					// Convert Tomlyn types to standard .NET types
					object convertedValue = ConvertTomlynValue(value);

					// If this is a Guid field and we already have fields in the current option, it's a new option
					if ( key.Equals("Guid", StringComparison.OrdinalIgnoreCase) && currentOption.Count > 0 )
					{
						// We have a complete option, process it
						Logger.LogVerbose($"Found complete option with Guid: {(currentOption.ContainsKey("Guid") ? currentOption["Guid"] : "unknown")}");
						ProcessCompleteOption(currentOption, options);
						currentOption = new Dictionary<string, object>();
					}

					currentOption[key] = convertedValue;
				}
			}

			// Process the last option if there is one
			if ( currentOption.Count > 0 )
			{
				Logger.LogVerbose($"Processing final option with {currentOption.Count} fields");
				ProcessCompleteOption(currentOption, options);
			}

			Logger.LogVerbose($"Grouped {optionFields.Count} fields into {options.Count} complete options");
			return options;
		}

		/// <summary>
		/// Processes a complete option dictionary and adds it to the options collection.
		/// </summary>
		private static void ProcessCompleteOption(
			Dictionary<string, object> optionDict,
			System.Collections.ObjectModel.ObservableCollection<Option> options)
		{
			Logger.LogVerbose($"Processing complete option with keys: {string.Join(", ", optionDict.Keys)}");

			Utility.Serializer.DeserializeGuidDictionary(optionDict, key: "Restrictions");
			Utility.Serializer.DeserializeGuidDictionary(optionDict, key: "Dependencies");

			var option = new Option();
			option.Name = GetRequiredValue<string>(optionDict, key: "Name");
			option.Description = GetValueOrDefault<string>(optionDict, key: "Description") ?? string.Empty;
			option.Guid = GetRequiredValue<Guid>(optionDict, key: "Guid");
			option.Restrictions = GetValueOrDefault<List<Guid>>(optionDict, key: "Restrictions") ?? new List<Guid>();
			option.Dependencies = GetValueOrDefault<List<Guid>>(optionDict, key: "Dependencies") ?? new List<Guid>();
			option.IsSelected = GetValueOrDefault<bool>(optionDict, key: "IsSelected");

			// Process option instructions if present
			if ( optionDict.ContainsKey("Instructions") )
			{
				object instructionsObj = optionDict["Instructions"];
				Logger.LogVerbose($"Option '{option.Name}' has Instructions field: type={instructionsObj?.GetType().Name ?? "null"}");

				// Handle TomlTableArray (from Tomlyn parser)
				if ( instructionsObj is Tomlyn.Model.TomlTableArray tomlTableArray )
				{
					Logger.LogVerbose($"Option Instructions is TomlTableArray with {tomlTableArray.Count} items");
					var instructionsList = new List<object>();
					foreach ( var item in tomlTableArray )
					{
						instructionsList.Add(item);
					}
					option.Instructions = DeserializeInstructions(instructionsList, option);
					Logger.LogVerbose($"Option '{option.Name}' now has {option.Instructions.Count} instructions");
				}
				else if ( instructionsObj is IList<object> instructionsList )
				{
					Logger.LogVerbose($"Option Instructions is IList<object> with {instructionsList.Count} items");
					option.Instructions = DeserializeInstructions(instructionsList, option);
					Logger.LogVerbose($"Option '{option.Name}' now has {option.Instructions.Count} instructions");
				}
				else
				{
					Logger.LogVerbose($"Option Instructions is NOT IList<object>, actual type: {instructionsObj?.GetType().Name ?? "null"}");
				}
			}

			options.Add(option);
			Logger.LogVerbose($"Successfully created option: Name='{option.Name}', Guid={option.Guid}, Instructions.Count={option.Instructions.Count}");
		}

		[NotNull]
		internal static T GetRequiredValue<T>(
			[NotNull] IDictionary<string, object> dict,
			[NotNull] string key)
		{
			T value = GetValue<T>(dict, key, required: true);
			return value == null
				? throw new InvalidOperationException("GetValue cannot return null for a required value.")
				: value;
		}

		[CanBeNull]
		internal static T GetValueOrDefault<T>(
			[NotNull] IDictionary<string, object> dict,
			[NotNull] string key) =>
			GetValue<T>(dict, key, required: false);

		[CanBeNull]
		internal static T GetValue<T>(
			[NotNull] IDictionary<string, object> dict,
			[NotNull] string key, bool required)
		{
			try
			{
				if ( dict is null )
					throw new ArgumentNullException(nameof(dict));
				if ( key is null )
					throw new ArgumentNullException(nameof(key));

				// Handle duplicate keys by consolidating values
				object value = null;

				// First try exact key match
				if ( dict.TryGetValue(key, out value) )
				{
					// Check if this is a collection that might contain duplicates
					if ( value is System.Collections.IEnumerable valueEnumerable && !(value is string) )
					{
						// For collections, consolidate duplicates by flattening nested collections
						var consolidatedList = new List<object>();
						foreach ( object item in valueEnumerable )
						{
							if ( item is System.Collections.IEnumerable nestedEnumerable && !(item is string) )
							{
								// Flatten nested collections (handles duplicate Instructions, etc.)
								foreach ( object nestedItem in nestedEnumerable )
								{
									consolidatedList.Add(nestedItem);
								}
							}
							else
							{
								consolidatedList.Add(item);
							}
						}
						value = consolidatedList;
					}
				}
				else
				{
					// Try case-insensitive match
					string caseInsensitiveKey = dict.Keys.FirstOrDefault(
						k => !(k is null) && k.Equals(key, StringComparison.OrdinalIgnoreCase)
					);
					if ( caseInsensitiveKey != null && dict.TryGetValue(caseInsensitiveKey, out value) )
					{
						// Check if this is a collection that might contain duplicates
						if ( value is System.Collections.IEnumerable caseInsensitiveEnumerable && !(value is string) )
						{
							// For collections, consolidate duplicates by flattening nested collections
							var consolidatedList = new List<object>();
							foreach ( object item in caseInsensitiveEnumerable )
							{
								if ( item is System.Collections.IEnumerable nestedEnumerable && !(item is string) )
								{
									// Flatten nested collections (handles duplicate Instructions, etc.)
									foreach ( object nestedItem in nestedEnumerable )
									{
										consolidatedList.Add(nestedItem);
									}
								}
								else
								{
									consolidatedList.Add(item);
								}
							}
							value = consolidatedList;
						}
					}
				}

				if ( value == null )
				{
					if ( !required )
						return default;
					throw new KeyNotFoundException($"[Error] Missing or invalid '{key}' field.");
				}
				Type targetType = typeof(T);
				switch ( value )
				{
					case null:
						throw new KeyNotFoundException($"[Error] Missing or invalid '{key}' field.");
					case T t:
						return t;
					case string valueStr:
						if ( string.IsNullOrEmpty(valueStr) )
						{
							return required
								? throw new KeyNotFoundException($"'{key}' field cannot be empty.")
								: default(T);
						}
						if ( targetType == typeof(Guid) )
						{
							string guidStr = Utility.Serializer.FixGuidString(valueStr);
							return !string.IsNullOrEmpty(guidStr) && Guid.TryParse(guidStr, out Guid guid)
								? (T)(object)guid
								: required
									? throw new ArgumentException($"'{key}' field is not a valid Guid!")
									: (T)(object)Guid.Empty;
						}
						if ( targetType == typeof(string) )
						{
#pragma warning disable CS8600
							// ReSharper disable once Possible System.InvalidCastException
							return (T)(object)valueStr;
#pragma warning restore CS8600
						}
						break;
				}

				// Backwards/forwards compatibility: String <-> List<string> conversion
				Type genericListDefinition = targetType.IsGenericType
					? targetType.GetGenericTypeDefinition()
					: null;

				// Converting string to List<string> (backwards compatibility)
				if ( (genericListDefinition == typeof(List<>) || genericListDefinition == typeof(IList<>))
					&& value is string delimitedString )
				{
					// Check if list element type is string
					Type[] genericArgs = typeof(T).GetGenericArguments();
					Type listElementType = genericArgs.Length > 0 ? genericArgs[0] : typeof(string);

					if ( listElementType == typeof(string) )
					{
						try
						{
							Type listType = typeof(List<>).MakeGenericType(listElementType);
							var list = (T)Activator.CreateInstance(listType);
							System.Reflection.MethodInfo addMethod = list?.GetType().GetMethod(name: "Add");

							// Split by semicolon or comma for delimited strings
							string[] parts = delimitedString.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
							foreach ( string part in parts )
							{
								string trimmed = part.Trim();
								if ( !string.IsNullOrWhiteSpace(trimmed) )
								{
									_ = addMethod?.Invoke(list, new object[] { trimmed });
								}
							}

							return list;
						}
						catch ( Exception ex )
						{
							Logger.LogWarning($"Failed to convert string to List<string> for '{key}': {ex.Message} - using default value");
							return default;
						}
					}
				}

				// Converting List<string> (or any IEnumerable<string>) to string (forwards compatibility)
				if ( targetType == typeof(string) &&
					value is System.Collections.IEnumerable enumerable &&
					!(value is string) )
				{
					try
					{
						// Try to join collection items as strings
						var items = new List<string>();
						foreach ( object item in enumerable )
						{
							string itemStr = item?.ToString();
							if ( !string.IsNullOrWhiteSpace(itemStr) )
							{
								items.Add(itemStr);
							}
						}

						if ( items.Count > 0 )
						{
							// Join with comma separator
							string result = string.Join(", ", items);
							return (T)(object)result;
						}
					}
					catch ( Exception ex )
					{
						Logger.LogWarning($"Failed to convert collection to string for '{key}': {ex.Message} - using default value");
						return default;
					}
				}

				if ( genericListDefinition == typeof(List<>) || genericListDefinition == typeof(IList<>) )
				{
					try
					{
						Type[] genericArgs = typeof(T).GetGenericArguments();
						Type listElementType = genericArgs.Length > 0
							? genericArgs[0]
							: typeof(string);
						Type listType = typeof(List<>).MakeGenericType(listElementType);
						var list = (T)Activator.CreateInstance(listType);
						System.Reflection.MethodInfo addMethod = list?.GetType().GetMethod(name: "Add");

						// Handle any IEnumerable (not just IEnumerable<object>)
						if ( value is System.Collections.IEnumerable collectionValue && !(value is string) )
						{
							foreach ( object item in collectionValue )
							{
								if ( listElementType == typeof(Guid)
									&& Guid.TryParse(item?.ToString(), out Guid guidItem) )
								{
									_ = addMethod?.Invoke(
										list,
										new[]
										{
									(object)guidItem,
										}
									);
								}
								else if ( listElementType == typeof(string) )
								{
									switch ( item )
									{
										case IEnumerable<object> nestedCollection when true:
											{
												foreach ( object nestedItem in nestedCollection )
												{
													string nestedStringValue = nestedItem?.ToString() ?? string.Empty;
													if ( !string.IsNullOrWhiteSpace(nestedStringValue) )
													{
														_ = addMethod?.Invoke(
															list,
															new[]
															{
															(object)nestedStringValue,
															}
														);
													}
												}

												break;
											}
										case string strItem:
											{
												if ( !string.IsNullOrWhiteSpace(strItem) )
												{
													_ = addMethod?.Invoke(
														list,
														new[]
														{
														(object)strItem,
														}
													);
												}

												break;
											}
										default:
											{
												string stringValue = item?.ToString() ?? string.Empty;
												if ( !string.IsNullOrWhiteSpace(stringValue) )
												{
													_ = addMethod?.Invoke(
														list,
														new[]
														{
														(object)stringValue,
														}
													);
												}

												break;
											}
									}
								}
								else
								{
									_ = addMethod?.Invoke(
										list,
										new[]
										{
									item,
										}
									);
								}
							}
						}
						else
						{
							_ = addMethod?.Invoke(
								list,
								new[]
								{
							value,
								}
							);
						}
						return list;
					}
					catch ( Exception ex )
					{
						Logger.LogWarning($"Failed to deserialize list field '{key}': {ex.Message} - using default value");
						return default;
					}
				}
				try
				{
					return (T)Convert.ChangeType(value, typeof(T));
				}
				catch ( Exception e )
				{
					Logger.LogWarning($"Could not deserialize key '{key}': {e.Message} - using default value");
					Logger.LogVerbose($"Deserialization error details for '{key}': {e}");
					return default;
				}
			}
			catch ( Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex )
			{
				Logger.LogWarning($"Runtime binding error for key '{key}': {ex.Message} - using default value");
				return default;
			}
			catch ( InvalidCastException ex )
			{
				Logger.LogWarning($"Invalid cast for key '{key}': {ex.Message} - using default value");
				return default;
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"Unexpected error deserializing key '{key}': {ex.Message} - using default value");
				return default;
			}
		}

		[CanBeNull]
		public static ModComponent DeserializeYamlComponent([NotNull] string yamlString)
		{
			if ( yamlString is null )
				throw new ArgumentNullException(nameof(yamlString));
			try
			{
				YamlSerialization.IDeserializer deserializer = new YamlSerialization.DeserializerBuilder()
					.WithNamingConvention(YamlSerialization.NamingConventions.PascalCaseNamingConvention.Instance)
					.IgnoreUnmatchedProperties()
					.Build();
				Dictionary<string, object> yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yamlString);
				if ( yamlDict == null )
				{
					Logger.LogError("Failed to deserialize YAML: result was null");
					return null;
				}

				// Pre-process the component dictionary to handle duplicate fields
				yamlDict = PreprocessComponentDictionary(yamlDict);

				var component = DeserializeComponent(yamlDict);
				return component;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to deserialize YAML component");
				return null;
			}
		}
		public static string SerializeModComponentAsTomlString(
			List<ModComponent> components,
			ComponentValidationContext validationContext = null)
		{
			Logger.LogVerbose("Saving to TOML string");
			var result = new StringBuilder();

			// Only include metadata section if there's meaningful content beyond the default values
			bool hasMeaningfulMetadata = !string.IsNullOrWhiteSpace(MainConfig.TargetGame) ||
										!string.IsNullOrWhiteSpace(MainConfig.BuildName) ||
										!string.IsNullOrWhiteSpace(MainConfig.BuildAuthor) ||
										!string.IsNullOrWhiteSpace(MainConfig.BuildDescription) ||
										MainConfig.LastModified.HasValue ||
										!string.IsNullOrWhiteSpace(MainConfig.BeforeModListContent) ||
										!string.IsNullOrWhiteSpace(MainConfig.AfterModListContent) ||
										(!string.IsNullOrWhiteSpace(MainConfig.WidescreenSectionContent) &&
										 MainConfig.WidescreenSectionContent != "Please install manually the widescreen implementations, e.g. uniws, before continuing.") ||
										!string.IsNullOrWhiteSpace(MainConfig.AspyrSectionContent);

			if ( hasMeaningfulMetadata )
			{
				var metadataTable = new TomlTable();

				// Only include fileFormatVersion if it's not the default "2.0"
				if ( MainConfig.FileFormatVersion != "2.0" )
					metadataTable["fileFormatVersion"] = MainConfig.FileFormatVersion ?? "2.0";
				else if ( !string.IsNullOrWhiteSpace(MainConfig.FileFormatVersion) )
					metadataTable["fileFormatVersion"] = MainConfig.FileFormatVersion;

				if ( !string.IsNullOrWhiteSpace(MainConfig.TargetGame) )
					metadataTable["targetGame"] = MainConfig.TargetGame;
				if ( !string.IsNullOrWhiteSpace(MainConfig.BuildName) )
					metadataTable["buildName"] = MainConfig.BuildName;
				if ( !string.IsNullOrWhiteSpace(MainConfig.BuildAuthor) )
					metadataTable["buildAuthor"] = MainConfig.BuildAuthor;
				if ( !string.IsNullOrWhiteSpace(MainConfig.BuildDescription) )
					metadataTable["buildDescription"] = MainConfig.BuildDescription;
				if ( MainConfig.LastModified.HasValue )
					metadataTable["lastModified"] = MainConfig.LastModified.Value;
				if ( !string.IsNullOrWhiteSpace(MainConfig.BeforeModListContent) )
					metadataTable["beforeModListContent"] = MainConfig.BeforeModListContent;
				if ( !string.IsNullOrWhiteSpace(MainConfig.AfterModListContent) )
					metadataTable["afterModListContent"] = MainConfig.AfterModListContent;
				if ( !string.IsNullOrWhiteSpace(MainConfig.WidescreenSectionContent) )
					metadataTable["widescreenSectionContent"] = MainConfig.WidescreenSectionContent;
				if ( !string.IsNullOrWhiteSpace(MainConfig.AspyrSectionContent) )
					metadataTable["aspyrSectionContent"] = MainConfig.AspyrSectionContent;

				var metadataRoot = new Dictionary<string, object> { ["metadata"] = metadataTable };
				result.AppendLine(Toml.FromModel(metadataRoot));
			}

			bool isFirst = true;
			foreach ( ModComponent component in components )
			{
				if ( !isFirst )
				{
					result.AppendLine();
					result.AppendLine();
				}
				isFirst = false;

				// TOML-specific: Add validation comments for component issues
				if ( validationContext != null && validationContext.HasIssues(component.Guid) )
				{
					List<string> componentIssues = validationContext.GetComponentIssues(component.Guid);
					if ( componentIssues.Count > 0 )
					{
						result.AppendLine("# VALIDATION ISSUES:");
						foreach ( string issue in componentIssues )
						{
							result.AppendLine($"# {issue}");
						}
					}
				}

				// TOML-specific: Add URL failure comments
				if ( validationContext != null && component.ModLinkFilenames != null && component.ModLinkFilenames.Count > 0 )
				{
					foreach ( string url in component.ModLinkFilenames.Keys )
					{
						List<string> urlFailures = validationContext.GetUrlFailures(url);
						if ( urlFailures.Count > 0 )
						{
							result.AppendLine($"# URL RESOLUTION FAILURE: {url}");
							foreach ( string failure in urlFailures )
							{
								result.AppendLine($"# {failure}");
							}
						}
					}
				}

				// Use unified serialization
				Dictionary<string, object> componentDict = SerializeComponentToDictionary(component, validationContext);

				var nestedContent = new StringBuilder();
				Dictionary<string, object> modLinkFilenamesDict = FixSerializedTomlDict(componentDict, nestedContent, validationContext, component);

				var rootTable = new Dictionary<string, object>
				{
					["thisMod"] = componentDict
				};
				string componentToml = Toml.FromModel(rootTable).Replace("[thisMod]", "[[thisMod]]");

				// Insert ModLinkFilenames inline if present
				if ( modLinkFilenamesDict != null && modLinkFilenamesDict.Count > 0 )
				{
					var mlf = new StringBuilder();
					mlf.Append("ModLinkFilenames = { ");

					bool firstUrl = true;
					foreach ( var urlEntry in modLinkFilenamesDict )
					{
						if ( !firstUrl )
							mlf.Append(", ");
						firstUrl = false;

						string url = urlEntry.Key;
						mlf.Append('"');
						mlf.Append(url.Replace("\"", "\\\""));
						mlf.Append("\" = { ");

						if ( urlEntry.Value is Dictionary<string, object> filenamesDict && filenamesDict.Count > 0 )
						{
							bool firstFile = true;
							foreach ( var fileEntry in filenamesDict )
							{
								if ( !firstFile )
									mlf.Append(", ");
								firstFile = false;

								string filename = fileEntry.Key;
								mlf.Append('"');
								mlf.Append(filename.Replace("\"", "\\\""));
								mlf.Append("\" = ");

								if ( fileEntry.Value is bool boolVal )
									mlf.Append(boolVal ? "true" : "false");
								else if ( fileEntry.Value is string strVal && strVal == "null" )
									mlf.Append("\"null\"");
								else
									mlf.Append("\"null\"");
							}
						}

						mlf.Append(" }");
					}

					mlf.AppendLine(" }");

					// Insert after the [[thisMod]] line
					int insertPos = componentToml.IndexOf('\n');
					if ( insertPos > 0 )
						componentToml = componentToml.Insert(insertPos + 1, mlf.ToString());
				}

				result.Append(componentToml.TrimEnd());

				if ( nestedContent.Length <= 0 )
					continue;
				result.AppendLine();
				result.Append(nestedContent.ToString());
			}

			return SanitizeUtf8(Utility.Serializer.FixWhitespaceIssues(result.ToString()));
		}

		private static Dictionary<string, object> FixSerializedTomlDict(
			Dictionary<string, object> serializedComponentDict,
			StringBuilder tomlString,
			ComponentValidationContext validationContext = null,
			ModComponent component = null
		)
		{
			if ( serializedComponentDict == null )
				throw new ArgumentNullException(nameof(serializedComponentDict));
			if ( tomlString == null )
				throw new ArgumentNullException(nameof(tomlString));

			// Remove metadata fields that were added by unified serialization (they're for format-specific use only)
			serializedComponentDict.Remove("_ValidationIssues");
			serializedComponentDict.Remove("_UrlFailures");
			serializedComponentDict.Remove("_HasInstructions");

			if ( serializedComponentDict.TryGetValue("Instructions", out object val) )
			{
				List<Dictionary<string, object>> instructionsList = null;

				if ( val is List<Dictionary<string, object>> list )
					instructionsList = list;
				else if ( val is IEnumerable<Dictionary<string, object>> enumerable )
					instructionsList = enumerable.ToList();

				if ( instructionsList != null && instructionsList.Count > 0 )
				{
					int instructionIndex = 0;
					foreach ( Dictionary<string, object> item in instructionsList )
					{
						if ( item == null || item.Count == 0 )
							continue;

						// Add validation comments for instruction issues
						if ( validationContext != null && component != null && instructionIndex < component.Instructions.Count )
						{
							Instruction instruction = component.Instructions[instructionIndex];
							List<string> instructionIssues = validationContext.GetInstructionIssues(instruction.Guid);
							if ( instructionIssues.Count > 0 )
							{
								tomlString.AppendLine();
								tomlString.AppendLine("# INSTRUCTION VALIDATION ISSUES:");
								foreach ( string issue in instructionIssues )
								{
									tomlString.AppendLine($"# {issue}");
								}
							}
						}

						var model = new Dictionary<string, object>
						{
							{
								"thisMod", new Dictionary<string, object>
								{
									{ "Instructions", item }
								}
							}
						};
						tomlString.AppendLine();
						tomlString.Append(Toml.FromModel(model).Replace("thisMod.Instructions", $"[thisMod.Instructions]"));
						instructionIndex++;
					}
				}

				serializedComponentDict.Remove("Instructions");
			}

			// Remove ModLinkFilenames - we'll add it manually after the main TOML generation
			Dictionary<string, object> modLinkFilenamesDict = null;
			if ( serializedComponentDict.TryGetValue("ModLinkFilenames", out object modLinkFilenamesVal) )
			{
				if ( modLinkFilenamesVal is Dictionary<string, object> mlf )
				{
					modLinkFilenamesDict = mlf;
					serializedComponentDict.Remove("ModLinkFilenames");
				}
			}

			bool hasOptions = serializedComponentDict.ContainsKey("Options");
			bool hasOptionsInstructions = serializedComponentDict.ContainsKey("OptionsInstructions");

			if ( hasOptions && hasOptionsInstructions )
			{
				if (
					serializedComponentDict["Options"] is List<Dictionary<string, object>> optionsList &&
					serializedComponentDict["OptionsInstructions"] is List<Dictionary<string, object>> optionsInstructionsList &&
					optionsInstructionsList.Count > 0 )
				{
					var instructionsByParent = optionsInstructionsList
						.Where(instr => instr != null && instr.ContainsKey("Parent"))
						.GroupBy(instr => instr["Parent"]?.ToString())
						.ToDictionary(g => g.Key, g => g.ToList());

					foreach ( Dictionary<string, object> optionDict in optionsList )
					{
						if ( optionDict is null || optionDict.Count == 0 )
							continue;

						// CRITICAL: Remove the Instructions field from optionDict before serializing
						// We use [[thisMod.Options.Instructions]] sections instead of inline arrays
						optionDict.Remove("Instructions");
						// Remove internal metadata field
						optionDict.Remove("_HasInstructions");

						var optionModel = new Dictionary<string, object>
						{
							{
								"thisMod", new Dictionary<string, object>
								{
									{ "Options", optionDict }
								}
							}
						};
						tomlString.AppendLine();
						tomlString.Append(Toml.FromModel(optionModel).Replace("thisMod.Options", "[thisMod.Options]"));

						if ( !optionDict.TryGetValue("Guid", out object guidObj) )
							continue;
						string optionGuid = guidObj?.ToString();
						if ( string.IsNullOrEmpty(optionGuid) || !instructionsByParent.TryGetValue(optionGuid, out List<Dictionary<string, object>> instructions) )
							continue;
						// Find the option in the component
						Option currentOption = null;
						if ( component != null && Guid.TryParse(optionGuid, out Guid optGuid) )
							currentOption = component.Options.FirstOrDefault(opt => opt.Guid == optGuid);

						int optionInstrIndex = 0;
						foreach ( Dictionary<string, object> instruction in instructions.Where(instruction => instruction != null && instruction.Count != 0) )
						{
							// Add validation comments for option instruction issues
							if ( validationContext != null && currentOption != null && optionInstrIndex < currentOption.Instructions.Count )
							{
								Instruction optionInstruction = currentOption.Instructions[optionInstrIndex];
								List<string> instructionIssues = validationContext.GetInstructionIssues(optionInstruction.Guid);
								if ( instructionIssues.Count > 0 )
								{
									tomlString.AppendLine();
									tomlString.AppendLine("# OPTION INSTRUCTION VALIDATION ISSUES:");
									foreach ( string issue in instructionIssues )
									{
										tomlString.AppendLine($"# {issue}");
									}
								}
							}

							var instrModel = new Dictionary<string, object>
							{
								{
									"thisMod", new Dictionary<string, object>
									{
										{ "OptionsInstructions", instruction }
									}
								}
							};
							tomlString.Append(Toml.FromModel(instrModel).Replace(
								"thisMod.OptionsInstructions",
								"[thisMod.Options.Instructions]"
							));
							optionInstrIndex++;
						}
					}

					serializedComponentDict.Remove("Options");
					serializedComponentDict.Remove("OptionsInstructions");
				}
			}

			var keysCopy = serializedComponentDict.Keys.ToList();
			foreach ( string key in keysCopy )
			{
				object value = serializedComponentDict[key];

				List<Dictionary<string, object>> listItems = null;
				if ( value is List<Dictionary<string, object>> list )
					listItems = list;
				else if ( value is IEnumerable<Dictionary<string, object>> enumerable ) listItems = enumerable.ToList();

				if ( listItems == null || listItems.Count == 0 )
					continue;

				foreach ( Dictionary<string, object> item in listItems.Where(item => item != null && item.Count != 0) )
				{
					var model = new Dictionary<string, object>
				{
					{
						"thisMod", new Dictionary<string, object>
						{
							{ key, item }
						}
					}
				};
					tomlString.AppendLine();
					tomlString.Append(Toml.FromModel(model).Replace($"thisMod.{key}", $"[thisMod.{key}]"));
				}

				serializedComponentDict.Remove(key);
			}

			return modLinkFilenamesDict;
		}
		public static string SerializeModComponentAsYamlString(
				List<ModComponent> components,
				ComponentValidationContext validationContext = null
			)
		{
			Logger.LogVerbose("Saving to YAML string");
			var sb = new StringBuilder();

			// Write metadata section
			WriteYamlMetadataSection(sb);

			YamlSerialization.ISerializer serializer = new YamlDotNet.Serialization.SerializerBuilder()
				.WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
				.ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitNull)
				.DisableAliases()
				.Build();
			foreach ( ModComponent component in components )
			{
				sb.AppendLine("---");

				// Use unified serialization
				Dictionary<string, object> dict = SerializeComponentToDictionary(component, validationContext);

				// YAML-specific: Render validation comments from metadata
				if ( dict.TryGetValue("_ValidationIssues", out object validationIssuesValue) && validationIssuesValue is List<string> componentIssues )
				{
					sb.AppendLine("# VALIDATION ISSUES:");
					foreach ( string issue in componentIssues )
					{
						sb.AppendLine($"# {issue}");
					}
					dict.Remove("_ValidationIssues");
				}

				// YAML-specific: Render URL failure comments from metadata
				if ( dict.TryGetValue("_UrlFailures", out object urlFailuresValue) && urlFailuresValue is Dictionary<string, List<string>> urlFailures )
				{
					foreach ( KeyValuePair<string, List<string>> kvp in urlFailures )
					{
						sb.AppendLine($"# URL RESOLUTION FAILURE: {kvp.Key}");
						foreach ( string failure in kvp.Value )
						{
							sb.AppendLine($"# {failure}");
						}
					}
					dict.Remove("_UrlFailures");
				}

				// YAML-specific: Remove internal metadata and convert action to lowercase
				dict.Remove("_HasInstructions");
				dict.Remove("OptionsInstructions"); // YAML doesn't use separate OptionsInstructions

				// YAML-specific: Convert Action strings to lowercase
				if ( dict.TryGetValue("Instructions", out object instructionsValue) && instructionsValue is List<Dictionary<string, object>> instructions )
				{
					foreach ( Dictionary<string, object> instr in instructions )
					{
						if ( instr.TryGetValue("Action", out object instructionActionValue) && instructionActionValue is string action )
						{
							instr["Action"] = action.ToLowerInvariant();
						}
						instr.Remove("_ValidationWarnings"); // YAML handles these as embedded fields in unified serialization
					}
				}

				if ( dict.TryGetValue("Options", out object optionsValue) && optionsValue is List<Dictionary<string, object>> options )
				{
					foreach ( Dictionary<string, object> opt in options )
					{
						if ( opt.ContainsKey("Instructions") && opt["Instructions"] is List<Dictionary<string, object>> optInstructions )
						{
							foreach ( Dictionary<string, object> instr in optInstructions )
							{
								if ( instr.TryGetValue("Action", out object optionInstructionActionValue) && optionInstructionActionValue is string action )
								{
									instr["Action"] = action.ToLowerInvariant();
								}
								instr.Remove("_ValidationWarnings");
							}
						}
					}
				}

				sb.AppendLine(serializer.Serialize(dict));
			}
			return SanitizeUtf8(sb.ToString());
		}

		private static void WriteYamlMetadataSection(StringBuilder sb)
		{
			bool hasAnyMetadata = !string.IsNullOrWhiteSpace(MainConfig.TargetGame)
				|| !string.IsNullOrWhiteSpace(MainConfig.BuildName)
				|| !string.IsNullOrWhiteSpace(MainConfig.BuildAuthor)
				|| !string.IsNullOrWhiteSpace(MainConfig.BuildDescription)
				|| !string.IsNullOrWhiteSpace(MainConfig.BeforeModListContent)
				|| !string.IsNullOrWhiteSpace(MainConfig.AfterModListContent)
				|| !string.IsNullOrWhiteSpace(MainConfig.WidescreenSectionContent)
				|| !string.IsNullOrWhiteSpace(MainConfig.AspyrSectionContent)
				|| MainConfig.LastModified.HasValue;

			if ( !hasAnyMetadata && MainConfig.FileFormatVersion == "2.0" )
				return;

			sb.AppendLine("---");
			sb.AppendLine("# Metadata");
			sb.AppendLine($"fileFormatVersion: \"{MainConfig.FileFormatVersion}\"");

			if ( !string.IsNullOrWhiteSpace(MainConfig.TargetGame) )
				sb.AppendLine($"targetGame: \"{MainConfig.TargetGame}\"");

			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildName) )
				sb.AppendLine($"buildName: \"{MainConfig.BuildName}\"");

			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildAuthor) )
				sb.AppendLine($"buildAuthor: \"{MainConfig.BuildAuthor}\"");

			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildDescription) )
			{
				string escapedDescription = MainConfig.BuildDescription
					.Replace("\\", "\\\\")
					.Replace("\"", "\\\"")
					.Replace("\n", "\\n")
					.Replace("\r", "\\r")
					.Replace("\t", "\\t");
				sb.AppendLine($"buildDescription: \"{escapedDescription}\"");
			}

			if ( MainConfig.LastModified.HasValue )
				sb.AppendLine($"lastModified: \"{MainConfig.LastModified.Value:O}\"");

			if ( !string.IsNullOrWhiteSpace(MainConfig.BeforeModListContent) )
			{
				string escapedBefore = EscapeYamlString(MainConfig.BeforeModListContent);
				sb.AppendLine($"beforeModListContent: \"{escapedBefore}\"");
			}

			if ( !string.IsNullOrWhiteSpace(MainConfig.AfterModListContent) )
			{
				string escapedAfter = EscapeYamlString(MainConfig.AfterModListContent);
				sb.AppendLine($"afterModListContent: \"{escapedAfter}\"");
			}

			if ( !string.IsNullOrWhiteSpace(MainConfig.WidescreenSectionContent) )
			{
				string escapedWidescreen = EscapeYamlString(MainConfig.WidescreenSectionContent);
				sb.AppendLine($"widescreenSectionContent: \"{escapedWidescreen}\"");
			}

			if ( !string.IsNullOrWhiteSpace(MainConfig.AspyrSectionContent) )
			{
				string escapedAspyr = EscapeYamlString(MainConfig.AspyrSectionContent);
				sb.AppendLine($"aspyrSectionContent: \"{escapedAspyr}\"");
			}

			sb.AppendLine();
		}

		private static string EscapeYamlString(string input)
		{
			if ( string.IsNullOrEmpty(input) )
				return input;

			return input
				.Replace("\\", "\\\\")
				.Replace("\"", "\\\"")
				.Replace("\n", "\\n")
				.Replace("\r", "\\r")
				.Replace("\t", "\\t");
		}

		public static string SerializeModComponentAsMarkdownString(
			List<ModComponent> components,
			ComponentValidationContext validationContext = null)
		{
			Logger.LogVerbose("Saving to Markdown string");
			return GenerateModDocumentation(
				components,
				MainConfig.BeforeModListContent,
				MainConfig.AfterModListContent,
				MainConfig.WidescreenSectionContent,
				MainConfig.AspyrSectionContent,
				validationContext);
		}
		public static string SerializeModComponentAsJsonString(
			List<ModComponent> components,
			ComponentValidationContext validationContext = null)
		{
			Logger.LogVerbose("Saving to JSON string");
			var jsonRoot = new JObject();

			var metadata = new JObject
			{
				["fileFormatVersion"] = MainConfig.FileFormatVersion ?? "2.0"
			};
			if ( !string.IsNullOrWhiteSpace(MainConfig.TargetGame) )
				metadata["targetGame"] = MainConfig.TargetGame;
			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildName) )
				metadata["buildName"] = MainConfig.BuildName;
			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildAuthor) )
				metadata["buildAuthor"] = MainConfig.BuildAuthor;
			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildDescription) )
				metadata["buildDescription"] = MainConfig.BuildDescription;
			if ( MainConfig.LastModified.HasValue )
				metadata["lastModified"] = MainConfig.LastModified.Value;
			if ( !string.IsNullOrWhiteSpace(MainConfig.BeforeModListContent) )
				metadata["beforeModListContent"] = MainConfig.BeforeModListContent;
			if ( !string.IsNullOrWhiteSpace(MainConfig.AfterModListContent) )
				metadata["afterModListContent"] = MainConfig.AfterModListContent;
			if ( !string.IsNullOrWhiteSpace(MainConfig.WidescreenSectionContent) )
				metadata["widescreenSectionContent"] = MainConfig.WidescreenSectionContent;
			if ( !string.IsNullOrWhiteSpace(MainConfig.AspyrSectionContent) )
				metadata["aspyrSectionContent"] = MainConfig.AspyrSectionContent;
			jsonRoot["metadata"] = metadata;

			var componentsArray = new JArray();
			foreach ( ModComponent c in components )
			{
				// Use unified serialization
				Dictionary<string, object> componentDict = SerializeComponentToDictionary(c, validationContext);

				// Convert to JObject with JSON-specific formatting
				JObject componentObj = DictionaryToJObject(componentDict);

				// JSON-specific: Add validation warnings as special fields
				if ( componentDict.TryGetValue("_ValidationIssues", out object validationIssuesValue) && validationIssuesValue is List<string> componentIssues )
				{
					componentObj["_validationWarnings"] = JArray.FromObject(componentIssues);
				}

				// JSON-specific: Add URL failure warnings
				if ( componentDict.TryGetValue("_UrlFailures", out object urlFailuresValue) && urlFailuresValue is Dictionary<string, List<string>> urlFailuresDict )
				{
					var urlFailures = new List<string>();
					foreach ( KeyValuePair<string, List<string>> kvp in urlFailuresDict )
					{
						urlFailures.Add($"URL: {kvp.Key}");
						urlFailures.AddRange(kvp.Value);
					}
					if ( urlFailures.Count > 0 )
					{
						componentObj["_urlResolutionFailures"] = JArray.FromObject(urlFailures);
					}
				}

				// JSON doesn't need OptionsInstructions - remove it (JSON nests instructions under options)
				componentObj.Remove("optionsInstructions");

				componentsArray.Add(componentObj);
			}
			jsonRoot["components"] = componentsArray;

			return SanitizeUtf8(jsonRoot.ToString(Newtonsoft.Json.Formatting.Indented));
		}
		public static string SerializeModComponentAsXmlString(
			List<ModComponent> components,
			ComponentValidationContext validationContext = null)
		{
			Logger.LogVerbose("Saving to XML string");

			var metadataElement = new XElement("Metadata",
				new XElement("FileFormatVersion", MainConfig.FileFormatVersion ?? "2.0"),
				!string.IsNullOrWhiteSpace(MainConfig.TargetGame)
					? new XElement("TargetGame", MainConfig.TargetGame)
					: null,
				!string.IsNullOrWhiteSpace(MainConfig.BuildName)
					? new XElement("BuildName", MainConfig.BuildName)
					: null,
				!string.IsNullOrWhiteSpace(MainConfig.BuildAuthor)
					? new XElement("BuildAuthor", MainConfig.BuildAuthor)
					: null,
				!string.IsNullOrWhiteSpace(MainConfig.BuildDescription)
					? new XElement("BuildDescription", MainConfig.BuildDescription)
					: null,
				MainConfig.LastModified.HasValue
					? new XElement("LastModified", MainConfig.LastModified.Value.ToString("o"))
					: null,
				!string.IsNullOrWhiteSpace(MainConfig.BeforeModListContent)
					? new XElement("BeforeModListContent", MainConfig.BeforeModListContent)
					: null,
				!string.IsNullOrWhiteSpace(MainConfig.AfterModListContent)
					? new XElement("AfterModListContent", MainConfig.AfterModListContent)
					: null,
				!string.IsNullOrWhiteSpace(MainConfig.WidescreenSectionContent)
					? new XElement("WidescreenSectionContent", MainConfig.WidescreenSectionContent)
					: null,
				!string.IsNullOrWhiteSpace(MainConfig.AspyrSectionContent)
					? new XElement("AspyrSectionContent", MainConfig.AspyrSectionContent)
					: null
			);

			var componentsElement = new XElement("Components");

			foreach ( ModComponent c in components )
			{
				var componentElement = new XElement("Component");

				// Add validation comment for component
				if ( validationContext != null )
				{
					List<string> componentIssues = validationContext.GetComponentIssues(c.Guid);
					if ( componentIssues.Count > 0 )
					{
						string issuesText = "VALIDATION ISSUES: " + string.Join("; ", componentIssues);
						componentElement.Add(new XComment(issuesText));
					}

					// Add URL failure comments
					if ( c.ModLinkFilenames != null && c.ModLinkFilenames.Count > 0 )
					{
						foreach ( string url in c.ModLinkFilenames.Keys )
						{
							List<string> urlFailures = validationContext.GetUrlFailures(url);
							if ( urlFailures.Count > 0 )
							{
								string failureText = $"URL RESOLUTION FAILURE ({url}): " + string.Join("; ", urlFailures);
								componentElement.Add(new XComment(failureText));
							}
						}
					}
				}

				componentElement.Add(new XElement("Guid", c.Guid.ToString()));
				componentElement.Add(new XElement("Name", c.Name));

				if ( !string.IsNullOrWhiteSpace(c.Author) )
					componentElement.Add(new XElement("Author", c.Author));
				if ( !string.IsNullOrWhiteSpace(c.Description) )
					componentElement.Add(new XElement("Description", c.Description));
				if ( !string.IsNullOrWhiteSpace(c._descriptionSpoilerFree) && c._descriptionSpoilerFree != c.Description )
					componentElement.Add(new XElement("DescriptionSpoilerFree", c._descriptionSpoilerFree));
				if ( c.Category?.Count > 0 )
					componentElement.Add(new XElement("Category", c.Category.Select(cat => new XElement("Item", cat))));
				if ( !string.IsNullOrWhiteSpace(c.Tier) )
					componentElement.Add(new XElement("Tier", c.Tier));
				if ( c.Language?.Count > 0 )
					componentElement.Add(new XElement("Language", c.Language.Select(lang => new XElement("Item", lang))));

				// Serialize ModLinkFilenames
				if ( c.ModLinkFilenames?.Count > 0 )
				{
					componentElement.Add(new XElement("ModLinkFilenames",
						c.ModLinkFilenames.Select(urlEntry =>
							new XElement("Url",
								new XAttribute("Value", urlEntry.Key),
								urlEntry.Value.Select(fileEntry =>
									new XElement("File",
										new XAttribute("Name", fileEntry.Key),
										new XAttribute("ShouldDownload", fileEntry.Value?.ToString() ?? "null")))))));
				}

				if ( c.ExcludedDownloads?.Count > 0 )
					componentElement.Add(new XElement("ExcludedDownloads", c.ExcludedDownloads.Select(file => new XElement("Item", file))));
				if ( !string.IsNullOrWhiteSpace(c.InstallationMethod) )
					componentElement.Add(new XElement("InstallationMethod", c.InstallationMethod));
				if ( !string.IsNullOrWhiteSpace(c.Directions) )
					componentElement.Add(new XElement("Directions", c.Directions));
				if ( !string.IsNullOrWhiteSpace(c._directionsSpoilerFree) && c._directionsSpoilerFree != c.Directions )
					componentElement.Add(new XElement("DirectionsSpoilerFree", c._directionsSpoilerFree));
				if ( !string.IsNullOrWhiteSpace(c.DownloadInstructions) )
					componentElement.Add(new XElement("DownloadInstructions", c.DownloadInstructions));
				if ( !string.IsNullOrWhiteSpace(c._downloadInstructionsSpoilerFree) && c._downloadInstructionsSpoilerFree != c.DownloadInstructions )
					componentElement.Add(new XElement("DownloadInstructionsSpoilerFree", c._downloadInstructionsSpoilerFree));
				if ( !string.IsNullOrWhiteSpace(c._usageWarningSpoilerFree) && c._usageWarningSpoilerFree != c.UsageWarning )
					componentElement.Add(new XElement("UsageWarningSpoilerFree", c._usageWarningSpoilerFree));
				if ( !string.IsNullOrWhiteSpace(c._screenshotsSpoilerFree) && c._screenshotsSpoilerFree != c.Screenshots )
					componentElement.Add(new XElement("ScreenshotsSpoilerFree", c._screenshotsSpoilerFree));
				if ( !string.IsNullOrWhiteSpace(c.KnownBugs) )
					componentElement.Add(new XElement("KnownBugs", c.KnownBugs));
				if ( !string.IsNullOrWhiteSpace(c.InstallationWarning) )
					componentElement.Add(new XElement("InstallationWarning", c.InstallationWarning));
				if ( !string.IsNullOrWhiteSpace(c.CompatibilityWarning) )
					componentElement.Add(new XElement("CompatibilityWarning", c.CompatibilityWarning));
				if ( !string.IsNullOrWhiteSpace(c.SteamNotes) )
					componentElement.Add(new XElement("SteamNotes", c.SteamNotes));
				if ( c.Dependencies?.Count > 0 )
					componentElement.Add(new XElement("Dependencies", c.Dependencies.Select(dep => new XElement("Item", dep))));
				if ( c.Restrictions?.Count > 0 )
					componentElement.Add(new XElement("Restrictions", c.Restrictions.Select(res => new XElement("Item", res))));
				if ( c.InstallBefore?.Count > 0 )
					componentElement.Add(new XElement("InstallBefore", c.InstallBefore.Select(ib => new XElement("Item", ib))));
				if ( c.InstallAfter?.Count > 0 )
					componentElement.Add(new XElement("InstallAfter", c.InstallAfter.Select(ia => new XElement("Item", ia))));
				if ( c.WidescreenOnly )
					componentElement.Add(new XElement("WidescreenOnly", c.WidescreenOnly));

				// Handle Instructions with validation comments
				if ( c.Instructions?.Count > 0 )
				{
					var instructionsElement = new XElement("Instructions");
					foreach ( Instruction instr in c.Instructions )
					{
						// Add validation comment before instruction
						if ( validationContext != null )
						{
							List<string> instructionIssues = validationContext.GetInstructionIssues(instr.Guid);
							if ( instructionIssues.Count > 0 )
							{
								string issuesText = "INSTRUCTION VALIDATION: " + string.Join("; ", instructionIssues);
								instructionsElement.Add(new XComment(issuesText));
							}
						}

						// Serialize Overwrite when it differs from default:
						// Delete default is false, so serialize when true
						// Move/Copy/Rename default is true, so serialize when false
						XElement overwriteElement = null;
						if ( instr.Action == ActionType.Delete && instr.Overwrite )
						{
							overwriteElement = new XElement("Overwrite", true);
						}
						else if ( !instr.Overwrite &&
							(instr.Action == ActionType.Move ||
							 instr.Action == ActionType.Copy ||
							 instr.Action == ActionType.Rename) )
						{
							overwriteElement = new XElement("Overwrite", false);
						}

						instructionsElement.Add(new XElement("Instruction",
							new XElement("Guid", instr.Guid.ToString()),
							new XElement("Action", instr.ActionString),
							instr.Source?.Count > 0
								? new XElement("Source", instr.Source.Select(s => new XElement("Item", s)))
								: null,
							!string.IsNullOrWhiteSpace(instr.Destination)
								? new XElement("Destination", instr.Destination)
								: null,
							!string.IsNullOrWhiteSpace(instr.Arguments) &&
							(instr.Action == ActionType.DelDuplicate ||
							 instr.Action == ActionType.Patcher ||
							 instr.Action == ActionType.Execute)
								? new XElement("Arguments", instr.Arguments)
								: null,
							overwriteElement
						));
					}
					componentElement.Add(instructionsElement);
				}

				// Handle Options with validation comments
				if ( c.Options?.Count > 0 )
				{
					var optionsElement = new XElement("Options");
					foreach ( Option opt in c.Options )
					{
						var optionElement = new XElement("Option");
						optionElement.Add(new XElement("Guid", opt.Guid.ToString()));
						if ( !string.IsNullOrWhiteSpace(opt.Name) )
							optionElement.Add(new XElement("Name", opt.Name));
						if ( !string.IsNullOrWhiteSpace(opt.Description) )
							optionElement.Add(new XElement("Description", opt.Description));

						// Handle option instructions
						if ( opt.Instructions?.Count > 0 )
						{
							var optInstructionsElement = new XElement("Instructions");
							foreach ( Instruction instr in opt.Instructions )
							{
								// Add validation comment before option instruction
								if ( validationContext != null )
								{
									List<string> instructionIssues = validationContext.GetInstructionIssues(instr.Guid);
									if ( instructionIssues.Count > 0 )
									{
										string issuesText = "OPTION INSTRUCTION VALIDATION: " + string.Join("; ", instructionIssues);
										optInstructionsElement.Add(new XComment(issuesText));
									}
								}

								// Serialize Overwrite when it differs from default:
								// Delete default is false, so serialize when true
								// Move/Copy/Rename default is true, so serialize when false
								XElement optOverwriteElement = null;
								if ( instr.Action == ActionType.Delete && instr.Overwrite )
								{
									optOverwriteElement = new XElement("Overwrite", true);
								}
								else if ( !instr.Overwrite &&
									(instr.Action == ActionType.Move ||
									 instr.Action == ActionType.Copy ||
									 instr.Action == ActionType.Rename) )
								{
									optOverwriteElement = new XElement("Overwrite", false);
								}

								optInstructionsElement.Add(new XElement("Instruction",
									new XElement("Guid", instr.Guid.ToString()),
									!string.IsNullOrWhiteSpace(instr.ActionString)
										? new XElement("Action", instr.ActionString)
										: null,
									instr.Source?.Count > 0
										? new XElement("Source", instr.Source.Select(s => new XElement("Item", s)))
										: null,
									!string.IsNullOrWhiteSpace(instr.Destination)
										? new XElement("Destination", instr.Destination)
										: null,
									(!string.IsNullOrWhiteSpace(instr.Arguments) &&
										(instr.Action == ActionType.DelDuplicate ||
										 instr.Action == ActionType.Patcher ||
										 instr.Action == ActionType.Execute))
										? new XElement("Arguments", instr.Arguments)
										: null,
									optOverwriteElement
								));
							}
							optionElement.Add(optInstructionsElement);
						}

						optionsElement.Add(optionElement);
					}
					componentElement.Add(optionsElement);
				}

				componentsElement.Add(componentElement);
			}

			var doc = new XDocument(
				new XDeclaration("2.0", "utf-8", "yes"),
				new XElement("ModBuild",
					metadataElement,
					componentsElement
				)
			);

			var sb = new StringBuilder();
			using ( var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }) )
			{
				doc.Save(writer);
			}
			return SanitizeUtf8(sb.ToString());
		}

		/// <summary>
		/// Serializes a single ModComponent to TOML string.
		/// This is the migrated version of ModComponent.SerializeComponent().
		/// </summary>
		[NotNull]
		public static string SerializeSingleComponentAsTomlString([NotNull] ModComponent component)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));

			try
			{
				// Use the existing serialization infrastructure to ensure correct format
				var components = new List<ModComponent> { component };
				return SerializeModComponentAsTomlString(components);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to serialize component to TOML");
				throw;
			}
		}


		public static Dictionary<string, object> XmlElementToDictionary(XElement element)
		{
			var dict = new Dictionary<string, object>();
			foreach ( XElement child in element.Elements() )
			{
				string childName = child.Name.LocalName;

				if ( childName == "ModLinkFilenames" && child.Elements("Url").Any() )
				{
					var modLinkFilenamesDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
					foreach ( XElement urlElem in child.Elements("Url") )
					{
						string url = urlElem.Attribute("Value")?.Value;
						if ( string.IsNullOrEmpty(url) )
							continue;

						var filenamesDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
						foreach ( XElement fileElem in urlElem.Elements("File") )
						{
							string filename = fileElem.Attribute("Name")?.Value;
							string shouldDownloadStr = fileElem.Attribute("ShouldDownload")?.Value;

							if ( !string.IsNullOrEmpty(filename) && bool.TryParse(shouldDownloadStr, out bool shouldDownload) )
							{
								filenamesDict[filename] = shouldDownload;
							}
						}

						if ( filenamesDict.Count > 0 )
						{
							modLinkFilenamesDict[url] = filenamesDict;
						}
					}
					dict[childName] = modLinkFilenamesDict;
				}
				else if ( child.Elements("Item").Any() )
				{
					List<object> list = child.Elements("Item").Select(item => (object)item.Value).ToList();
					dict[childName] = list;
				}
				else if ( child.HasElements && !child.Elements("Item").Any() )
				{
					if ( child.Elements().All(e => e.Name.LocalName == child.Elements().First().Name.LocalName) )
					{
						List<object> list = child.Elements().Select(e => (object)XmlElementToDictionary(e)).ToList();
						dict[childName] = list;
					}
					else
					{
						dict[childName] = XmlElementToDictionary(child);
					}
				}
				else
				{
					dict[childName] = child.Value;
				}
			}
			return dict;
		}

		public static async Task<string> GenerateModDocumentationAsync(
			[NotNull] string filePath,
			[NotNull][ItemNotNull] List<ModComponent> componentsList,
			[CanBeNull] string beforeModListContent = null,
			[CanBeNull] string afterModListContent = null,
			[CanBeNull] string widescreenSectionContent = null,
			[CanBeNull] string aspyrSectionContent = null,
			[CanBeNull] ComponentValidationContext validationContext = null)
		{
			return await Task.Run(() => GenerateModDocumentation(
				componentsList,
				beforeModListContent,
				afterModListContent,
				widescreenSectionContent,
				aspyrSectionContent,
				validationContext
			));
		}

		[NotNull]
		public static string GenerateModDocumentation(
			[NotNull][ItemNotNull] List<ModComponent> componentsList,
			[CanBeNull] string beforeModListContent = null,
			[CanBeNull] string afterModListContent = null,
			[CanBeNull] string widescreenSectionContent = null,
			[CanBeNull] string aspyrSectionContent = null,
			[CanBeNull] ComponentValidationContext validationContext = null)
		{
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			var sb = new StringBuilder();

			if ( !string.IsNullOrWhiteSpace(beforeModListContent) )
			{
				_ = sb.Append(beforeModListContent);
				if ( !beforeModListContent.EndsWith("\n") )
				{
					_ = sb.AppendLine();
				}
				_ = sb.AppendLine();
			}

			_ = sb.AppendLine("## Mod List");

			var guidToName = componentsList.ToDictionary(c => c.Guid, c => c.Name);

			bool widescreenHeaderWritten = false;
			bool aspyrHeaderWritten = false;

			for ( int i = 0; i < componentsList.Count; i++ )
			{
				ModComponent component = componentsList[i];

				if ( component.AspyrExclusive == true && !aspyrHeaderWritten && !string.IsNullOrWhiteSpace(aspyrSectionContent) )
				{
					_ = sb.AppendLine();
					_ = sb.AppendLine(aspyrSectionContent.TrimEnd());
					_ = sb.AppendLine();
					aspyrHeaderWritten = true;
				}

				if ( component.WidescreenOnly && !widescreenHeaderWritten && !string.IsNullOrWhiteSpace(widescreenSectionContent) )
				{
					_ = sb.AppendLine();
					_ = sb.AppendLine(widescreenSectionContent.TrimEnd());
					_ = sb.AppendLine();
					widescreenHeaderWritten = true;
				}

				if ( i > 0 )
				{
					_ = sb.AppendLine("___");
					_ = sb.AppendLine();
				}
				else
				{
					_ = sb.AppendLine();
				}

				// Add validation warnings for component
				if ( validationContext != null )
				{
					List<string> componentIssues = validationContext.GetComponentIssues(component.Guid);
					if ( componentIssues.Count > 0 )
					{
						_ = sb.AppendLine("> **⚠️ VALIDATION WARNINGS:**");
						foreach ( string issue in componentIssues )
						{
							_ = sb.AppendLine($"> - {issue}");
						}
						_ = sb.AppendLine();
					}

					// Add URL failure warnings
					if ( component.ModLinkFilenames != null && component.ModLinkFilenames.Count > 0 )
					{
						foreach ( string url in component.ModLinkFilenames.Keys )
						{
							List<string> urlFailures = validationContext.GetUrlFailures(url);
							if ( urlFailures.Count > 0 )
							{
								_ = sb.AppendLine($"> **⚠️ URL RESOLUTION FAILURE:** `{url}`");
								foreach ( string failure in urlFailures )
								{
									_ = sb.AppendLine($"> - {failure}");
								}
								_ = sb.AppendLine();
							}
						}
					}
				}

				string heading = !string.IsNullOrWhiteSpace(component.Heading) ? component.Heading : component.Name;
				_ = sb.Append("### ").AppendLine(heading);
				_ = sb.AppendLine();

				if ( !string.IsNullOrWhiteSpace(component.NameFieldContent) )
				{
					_ = sb.Append("**Name:** ").AppendLine(component.NameFieldContent);
				}
				else if ( component.ModLinkFilenames?.Count > 0 )
				{
					List<string> urls = component.ModLinkFilenames.Keys.ToList();
					if ( urls.Count > 0 && !string.IsNullOrWhiteSpace(urls[0]) )
					{
						_ = sb.Append("**Name:** [").Append(component.Name).Append("](")
							.Append(urls[0]).Append(")");

						for ( int linkIdx = 1; linkIdx < urls.Count; linkIdx++ )
						{
							if ( !string.IsNullOrWhiteSpace(urls[linkIdx]) )
							{
								_ = sb.Append(" and [**Patch**](").Append(urls[linkIdx]).Append(")");
							}
						}

						_ = sb.AppendLine();
					}
					else
					{
						_ = sb.Append("**Name:** ").AppendLine(component.Name);
					}
				}
				else
				{
					_ = sb.Append("**Name:** ").AppendLine(component.Name);
				}

				_ = sb.AppendLine();

				if ( !string.IsNullOrWhiteSpace(component.Author) )
				{
					_ = sb.Append("**Author:** ").AppendLine(component.Author);
					_ = sb.AppendLine();
				}

				if ( !string.IsNullOrWhiteSpace(component.Description) )
				{
					_ = sb.Append("**Description:** ").AppendLine(component.Description);
					_ = sb.AppendLine();
				}

				if ( !string.IsNullOrWhiteSpace(component.Screenshots) )
				{
					_ = sb.Append("**Screenshots:** ").AppendLine(component.Screenshots);
					_ = sb.AppendLine();
				}

				string categoryStr;
				if ( component.Category?.Count > 0 )
				{
					if ( component.Category.Count == 1 )
						categoryStr = component.Category[0];
					else if ( component.Category.Count == 2 )
						categoryStr = $"{component.Category[0]} & {component.Category[1]}";
					else
					{
						IEnumerable<string> allButLast = component.Category.Take(component.Category.Count - 1);
						string last = component.Category[component.Category.Count - 1];
						categoryStr = $"{string.Join(", ", allButLast)} & {last}";
					}
				}
				else
				{
					categoryStr = "Uncategorized";
				}
				string tierStr = !string.IsNullOrWhiteSpace(component.Tier) ? component.Tier : "Unspecified";
				_ = sb.Append("**Category & Tier:** ").Append(categoryStr).Append(" / ").AppendLine(tierStr);
				_ = sb.AppendLine();

				string languageSupport = GetNonEnglishFunctionalityText(component.Language);
				if ( languageSupport != "UNKNOWN" )
				{
					_ = sb.Append("**Non-English Functionality:** ").AppendLine(languageSupport);
					_ = sb.AppendLine();
				}

				if ( !string.IsNullOrWhiteSpace(component.InstallationMethod) )
				{
					_ = sb.Append("**Installation Method:** ").AppendLine(component.InstallationMethod);
				}

				if ( !string.IsNullOrWhiteSpace(component.KnownBugs) )
				{
					_ = sb.AppendLine();
					_ = sb.Append("**Known Bugs:** ").AppendLine(component.KnownBugs);
				}

				if ( !string.IsNullOrWhiteSpace(component.InstallationWarning) )
				{
					_ = sb.AppendLine();
					_ = sb.Append("**Installation Warning:** ").AppendLine(component.InstallationWarning);
				}

				if ( !string.IsNullOrWhiteSpace(component.CompatibilityWarning) )
				{
					_ = sb.AppendLine();
					_ = sb.Append("**Compatibility Warning:** ").AppendLine(component.CompatibilityWarning);
				}

				if ( !string.IsNullOrWhiteSpace(component.SteamNotes) )
				{
					_ = sb.AppendLine();
					_ = sb.Append("**Steam Notes:** ").AppendLine(component.SteamNotes);
				}

				if ( component.Dependencies?.Count > 0 )
				{
					var masterNames = component.Dependencies
						.Select(guid =>
						{
							if ( component.DependencyGuidToOriginalName.TryGetValue(guid, out string originalName) )
								return originalName;
							if ( guidToName.TryGetValue(guid, out string nameFromGuid) )
								return nameFromGuid;
							return null;
						})
						.Where(name => !string.IsNullOrWhiteSpace(name))
						.ToList();

					if ( masterNames.Count > 0 )
					{
						_ = sb.AppendLine();
						_ = sb.Append("**Masters:** ").AppendLine(string.Join(", ", masterNames));
					}
				}

				if ( !string.IsNullOrWhiteSpace(component.DownloadInstructions) )
				{
					_ = sb.AppendLine();
					_ = sb.Append("**Download Instructions:** ").AppendLine(component.DownloadInstructions);
				}

				if ( !string.IsNullOrWhiteSpace(component.Directions) )
				{
					_ = sb.AppendLine();
					_ = sb.Append("**Installation Instructions:** ").AppendLine(component.Directions);
				}

				if ( !string.IsNullOrWhiteSpace(component.UsageWarning) )
				{
					_ = sb.AppendLine();
					_ = sb.Append("**Usage Warning:** ").AppendLine(component.UsageWarning);
				}

				_ = sb.AppendLine();

				if ( component.Instructions.Count > 0 || component.Options.Count > 0 )
					GenerateModSyncMetadata(sb, component);
			}

			if ( string.IsNullOrWhiteSpace(afterModListContent) )
				return sb.ToString();
			_ = sb.AppendLine();
			_ = sb.Append(afterModListContent);

			return SanitizeUtf8(sb.ToString());
		}

		private static void GenerateModSyncMetadata(
			[NotNull] StringBuilder sb,
			[NotNull] ModComponent component)
		{
			if ( component.Instructions.Count == 0 && component.Options.Count == 0 )
				return;

			_ = sb.AppendLine("<!--<<ModSync>>");

			try
			{
				// Serialize to YAML for markdown HTML comments instead of TOML
				string yaml = SerializeSingleComponentAsYamlString(component);
				_ = sb.Append(yaml);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to serialize component for ModSync metadata");
				_ = sb.AppendLine($"Guid: {component.Guid}");
			}

			_ = sb.AppendLine("-->");
			_ = sb.AppendLine();
		}

		/// <summary>
		/// Serializes a single component to YAML string without metadata or document separators.
		/// This is used for embedding component data in markdown HTML comments.
		/// </summary>
		private static string SerializeSingleComponentAsYamlString(ModComponent component)
		{
			Logger.LogVerbose("Saving single component to YAML string");

			YamlSerialization.ISerializer serializer = new YamlDotNet.Serialization.SerializerBuilder()
				.WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
				.ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitNull)
				.DisableAliases()
				.Build();

			// Use unified serialization
			Dictionary<string, object> dict = SerializeComponentToDictionary(component, null);

			// YAML-specific: Remove internal metadata and convert action to lowercase
			dict.Remove("_HasInstructions");
			dict.Remove("OptionsInstructions"); // YAML doesn't use separate OptionsInstructions

			// YAML-specific: Convert Action strings to lowercase
			if ( dict.TryGetValue("Instructions", out object instructionsValue) && instructionsValue is List<Dictionary<string, object>> instructions )
			{
				foreach ( Dictionary<string, object> instr in instructions )
				{
					if ( instr.TryGetValue("Action", out object instructionActionValue) && instructionActionValue is string action )
					{
						instr["Action"] = action.ToLowerInvariant();
					}
					instr.Remove("_ValidationWarnings"); // YAML handles these as embedded fields in unified serialization
				}
			}

			if ( dict.TryGetValue("Options", out object optionsValue) && optionsValue is List<Dictionary<string, object>> options )
			{
				foreach ( Dictionary<string, object> opt in options )
				{
					if ( opt.ContainsKey("Instructions") && opt["Instructions"] is List<Dictionary<string, object>> optInstructions )
					{
						foreach ( Dictionary<string, object> instr in optInstructions )
						{
							if ( instr.TryGetValue("Action", out object optionInstructionActionValue) && optionInstructionActionValue is string action )
							{
								instr["Action"] = action.ToLowerInvariant();
							}
							instr.Remove("_ValidationWarnings");
						}
					}
				}
			}

			return serializer.Serialize(dict);
		}

		[NotNull]
		private static string GetNonEnglishFunctionalityText([CanBeNull][ItemCanBeNull] List<string> languages)
		{
			if ( languages == null || languages.Count == 0 )
				return "UNKNOWN";

			if ( languages.Count == 1 && languages.Any(lang =>
				string.Equals(lang, "UNKNOWN", StringComparison.OrdinalIgnoreCase)) )
			{
				return "UNKNOWN";
			}

			if ( languages.Any(lang => string.Equals(lang, b: "All", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(lang, b: "YES", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(lang, b: "Universal", StringComparison.OrdinalIgnoreCase)) )
			{
				return "YES";
			}

			if ( languages.Count == 1 && languages.Any(lang =>
				string.Equals(lang, b: "English", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(lang, b: "EN", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(lang, b: "NO", StringComparison.OrdinalIgnoreCase)) )
			{
				return "NO";
			}

			if ( languages.Any(lang => string.Equals(lang, b: "Partial", StringComparison.OrdinalIgnoreCase)
				|| (!string.IsNullOrEmpty(lang) && lang.IndexOf("Partial", StringComparison.OrdinalIgnoreCase) >= 0)) )
			{
				return "PARTIAL - Some text will be blank or in English";
			}

			if ( languages.Count > 1 && languages.Any(lang =>
				string.Equals(lang, b: "English", StringComparison.OrdinalIgnoreCase)) )
			{
				return "PARTIAL - Supported languages: " + string.Join(", ", languages);
			}

			if ( languages.Count == 1 )
			{
				string singleLang = languages[0];
				if ( string.IsNullOrEmpty(singleLang) )
					return "Supported languages: " + string.Join(", ", languages);
				string trimmed = singleLang.TrimStart();
				if ( trimmed.StartsWith("YES", StringComparison.OrdinalIgnoreCase) ||
					 trimmed.StartsWith("NO", StringComparison.OrdinalIgnoreCase) ||
					 trimmed.StartsWith("PARTIAL", StringComparison.OrdinalIgnoreCase) ||
					 trimmed.IndexOf("ONLY", StringComparison.OrdinalIgnoreCase) >= 0 )
				{
					return singleLang;
				}
			}

			return "Supported languages: " + string.Join(", ", languages);
		}

		#region Unified Serialization Functions

		// ============================================================================
		// UNIFIED SERIALIZATION ARCHITECTURE
		// ============================================================================
		// These functions centralize ALL conditional logic for serialization:
		// - ActionType checking for Arguments, Destination, Overwrite
		// - Field presence/null/empty checks
		// - Validation context handling
		//
		// CURRENT STATUS:
		// ✓ TOML - Uses unified serialization (with special pre/post processing)
		// ✓ YAML - Uses unified serialization
		// ⚠ JSON  - TODO: Refactor to use DictToJToken helper (currently works but duplicates logic)
		// ⚠ XML   - TODO: Refactor to use unified serialization (currently works but duplicates logic)
		//
		// BENEFITS:
		// - Single source of truth for field serialization rules
		// - Consistent behavior across all formats
		// - Easier maintenance (change once, applies everywhere)
		// - No more hunting for duplicated ActionType checks across 12 locations
		// ============================================================================

		/// <summary>
		/// Serializes an instruction to a dictionary with all conditional logic unified.
		/// Handles: ActionType-specific fields (Arguments, Destination, Overwrite), Dependencies, Restrictions, Validation warnings.
		/// </summary>
		private static Dictionary<string, object> SerializeInstructionToDictionary(
			[NotNull] Instruction instr,
			[CanBeNull] ComponentValidationContext validationContext = null)
		{
			var instrDict = new Dictionary<string, object>();

			if ( instr.Guid != Guid.Empty )
				instrDict["Guid"] = instr.Guid.ToString();
			if ( !string.IsNullOrWhiteSpace(instr.ActionString) )
				instrDict["Action"] = instr.ActionString;
			if ( instr.Source.Count > 0 )
				instrDict["Source"] = instr.Source;
			if ( !string.IsNullOrWhiteSpace(instr.Destination) )
				instrDict["Destination"] = instr.Destination;

			// Serialize Overwrite when it differs from default:
			// Delete default is false, so serialize when true
			// Move/Copy/Rename default is true, so serialize when false
			if ( instr.Action == ActionType.Delete && instr.Overwrite )
			{
				instrDict["Overwrite"] = instr.Overwrite;
			}
			else if (
				!instr.Overwrite
				&&
				(
					instr.Action == ActionType.Move
					|| instr.Action == ActionType.Copy
					|| instr.Action == ActionType.Rename
				)
			)
			{
				instrDict["Overwrite"] = instr.Overwrite;
			}

			// Serialize Arguments for specific action types
			if (
				!string.IsNullOrWhiteSpace(instr.Arguments)
				&&
				(
					instr.Action == ActionType.DelDuplicate
					|| instr.Action == ActionType.Execute
					|| instr.Action == ActionType.Patcher
				)
			)
			{
				instrDict["Arguments"] = instr.Arguments;
			}

			if ( instr.Dependencies.Count > 0 )
				instrDict["Dependencies"] = instr.Dependencies.Select(g => g.ToString()).ToList();
			if ( instr.Restrictions.Count > 0 )
				instrDict["Restrictions"] = instr.Restrictions.Select(g => g.ToString()).ToList();

			// Add validation warnings if present (format-specific rendering)
			if ( validationContext != null )
			{
				List<string> instructionIssues = validationContext.GetInstructionIssues(instr.Guid);
				if ( instructionIssues.Count > 0 )
					instrDict["_ValidationWarnings"] = instructionIssues;
			}

			return instrDict;
		}

		/// <summary>
		/// Serializes an option to a dictionary with all conditional logic unified
		/// </summary>
		private static Dictionary<string, object> SerializeOptionToDictionary(
			[NotNull] Option opt,
			[CanBeNull] ComponentValidationContext validationContext = null)
		{
			var optDict = new Dictionary<string, object>();

			if ( opt.Guid != Guid.Empty )
				optDict["Guid"] = opt.Guid.ToString();
			if ( !string.IsNullOrWhiteSpace(opt.Name) )
				optDict["Name"] = opt.Name;
			if ( !string.IsNullOrWhiteSpace(opt.Description) )
				optDict["Description"] = opt.Description;
			if ( opt.IsSelected )
				optDict["IsSelected"] = opt.IsSelected;
			if ( opt.Restrictions.Count > 0 )
				optDict["Restrictions"] = opt.Restrictions.Select(g => g.ToString()).ToList();
			if ( opt.Dependencies.Count > 0 )
				optDict["Dependencies"] = opt.Dependencies.Select(g => g.ToString()).ToList();

			// Serialize instructions if present
			if ( opt.Instructions.Count <= 0 )
				return optDict;
			List<Dictionary<string, object>> instructionsList = opt.Instructions.Select(instr => SerializeInstructionToDictionary(instr, validationContext)).ToList();
			optDict["Instructions"] = instructionsList;

			return optDict;
		}

		/// <summary>
		/// Serializes a component to a dictionary with all conditional logic unified
		/// </summary>
		private static Dictionary<string, object> SerializeComponentToDictionary(
			[NotNull] ModComponent component,
			[CanBeNull] ComponentValidationContext validationContext = null)
		{
			var componentDict = new Dictionary<string, object>();

			if ( component.Guid != Guid.Empty )
				componentDict["Guid"] = component.Guid.ToString();
			if ( !string.IsNullOrWhiteSpace(component.Name) )
				componentDict["Name"] = component.Name;
			if ( !string.IsNullOrWhiteSpace(component.Author) )
				componentDict["Author"] = component.Author;
			if ( !string.IsNullOrWhiteSpace(component.Tier) )
				componentDict["Tier"] = component.Tier;
			if ( !string.IsNullOrWhiteSpace(component.Description) )
				componentDict["Description"] = component.Description;
			if ( !string.IsNullOrWhiteSpace(component._descriptionSpoilerFree) && component._descriptionSpoilerFree != component.Description )
				componentDict["DescriptionSpoilerFree"] = component._descriptionSpoilerFree;
			if ( !string.IsNullOrWhiteSpace(component.InstallationMethod) )
				componentDict["InstallationMethod"] = component.InstallationMethod;
			if ( !string.IsNullOrWhiteSpace(component.Directions) )
				componentDict["Directions"] = component.Directions;
			if ( !string.IsNullOrWhiteSpace(component._directionsSpoilerFree) && component._directionsSpoilerFree != component.Directions )
				componentDict["DirectionsSpoilerFree"] = component._directionsSpoilerFree;
			if ( !string.IsNullOrWhiteSpace(component.DownloadInstructions) )
				componentDict["DownloadInstructions"] = component.DownloadInstructions;
			if ( !string.IsNullOrWhiteSpace(component._downloadInstructionsSpoilerFree) && component._downloadInstructionsSpoilerFree != component.DownloadInstructions )
				componentDict["DownloadInstructionsSpoilerFree"] = component._downloadInstructionsSpoilerFree;
			if ( !string.IsNullOrWhiteSpace(component.UsageWarning) )
				componentDict["UsageWarning"] = component.UsageWarning;
			if ( !string.IsNullOrWhiteSpace(component._usageWarningSpoilerFree) && component._usageWarningSpoilerFree != component.UsageWarning )
				componentDict["UsageWarningSpoilerFree"] = component._usageWarningSpoilerFree;
			if ( !string.IsNullOrWhiteSpace(component.Screenshots) )
				componentDict["Screenshots"] = component.Screenshots;
			if ( !string.IsNullOrWhiteSpace(component._screenshotsSpoilerFree) && component._screenshotsSpoilerFree != component.Screenshots )
				componentDict["ScreenshotsSpoilerFree"] = component._screenshotsSpoilerFree;
			if ( !string.IsNullOrWhiteSpace(component.KnownBugs) )
				componentDict["KnownBugs"] = component.KnownBugs;
			if ( !string.IsNullOrWhiteSpace(component.InstallationWarning) )
				componentDict["InstallationWarning"] = component.InstallationWarning;
			if ( !string.IsNullOrWhiteSpace(component.CompatibilityWarning) )
				componentDict["CompatibilityWarning"] = component.CompatibilityWarning;
			if ( !string.IsNullOrWhiteSpace(component.SteamNotes) )
				componentDict["SteamNotes"] = component.SteamNotes;
			if ( !string.IsNullOrWhiteSpace(component.Heading) )
				componentDict["Heading"] = component.Heading;
			if ( component.IsSelected )
				componentDict["IsSelected"] = component.IsSelected;
			if ( component.WidescreenOnly )
				componentDict["WidescreenOnly"] = component.WidescreenOnly;

			if ( component.Category.Count > 0 )
				componentDict["Category"] = component.Category;
			if ( component.Language.Count > 0 )
				componentDict["Language"] = component.Language;

			// Serialize ModLinkFilenames
			if ( component.ModLinkFilenames.Count > 0 )
				componentDict["ModLinkFilenames"] = SerializeModLinkFilenames(component.ModLinkFilenames);

			if ( component.ExcludedDownloads.Count > 0 )
				componentDict["ExcludedDownloads"] = component.ExcludedDownloads;
			if ( component.Dependencies.Count > 0 )
				componentDict["Dependencies"] = component.Dependencies.Select(g => g.ToString()).ToList();
			if ( component.Restrictions.Count > 0 )
				componentDict["Restrictions"] = component.Restrictions.Select(g => g.ToString()).ToList();
			if ( component.InstallAfter.Count > 0 )
				componentDict["InstallAfter"] = component.InstallAfter.Select(g => g.ToString()).ToList();
			if ( component.InstallBefore.Count > 0 )
				componentDict["InstallBefore"] = component.InstallBefore.Select(g => g.ToString()).ToList();

			// Serialize instructions
			if ( component.Instructions.Count > 0 )
			{
				var instructionsList = component.Instructions.Select(instr => SerializeInstructionToDictionary(instr, validationContext)).ToList();
				componentDict["Instructions"] = instructionsList;
			}

			// Serialize options
			if ( component.Options.Count > 0 )
			{
				var optionsList = new List<Dictionary<string, object>>();
				var optionsInstructionsList = new List<Dictionary<string, object>>();

				foreach ( Option opt in component.Options )
				{
					Dictionary<string, object> optDict = SerializeOptionToDictionary(opt, validationContext);

					// For TOML format, we need to separate option instructions
					if ( optDict.ContainsKey("Instructions") && optDict["Instructions"] is List<Dictionary<string, object>> optInstructions )
					{
						// Add Parent field for TOML's OptionsInstructions format
						foreach ( Dictionary<string, object> instrDict in optInstructions )
						{
							var instrDictWithParent = new Dictionary<string, object>(instrDict);
							instrDictWithParent["Parent"] = opt.Guid.ToString();
							optionsInstructionsList.Add(instrDictWithParent);
						}

						// Remove Instructions from optDict for TOML (they'll be in OptionsInstructions)
						// But keep them for other formats - each format can decide what to do
						optDict["_HasInstructions"] = true;
					}

					optionsList.Add(optDict);
				}

				componentDict["Options"] = optionsList;
				if ( optionsInstructionsList.Count > 0 )
					componentDict["OptionsInstructions"] = optionsInstructionsList;
			}

			// Store validation context metadata (format-specific rendering will use this)
			if ( validationContext != null )
			{
				List<string> componentIssues = validationContext.GetComponentIssues(component.Guid);
				if ( componentIssues.Count > 0 )
					componentDict["_ValidationIssues"] = componentIssues;

				if ( component.ModLinkFilenames != null && component.ModLinkFilenames.Count > 0 )
				{
					var urlFailures = new Dictionary<string, List<string>>();
					foreach ( string url in component.ModLinkFilenames.Keys )
					{
						List<string> failures = validationContext.GetUrlFailures(url);
						if ( failures.Count > 0 )
							urlFailures[url] = failures;
					}
					if ( urlFailures.Count > 0 )
						componentDict["_UrlFailures"] = urlFailures;
				}
			}

			return componentDict;
		}

		#endregion

		#region Format-Specific Conversion Helpers

		/// <summary>
		/// Converts a JToken to a Dictionary recursively, handling nested JObjects and JArrays.
		/// This ensures proper deserialization of nested structures like Options with Instructions.
		/// </summary>
		private static Dictionary<string, object> JTokenToDictionary(JToken token)
		{
			if ( token == null || token.Type == JTokenType.Null )
				return new Dictionary<string, object>();

			if ( !(token is JObject jobj) )
				throw new ArgumentException("Token must be a JObject");

			var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			foreach ( JProperty prop in jobj.Properties() )
			{
				string key = prop.Name;
				JToken value = prop.Value;

				// Convert key to PascalCase to match expected format
				if ( !string.IsNullOrEmpty(key) )
				{
					key = char.ToUpperInvariant(key[0]) + key.Substring(1);
				}

				switch ( value.Type )
				{
					case JTokenType.Null:
						result[key] = null;
						break;
					case JTokenType.Object:
						// Recursively convert nested objects
						result[key] = JTokenToDictionary(value);
						break;
					case JTokenType.Array:
						// Convert arrays to List<object>
						var list = new List<object>();
						foreach ( JToken item in (JArray)value )
						{
							if ( item.Type == JTokenType.Object )
							{
								list.Add(JTokenToDictionary(item));
							}
							else
							{
								list.Add(((JValue)item).Value);
							}
						}
						result[key] = list;
						break;
					case JTokenType.Integer:
					case JTokenType.Float:
					case JTokenType.String:
					case JTokenType.Boolean:
					case JTokenType.Date:
					case JTokenType.Bytes:
					case JTokenType.Guid:
					case JTokenType.Uri:
					case JTokenType.TimeSpan:
						result[key] = ((JValue)value).Value;
						break;
					default:
						Logger.LogWarning($"Unexpected JSON token type for key '{key}': {value.Type}");
						result[key] = value.ToString();
						break;
				}
			}

			return result;
		}

		/// <summary>
		/// Converts a dictionary to a JObject, recursively handling nested structures
		/// </summary>
		private static JObject DictionaryToJObject(Dictionary<string, object> dict)
		{
			var jobj = new JObject();
			foreach ( KeyValuePair<string, object> kvp in dict )
			{
				string key = kvp.Key;

				// Skip metadata fields starting with underscore (they're for format-specific use)
				if ( key.StartsWith("_") )
					continue;

				// Convert to camelCase for JSON
				key = char.ToLowerInvariant(key[0]) + key.Substring(1);

				object value = kvp.Value;

				if ( value == null )
				{
					jobj[key] = null;
				}
				else if ( value is Dictionary<string, object> nestedDict )
				{
					jobj[key] = DictionaryToJObject(nestedDict);
				}
				else if ( value is List<Dictionary<string, object>> listOfDicts )
				{
					var jarray = new JArray();
					foreach ( Dictionary<string, object> d in listOfDicts )
					{
						jarray.Add(DictionaryToJObject(d));
					}
					jobj[key] = jarray;
				}
				else if ( value is System.Collections.IEnumerable enumerable && !(value is string) )
				{
					jobj[key] = JArray.FromObject(enumerable);
				}
				else
				{
					jobj[key] = JToken.FromObject(value);
				}
			}
			return jobj;
		}

		#endregion

		private static Dictionary<string, object> SerializeModLinkFilenames(
			Dictionary<string, Dictionary<string, bool?>> modLinkFilenames
		)
		{
			var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			if ( modLinkFilenames == null || modLinkFilenames.Count == 0 )
				return result;

			foreach ( KeyValuePair<string, Dictionary<string, bool?>> kvp in modLinkFilenames )
			{
				string url = kvp.Key;
				Dictionary<string, bool?> filenamesDict = kvp.Value;

				if ( filenamesDict == null || filenamesDict.Count == 0 )
				{
					// Empty dictionary means auto-discover files
					result[url] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
					continue;
				}

				var serializedFilenames = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
				foreach ( KeyValuePair<string, bool?> fileKvp in filenamesDict )
				{
					string filename = fileKvp.Key;
					bool? shouldDownload = fileKvp.Value;

					// Serialize: null = "null", true = true, false = false
					if ( shouldDownload.HasValue )
						serializedFilenames[filename] = shouldDownload.Value;
					else
						serializedFilenames[filename] = "null";
				}
				result[url] = serializedFilenames;
			}
			return result;
		}

		public static Dictionary<string, Dictionary<string, bool?>> DeserializeModLinkFilenames(IDictionary<string, object> componentDict)
		{
			var result = new Dictionary<string, Dictionary<string, bool?>>(StringComparer.OrdinalIgnoreCase);

			try
			{
				if ( (!componentDict.TryGetValue("ModLinkFilenames", out object modLinkFilenamesObj) &&
					  !componentDict.TryGetValue("modLinkFilenames", out modLinkFilenamesObj)) || modLinkFilenamesObj == null )
				{
					return result;
				}

				if ( modLinkFilenamesObj is IDictionary<string, object> urlDict )
				{
					foreach ( KeyValuePair<string, object> kvp in urlDict )
					{
						string url = kvp.Key;
						var filenameDict = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);

						if ( kvp.Value is IDictionary<string, object> filenameObj )
						{
							foreach ( KeyValuePair<string, object> fileKvp in filenameObj )
							{
								string filename = fileKvp.Key;
								bool? shouldDownload = null;

								if ( fileKvp.Value is bool boolVal )
									shouldDownload = boolVal;
								else if ( fileKvp.Value != null )
								{
									string valueStr = fileKvp.Value.ToString();
									if ( string.IsNullOrEmpty(valueStr) )
									{
										Logger.LogVerbose(
											$"Failed to deserialize ModLinkFilenames (non-fatal): {filename} is null or empty");
										continue;
									}

									if ( !valueStr.Equals("null", StringComparison.OrdinalIgnoreCase) &&
										bool.TryParse(valueStr, out bool parsedBool) )
									{
										shouldDownload = parsedBool;
									}
									// else remains null (default)
								}

								filenameDict[filename] = shouldDownload;
							}
						}

						result[url] = filenameDict;
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"Failed to deserialize ModLinkFilenames (non-fatal): {ex.Message}");
			}

			return result;
		}
		#endregion
	}
}

