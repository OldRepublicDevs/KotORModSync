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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Tomlyn;
using Tomlyn.Model;
using KOTORModSync.Core.FileSystemUtils;
using YamlSerialization = YamlDotNet.Serialization;
using static KOTORModSync.Core.Instruction;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Centralized service for loading and saving ModComponent lists from/to various formats (TOML, YAML, Markdown).
	/// </summary>
	public static class ModComponentSerializationService
	{
		#region Loading Functions
		/// <summary>
		/// Loads components from a TOML string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromTomlString([NotNull] string tomlContent)
		{
			Logger.LogVerbose($"Loading from TOML string");
			if ( tomlContent == null )
				throw new ArgumentNullException(nameof(tomlContent));
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

			// Check if "thisMod" key exists
			if ( !tomlTable.ContainsKey("thisMod") )
				throw new InvalidDataException("TOML content does not contain 'thisMod' array.");

			// Handle TomlTableArray properly
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

					// Handle flattened Options.Instructions format (new format with Parent field)
					if ( componentDict.ContainsKey("_OptionsInstructions") || componentDict.ContainsKey("Options") )
					{
						// Try to find nested Options.Instructions
						object optionsInstructionsObj = null;
						if ( componentDict.TryGetValue("_OptionsInstructions", out optionsInstructionsObj) ||
							 (componentDict.TryGetValue("Options", out var optionsObj) &&
							  optionsObj is IDictionary<string, object> optionsDict &&
							  optionsDict.TryGetValue("Instructions", out optionsInstructionsObj)) )
						{
							if ( optionsInstructionsObj is IList<object> optionInstructionsList )
							{
								// Group instructions by Parent GUID
								var instructionsByParent = new Dictionary<string, List<object>>();
								foreach ( var instrObj in optionInstructionsList )
								{
									if ( instrObj is IDictionary<string, object> instrDict &&
										 instrDict.TryGetValue("Parent", out var parentObj) )
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

								// Assign instructions to their parent options
								foreach ( var option in thisComponent.Options )
								{
									string optionGuidStr = option.Guid.ToString();
									if ( instructionsByParent.TryGetValue(optionGuidStr, out var instructions) )
									{
										// Only replace if the option doesn't already have instructions (from old format)
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
		private static readonly string[] yamlSeparator = new[] { "---" };
		private static readonly string[] newLineSeparator = new[] { "\r\n", "\r", "\n" };
		/// <summary>
		/// Loads components from a YAML string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromYamlString([NotNull] string yamlContent)
		{
			Logger.LogVerbose($"Loading from YAML string");
			if ( yamlContent == null )
				throw new ArgumentNullException(nameof(yamlContent));
			var components = new List<ModComponent>();
			// Split YAML by document separator if multiple components
			var yamlDocs = yamlContent.Split(yamlSeparator, StringSplitOptions.RemoveEmptyEntries);
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
		/// <summary>
		/// Loads components from a Markdown string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromMarkdownString([NotNull] string markdownContent)
		{
			Logger.LogVerbose($"Loading from Markdown string");
			if ( markdownContent == null )
				throw new ArgumentNullException(nameof(markdownContent));
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);
			MarkdownParserResult result = parser.Parse(markdownContent);
			if ( result.Components == null || result.Components.Count == 0 )
				throw new InvalidDataException("No valid components found in Markdown content.");
			return result.Components.ToList();
		}
		/// <summary>
		/// Loads components from a JSON string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromJsonString([NotNull] string jsonContent)
		{
			Logger.LogVerbose($"Loading from JSON string");
			if ( jsonContent == null )
				throw new ArgumentNullException(nameof(jsonContent));
			var jsonObject = JObject.Parse(jsonContent);
			// Parse metadata if present
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
		/// <summary>
		/// Loads components from an XML string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromXmlString([NotNull] string xmlContent)
		{
			Logger.LogVerbose($"Loading from XML string");
			if ( xmlContent == null )
				throw new ArgumentNullException(nameof(xmlContent));
			var doc = XDocument.Parse(xmlContent);
			var root = doc.Root;
			// Parse metadata
			var metadataElem = root?.Element("Metadata");
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
			var componentsElem = root?.Element("Components");
			if ( componentsElem != null )
			{
				foreach ( var compElem in componentsElem.Elements("Component") )
				{
					var compDict = XmlElementToDictionary(compElem);
					var component = new ModComponent();
					component.DeserializeComponent(compDict);
					components.Add(component);
				}
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in XML content.");
			return components;
		}
		private static readonly char[] iniKeyValueSeparators = new[] { '=' };
		private static readonly char[] categorySeparator = new[] { ',', ';' };

		/// <summary>
		/// Loads components from an INI string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromIniString([NotNull] string iniContent)
		{
			Logger.LogVerbose($"Loading from INI string");
			if ( iniContent == null )
				throw new ArgumentNullException(nameof(iniContent));
			var components = new List<ModComponent>();
			var lines = iniContent.Split(newLineSeparator, StringSplitOptions.RemoveEmptyEntries);
			Dictionary<string, object> currentSection = null;
			string currentSectionName = null;
			foreach ( var line in lines )
			{
				var trimmed = line.Trim();
				if ( string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#") )
					continue;
				if ( trimmed.StartsWith("[") && trimmed.EndsWith("]") )
				{
					// Save previous section if it was a component
					if ( currentSection != null && currentSectionName != null && currentSectionName.StartsWith("Component") )
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
					var parts = trimmed.Split(iniKeyValueSeparators, 2);
					if ( parts.Length == 2 )
					{
						var key = parts[0].Trim();
						var value = parts[1].Trim();
						currentSection[key] = value;
					}
				}
			}
			// Save last section
			if ( currentSection != null && currentSectionName != null && currentSectionName.StartsWith("Component") )
			{
				var component = new ModComponent();
				component.DeserializeComponent(currentSection);
				components.Add(component);
			}
			if ( components.Count == 0 )
				throw new InvalidDataException("No valid components found in INI content.");
			return components;
		}
		/// <summary>
		/// Loads components from a string, attempting TOML first, then JSON, then YAML, then XML, then Markdown.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> LoadFromString([NotNull] string content, [CanBeNull] string format = null)
		{
			Logger.LogVerbose($"Loading from string with format: {format ?? "auto-detect"}");
			if ( content == null )
				throw new ArgumentNullException(nameof(content));

			// If format is provided, dispatch directly
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

			// Auto-detect / try fallback order
			try
			{
				return LoadFromTomlString(content);
			}
			catch ( Exception tomlEx )
			{
				Logger.LogVerbose($"TOML parsing failed: {tomlEx.Message}");

				// Try Markdown next
				try
				{
					return LoadFromMarkdownString(content);
				}
				catch ( Exception mdEx )
				{
					Logger.LogVerbose($"Markdown parsing failed: {mdEx.Message}");

					// Try YAML next
					try
					{
						return LoadFromYamlString(content);
					}
					catch ( Exception yamlEx )
					{
						Logger.LogVerbose($"YAML parsing failed: {yamlEx.Message}");

						// Try TOML again (now as fallback after YAML)
						try
						{
							return LoadFromTomlString(content);
						}
						catch ( Exception tomlSecondEx )
						{
							Logger.LogVerbose($"TOML (second attempt) parsing failed: {tomlSecondEx.Message}");

							// Try JSON next
							try
							{
								return LoadFromJsonString(content);
							}
							catch ( Exception jsonEx )
							{
								Logger.LogVerbose($"JSON parsing failed: {jsonEx.Message}");

								// If all else fails, try XML last
								return LoadFromXmlString(content);
							}
						}
					}
				}
			}
		}
		/// <summary>
		/// Async version: Loads components from a TOML string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromTomlStringAsync([NotNull] string tomlContent)
		{
			return Task.Run(() => LoadFromTomlString(tomlContent));
		}
		/// <summary>
		/// Async version: Loads components from a YAML string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromYamlStringAsync([NotNull] string yamlContent)
		{
			return Task.Run(() => LoadFromYamlString(yamlContent));
		}
		/// <summary>
		/// Async version: Loads components from a Markdown string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromMarkdownStringAsync([NotNull] string markdownContent)
		{
			return Task.Run(() => LoadFromMarkdownString(markdownContent));
		}
		/// <summary>
		/// Async version: Loads components from a JSON string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromJsonStringAsync([NotNull] string jsonContent)
		{
			return Task.Run(() => LoadFromJsonString(jsonContent));
		}
		/// <summary>
		/// Async version: Loads components from an XML string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromXmlStringAsync([NotNull] string xmlContent)
		{
			return Task.Run(() => LoadFromXmlString(xmlContent));
		}
		/// <summary>
		/// Async version: Loads components from an INI string.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromIniStringAsync([NotNull] string iniContent)
		{
			return Task.Run(() => LoadFromIniString(iniContent));
		}
		/// <summary>
		/// Async version: Loads components from a string, attempting TOML first, then JSON, then YAML, then XML, then Markdown.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static Task<List<ModComponent>> LoadFromStringAsync([NotNull] string content, [CanBeNull] string format = null)
		{
			return Task.Run(() => LoadFromString(content, format));
		}
		#endregion
		#region Saving Functions
		/// <summary>
		/// Saves components to a string in the specified format.
		/// </summary>
		[NotNull]
		public static string SaveToString(
			[NotNull] List<ModComponent> components,
			[NotNull] string format = "toml"
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
					return SaveToTomlString(components);
				case "yaml":
				case "yml":
					return SaveToYamlString(components);
				case "md":
				case "markdown":
					return SaveToMarkdownString(components);
				case "json":
					return SaveToJsonString(components);
				case "xml":
					return SaveToXmlString(components);
				case "ini":
					return SaveToIniString(components);
				default:
					throw new NotSupportedException($"Unsupported format: {format}");
			}
		}
		/// <summary>
		/// Async version: Saves components to a string in the specified format.
		/// </summary>
		[NotNull]
		public static Task<string> SaveToStringAsync([NotNull] List<ModComponent> components, [NotNull] string format = "toml")
		{
			return Task.Run(() => SaveToString(components, format));
		}
		#endregion
		#region Public Helpers

		/// <summary>
		/// Parses the metadata section from a TOML table and populates MainConfig.
		/// </summary>
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
						MainConfig.FileFormatVersion = versionObj?.ToString() ?? "2.0";
					if ( metadataTable.TryGetValue("targetGame", out object gameObj) )
						MainConfig.TargetGame = gameObj?.ToString() ?? string.Empty;
					if ( metadataTable.TryGetValue("buildName", out object nameObj) )
						MainConfig.BuildName = nameObj?.ToString() ?? string.Empty;
					if ( metadataTable.TryGetValue("buildAuthor", out object authorObj) )
						MainConfig.BuildAuthor = authorObj?.ToString() ?? string.Empty;
					if ( metadataTable.TryGetValue("buildDescription", out object descObj) )
						MainConfig.BuildDescription = descObj?.ToString() ?? string.Empty;
					if ( metadataTable.TryGetValue("lastModified", out object modifiedObj) )
					{
						if ( modifiedObj != null && DateTime.TryParse(modifiedObj.ToString(), out DateTime parsedDate) )
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
					instruction.Action = Instruction.ActionType.Patcher;
					_ = Logger.LogVerboseAsync($" -- Deserialize instruction #{index + 1} action '{strAction}' -> Patcher (backward compatibility)");
				}
				else if ( Enum.TryParse(strAction, ignoreCase: true, out Instruction.ActionType action) )
				{
					instruction.Action = action;
					_ = Logger.LogVerboseAsync($" -- Deserialize instruction #{index + 1} action '{action}'");
				}
				else
				{
					_ = Logger.LogErrorAsync(
						$"{Environment.NewLine} -- Missing/invalid action for instruction #{index}"
					);
					instruction.Action = Instruction.ActionType.Unset;
				}
				instruction.Arguments = GetValueOrDefault<string>(instructionDict, key: "Arguments") ?? string.Empty;
				instruction.Overwrite = instructionDict.ContainsKey("Overwrite")
					? GetValueOrDefault<bool>(instructionDict, key: "Overwrite")
					: true;
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
				else if ( parentComponent is Option parentOpt )
					instruction.SetParentComponent(parentOpt);
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
			{
				return new System.Collections.ObjectModel.ObservableCollection<Option>();
			}
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
		internal static T GetRequiredValue<T>([NotNull] IDictionary<string, object> dict, [NotNull] string key)
		{
			T value = GetValue<T>(dict, key, required: true);
			return value == null
				? throw new InvalidOperationException("GetValue cannot return null for a required value.")
				: value;
		}

		[CanBeNull]
		internal static T GetValueOrDefault<T>([NotNull] IDictionary<string, object> dict, [NotNull] string key) =>
			GetValue<T>(dict, key, required: false);

		[CanBeNull]
		internal static T GetValue<T>([NotNull] IDictionary<string, object> dict, [NotNull] string key, bool required)
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
								if ( item is IEnumerable<object> nestedCollection && !(item is string) )
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
								}
								else if ( item is string strItem )
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
								}
								else
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
		public static string SaveToTomlString(List<ModComponent> components)
		{
			Logger.LogVerbose($"Saving to TOML string");
			var result = new StringBuilder();

			// Add metadata section
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

			// Serialize each component
			bool isFirst = true;
			foreach ( ModComponent component in components )
			{
				if ( !isFirst )
				{
					result.AppendLine();
					result.AppendLine();
				}
				isFirst = false;

				// Serialize component to dictionary
				var componentDict = new Dictionary<string, object>();

				// Add all component properties
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
				if ( !string.IsNullOrWhiteSpace(component.InstallationMethod) )
					componentDict["InstallationMethod"] = component.InstallationMethod;
				if ( !string.IsNullOrWhiteSpace(component.Directions) )
					componentDict["Directions"] = component.Directions;
				if ( !string.IsNullOrWhiteSpace(component.DownloadInstructions) )
					componentDict["DownloadInstructions"] = component.DownloadInstructions;
				if ( !string.IsNullOrWhiteSpace(component.UsageWarning) )
					componentDict["UsageWarning"] = component.UsageWarning;
				if ( !string.IsNullOrWhiteSpace(component.Screenshots) )
					componentDict["Screenshots"] = component.Screenshots;
				if ( !string.IsNullOrWhiteSpace(component.KnownBugs) )
					componentDict["KnownBugs"] = component.KnownBugs;
				if ( !string.IsNullOrWhiteSpace(component.InstallationWarning) )
					componentDict["InstallationWarning"] = component.InstallationWarning;
				if ( !string.IsNullOrWhiteSpace(component.CompatibilityWarning) )
					componentDict["CompatibilityWarning"] = component.CompatibilityWarning;
				if ( !string.IsNullOrWhiteSpace(component.SteamNotes) )
					componentDict["SteamNotes"] = component.SteamNotes;
				//if ( !string.IsNullOrWhiteSpace(component.Heading) )
				//	componentDict["Heading"] = component.Heading;
				if ( component.IsSelected )
					componentDict["IsSelected"] = component.IsSelected;
				if ( component.WidescreenOnly )
					componentDict["WidescreenOnly"] = component.WidescreenOnly;

				// Add list properties
				if ( component.Category?.Count > 0 )
					componentDict["Category"] = component.Category;
				if ( component.Language?.Count > 0 )
					componentDict["Language"] = component.Language;
				if ( component.ModLink?.Count > 0 )
					componentDict["ModLink"] = component.ModLink;
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

				// Add Instructions
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

				// Add Options
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

						// Collect option instructions with Parent field
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

				// Use FixSerializedTomlDict to handle nested structures
				var nestedContent = new StringBuilder();
				FixSerializedTomlDict(componentDict, nestedContent);

				// Wrap in thisMod and convert [thisMod] to [[thisMod]]
				var rootTable = new Dictionary<string, object>
				{
					["thisMod"] = componentDict
				};
				string componentToml = Toml.FromModel(rootTable).Replace("[thisMod]", "[[thisMod]]");
				result.Append(componentToml.TrimEnd());

				// Append nested content (Instructions, Options, Options.Instructions)
				if ( nestedContent.Length > 0 )
				{
					result.AppendLine();
					result.Append(nestedContent.ToString());
				}
			}

			return Utility.Serializer.FixWhitespaceIssues(result.ToString());
		}

		private static void FixSerializedTomlDict(
			Dictionary<string, object> serializedComponentDict,
			StringBuilder tomlString
		)
		{
			if ( serializedComponentDict == null )
				throw new ArgumentNullException(nameof(serializedComponentDict));
			if ( tomlString == null )
				throw new ArgumentNullException(nameof(tomlString));

			// Handle Options and OptionsInstructions specially - interleave them
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
					foreach ( var item in instructionsList )
					{
						if ( item == null || item.Count == 0 )
							continue;

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
					}
				}

				serializedComponentDict.Remove("Instructions");
			}

			bool hasOptions = serializedComponentDict.ContainsKey("Options");
			bool hasOptionsInstructions = serializedComponentDict.ContainsKey("OptionsInstructions");

			if ( hasOptions && hasOptionsInstructions )
			{
				var optionsList = serializedComponentDict["Options"] as List<Dictionary<string, object>>;
				var optionsInstructionsList = serializedComponentDict["OptionsInstructions"] as List<Dictionary<string, object>>;

				if ( optionsList != null && optionsInstructionsList != null )
				{
					// Group instructions by Parent GUID
					var instructionsByParent = optionsInstructionsList
						.Where(instr => instr != null && instr.ContainsKey("Parent"))
						.GroupBy(instr => instr["Parent"]?.ToString())
						.ToDictionary(g => g.Key, g => g.ToList());

					// Interleave: for each option, output option then its instructions
					for ( int i = 0; i < optionsList.Count; i++ )
					{
						Dictionary<string, object> optionDict = optionsList[i];
						if ( optionDict is null || optionDict.Count == 0 )
							continue;

						// Output the option
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

						// Output instructions for this option (if any)
						if ( optionDict.TryGetValue("Guid", out object guidObj) )
						{
							string optionGuid = guidObj?.ToString();
							if ( !string.IsNullOrEmpty(optionGuid) && instructionsByParent.TryGetValue(optionGuid, out var instructions) )
							{
								foreach ( var instruction in instructions )
								{
									if ( instruction == null || instruction.Count == 0 )
										continue;

									var instrModel = new Dictionary<string, object>
								{
									{
										"thisMod", new Dictionary<string, object>
										{
											{ "OptionsInstructions", instruction }
										}
									}
								};
									tomlString.Append(Toml.FromModel(instrModel).Replace("thisMod.OptionsInstructions", "[thisMod.OptionsInstructions]"));
								}
							}
						}
					}

					// Remove both keys since we've handled them
					serializedComponentDict.Remove("Options");
					serializedComponentDict.Remove("OptionsInstructions");
				}
			}

			// Find all other list properties that contain complex objects (not simple types like string/Guid)
			var keysCopy = serializedComponentDict.Keys.ToList();
			foreach ( string key in keysCopy )
			{
				object value = serializedComponentDict[key];

				// Check if it's a list of dictionaries (complex objects)
				List<Dictionary<string, object>> listItems = null;
				if ( value is List<Dictionary<string, object>> list )
				{
					listItems = list;
				}
				else if ( value is IEnumerable<Dictionary<string, object>> enumerable )
				{
					listItems = enumerable.ToList();
				}

				if ( listItems == null || listItems.Count == 0 )
					continue;

				// Generate [[thisMod.Key]] sections for each item in the list
				foreach ( var item in listItems )
				{
					if ( item == null || item.Count == 0 )
						continue;

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

				// Remove the key from the main dictionary since we've serialized it separately
				serializedComponentDict.Remove(key);
			}
		}
		public static string SaveToYamlString(List<ModComponent> components)
		{
			Logger.LogVerbose($"Saving to YAML string");
			var sb = new StringBuilder();
			var serializer = new YamlDotNet.Serialization.SerializerBuilder()
				.WithNamingConvention(YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention.Instance)
				.ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitNull)
				.Build();
			foreach ( ModComponent component in components )
			{
				sb.AppendLine("---");
				// Serialize to dictionary first to control what gets serialized
				var dict = new Dictionary<string, object>
				{
					{ "Guid", component.Guid },
					{ "Name", component.Name }
				};

				if ( !string.IsNullOrWhiteSpace(component.Author) )
					dict["Author"] = component.Author;
				if ( !string.IsNullOrWhiteSpace(component.Description) )
					dict["Description"] = component.Description;
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
				if ( !string.IsNullOrWhiteSpace(component.Directions) )
					dict["Directions"] = component.Directions;
				if ( !string.IsNullOrWhiteSpace(component.Screenshots) )
					dict["Screenshots"] = component.Screenshots;
				if ( component.Language?.Count > 0 )
					dict["Language"] = component.Language;
				if ( component.ModLink?.Count > 0 )
					dict["ModLink"] = component.ModLink;
				if ( component.ExcludedDownloads?.Count > 0 )
					dict["ExcludedDownloads"] = component.ExcludedDownloads;
				if ( component.Instructions?.Count > 0 )
				{
					var instructions = new List<Dictionary<string, object>>();
					foreach ( var inst in component.Instructions )
					{
						var instDict = new Dictionary<string, object>
					{
						{ "Action", inst.Action.ToString().ToLowerInvariant() }
					};

						// Conditional field serialization based on action type
						if ( inst.Source?.Count > 0 )
							instDict["Source"] = inst.Source.Count == 1 ? (object)inst.Source[0] : inst.Source;

						// Destination: Move, Patcher, Copy
						if ( !string.IsNullOrEmpty(inst.Destination) &&
							(inst.Action == Instruction.ActionType.Move ||
							 inst.Action == Instruction.ActionType.Patcher ||
							 inst.Action == Instruction.ActionType.Copy) )
							instDict["Destination"] = inst.Destination;

						// Overwrite: Move, Copy (only serialize when false, since true is default)
						if ( !inst.Overwrite &&
							(inst.Action == Instruction.ActionType.Move ||
							 inst.Action == Instruction.ActionType.Copy) )
							instDict["Overwrite"] = false;

						// Arguments: Patcher, Execute
						if ( !string.IsNullOrEmpty(inst.Arguments) &&
							(inst.Action == Instruction.ActionType.Patcher ||
							 inst.Action == Instruction.ActionType.Execute) )
							instDict["Arguments"] = inst.Arguments;

						instructions.Add(instDict);
					}
					dict["Instructions"] = instructions;
				}
				sb.AppendLine(serializer.Serialize(dict));
			}
			return sb.ToString();
		}
		public static string SaveToMarkdownString(List<ModComponent> components)
		{
			Logger.LogVerbose($"Saving to Markdown string");
			return GenerateModDocumentation(
				components,
				MainConfig.BeforeModListContent,
				MainConfig.AfterModListContent,
				MainConfig.WidescreenSectionContent,
				MainConfig.AspyrSectionContent);
		}
		public static string SaveToJsonString(List<ModComponent> components)
		{
			Logger.LogVerbose($"Saving to JSON string");
			// Build JSON structure manually to support conditional field serialization
			var jsonRoot = new JObject();

			// Metadata section
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

			// Components array
			var componentsArray = new JArray();
			foreach ( var c in components )
			{
				var componentObj = new JObject
				{
					["guid"] = c.Guid.ToString(),
					["name"] = c.Name
				};

				if ( !string.IsNullOrWhiteSpace(c.Author) ) componentObj["author"] = c.Author;
				if ( !string.IsNullOrWhiteSpace(c.Description) ) componentObj["description"] = c.Description;
				if ( c.Category?.Count > 0 ) componentObj["category"] = JArray.FromObject(c.Category);
				if ( !string.IsNullOrWhiteSpace(c.Tier) ) componentObj["tier"] = c.Tier;
				if ( c.Language?.Count > 0 ) componentObj["language"] = JArray.FromObject(c.Language);
				if ( c.ModLink?.Count > 0 ) componentObj["modLink"] = JArray.FromObject(c.ModLink);
				if ( c.ExcludedDownloads?.Count > 0 ) componentObj["excludedDownloads"] = JArray.FromObject(c.ExcludedDownloads);
				if ( !string.IsNullOrWhiteSpace(c.InstallationMethod) ) componentObj["installationMethod"] = c.InstallationMethod;
				if ( !string.IsNullOrWhiteSpace(c.Directions) ) componentObj["directions"] = c.Directions;
				if ( !string.IsNullOrWhiteSpace(c.DownloadInstructions) ) componentObj["downloadInstructions"] = c.DownloadInstructions;
				if ( !string.IsNullOrWhiteSpace(c.UsageWarning) ) componentObj["usageWarning"] = c.UsageWarning;
				if ( !string.IsNullOrWhiteSpace(c.Screenshots) ) componentObj["screenshots"] = c.Screenshots;
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

				// Instructions with conditional serialization
				if ( c.Instructions?.Count > 0 )
				{
					var instructionsArray = new JArray();
					foreach ( var i in c.Instructions )
					{
						var instrObj = new JObject
						{
							["guid"] = i.Guid,
							["action"] = i.ActionString
						};

						if ( i.Source?.Count > 0 )
							instrObj["source"] = JArray.FromObject(i.Source);

						// Conditional fields based on action type
						if ( !string.IsNullOrEmpty(i.Destination) &&
							(i.Action == Instruction.ActionType.Move ||
							 i.Action == Instruction.ActionType.Patcher ||
							 i.Action == Instruction.ActionType.Copy) )
							instrObj["destination"] = i.Destination;

						if ( !string.IsNullOrEmpty(i.Arguments) &&
							(i.Action == Instruction.ActionType.Patcher ||
							 i.Action == Instruction.ActionType.Execute) )
							instrObj["arguments"] = i.Arguments;

						if ( !i.Overwrite &&
							(i.Action == Instruction.ActionType.Move ||
							 i.Action == Instruction.ActionType.Copy) )
							instrObj["overwrite"] = false;

						if ( i.Dependencies?.Count > 0 )
							instrObj["dependencies"] = JArray.FromObject(i.Dependencies);
						if ( i.Restrictions?.Count > 0 )
							instrObj["restrictions"] = JArray.FromObject(i.Restrictions);

						instructionsArray.Add(instrObj);
					}
					componentObj["instructions"] = instructionsArray;
				}

				// Options
				if ( c.Options?.Count > 0 )
				{
					var optionsArray = new JArray();
					foreach ( var o in c.Options )
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
							foreach ( var i in o.Instructions )
							{
								var instrObj = new JObject
								{
									["guid"] = i.Guid,
									["action"] = i.ActionString
								};

								if ( i.Source?.Count > 0 )
									instrObj["source"] = JArray.FromObject(i.Source);

								// Conditional fields based on action type
								if ( !string.IsNullOrEmpty(i.Destination) &&
									(i.Action == Instruction.ActionType.Move ||
									 i.Action == Instruction.ActionType.Patcher ||
									 i.Action == Instruction.ActionType.Copy) )
									instrObj["destination"] = i.Destination;

								if ( !string.IsNullOrEmpty(i.Arguments) &&
									(i.Action == Instruction.ActionType.Patcher ||
									 i.Action == Instruction.ActionType.Execute) )
									instrObj["arguments"] = i.Arguments;

								if ( !i.Overwrite &&
									(i.Action == Instruction.ActionType.Move ||
									 i.Action == Instruction.ActionType.Copy) )
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

			return jsonRoot.ToString(Newtonsoft.Json.Formatting.Indented);
		}
		public static string SaveToXmlString(List<ModComponent> components)
		{
			Logger.LogVerbose($"Saving to XML string");
			var doc = new XDocument(
				new XDeclaration("2.0", "utf-8", "yes"),
				new XElement("ModBuild",
					new XElement("Metadata",
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
					),
					new XElement("Components",
						components.Select(c => new XElement("Component",
							new XElement("Guid", c.Guid.ToString()),
							new XElement("Name", c.Name),
							string.IsNullOrWhiteSpace(c.Author)
								? null
								: new XElement("Author", c.Author),
							string.IsNullOrWhiteSpace(c.Description)
								? null
								: new XElement("Description", c.Description),
							(c.Category?.Count > 0)
								? new XElement("Category", c.Category.Select(cat => new XElement("Item", cat)))
								: null,
							string.IsNullOrWhiteSpace(c.Tier) ? null : new XElement("Tier", c.Tier),
							(c.Language?.Count > 0)
								? new XElement("Language", c.Language.Select(lang => new XElement("Item", lang)))
								: null,
							(c.ModLink?.Count > 0)
								? new XElement("ModLink", c.ModLink.Select(link => new XElement("Item", link)))
								: null,
							(c.ExcludedDownloads?.Count > 0)
								? new XElement("ExcludedDownloads", c.ExcludedDownloads.Select(file => new XElement("Item", file)))
								: null,
							string.IsNullOrWhiteSpace(c.InstallationMethod)
								? null
								: new XElement("InstallationMethod", c.InstallationMethod),
							string.IsNullOrWhiteSpace(c.Directions)
								? null
								: new XElement("Directions", c.Directions),
							string.IsNullOrWhiteSpace(c.DownloadInstructions)
								? null
								: new XElement("DownloadInstructions", c.DownloadInstructions),
							(c.Dependencies?.Count > 0)
								? new XElement("Dependencies", c.Dependencies.Select(dep => new XElement("Item", dep)))
								: null,
							(c.Restrictions?.Count > 0)
								? new XElement("Restrictions", c.Restrictions.Select(res => new XElement("Item", res)))
								: null,
							(c.InstallBefore?.Count > 0)
								? new XElement("InstallBefore", c.InstallBefore.Select(ib => new XElement("Item", ib)))
								: null,
							(c.InstallAfter?.Count > 0)
								? new XElement("InstallAfter", c.InstallAfter.Select(ia => new XElement("Item", ia)))
								: null,
							c.WidescreenOnly
								? new XElement("WidescreenOnly", c.WidescreenOnly)
								: null,
							(c.Instructions?.Count > 0)
								? new XElement("Instructions",
									c.Instructions.Select(instr => new XElement("Instruction",
										new XElement("Guid", instr.Guid.ToString()),
										new XElement("Action", instr.ActionString),
										(instr.Source?.Count > 0)
											? new XElement("Source", instr.Source.Select(s => new XElement("Item", s)))
											: null,
										string.IsNullOrWhiteSpace(instr.Destination)
											? null
											: new XElement("Destination", instr.Destination),
									string.IsNullOrWhiteSpace(instr.Arguments)
										? null
										: new XElement("Arguments", instr.Arguments),
									!instr.Overwrite
										? new XElement("Overwrite", false)
										: null
								))
							)
							: null,
							(c.Options?.Count > 0)
								? new XElement("Options",
									c.Options.Select(opt => new XElement("Option",
										new XElement("Guid", opt.Guid.ToString()),
										string.IsNullOrWhiteSpace(opt.Name)
											? null
											: new XElement("Name", opt.Name),
										string.IsNullOrWhiteSpace(opt.Description)
											? null
											: new XElement("Description", opt.Description),
										(opt.Instructions?.Count > 0)
											? new XElement("Instructions",
												opt.Instructions.Select(instr => new XElement("Instruction",
													new XElement("Guid", instr.Guid.ToString()),
													string.IsNullOrWhiteSpace(instr.ActionString)
														? null
														: new XElement("Action", instr.ActionString),
													(instr.Source?.Count > 0)
														? new XElement("Source", instr.Source.Select(s => new XElement("Item", s)))
														: null,
													string.IsNullOrWhiteSpace(instr.Destination)
														? null
														: new XElement("Destination", instr.Destination),
													string.IsNullOrWhiteSpace(instr.Arguments)
														? null
														: new XElement("Arguments", instr.Arguments),
													!instr.Overwrite
														? new XElement("Overwrite", false)
														: null
											))
										)
										: null
								))
							)
							: null
						))
					)
				)
			);
			var sb = new StringBuilder();
			using ( var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }) )
			{
				doc.Save(writer);
			}
			return sb.ToString();
		}
		public static string SaveToIniString(List<ModComponent> components)
		{
			var sb = new StringBuilder();
			// Metadata section
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
			// Components
			for ( int i = 0; i < components.Count; i++ )
			{
				var c = components[i];
				sb.AppendLine($"[Component{i + 1}]");
				sb.AppendLine($"Guid={c.Guid}");
				sb.AppendLine($"Name={c.Name}");
				if ( !string.IsNullOrWhiteSpace(c.Author) )
					sb.AppendLine($"Author={c.Author}");
				if ( !string.IsNullOrWhiteSpace(c.Description) )
					sb.AppendLine($"Description={c.Description}");
				if ( c.Category?.Count > 0 )
					sb.AppendLine($"Category={string.Join(",", c.Category)}");
				if ( !string.IsNullOrWhiteSpace(c.Tier) )
					sb.AppendLine($"Tier={c.Tier}");
				if ( c.Language?.Count > 0 )
					sb.AppendLine($"Language={string.Join(",", c.Language)}");
				if ( c.ModLink?.Count > 0 )
					sb.AppendLine($"ModLink={string.Join("|", c.ModLink)}");
				if ( c.ExcludedDownloads?.Count > 0 )
					sb.AppendLine($"ExcludedDownloads={string.Join("|", c.ExcludedDownloads)}");
				if ( !string.IsNullOrWhiteSpace(c.InstallationMethod) )
					sb.AppendLine($"InstallationMethod={c.InstallationMethod}");
				if ( !string.IsNullOrWhiteSpace(c.Directions) )
					sb.AppendLine($"Directions={c.Directions.Replace("\r\n", "\\n").Replace("\n", "\\n")}");
				if ( c.Dependencies?.Count > 0 )
					sb.AppendLine($"Dependencies={string.Join(",", c.Dependencies)}");
				if ( c.Restrictions?.Count > 0 )
					sb.AppendLine($"Restrictions={string.Join(",", c.Restrictions)}");
				if ( c.WidescreenOnly )
					sb.AppendLine($"WidescreenOnly=true");
				sb.AppendLine();
			}
			return sb.ToString();
		}
		/// <summary>
		/// Helper to convert XElement to Dictionary for deserialization.
		/// </summary>
		public static Dictionary<string, object> XmlElementToDictionary(XElement element)
		{
			var dict = new Dictionary<string, object>();
			foreach ( var child in element.Elements() )
			{
				var childName = child.Name.LocalName;
				// Handle list elements
				if ( child.Elements("Item").Any() )
				{
					var list = child.Elements("Item").Select(item => (object)item.Value).ToList();
					dict[childName] = list;
				}
				// Handle nested objects
				else if ( child.HasElements && !child.Elements("Item").Any() )
				{
					// Check if it's a list of complex objects (like Instructions, Options)
					if ( child.Elements().All(e => e.Name.LocalName == child.Elements().First().Name.LocalName) )
					{
						var list = child.Elements().Select(e => (object)XmlElementToDictionary(e)).ToList();
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

		/// <summary>
		/// Generates markdown documentation from a list of mod components.
		/// This is the source of truth for documentation generation.
		/// </summary>
		[NotNull]
		public static string GenerateModDocumentation(
			[NotNull][ItemNotNull] List<ModComponent> componentsList,
			[CanBeNull] string beforeModListContent = null,
			[CanBeNull] string afterModListContent = null,
			[CanBeNull] string widescreenSectionContent = null,
			[CanBeNull] string aspyrSectionContent = null)
		{
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			var sb = new StringBuilder();

			// Add before content
			if ( !string.IsNullOrWhiteSpace(beforeModListContent) )
			{
				_ = sb.Append(beforeModListContent);
				if ( !beforeModListContent.EndsWith("\n") )
				{
					_ = sb.AppendLine();
				}
				_ = sb.AppendLine();
			}

			// Add mod list header
			_ = sb.AppendLine("## Mod List");

			// Create GUID to name mapping for dependency resolution
			var guidToName = componentsList.ToDictionary(c => c.Guid, c => c.Name);

			bool widescreenHeaderWritten = false;
			bool aspyrHeaderWritten = false;

			for ( int i = 0; i < componentsList.Count; i++ )
			{
				ModComponent component = componentsList[i];

				// Add Aspyr section header if needed
				if ( component.AspyrExclusive == true && !aspyrHeaderWritten && !string.IsNullOrWhiteSpace(aspyrSectionContent) )
				{
					_ = sb.AppendLine();
					_ = sb.AppendLine(aspyrSectionContent.TrimEnd());
					_ = sb.AppendLine();
					aspyrHeaderWritten = true;
				}

				// Add widescreen section header if needed
				if ( component.WidescreenOnly && !widescreenHeaderWritten && !string.IsNullOrWhiteSpace(widescreenSectionContent) )
				{
					_ = sb.AppendLine();
					_ = sb.AppendLine(widescreenSectionContent.TrimEnd());
					_ = sb.AppendLine();
					widescreenHeaderWritten = true;
				}

				// Add separator between components (but not before first)
				if ( i > 0 )
				{
					_ = sb.AppendLine("___");
					_ = sb.AppendLine();
				}
				else
				{
					_ = sb.AppendLine();
				}

				// Component heading
				string heading = !string.IsNullOrWhiteSpace(component.Heading) ? component.Heading : component.Name;
				_ = sb.Append("### ").AppendLine(heading);
				_ = sb.AppendLine();

				// Name field with links
				if ( !string.IsNullOrWhiteSpace(component.NameFieldContent) )
				{
					_ = sb.Append("**Name:** ").AppendLine(component.NameFieldContent);
				}
				else if ( component.ModLink?.Count > 0 && !string.IsNullOrWhiteSpace(component.ModLink[0]) )
				{
					_ = sb.Append("**Name:** [").Append(component.Name).Append("](")
						.Append(component.ModLink[0]).Append(")");

					// Additional patch links
					for ( int linkIdx = 1; linkIdx < component.ModLink.Count; linkIdx++ )
					{
						if ( !string.IsNullOrWhiteSpace(component.ModLink[linkIdx]) )
						{
							_ = sb.Append(" and [**Patch**](").Append(component.ModLink[linkIdx]).Append(")");
						}
					}

					_ = sb.AppendLine();
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
						// Oxford comma for 3+ categories
						var allButLast = component.Category.Take(component.Category.Count - 1);
						var last = component.Category[component.Category.Count - 1];
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

				// Output Masters/Dependencies if present
				// Always use Dependencies (GUIDs) as source of truth and map to original names
				if ( component.Dependencies?.Count > 0 )
				{
					// Map each GUID to its original name (if available) or resolved name
					var masterNames = component.Dependencies
						.Select(guid =>
						{
							if ( component.DependencyGuidToOriginalName.TryGetValue(guid, out string originalName) )
								return originalName;
							if ( guidToName.ContainsKey(guid) )
								return guidToName[guid];
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
				{
					GenerateModSyncMetadata(sb, component);
				}
			}

			// Add after content
			if ( !string.IsNullOrWhiteSpace(afterModListContent) )
			{
				_ = sb.AppendLine();
				_ = sb.Append(afterModListContent);
			}

			return sb.ToString();
		}

		private static void GenerateModSyncMetadata([NotNull] StringBuilder sb, [NotNull] ModComponent component)
		{
			// Skip if no instructions or options
			if ( component.Instructions.Count == 0 && component.Options.Count == 0 )
				return;

			_ = sb.AppendLine("<!--<<ModSync>>");

			try
			{
				// Serialize component to TOML
				string toml = component.SerializeComponent();

				_ = sb.Append(toml);
			}
			catch ( Exception ex )
			{
				// Fallback to minimal metadata on serialization error
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
				string.Equals(lang, b: "UNKNOWN", StringComparison.OrdinalIgnoreCase)) )
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

			// Handle single language with YES/NO/PARTIAL prefix or ONLY suffix
			if ( languages.Count == 1 )
			{
				string singleLang = languages[0];
				if ( !string.IsNullOrEmpty(singleLang) )
				{
					string trimmed = singleLang.TrimStart();
					if ( trimmed.StartsWith("YES", StringComparison.OrdinalIgnoreCase) ||
						 trimmed.StartsWith("NO", StringComparison.OrdinalIgnoreCase) ||
						 trimmed.StartsWith("PARTIAL", StringComparison.OrdinalIgnoreCase) ||
						 trimmed.IndexOf("ONLY", StringComparison.OrdinalIgnoreCase) >= 0 )
					{
						return singleLang;
					}
				}
			}

			return "Supported languages: " + string.Join(", ", languages);
		}
		#endregion
	}
}
