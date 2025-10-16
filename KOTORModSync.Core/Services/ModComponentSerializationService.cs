// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core.Parsing;
using Newtonsoft.Json.Linq;
using Tomlyn;
using Tomlyn.Model;
using static KOTORModSync.Core.Instruction;
using YamlSerialization = YamlDotNet.Serialization;

namespace KOTORModSync.Core.Services
{
	public class ComponentValidationContext
	{
		public Dictionary<Guid, List<string>> ComponentIssues { get; set; } = new Dictionary<Guid, List<string>>();
		public Dictionary<Guid, List<string>> InstructionIssues { get; set; } = new Dictionary<Guid, List<string>>();
		public Dictionary<string, List<string>> UrlFailures { get; set; } = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

		public void AddComponentIssue(Guid componentGuid, string issue)
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

		/// <summary>
		/// Creates a validation context from VirtualFileSystemProvider validation issues
		/// </summary>
		public static ComponentValidationContext FromVirtualFileSystem(
			[NotNull] List<ModComponent> components,
			[NotNull] FileSystem.VirtualFileSystemProvider vfs)
		{
			var context = new ComponentValidationContext();
			List<FileSystem.ValidationIssue> issues = vfs.GetValidationIssues();

			foreach ( FileSystem.ValidationIssue issue in issues )
			{
				if ( issue.AffectedComponent != null )
				{
					context.AddComponentIssue(issue.AffectedComponent.Guid, $"{issue.Category}: {issue.Message}");
				}

				if ( issue.AffectedInstruction != null )
				{
					context.AddInstructionIssue(issue.AffectedInstruction.Guid, $"{issue.Category}: {issue.Message}");
				}
			}

			return context;
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
						byte[] bytes = targetEncoding.GetBytes(new char[] { c });
						// If successful, add it to result
						result.Append(c);
					}
					catch
					{
						// If encoding fails, ignore/skip this character
						var lineNumber = input.Substring(0, input.IndexOf(c)).Count(x => x == '\n') + 1;
						var columnNumber = input.Substring(0, input.IndexOf(c)).Length - input.Substring(0, input.IndexOf(c)).LastIndexOf('\n') + 1;
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
		public static List<ModComponent> LoadFromTomlString([NotNull] string tomlContent)
		{
			Logger.LogVerbose($"Loading from TOML string");
			if ( tomlContent == null )
				throw new ArgumentNullException(nameof(tomlContent));
			tomlContent = SanitizeUtf8(tomlContent);
			tomlContent = tomlContent
				.Replace(oldValue: "Instructions = []", string.Empty)
				.Replace(oldValue: "Options = []", string.Empty);
			if ( string.IsNullOrWhiteSpace(tomlContent) )
				throw new InvalidDataException("TOML content is empty.");
			tomlContent = Utility.Serializer.FixWhitespaceIssues(tomlContent);
			var tomlDocument = Toml.Parse(tomlContent);
			if ( tomlDocument.HasErrors )
			{
				foreach ( var message in tomlDocument.Diagnostics )
				{
					if ( message != null )
						Logger.LogError(message.Message);
				}
			}
			var tomlTable = tomlDocument.ToModel();
			ParseMetadataSection(tomlTable);

			if ( !tomlTable.ContainsKey("thisMod") )
				throw new InvalidDataException("TOML content does not contain 'thisMod' array.");

			object thisModObj = tomlTable["thisMod"];
			IEnumerable<object> componentTables;

			if ( thisModObj is TomlTableArray tomlTableArray )
			{
				componentTables = tomlTableArray.Cast<object>();
			}
			else if ( thisModObj is System.Collections.IList list )
			{
				componentTables = list.Cast<object>();
			}
			else
			{
				throw new InvalidDataException($"TOML 'thisMod' is not a valid array type. Got: {thisModObj?.GetType().Name ?? "null"}");
			}

			var components = new List<ModComponent>();

			foreach ( var tomlComponent in componentTables )
			{
				if ( tomlComponent == null )
					continue;

				try
				{
					var thisComponent = new ModComponent();
					var componentDict = tomlComponent as IDictionary<string, object>
						?? throw new InvalidCastException("Failed to cast TOML component to IDictionary<string, object>");

					thisComponent.DeserializeComponent(componentDict);

					if ( componentDict.ContainsKey("_OptionsInstructions") || componentDict.ContainsKey("Options") )
					{
						if ( componentDict.TryGetValue("_OptionsInstructions", out object optionsInstructionsObj) ||
							 (componentDict.TryGetValue("Options", out object optionsObj) &&
							  optionsObj is IDictionary<string, object> optionsDict &&
							  optionsDict.TryGetValue("Instructions", out optionsInstructionsObj)) )
						{
							if ( optionsInstructionsObj is IList<object> optionInstructionsList )
							{
								var instructionsByParent = new Dictionary<string, List<object>>();
								foreach ( object instrObj in optionInstructionsList )
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
						}
					}

					components.Add(thisComponent);
				}
				catch ( Exception ex )
				{
					Logger.LogError($"Failed to deserialize component: {ex.Message}");
					Logger.LogError($"Exception type: {ex.GetType().Name}");
					Logger.LogError($"Stack trace: {ex.StackTrace}");
					throw;
				}
			}

			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in TOML content.");

			return components;
		}
		private static readonly string[] s_yamlSeparator = new[] { "---" };
		private static readonly string[] s_newLineSeparator = new[] { "\r\n", "\r", "\n" };
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromYamlString([NotNull] string yamlContent)
		{
			Logger.LogVerbose($"Loading from YAML string");
			if ( yamlContent == null )
				throw new ArgumentNullException(nameof(yamlContent));
			yamlContent = SanitizeUtf8(yamlContent);
			var components = new List<ModComponent>();
			string[] yamlDocs = yamlContent.Split(s_yamlSeparator, StringSplitOptions.RemoveEmptyEntries);
			foreach ( string yamlDoc in yamlDocs )
			{
				if ( string.IsNullOrWhiteSpace(yamlDoc) )
					continue;
				ModComponent component = DeserializeYAMLComponent(yamlDoc.Trim());
				if ( component != null )
				{
					components.Add(component);
				}
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in YAML content.");
			return components;
		}
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromMarkdownString([NotNull] string markdownContent)
		{
			Logger.LogVerbose("Loading from Markdown string");
			if ( markdownContent == null )
				throw new ArgumentNullException(nameof(markdownContent));
			markdownContent = SanitizeUtf8(markdownContent);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);
			MarkdownParserResult result = parser.Parse(markdownContent);
			if ( result.Components == null || result.Components.Count == 0 )
				throw new InvalidDataException("No valid components found in Markdown content.");
			return result.Components.ToList();
		}
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromJsonString([NotNull] string jsonContent)
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
			}
			var components = new List<ModComponent>();
			if ( jsonObject["components"] is JArray componentsArray )
			{
				foreach ( var compToken in componentsArray )
				{
					var compDict = compToken.ToObject<Dictionary<string, object>>();
					var component = new ModComponent();
					component.DeserializeComponent(compDict);
					components.Add(component);
				}
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in JSON content.");
			return components;
		}
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromXmlString([NotNull] string xmlContent)
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
			}
			var components = new List<ModComponent>();
			XElement componentsElem = root?.Element("Components");
			if ( componentsElem != null )
			{
				foreach ( XElement compElem in componentsElem.Elements("Component") )
				{
					Dictionary<string, object> compDict = XmlElementToDictionary(compElem);
					var component = new ModComponent();
					component.DeserializeComponent(compDict);
					components.Add(component);
				}
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in XML content.");
			return components;
		}
		private static readonly char[] s_iniKeyValueSeparators = new[] { '=' };

		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromIniString([NotNull] string iniContent)
		{
			Logger.LogVerbose($"Loading from INI string");
			if ( iniContent == null )
				throw new ArgumentNullException(nameof(iniContent));
			iniContent = SanitizeUtf8(iniContent);
			var components = new List<ModComponent>();
			var lines = iniContent.Split(s_newLineSeparator, StringSplitOptions.RemoveEmptyEntries);
			Dictionary<string, object> currentSection = null;
			string currentSectionName = null;
			foreach ( string line in lines )
			{
				string trimmed = line.Trim();
				if ( string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#") )
					continue;
				if ( trimmed.StartsWith("[") && trimmed.EndsWith("]") )
				{
					if ( currentSection != null && currentSectionName.StartsWith("Component") )
					{
						var component = new ModComponent();
						component.DeserializeComponent(currentSection);
						components.Add(component);
					}
					currentSectionName = trimmed.Substring(1, trimmed.Length - 2);
					currentSection = new Dictionary<string, object>();
				}
				else if ( trimmed.Contains("=") && currentSection != null )
				{
					string[] parts = trimmed.Split(s_iniKeyValueSeparators, 2);
					if ( parts.Length == 2 )
					{
						string key = parts[0].Trim();
						string value = parts[1].Trim();
						currentSection[key] = value;
					}
				}
			}
			if ( currentSection != null && currentSectionName.StartsWith("Component") )
			{
				var component = new ModComponent();
				component.DeserializeComponent(currentSection);
				components.Add(component);
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in INI content.");
			return components;
		}
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromString(
			[NotNull] string content,
			[CanBeNull] string format = null)
		{
			Logger.LogVerbose($"Loading from string with format: {format ?? "auto-detect"}");
			if ( content == null )
				throw new ArgumentNullException(nameof(content));

			if ( !string.IsNullOrWhiteSpace(format) )
			{
				string fmt = format.Trim().ToLowerInvariant();
				switch ( fmt )
				{
					case "toml":
					case "tml":
						return LoadFromTomlString(content);
					case "json":
						return LoadFromJsonString(content);
					case "yaml":
					case "yml":
						return LoadFromYamlString(content);
					case "xml":
						return LoadFromXmlString(content);
					case "md":
					case "markdown":
						return LoadFromMarkdownString(content);
					case "ini":
						return LoadFromIniString(content);
					default:
						throw new ArgumentException($"Unknown format \"{format}\" passed to LoadFromString.");
				}
			}

			try
			{
				return LoadFromTomlString(content);
			}
			catch ( Exception tomlEx )
			{
				Logger.LogVerbose($"TOML parsing failed: {tomlEx.Message}");

				try
				{
					return LoadFromMarkdownString(content);
				}
				catch ( Exception mdEx )
				{
					Logger.LogVerbose($"Markdown parsing failed: {mdEx.Message}");

					try
					{
						return LoadFromYamlString(content);
					}
					catch ( Exception yamlEx )
					{
						Logger.LogVerbose($"YAML parsing failed: {yamlEx.Message}");

						try
						{
							return LoadFromTomlString(content);
						}
						catch ( Exception tomlSecondEx )
						{
							Logger.LogVerbose($"TOML (second attempt) parsing failed: {tomlSecondEx.Message}");

							try
							{
								return LoadFromJsonString(content);
							}
							catch ( Exception jsonEx )
							{
								Logger.LogVerbose($"JSON parsing failed: {jsonEx.Message}");

								return LoadFromXmlString(content);
							}
						}
					}
				}
			}
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromTomlStringAsync([NotNull] string tomlContent)
		{
			return Task.Run(() => LoadFromTomlString(tomlContent));
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromYamlStringAsync([NotNull] string yamlContent)
		{
			return Task.Run(() => LoadFromYamlString(yamlContent));
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromMarkdownStringAsync([NotNull] string markdownContent)
		{
			return Task.Run(() => LoadFromMarkdownString(markdownContent));
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromJsonStringAsync([NotNull] string jsonContent)
		{
			return Task.Run(() => LoadFromJsonString(jsonContent));
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromXmlStringAsync([NotNull] string xmlContent)
		{
			return Task.Run(() => LoadFromXmlString(xmlContent));
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromIniStringAsync([NotNull] string iniContent)
		{
			return Task.Run(() => LoadFromIniString(iniContent));
		}
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromStringAsync([NotNull] string content, [CanBeNull] string format = null)
		{
			return Task.Run(() => LoadFromString(content, format));
		}
		#endregion
		#region Saving Functions
		[NotNull]
		public static string SaveToString(
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
					return SaveToTomlString(components, validationContext);
				case "yaml":
				case "yml":
					return SaveToYamlString(components, validationContext);
				case "md":
				case "markdown":
					return SaveToMarkdownString(components, validationContext);
				case "json":
					return SaveToJsonString(components, validationContext);
				case "xml":
					return SaveToXmlString(components, validationContext);
				case "ini":
					return SaveToIniString(components, validationContext);
				default:
					throw new NotSupportedException($"Unsupported format: {format}");
			}
		}
		[NotNull]
		public static Task<string> SaveToStringAsync([NotNull] List<ModComponent> components, [NotNull] string format = "toml")
		{
			return Task.Run(() => SaveToString(components, format));
		}
		#endregion
		#region Public Helpers

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
			try
			{
				if ( tomlTable.TryGetValue("metadata", out object metadataObj) && metadataObj is TomlTable metadataTable )
				{
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
					Logger.LogVerbose($"Loaded metadata: Game={MainConfig.TargetGame}, Version={MainConfig.FileFormatVersion}, Build={MainConfig.BuildName}");
				}
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
			var instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>();
			for ( int index = 0; index < instructionsSerializedList.Count; index++ )
			{
				Dictionary<string, object> instructionDict =
					Utility.Serializer.SerializeIntoDictionary(instructionsSerializedList[index]);
				Utility.Serializer.DeserializePathInDictionary(instructionDict, key: "Source");
				Utility.Serializer.DeserializeGuidDictionary(instructionDict, key: "Restrictions");
				Utility.Serializer.DeserializeGuidDictionary(instructionDict, key: "Dependencies");
				var instruction = new Instruction();
				string strAction = GetValueOrDefault<string>(instructionDict, key: "Action");
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
				instruction.Overwrite = !instructionDict.ContainsKey("Overwrite")
										|| GetValueOrDefault<bool>(instructionDict, key: "Overwrite");
				instruction.Restrictions = GetValueOrDefault<List<Guid>>(instructionDict, key: "Restrictions")
					?? new List<Guid>();
				instruction.Dependencies = GetValueOrDefault<List<Guid>>(instructionDict, key: "Dependencies")
					?? new List<Guid>();
				instruction.Source = GetValueOrDefault<List<string>>(instructionDict, key: "Source")
					?? new List<string>();
				instruction.Destination = GetValueOrDefault<string>(
						instructionDict,
						key: "Destination"
					)
					?? string.Empty;
				instructions.Add(instruction);
				if ( parentComponent is ModComponent parentMc )
					instruction.SetParentComponent(parentMc);
			}
			return instructions;
		}

		[ItemNotNull]
		[NotNull]
		internal static System.Collections.ObjectModel.ObservableCollection<Option> DeserializeOptions(
			[CanBeNull][ItemCanBeNull] IList<object> optionsSerializedList
		)
		{
			if ( optionsSerializedList == null || optionsSerializedList.Count == 0 )
				return new System.Collections.ObjectModel.ObservableCollection<Option>();

			var options = new System.Collections.ObjectModel.ObservableCollection<Option>();
			for ( int index = 0; index < optionsSerializedList.Count; index++ )
			{
				var optionsDict = (IDictionary<string, object>)optionsSerializedList[index];
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
				if ( !dict.TryGetValue(key, out object value) )
				{
					string caseInsensitiveKey = dict.Keys.FirstOrDefault(
						k => !(k is null) && k.Equals(key, StringComparison.OrdinalIgnoreCase)
					);
					if ( !dict.TryGetValue(caseInsensitiveKey ?? string.Empty, out object val2) && !required )
						return default;
					value = val2;
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
							return (T)(object)valueStr;
#pragma warning restore CS8600
						}
						break;
				}
				Type genericListDefinition = targetType.IsGenericType
					? targetType.GetGenericTypeDefinition()
					: null;
				if ( genericListDefinition == typeof(List<>) || genericListDefinition == typeof(IList<>) )
				{
					Type[] genericArgs = typeof(T).GetGenericArguments();
					Type listElementType = genericArgs.Length > 0
						? genericArgs[0]
						: typeof(string);
					Type listType = typeof(List<>).MakeGenericType(listElementType);
					var list = (T)Activator.CreateInstance(listType);
					System.Reflection.MethodInfo addMethod = list?.GetType().GetMethod(name: "Add");
					if ( value is IEnumerable<object> enumerableValue )
					{
						foreach ( object item in enumerableValue )
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
												string stringValue = nestedItem?.ToString() ?? string.Empty;
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
				try
				{
					return (T)Convert.ChangeType(value, typeof(T));
				}
				catch ( Exception e )
				{
					Logger.LogError($"Could not deserialize key '{key}'");
					if ( required )
						throw;
					Logger.LogException(e);
				}
			}
			catch ( Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ) when ( !required )
			{
				return default;
			}
			catch ( InvalidCastException ) when ( !required )
			{
				return default;
			}
			return default;
		}

		[CanBeNull]
		public static ModComponent DeserializeYAMLComponent([NotNull] string yamlString)
		{
			if ( yamlString is null )
				throw new ArgumentNullException(nameof(yamlString));
			try
			{
				var deserializer = new YamlSerialization.DeserializerBuilder()
					.WithNamingConvention(YamlSerialization.NamingConventions.PascalCaseNamingConvention.Instance)
					.IgnoreUnmatchedProperties()
					.Build();
				var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yamlString);
				if ( yamlDict == null )
				{
					Logger.LogError("Failed to deserialize YAML: result was null");
					return null;
				}
				var component = new ModComponent();
				component.DeserializeComponent(yamlDict);
				return component;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to deserialize YAML component");
				return null;
			}
		}
		public static string SaveToTomlString(
			List<ModComponent> components,
			ComponentValidationContext validationContext = null)
		{
			Logger.LogVerbose($"Saving to TOML string");
			var result = new StringBuilder();

			var metadataTable = new TomlTable
			{
				["fileFormatVersion"] = MainConfig.FileFormatVersion ?? "2.0"
			};
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

			var metadataRoot = new Dictionary<string, object> { ["metadata"] = metadataTable };
			result.AppendLine(Toml.FromModel(metadataRoot));

			bool isFirst = true;
			foreach ( ModComponent component in components )
			{
				if ( !isFirst )
				{
					result.AppendLine();
					result.AppendLine();
				}
				isFirst = false;

				// Add validation comments for component issues
				if ( validationContext != null )
				{
					var componentIssues = validationContext.GetComponentIssues(component.Guid);
					if ( componentIssues.Count > 0 )
					{
						result.AppendLine("# VALIDATION ISSUES:");
						foreach ( var issue in componentIssues )
						{
							result.AppendLine($"# {issue}");
						}
					}

					// Add URL failure comments
					if ( component.ModLinkFilenames != null && component.ModLinkFilenames.Count > 0 )
					{
						foreach ( var url in component.ModLinkFilenames.Keys )
						{
							var urlFailures = validationContext.GetUrlFailures(url);
							if ( urlFailures.Count > 0 )
							{
								result.AppendLine($"# URL RESOLUTION FAILURE: {url}");
								foreach ( var failure in urlFailures )
								{
									result.AppendLine($"# {failure}");
								}
							}
						}
					}
				}

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
				if ( component.IsSelected )
					componentDict["IsSelected"] = component.IsSelected;
				if ( component.WidescreenOnly )
					componentDict["WidescreenOnly"] = component.WidescreenOnly;

				if ( component.Category?.Count > 0 )
					componentDict["Category"] = component.Category;
				if ( component.Language?.Count > 0 )
					componentDict["Language"] = component.Language;

				// Serialize ModLinkFilenames - keep as inline nested dictionary
				if ( component.ModLinkFilenames?.Count > 0 )
				{
					componentDict["ModLinkFilenames"] = SerializeModLinkFilenames(component.ModLinkFilenames);
				}

				if ( component.ExcludedDownloads?.Count > 0 )
					componentDict["ExcludedDownloads"] = component.ExcludedDownloads;
				if ( component.Dependencies?.Count > 0 )
					componentDict["Dependencies"] = component.Dependencies.Select(g => g.ToString()).ToList();
				if ( component.Restrictions?.Count > 0 )
					componentDict["Restrictions"] = component.Restrictions.Select(g => g.ToString()).ToList();
				if ( component.InstallAfter?.Count > 0 )
					componentDict["InstallAfter"] = component.InstallAfter.Select(g => g.ToString()).ToList();
				if ( component.InstallBefore?.Count > 0 )
					componentDict["InstallBefore"] = component.InstallBefore.Select(g => g.ToString()).ToList();

				if ( component.Instructions?.Count > 0 )
				{
					var instructionsList = new List<Dictionary<string, object>>();
					foreach ( Instruction instr in component.Instructions )
					{
						var instrDict = new Dictionary<string, object>();
						if ( instr.Guid != Guid.Empty )
							instrDict["Guid"] = instr.Guid.ToString();
						if ( !string.IsNullOrWhiteSpace(instr.ActionString) )
							instrDict["Action"] = instr.ActionString;
						if ( instr.Source?.Count > 0 )
							instrDict["Source"] = instr.Source;
						if ( !string.IsNullOrWhiteSpace(instr.Destination) )
							instrDict["Destination"] = instr.Destination;
						if (
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
						if ( instr.Dependencies?.Count > 0 )
							instrDict["Dependencies"] = instr.Dependencies.Select(g => g.ToString()).ToList();
						if ( instr.Restrictions?.Count > 0 )
							instrDict["Restrictions"] = instr.Restrictions.Select(g => g.ToString()).ToList();
						instructionsList.Add(instrDict);
					}
					componentDict["Instructions"] = instructionsList;
				}

				if ( component.Options?.Count > 0 )
				{
					var optionsList = new List<Dictionary<string, object>>();
					var optionsInstructionsList = new List<Dictionary<string, object>>();

					foreach ( Option opt in component.Options )
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
						if ( opt.Restrictions?.Count > 0 )
							optDict["Restrictions"] = opt.Restrictions.Select(g => g.ToString()).ToList();
						if ( opt.Dependencies?.Count > 0 )
							optDict["Dependencies"] = opt.Dependencies.Select(g => g.ToString()).ToList();
						optionsList.Add(optDict);

						if ( opt.Instructions?.Count > 0 )
						{
							foreach ( Instruction instr in opt.Instructions )
							{
								var instrDict = new Dictionary<string, object>();
								instrDict["Parent"] = opt.Guid.ToString();
								if ( instr.Guid != Guid.Empty )
									instrDict["Guid"] = instr.Guid.ToString();
								if ( !string.IsNullOrWhiteSpace(instr.ActionString) )
									instrDict["Action"] = instr.ActionString;
								if ( instr.Source?.Count > 0 )
									instrDict["Source"] = instr.Source;
								if ( !string.IsNullOrWhiteSpace(instr.Destination) )
									instrDict["Destination"] = instr.Destination;
								if (
									!instr.Overwrite
									&&
									(
										instr.Action == ActionType.Move ||
									 	instr.Action == ActionType.Copy ||
									 	instr.Action == ActionType.Rename
									)
								)
								{
									instrDict["Overwrite"] = instr.Overwrite;
								}

								if (
									!string.IsNullOrWhiteSpace(instr.Arguments)
									&&
									(
										instr.Action == ActionType.DelDuplicate ||
									 	instr.Action == ActionType.Execute ||
									 	instr.Action == ActionType.Patcher
									)
								)
								{
									instrDict["Arguments"] = instr.Arguments;
								}
								if ( instr.Dependencies?.Count > 0 )
									instrDict["Dependencies"] = instr.Dependencies.Select(g => g.ToString()).ToList();
								if ( instr.Restrictions?.Count > 0 )
									instrDict["Restrictions"] = instr.Restrictions.Select(g => g.ToString()).ToList();
								optionsInstructionsList.Add(instrDict);
							}
						}
					}
					componentDict["Options"] = optionsList;
					if ( optionsInstructionsList.Count > 0 )
						componentDict["OptionsInstructions"] = optionsInstructionsList;
				}

				var nestedContent = new StringBuilder();
				var modLinkFilenamesDict = FixSerializedTomlDict(componentDict, nestedContent, validationContext, component);

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
								{
									mlf.Append(boolVal ? "true" : "false");
								}
								else if ( fileEntry.Value is string strVal && strVal == "null" )
								{
									mlf.Append("\"null\"");
								}
								else
								{
									mlf.Append("\"null\"");
								}
							}
						}

						mlf.Append(" }");
					}

					mlf.AppendLine(" }");

					// Insert after the [[thisMod]] line
					int insertPos = componentToml.IndexOf('\n');
					if ( insertPos > 0 )
					{
						componentToml = componentToml.Insert(insertPos + 1, mlf.ToString());
					}
				}

				result.Append(componentToml.TrimEnd());

				if ( nestedContent.Length > 0 )
				{
					result.AppendLine();
					result.Append(nestedContent.ToString());
				}
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

			if ( serializedComponentDict.TryGetValue("Instructions", out object val) )
			{
				List<Dictionary<string, object>> instructionsList = null;

				if ( val is List<Dictionary<string, object>> list )
				{
					instructionsList = list;
				}
				else if ( val is IEnumerable<Dictionary<string, object>> enumerable )
				{
					instructionsList = enumerable.ToList();
				}

				if ( instructionsList != null && instructionsList.Count > 0 )
				{
					int instructionIndex = 0;
					foreach ( var item in instructionsList )
					{
						if ( item == null || item.Count == 0 )
							continue;

						// Add validation comments for instruction issues
						if ( validationContext != null && component != null && instructionIndex < component.Instructions.Count )
						{
							var instruction = component.Instructions[instructionIndex];
							var instructionIssues = validationContext.GetInstructionIssues(instruction.Guid);
							if ( instructionIssues.Count > 0 )
							{
								tomlString.AppendLine();
								tomlString.AppendLine("# INSTRUCTION VALIDATION ISSUES:");
								foreach ( var issue in instructionIssues )
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
						tomlString.Append(Toml.FromModel(model).Replace($"thisMod.Instructions", $"[thisMod.Instructions]"));
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
				var optionsList = serializedComponentDict["Options"] as List<Dictionary<string, object>>;
				var optionsInstructionsList = serializedComponentDict["OptionsInstructions"] as List<Dictionary<string, object>>;

				if ( optionsList != null && optionsInstructionsList != null )
				{
					var instructionsByParent = optionsInstructionsList
						.Where(instr => instr != null && instr.ContainsKey("Parent"))
						.GroupBy(instr => instr["Parent"]?.ToString())
						.ToDictionary(g => g.Key, g => g.ToList());

					for ( int i = 0; i < optionsList.Count; i++ )
					{
						Dictionary<string, object> optionDict = optionsList[i];
						if ( optionDict is null || optionDict.Count == 0 )
							continue;

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

						if ( optionDict.TryGetValue("Guid", out object guidObj) )
						{
							string optionGuid = guidObj?.ToString();
							if ( !string.IsNullOrEmpty(optionGuid) && instructionsByParent.TryGetValue(optionGuid, out var instructions) )
							{
								// Find the option in the component
								Option currentOption = null;
								if ( component != null && Guid.TryParse(optionGuid, out Guid optGuid) )
								{
									currentOption = component.Options.FirstOrDefault(opt => opt.Guid == optGuid);
								}

								int optionInstrIndex = 0;
								foreach ( var instruction in instructions.Where(instruction => instruction != null && instruction.Count != 0) )
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
		public static string SaveToYamlString(
				List<ModComponent> components,
				ComponentValidationContext validationContext = null
			)
		{
			Logger.LogVerbose("Saving to YAML string");
			var sb = new StringBuilder();
			YamlSerialization.ISerializer serializer = new YamlDotNet.Serialization.SerializerBuilder()
				.WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
				.ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitNull)
				.DisableAliases()
				.Build();
			foreach ( ModComponent component in components )
			{
				sb.AppendLine("---");

				// Add validation comments for component issues
				if ( validationContext != null )
				{
					List<string> componentIssues = validationContext.GetComponentIssues(component.Guid);
					if ( componentIssues.Count > 0 )
					{
						sb.AppendLine("# VALIDATION ISSUES:");
						foreach ( string issue in componentIssues )
						{
							sb.AppendLine($"# {issue}");
						}
					}

					// Add URL failure comments
					if ( component.ModLinkFilenames is { Count: > 0 } )
					{
						foreach ( string url in component.ModLinkFilenames.Keys )
						{
							List<string> urlFailures = validationContext.GetUrlFailures(url);
							if ( urlFailures.Count > 0 )
							{
								sb.AppendLine($"# URL RESOLUTION FAILURE: {url}");
								foreach ( string failure in urlFailures )
								{
									sb.AppendLine($"# {failure}");
								}
							}
						}
					}
				}
				var dict = new Dictionary<string, object>
				{
					{ "Guid", component.Guid },
					{ "Name", component.Name }
				};

				if ( !string.IsNullOrWhiteSpace(component.Author) )
					dict["Author"] = component.Author;
				if ( !string.IsNullOrWhiteSpace(component.Description) )
					dict["Description"] = component.Description;
				if ( !string.IsNullOrWhiteSpace(component._descriptionSpoilerFree) && component._descriptionSpoilerFree != component.Description )
					dict["DescriptionSpoilerFree"] = component._descriptionSpoilerFree;
				if ( component.Category?.Count > 0 )
					dict["Category"] = component.Category;
				if ( !string.IsNullOrWhiteSpace(component.Tier) )
					dict["Tier"] = component.Tier;
				if ( component.InstallBefore?.Count > 0 )
					dict["InstallBefore"] = component.InstallBefore;
				if ( component.InstallAfter?.Count > 0 )
					dict["InstallAfter"] = component.InstallAfter;
				if ( !string.IsNullOrWhiteSpace(component.InstallationMethod) )
					dict["InstallationMethod"] = component.InstallationMethod;
				if ( !string.IsNullOrWhiteSpace(component.UsageWarning) )
					dict["UsageWarning"] = component.UsageWarning;
				if ( !string.IsNullOrWhiteSpace(component._usageWarningSpoilerFree) && component._usageWarningSpoilerFree != component.UsageWarning )
					dict["UsageWarningSpoilerFree"] = component._usageWarningSpoilerFree;
				if ( !string.IsNullOrWhiteSpace(component.CompatibilityWarning) )
					dict["CompatibilityWarning"] = component.CompatibilityWarning;
				if ( !string.IsNullOrWhiteSpace(component.KnownBugs) )
					dict["KnownBugs"] = component.KnownBugs;
				if ( !string.IsNullOrWhiteSpace(component.InstallationWarning) )
					dict["InstallationWarning"] = component.InstallationWarning;
				if ( !string.IsNullOrWhiteSpace(component.SteamNotes) )
					dict["SteamNotes"] = component.SteamNotes;
				if ( component.IsSelected )
					dict["IsSelected"] = component.IsSelected;
				if ( component.Dependencies?.Count > 0 )
					dict["Dependencies"] = component.Dependencies;
				if ( component.Restrictions?.Count > 0 )
					dict["Restrictions"] = component.Restrictions;
				if ( !string.IsNullOrWhiteSpace(component.DownloadInstructions) )
					dict["DownloadInstructions"] = component.DownloadInstructions;
				if ( !string.IsNullOrWhiteSpace(component._downloadInstructionsSpoilerFree) && component._downloadInstructionsSpoilerFree != component.DownloadInstructions )
					dict["DownloadInstructionsSpoilerFree"] = component._downloadInstructionsSpoilerFree;
				if ( !string.IsNullOrWhiteSpace(component.Directions) )
					dict["Directions"] = component.Directions;
				if ( !string.IsNullOrWhiteSpace(component._directionsSpoilerFree) && component._directionsSpoilerFree != component.Directions )
					dict["DirectionsSpoilerFree"] = component._directionsSpoilerFree;
				if ( !string.IsNullOrWhiteSpace(component.Screenshots) )
					dict["Screenshots"] = component.Screenshots;
				if ( !string.IsNullOrWhiteSpace(component._screenshotsSpoilerFree) && component._screenshotsSpoilerFree != component.Screenshots )
					dict["ScreenshotsSpoilerFree"] = component._screenshotsSpoilerFree;
				if ( component.Language?.Count > 0 )
					dict["Language"] = component.Language;

				// Serialize ModLinkFilenames
				if ( component.ModLinkFilenames?.Count > 0 )
				{
					dict["ModLinkFilenames"] = SerializeModLinkFilenames(component.ModLinkFilenames);
				}

				if ( component.ExcludedDownloads?.Count > 0 )
					dict["ExcludedDownloads"] = component.ExcludedDownloads;
				if ( component.Instructions?.Count > 0 )
				{
					var instructions = new List<Dictionary<string, object>>();
					foreach ( Instruction inst in component.Instructions )
					{
						var instDict = new Dictionary<string, object>
					{
						{ "Action", inst.Action.ToString().ToLowerInvariant() }
					};

						// Add validation warnings as a special field
						if ( validationContext != null )
						{
							List<string> instructionIssues = validationContext.GetInstructionIssues(inst.Guid);
							if ( instructionIssues.Count > 0 )
							{
								instDict["_ValidationWarnings"] = instructionIssues;
							}
						}

						if ( inst.Source?.Count > 0 )
							instDict["Source"] = inst.Source;

						if ( !string.IsNullOrEmpty(inst.Destination) &&
							(inst.Action == ActionType.Move ||
							 inst.Action == ActionType.Patcher ||
							 inst.Action == ActionType.Copy) )
							instDict["Destination"] = inst.Destination;

						if ( !inst.Overwrite &&
							(inst.Action == ActionType.Move ||
							 inst.Action == ActionType.Copy) )
							instDict["Overwrite"] = false;

						if ( !string.IsNullOrEmpty(inst.Arguments) &&
							(inst.Action == ActionType.Patcher ||
							 inst.Action == ActionType.Execute) )
							instDict["Arguments"] = inst.Arguments;

						instructions.Add(instDict);
					}
					dict["Instructions"] = instructions;
				}
				sb.AppendLine(serializer.Serialize(dict));
			}
			return SanitizeUtf8(sb.ToString());
		}
		public static string SaveToMarkdownString(
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
		public static string SaveToJsonString(
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
			jsonRoot["metadata"] = metadata;

			var componentsArray = new JArray();
			foreach ( ModComponent c in components )
			{
				var componentObj = new JObject
				{
					["guid"] = c.Guid.ToString(),
					["name"] = c.Name
				};

				// Add validation warnings for component
				if ( validationContext != null )
				{
					List<string> componentIssues = validationContext.GetComponentIssues(c.Guid);
					if ( componentIssues.Count > 0 )
						componentObj["_validationWarnings"] = JArray.FromObject(componentIssues);

					// Add URL failure warnings
					if ( c.ModLinkFilenames != null && c.ModLinkFilenames.Count > 0 )
					{
						var urlFailures = new List<string>();
						foreach ( var url in c.ModLinkFilenames.Keys )
						{
							List<string> failures = validationContext.GetUrlFailures(url);
							if ( failures.Count > 0 )
							{
								urlFailures.Add($"URL: {url}");
								urlFailures.AddRange(failures);
							}
						}
						if ( urlFailures.Count > 0 )
						{
							componentObj["_urlResolutionFailures"] = JArray.FromObject(urlFailures);
						}
					}
				}

				if ( !string.IsNullOrWhiteSpace(c.Author) ) componentObj["author"] = c.Author;
				if ( !string.IsNullOrWhiteSpace(c.Description) ) componentObj["description"] = c.Description;
				if ( !string.IsNullOrWhiteSpace(c._descriptionSpoilerFree) && c._descriptionSpoilerFree != c.Description ) componentObj["descriptionSpoilerFree"] = c._descriptionSpoilerFree;
				if ( c.Category?.Count > 0 ) componentObj["category"] = JArray.FromObject(c.Category);
				if ( !string.IsNullOrWhiteSpace(c.Tier) ) componentObj["tier"] = c.Tier;
				if ( c.Language?.Count > 0 ) componentObj["language"] = JArray.FromObject(c.Language);

				// Serialize ModLinkFilenames
				if ( c.ModLinkFilenames?.Count > 0 )
				{
					componentObj["modLinkFilenames"] = JObject.FromObject(SerializeModLinkFilenames(c.ModLinkFilenames));
				}

				if ( c.ExcludedDownloads?.Count > 0 ) componentObj["excludedDownloads"] = JArray.FromObject(c.ExcludedDownloads);
				if ( !string.IsNullOrWhiteSpace(c.InstallationMethod) ) componentObj["installationMethod"] = c.InstallationMethod;
				if ( !string.IsNullOrWhiteSpace(c.Directions) ) componentObj["directions"] = c.Directions;
				if ( !string.IsNullOrWhiteSpace(c._directionsSpoilerFree) && c._directionsSpoilerFree != c.Directions ) componentObj["directionsSpoilerFree"] = c._directionsSpoilerFree;
				if ( !string.IsNullOrWhiteSpace(c.DownloadInstructions) ) componentObj["downloadInstructions"] = c.DownloadInstructions;
				if ( !string.IsNullOrWhiteSpace(c._downloadInstructionsSpoilerFree) && c._downloadInstructionsSpoilerFree != c.DownloadInstructions ) componentObj["downloadInstructionsSpoilerFree"] = c._downloadInstructionsSpoilerFree;
				if ( !string.IsNullOrWhiteSpace(c.UsageWarning) ) componentObj["usageWarning"] = c.UsageWarning;
				if ( !string.IsNullOrWhiteSpace(c._usageWarningSpoilerFree) && c._usageWarningSpoilerFree != c.UsageWarning ) componentObj["usageWarningSpoilerFree"] = c._usageWarningSpoilerFree;
				if ( !string.IsNullOrWhiteSpace(c.Screenshots) ) componentObj["screenshots"] = c.Screenshots;
				if ( !string.IsNullOrWhiteSpace(c._screenshotsSpoilerFree) && c._screenshotsSpoilerFree != c.Screenshots ) componentObj["screenshotsSpoilerFree"] = c._screenshotsSpoilerFree;
				if ( !string.IsNullOrWhiteSpace(c.KnownBugs) ) componentObj["knownBugs"] = c.KnownBugs;
				if ( !string.IsNullOrWhiteSpace(c.InstallationWarning) ) componentObj["installationWarning"] = c.InstallationWarning;
				if ( !string.IsNullOrWhiteSpace(c.CompatibilityWarning) ) componentObj["compatibilityWarning"] = c.CompatibilityWarning;
				if ( !string.IsNullOrWhiteSpace(c.SteamNotes) ) componentObj["steamNotes"] = c.SteamNotes;
				if ( c.Dependencies?.Count > 0 ) componentObj["dependencies"] = JArray.FromObject(c.Dependencies);
				if ( c.Restrictions?.Count > 0 ) componentObj["restrictions"] = JArray.FromObject(c.Restrictions);
				if ( c.InstallBefore?.Count > 0 ) componentObj["installBefore"] = JArray.FromObject(c.InstallBefore);
				if ( c.InstallAfter?.Count > 0 ) componentObj["installAfter"] = JArray.FromObject(c.InstallAfter);
				if ( !string.IsNullOrWhiteSpace(c.Heading) ) componentObj["heading"] = c.Heading;
				if ( c.WidescreenOnly ) componentObj["widescreenOnly"] = c.WidescreenOnly;

				if ( c.Instructions?.Count > 0 )
				{
					var instructionsArray = new JArray();
					foreach ( Instruction i in c.Instructions )
					{
						var instrObj = new JObject
						{
							["guid"] = i.Guid,
							["action"] = i.ActionString
						};

						// Add validation warnings for instruction
						if ( validationContext != null )
						{
							List<string> instructionIssues = validationContext.GetInstructionIssues(i.Guid);
							if ( instructionIssues.Count > 0 )
								instrObj["_validationWarnings"] = JArray.FromObject(instructionIssues);
						}

						if ( i.Source?.Count > 0 )
							instrObj["source"] = JArray.FromObject(i.Source);

						if ( !string.IsNullOrEmpty(i.Destination) &&
							(i.Action == ActionType.Move ||
							 i.Action == ActionType.Patcher ||
							 i.Action == ActionType.Copy) )
							instrObj["destination"] = i.Destination;

						if ( !string.IsNullOrEmpty(i.Arguments) &&
							(i.Action == ActionType.Patcher ||
							 i.Action == ActionType.Execute) )
							instrObj["arguments"] = i.Arguments;

						if ( !i.Overwrite &&
							(i.Action == ActionType.Move ||
							 i.Action == ActionType.Copy) )
							instrObj["overwrite"] = false;

						if ( i.Dependencies?.Count > 0 )
							instrObj["dependencies"] = JArray.FromObject(i.Dependencies);
						if ( i.Restrictions?.Count > 0 )
							instrObj["restrictions"] = JArray.FromObject(i.Restrictions);

						instructionsArray.Add(instrObj);
					}
					componentObj["instructions"] = instructionsArray;
				}

				if ( c.Options?.Count > 0 )
				{
					var optionsArray = new JArray();
					foreach ( Option o in c.Options )
					{
						var optionObj = new JObject
						{
							["guid"] = o.Guid,
							["name"] = o.Name
						};

						if ( !string.IsNullOrWhiteSpace(o.Description) ) optionObj["description"] = o.Description;
						if ( o.Restrictions?.Count > 0 ) optionObj["restrictions"] = JArray.FromObject(o.Restrictions);
						if ( o.Dependencies?.Count > 0 ) optionObj["dependencies"] = JArray.FromObject(o.Dependencies);

						if ( o.Instructions?.Count > 0 )
						{
							var optInstructionsArray = new JArray();
							foreach ( Instruction i in o.Instructions )
							{
								var instrObj = new JObject
								{
									["guid"] = i.Guid,
									["action"] = i.ActionString
								};

								// Add validation warnings for option instruction
								if ( validationContext != null )
								{
									List<string> instructionIssues = validationContext.GetInstructionIssues(i.Guid);
									if ( instructionIssues.Count > 0 )
									{
										instrObj["_validationWarnings"] = JArray.FromObject(instructionIssues);
									}
								}

								if ( i.Source?.Count > 0 )
									instrObj["source"] = JArray.FromObject(i.Source);

								if ( !string.IsNullOrEmpty(i.Destination) &&
									(i.Action == ActionType.Move ||
									 i.Action == ActionType.Patcher ||
									 i.Action == ActionType.Copy) )
									instrObj["destination"] = i.Destination;

								if ( !string.IsNullOrEmpty(i.Arguments) &&
									(i.Action == ActionType.Patcher ||
									 i.Action == ActionType.Execute) )
									instrObj["arguments"] = i.Arguments;

								if ( !i.Overwrite &&
									(i.Action == ActionType.Move ||
									 i.Action == ActionType.Copy) )
									instrObj["overwrite"] = false;

								if ( i.Dependencies?.Count > 0 )
									instrObj["dependencies"] = JArray.FromObject(i.Dependencies);
								if ( i.Restrictions?.Count > 0 )
									instrObj["restrictions"] = JArray.FromObject(i.Restrictions);

								optInstructionsArray.Add(instrObj);
							}
							optionObj["instructions"] = optInstructionsArray;
						}

						optionsArray.Add(optionObj);
					}
					componentObj["options"] = optionsArray;
				}

				componentsArray.Add(componentObj);
			}
			jsonRoot["components"] = componentsArray;

			return SanitizeUtf8(jsonRoot.ToString(Newtonsoft.Json.Formatting.Indented));
		}
		public static string SaveToXmlString(
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

						instructionsElement.Add(new XElement("Instruction",
							new XElement("Guid", instr.Guid.ToString()),
							new XElement("Action", instr.ActionString),
							instr.Source?.Count > 0
								? new XElement("Source", instr.Source.Select(s => new XElement("Item", s)))
								: null,
							!string.IsNullOrWhiteSpace(instr.Destination)
								? new XElement("Destination", instr.Destination)
								: null,
							!string.IsNullOrWhiteSpace(instr.Arguments)
								? new XElement("Arguments", instr.Arguments)
								: null,
							!instr.Overwrite
								? new XElement("Overwrite", false)
								: null
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
									!string.IsNullOrWhiteSpace(instr.Arguments)
										? new XElement("Arguments", instr.Arguments)
										: null,
									!instr.Overwrite
										? new XElement("Overwrite", false)
										: null
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
		public static string SaveToIniString(
			List<ModComponent> components,
			ComponentValidationContext validationContext = null)
		{
			var sb = new StringBuilder();
			sb.AppendLine("[Metadata]");
			sb.AppendLine($"fileFormatVersion={MainConfig.FileFormatVersion ?? "2.0"}");
			if ( !string.IsNullOrWhiteSpace(MainConfig.TargetGame) )
				sb.AppendLine($"targetGame={MainConfig.TargetGame}");
			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildName) )
				sb.AppendLine($"buildName={MainConfig.BuildName}");
			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildAuthor) )
				sb.AppendLine($"buildAuthor={MainConfig.BuildAuthor}");
			if ( !string.IsNullOrWhiteSpace(MainConfig.BuildDescription) )
				sb.AppendLine($"buildDescription={MainConfig.BuildDescription}");
			if ( MainConfig.LastModified.HasValue )
				sb.AppendLine($"lastModified={MainConfig.LastModified.Value:o}");
			sb.AppendLine();
			for ( int i = 0; i < components.Count; i++ )
			{
				ModComponent c = components[i];

				// Add validation comments for component
				if ( validationContext != null )
				{
					List<string> componentIssues = validationContext.GetComponentIssues(c.Guid);
					if ( componentIssues.Count > 0 )
					{
						sb.AppendLine("; VALIDATION ISSUES:");
						foreach ( string issue in componentIssues )
						{
							sb.AppendLine($"; {issue}");
						}
					}

					// Add URL failure comments
					if ( c.ModLinkFilenames != null && c.ModLinkFilenames.Count > 0 )
					{
						foreach ( string url in c.ModLinkFilenames.Keys )
						{
							List<string> urlFailures = validationContext.GetUrlFailures(url);
							if ( urlFailures.Count > 0 )
							{
								sb.AppendLine($"; URL RESOLUTION FAILURE: {url}");
								foreach ( string failure in urlFailures )
								{
									sb.AppendLine($"; {failure}");
								}
							}
						}
					}
				}

				sb.AppendLine($"[Component{i + 1}]");
				sb.AppendLine($"Guid={c.Guid}");
				sb.AppendLine($"Name={c.Name}");
				if ( !string.IsNullOrWhiteSpace(c.Author) )
					sb.AppendLine($"Author={c.Author}");
				if ( !string.IsNullOrWhiteSpace(c.Description) )
					sb.AppendLine($"Description={c.Description}");
				if ( !string.IsNullOrWhiteSpace(c._descriptionSpoilerFree) && c._descriptionSpoilerFree != c.Description )
					sb.AppendLine($"DescriptionSpoilerFree={c._descriptionSpoilerFree}");
				if ( c.Category?.Count > 0 )
					sb.AppendLine($"Category={string.Join(",", c.Category)}");
				if ( !string.IsNullOrWhiteSpace(c.Tier) )
					sb.AppendLine($"Tier={c.Tier}");
				if ( c.Language?.Count > 0 )
					sb.AppendLine($"Language={string.Join(",", c.Language)}");

				// Serialize ModLinkFilenames URLs as ModLinkFilenames for INI format (INI doesn't support nested structures well)
				if ( c.ModLinkFilenames?.Count > 0 )
				{
					List<string> urls = c.ModLinkFilenames.Keys.ToList();
					if ( urls.Count > 0 )
						sb.AppendLine($"ModLinkFilenames={string.Join("|", urls)}");
				}

				if ( c.ExcludedDownloads?.Count > 0 )
					sb.AppendLine($"ExcludedDownloads={string.Join("|", c.ExcludedDownloads)}");
				if ( !string.IsNullOrWhiteSpace(c.InstallationMethod) )
					sb.AppendLine($"InstallationMethod={c.InstallationMethod}");
				if ( !string.IsNullOrWhiteSpace(c.Directions) )
					sb.AppendLine($"Directions={c.Directions.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( !string.IsNullOrWhiteSpace(c._directionsSpoilerFree) && c._directionsSpoilerFree != c.Directions )
					sb.AppendLine($"DirectionsSpoilerFree={c._directionsSpoilerFree.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( !string.IsNullOrWhiteSpace(c.DownloadInstructions) )
					sb.AppendLine($"DownloadInstructions={c.DownloadInstructions.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( !string.IsNullOrWhiteSpace(c._downloadInstructionsSpoilerFree) && c._downloadInstructionsSpoilerFree != c.DownloadInstructions )
					sb.AppendLine($"DownloadInstructionsSpoilerFree={c._downloadInstructionsSpoilerFree.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( !string.IsNullOrWhiteSpace(c.UsageWarning) )
					sb.AppendLine($"UsageWarning={c.UsageWarning.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( !string.IsNullOrWhiteSpace(c._usageWarningSpoilerFree) && c._usageWarningSpoilerFree != c.UsageWarning )
					sb.AppendLine($"UsageWarningSpoilerFree={c._usageWarningSpoilerFree.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( !string.IsNullOrWhiteSpace(c.Screenshots) )
					sb.AppendLine($"Screenshots={c.Screenshots.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( !string.IsNullOrWhiteSpace(c._screenshotsSpoilerFree) && c._screenshotsSpoilerFree != c.Screenshots )
					sb.AppendLine($"ScreenshotsSpoilerFree={c._screenshotsSpoilerFree.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( c.Dependencies?.Count > 0 )
					sb.AppendLine($"Dependencies={string.Join(",", c.Dependencies)}");
				if ( c.Restrictions?.Count > 0 )
					sb.AppendLine($"Restrictions={string.Join(",", c.Restrictions)}");
				if ( c.WidescreenOnly )
					sb.AppendLine($"WidescreenOnly=true");
				sb.AppendLine();
			}
			return SanitizeUtf8(sb.ToString());
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
					{
						categoryStr = component.Category[0];
					}
					else if ( component.Category.Count == 2 )
					{
						categoryStr = $"{component.Category[0]} & {component.Category[1]}";
					}
					else
					{
						IEnumerable<String> allButLast = component.Category.Take(component.Category.Count - 1);
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
				string toml = component.SerializeComponent();

				_ = sb.Append(toml);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to serialize component for ModSync metadata");
				_ = sb.AppendLine($"Guid: {component.Guid}");
			}

			_ = sb.AppendLine("-->");
			_ = sb.AppendLine();
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
				if ( !componentDict.TryGetValue("ModLinkFilenames", out object modLinkFilenamesObj) &&
					!componentDict.TryGetValue("modLinkFilenames", out modLinkFilenamesObj) )
				{
					return result;
				}

				if ( modLinkFilenamesObj == null )
					return result;

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
									Debug.Assert(valueStr != null, nameof(valueStr) + " != null");
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
