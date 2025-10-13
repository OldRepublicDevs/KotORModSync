



using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;
using YamlSerialization = YamlDotNet.Serialization;

namespace KOTORModSync.Core
{
	public class ModComponent : INotifyPropertyChanged
	{
		public enum InstallExitCode
		{
			[Description("Completed Successfully")]
			Success,

			[Description("A dependency or restriction violation between components has occurred.")]
			DependencyViolation,

			[Description("User cancelled the installation.")]
			UserCancelledInstall,

			[Description("An invalid operation was attempted.")]
			InvalidOperation,
			UnknownError,
		}

		public enum ComponentInstallState
		{
			Pending,
			Running,
			Completed,
			Failed,
			Blocked,
			Skipped,
		}

		public enum InstructionInstallState
		{
			Pending,
			Completed,
			Failed,
		}

		[JsonObject(MemberSerialization.OptIn)]
		public sealed class InstructionCheckpoint
		{
			[JsonProperty]
			public Guid InstructionId { get; set; }

			[JsonProperty]
			public int InstructionIndex { get; set; }

			[JsonProperty]
			public InstructionInstallState State { get; set; } = InstructionInstallState.Pending;

			[JsonProperty]
			public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

			public InstructionCheckpoint Clone() => new InstructionCheckpoint
			{
				InstructionId = InstructionId,
				InstructionIndex = InstructionIndex,
				State = State,
				LastUpdatedUtc = LastUpdatedUtc,
			};
		}

		[JsonObject(MemberSerialization.OptIn)]
		private sealed class ComponentCheckpoint
		{
			[JsonProperty]
			public Guid ComponentId { get; set; }

			[JsonProperty]
			public ComponentInstallState State { get; set; } = ComponentInstallState.Pending;

			[JsonProperty]
			public DateTimeOffset? LastStartedUtc { get; set; }

			[JsonProperty]
			public DateTimeOffset? LastCompletedUtc { get; set; }

			[JsonProperty]
			public string BackupDirectory { get; set; }

			[JsonProperty]
			public List<InstructionCheckpoint> Instructions { get; set; } = new List<InstructionCheckpoint>();

			[JsonProperty]
			public Guid InstallSessionId { get; set; } = Guid.Empty;

			[JsonProperty]
			public string CheckpointVersion { get; set; } = "1.0";
		}

		private void PersistCheckpointInternal()
		{
			try
			{
				EnsureCheckpointLoaded();
				_currentCheckpoint.ComponentId = Guid;
				_currentCheckpoint.State = InstallState;
				_currentCheckpoint.LastStartedUtc = LastStartedUtc;
				_currentCheckpoint.LastCompletedUtc = LastCompletedUtc;
				_currentCheckpoint.BackupDirectory = _lastKnownBackupDirectory;
				_currentCheckpoint.Instructions = _instructionCheckpoints.Select(c => c.Clone()).ToList();
				_currentCheckpoint.InstallSessionId = EnsureInstallSessionId();
				SaveCheckpointToDisk(_currentCheckpoint);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to persist checkpoint state");
			}
		}

		internal void PersistCheckpoint() => PersistCheckpointInternal();

		private void EnsureCheckpointLoaded()
		{
			if ( _checkpointLoaded )
				return;

			_checkpointLoaded = true;

			try
			{
				string checkpointPath = GetComponentCheckpointFilePath();
				if ( File.Exists(checkpointPath) )
				{
					string json = File.ReadAllText(checkpointPath);
					ComponentCheckpoint checkpoint = JsonConvert.DeserializeObject<ComponentCheckpoint>(json);
					if ( checkpoint != null && checkpoint.ComponentId == Guid )
					{
						_currentCheckpoint = checkpoint;
						InstallState = checkpoint.State;
						LastStartedUtc = checkpoint.LastStartedUtc;
						LastCompletedUtc = checkpoint.LastCompletedUtc;
						_lastKnownBackupDirectory = checkpoint.BackupDirectory;
						_instructionCheckpoints.Clear();
						_instructionCheckpoints.AddRange(checkpoint.Instructions.Select(c => c.Clone()));
						_installSessionId = checkpoint.InstallSessionId;
						return;
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to load checkpoint state");
			}

			_currentCheckpoint = new ComponentCheckpoint
			{
				ComponentId = Guid,
				State = InstallState,
				LastStartedUtc = LastStartedUtc,
				LastCompletedUtc = LastCompletedUtc,
				BackupDirectory = _lastKnownBackupDirectory,
				Instructions = _instructionCheckpoints.Select(c => c.Clone()).ToList(),
				InstallSessionId = EnsureInstallSessionId(),
			};
		}

		private Guid EnsureInstallSessionId()
		{
			if ( _installSessionId == Guid.Empty )
				_installSessionId = Guid.NewGuid();
			return _installSessionId;
		}

		private static string GetComponentCheckpointFilePath()
		{
			DirectoryInfo destination = MainConfig.DestinationPath;
			if ( destination == null )
				throw new InvalidOperationException("DestinationPath must be set before installing components.");

			string hiddenDirectory = Path.Combine(destination.FullName, CheckpointFolderName);
			_ = Directory.CreateDirectory(hiddenDirectory);
			return Path.Combine(hiddenDirectory, CheckpointFileName);
		}

		private static void SaveCheckpointToDisk(ComponentCheckpoint checkpoint)
		{
			string path = GetComponentCheckpointFilePath();
			string json = JsonConvert.SerializeObject(checkpoint, s_checkpointSerializerSettings);
			File.WriteAllText(path, json);
		}

		[NotNull] private string _author = string.Empty;

		[NotNull] private List<string> _category = new List<string>();

		[NotNull] private List<Guid> _dependencies = new List<Guid>();

		[NotNull] private string _description = string.Empty;

		[NotNull] private string _directions = string.Empty;

		private Guid _guid;

		[NotNull] private List<Guid> _installAfter = new List<Guid>();

		[NotNull] private string _installationMethod = string.Empty;

		[NotNull] private List<Guid> _installBefore = new List<Guid>();

		[NotNull]
		[ItemNotNull]
		private ObservableCollection<Instruction> _instructions = new ObservableCollection<Instruction>();

		private bool _isSelected;
		private ComponentInstallState _installState = ComponentInstallState.Pending;
		private DateTimeOffset? _lastStartedUtc;
		private DateTimeOffset? _lastCompletedUtc;
		private readonly List<InstructionCheckpoint> _instructionCheckpoints = new List<InstructionCheckpoint>();
		private string _lastKnownBackupDirectory;
		private ComponentCheckpoint _currentCheckpoint;
		private static readonly SemaphoreSlim s_checkpointSemaphore = new SemaphoreSlim(1, 1);
		private const string CheckpointFileName = "install_state.json";
		public const string CheckpointFolderName = ".kotor_modsync";
		private static readonly JsonSerializerSettings s_checkpointSerializerSettings = new JsonSerializerSettings
		{
			Formatting = Formatting.Indented,
			Converters = { new StringEnumConverter() },
		};
		private bool _checkpointLoaded;
		private Guid _installSessionId;

		[NotNull][ItemNotNull] private List<string> _language = new List<string>();

		[NotNull] private List<string> _modLink = new List<string>();

		[NotNull] private string _name = string.Empty;

		[NotNull] private ObservableCollection<Option> _options = new ObservableCollection<Option>();

		[NotNull] private List<Guid> _restrictions = new List<Guid>();

		[NotNull] private string _tier = string.Empty;

		private bool _isDownloaded;
		private bool _isValidating;

		public Guid Guid
		{
			get => _guid;
			set
			{
				_guid = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string Name
		{
			get => _name;
			set
			{
				_name = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string Author
		{
			get => _author;
			set
			{
				_author = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public List<string> Category
		{
			get => _category;
			set
			{
				_category = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string Tier
		{
			get => _tier;
			set
			{
				
				_tier = CategoryTierDefinitions.NormalizeTier(value);
				OnPropertyChanged();
			}
		}

		[NotNull]
		[ItemNotNull]
		public List<string> Language
		{
			get => _language;
			set
			{
				_language = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public List<string> ModLink
		{
			get => _modLink;
			set
			{
				_modLink = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string Description
		{
			get => _description;
			set
			{
				_description = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string InstallationMethod
		{
			get => _installationMethod;
			set
			{
				_installationMethod = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string Directions
		{
			get => _directions;
			set
			{
				_directions = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public List<Guid> Dependencies
		{
			get => _dependencies;
			set
			{
				_dependencies = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public List<Guid> Restrictions
		{
			get => _restrictions;
			set
			{
				_restrictions = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public List<Guid> InstallBefore
		{
			get => _installBefore;
			set
			{
				_installBefore = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public List<Guid> InstallAfter
		{
			get => _installAfter;
			set
			{
				_installAfter = value;
				OnPropertyChanged();
			}
		}

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				_isSelected = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		[ItemNotNull]
		public ObservableCollection<Instruction> Instructions
		{
			get => _instructions;
			set
			{
				if ( _instructions != value )
				{
					_instructions = value;
					OnPropertyChanged();
				}
			}
		}

		[JsonIgnore]
		public ComponentInstallState InstallState
		{
			get => _installState;
			set
			{
				if ( _installState == value )
					return;

				_installState = value;
				OnPropertyChanged();
			}
		}

		[JsonIgnore]
		[CanBeNull]
		public DateTimeOffset? LastStartedUtc
		{
			get => _lastStartedUtc;
			set
			{
				if ( _lastStartedUtc == value )
					return;

				_lastStartedUtc = value;
				OnPropertyChanged();
			}
		}

		[JsonIgnore]
		[CanBeNull]
		public DateTimeOffset? LastCompletedUtc
		{
			get => _lastCompletedUtc;
			set
			{
				if ( _lastCompletedUtc == value )
					return;

				_lastCompletedUtc = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		internal IReadOnlyList<InstructionCheckpoint> InstructionCheckpoints => _instructionCheckpoints;

		internal void ReplaceInstructionCheckpoints(IEnumerable<InstructionCheckpoint> checkpoints)
		{
			_instructionCheckpoints.Clear();
			if ( checkpoints != null )
				_instructionCheckpoints.AddRange(checkpoints.Select(c => c.Clone()));
		}

		[NotNull]
		public ObservableCollection<Option> Options
		{
			get => _options;
			set
			{
				if ( _options == value )
					return;

				_options.CollectionChanged -= OptionsCollectionChanged;
				_options = value;
				_options.CollectionChanged += OptionsCollectionChanged;

				OnPropertyChanged();
			}
		}

		[JsonIgnore]
		public bool IsDownloaded
		{
			get => _isDownloaded;
			set
			{
				if ( _isDownloaded == value )
					return;
				_isDownloaded = value;
				OnPropertyChanged();
			}
		}

		[JsonIgnore]
		public bool IsValidating
		{
			get => _isValidating;
			set
			{
				if ( _isValidating == value )
					return;
				_isValidating = value;
				OnPropertyChanged();
			}
		}

		private static readonly string[] s_separator = {
		"\r\n", "\n",
	};
		private static readonly string[] s_categorySeparator = { ",", ";" };

		
		public event PropertyChangedEventHandler PropertyChanged;
		private void OptionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			
			
			OnPropertyChanged(nameof(Options));
		}

		private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		[NotNull]
		public string SerializeComponent()
		{
			try
			{
				
				var serializableData = new SerializableComponentData
				{
					Guid = Guid,
					Instructions = Instructions.Count > 0
						? Instructions.Select(i => new SerializableInstruction
						{
							Guid = i.Guid,
							Action = i.Action != Instruction.ActionType.Unset ? i.Action.ToString() : null,
							Source = i.Source?.Count > 0 ? i.Source : null,
							Destination = !string.IsNullOrWhiteSpace(i.Destination) ? i.Destination : null,
							Overwrite = i.Overwrite ? (bool?)true : null,
						}).ToList()
						: null,
					Options = Options.Count > 0
						? Options.Select(o => new SerializableOption
						{
							Guid = o.Guid,
							Name = !string.IsNullOrWhiteSpace(o.Name) ? o.Name : null,
							Description = !string.IsNullOrWhiteSpace(o.Description) ? o.Description : null,
							IsSelected = o.IsSelected ? (bool?)true : null,
							Restrictions = o.Restrictions?.Count > 0 ? o.Restrictions : null,
							Instructions = o.Instructions.Count > 0
								? o.Instructions.Select(i => new SerializableInstruction
								{
									Guid = i.Guid,
									Action = i.Action != Instruction.ActionType.Unset ? i.Action.ToString() : null,
									Source = i.Source?.Count > 0 ? i.Source : null,
									Destination = !string.IsNullOrWhiteSpace(i.Destination) ? i.Destination : null,
									Overwrite = i.Overwrite ? (bool?)true : null,
								}).ToList()
								: null,
						}).ToList()
						: null,
				};

				
				Dictionary<string, object> dictionary = Serializer.SerializeIntoDictionary(serializableData);
				string tomlString = TomlWriter.WriteString(dictionary);

				if ( string.IsNullOrWhiteSpace(tomlString) )
					throw new InvalidOperationException("Could not serialize component to TOML");

				return tomlString;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to serialize component to TOML");
				throw;
			}
		}

		
		private class SerializableComponentData
		{
			public Guid Guid { get; set; }
			public List<SerializableInstruction> Instructions { get; set; }
			public List<SerializableOption> Options { get; set; }
		}

		private class SerializableInstruction
		{
			public Guid Guid { get; set; }
			public string Action { get; set; }
			public List<string> Source { get; set; }
			public string Destination { get; set; }
			public bool? Overwrite { get; set; }
		}

		private class SerializableOption
		{
			public Guid Guid { get; set; }
			public string Name { get; set; }
			public string Description { get; set; }
			public bool? IsSelected { get; set; }
			public List<Guid> Restrictions { get; set; }
			public List<SerializableInstruction> Instructions { get; set; }
		}


		private void DeserializeComponent([NotNull] IDictionary<string, object> componentDict)
		{
			Guid = GetRequiredValue<Guid>(componentDict, key: "Guid");
			Name = GetRequiredValue<string>(componentDict, key: "Name");
			_ = Logger.LogAsync($" == Deserialize next component '{Name}' ==");
			Author = GetValueOrDefault<string>(componentDict, key: "Author") ?? string.Empty;

			
			Category = GetValueOrDefault<List<string>>(componentDict, key: "Category") ?? new List<string>();
			if ( Category.Count == 0 )
			{
				
				string categoryStr = GetValueOrDefault<string>(componentDict, key: "Category") ?? string.Empty;
				if ( !string.IsNullOrEmpty(categoryStr) )
				{
					
					
					Category = categoryStr.Split(
						s_categorySeparator,
						StringSplitOptions.RemoveEmptyEntries
					).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
				}
			}
			else if ( Category.Count == 1 )
			{
				
				
				string singleCategory = Category[0];
				if ( !string.IsNullOrEmpty(singleCategory) &&
					 (singleCategory.Contains(',') || singleCategory.Contains(';')) )
				{
					
					Category = singleCategory.Split(
						s_categorySeparator,
						StringSplitOptions.RemoveEmptyEntries
					).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
				}
				else if ( string.IsNullOrWhiteSpace(singleCategory) )
				{
					
					Category = new List<string>();
				}
			}

			Tier = GetValueOrDefault<string>(componentDict, key: "Tier") ?? string.Empty;
			Description = GetValueOrDefault<string>(componentDict, key: "Description") ?? string.Empty;
			Directions = GetValueOrDefault<string>(componentDict, key: "Directions") ?? string.Empty;
			Language = GetValueOrDefault<List<string>>(componentDict, key: "Language") ?? new List<string>();
			ModLink = GetValueOrDefault<List<string>>(componentDict, key: "ModLink") ?? new List<string>();
			if ( ModLink.IsNullOrEmptyCollection() )
			{
				string modLink = GetValueOrDefault<string>(componentDict, key: "ModLink") ?? string.Empty;
				if ( string.IsNullOrEmpty(modLink) )
				{
					Logger.LogError("Could not deserialize key 'ModLink'");
				}
				else
				{
					ModLink = modLink.Split(
						s_separator,
						StringSplitOptions.None
					).ToList();
				}
			}

			Dependencies = GetValueOrDefault<List<Guid>>(componentDict, key: "Dependencies") ?? new List<Guid>();
			Restrictions = GetValueOrDefault<List<Guid>>(componentDict, key: "Restrictions") ?? new List<Guid>();
			InstallBefore = GetValueOrDefault<List<Guid>>(componentDict, key: "InstallBefore") ?? new List<Guid>();
			InstallAfter = GetValueOrDefault<List<Guid>>(componentDict, key: "InstallAfter") ?? new List<Guid>();

			IsSelected = GetValueOrDefault<bool>(componentDict, key: "IsSelected");

			Instructions = DeserializeInstructions(
				GetValueOrDefault<IList<object>>(componentDict, key: "Instructions"), this
			);
			Options = DeserializeOptions(GetValueOrDefault<IList<object>>(componentDict, key: "Options"));

			
			_ = Logger.LogAsync($"Successfully deserialized component '{Name}'");
		}

		public static void OutputConfigFile(
			[ItemNotNull][NotNull] IEnumerable<ModComponent> components,
			[NotNull] string filePath
		)
		{
			if ( components is null )
				throw new ArgumentNullException(nameof(components));
			if ( filePath is null )
				throw new ArgumentNullException(nameof(filePath));

			var stringBuilder = new StringBuilder();

			foreach ( ModComponent thisComponent in components )
			{
				_ = stringBuilder.AppendLine("---"); 
				_ = stringBuilder.AppendLine(thisComponent.SerializeComponent());
			}

			string yamlString = stringBuilder.ToString();
			if ( MainConfig.CaseInsensitivePathing )
				filePath = PathHelper.GetCaseSensitivePath(filePath, isFile: true).Item1;
			File.WriteAllText(filePath, yamlString);
		}

		[NotNull]
		public static string GenerateModDocumentation([NotNull][ItemNotNull] List<ModComponent> componentsList)
		{
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			var sb = new StringBuilder();

			
			for ( int i = 0; i < componentsList.Count; i++ )
			{
				ModComponent component = componentsList[i];

				
				_ = sb.AppendLine();
				_ = sb.AppendLine("___");
				_ = sb.AppendLine();

				
				_ = sb.Append("### ").AppendLine(component.Name);
				_ = sb.AppendLine();

				
				if ( component.ModLink?.Count > 0 && !string.IsNullOrWhiteSpace(component.ModLink[0]) )
				{
					_ = sb.Append("**Name:** [").Append(component.Name).Append("](")
						.Append(component.ModLink[0]).AppendLine(")");
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

				
				string categoryStr = component.Category?.Count > 0
					? string.Join(" & ", component.Category)
					: "Uncategorized";
				string tierStr = !string.IsNullOrWhiteSpace(component.Tier) ? component.Tier : "Unspecified";
				_ = sb.Append("**Category & Tier:** ").Append(categoryStr).Append(" / ").AppendLine(tierStr);
				_ = sb.AppendLine();

				
				string languageSupport = GetNonEnglishFunctionalityText(component.Language);
				_ = sb.Append("**Non-English Functionality:** ").AppendLine(languageSupport);
				_ = sb.AppendLine();

				
				if ( !string.IsNullOrWhiteSpace(component.InstallationMethod) )
				{
					_ = sb.Append("**Installation Method:** ").AppendLine(component.InstallationMethod);
					_ = sb.AppendLine();
				}

				
				if ( !string.IsNullOrWhiteSpace(component.Directions) )
				{
					_ = sb.Append("**Installation Instructions:** ").AppendLine(component.Directions);
					_ = sb.AppendLine();
				}

				
				if ( component.Instructions.Count > 0 || component.Options.Count > 0 )
				{
					GenerateModSyncMetadata(sb, component);
				}
			}

			return sb.ToString();
		}

		
		
		
		private static void GenerateModSyncMetadata([NotNull] StringBuilder sb, [NotNull] ModComponent component)
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

			
			return "Supported languages: " + string.Join(", ", languages);
		}

		[ItemNotNull]
		[NotNull]
		private ObservableCollection<Instruction> DeserializeInstructions(
			[CanBeNull][ItemCanBeNull] IList<object> instructionsSerializedList,
			ModComponent thisComponent
		)
		{
			if ( instructionsSerializedList.IsNullOrEmptyCollection() )
			{
				_ = Logger.LogWarningAsync($"No instructions found for component '{Name}'");
				return new ObservableCollection<Instruction>();
			}

			var instructions = new ObservableCollection<Instruction>();

			
			for ( int index = 0; index < instructionsSerializedList.Count; index++ )
			{
				Dictionary<string, object> instructionDict =
					Serializer.SerializeIntoDictionary(instructionsSerializedList[index]);

				Serializer.DeserializePathInDictionary(instructionDict, key: "Source");
				Serializer.DeserializeGuidDictionary(instructionDict, key: "Restrictions");
				Serializer.DeserializeGuidDictionary(instructionDict, key: "Dependencies");

				var instruction = new Instruction();
				string strAction = GetValueOrDefault<string>(instructionDict, key: "Action");

				
				if ( string.Equals(strAction, "TSLPatcher", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(strAction, "HoloPatcher", StringComparison.OrdinalIgnoreCase) )
				{
					instruction.Action = Instruction.ActionType.Patcher;
					_ = Logger.LogAsync($" -- Deserialize instruction #{index + 1} action '{strAction}' -> Patcher (backward compatibility)");
				}
				else if ( Enum.TryParse(strAction, ignoreCase: true, out Instruction.ActionType action) )
				{
					instruction.Action = action;
					_ = Logger.LogAsync($" -- Deserialize instruction #{index + 1} action '{action}'");
				}
				else
				{
					_ = Logger.LogErrorAsync(
						$"{Environment.NewLine} -- Missing/invalid action for instruction #{index}"
					);
					instruction.Action = Instruction.ActionType.Unset;
				}

				instruction.Arguments = GetValueOrDefault<string>(instructionDict, key: "Arguments") ?? string.Empty;
				instruction.Overwrite = GetValueOrDefault<bool>(instructionDict, key: "Overwrite");

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
				instruction.SetParentComponent(thisComponent);
			}

			return instructions;
		}

		[ItemNotNull]
		[NotNull]
		private ObservableCollection<Option> DeserializeOptions(
			[CanBeNull][ItemCanBeNull] IList<object> optionsSerializedList
		)
		{
			if ( optionsSerializedList.IsNullOrEmptyCollection() )
			{
				_ = Logger.LogVerboseAsync($"No options found for component '{Name}'");
				return new ObservableCollection<Option>();
			}

			var options = new ObservableCollection<Option>();

			
			for ( int index = 0; index < optionsSerializedList.Count; index++ )
			{
				var optionsDict = (IDictionary<string, object>)optionsSerializedList[index];
				if ( optionsDict is null )
					continue;

				Serializer.DeserializeGuidDictionary(optionsDict, key: "Restrictions");
				Serializer.DeserializeGuidDictionary(optionsDict, key: "Dependencies");

				var option = new Option();
				_ = Logger.LogAsync($"-- Deserialize option #{index + 1}");

				option.Name = GetRequiredValue<string>(optionsDict, key: "Name");
				option.Description = GetValueOrDefault<string>(optionsDict, key: "Description") ?? string.Empty;
				_ = Logger.LogAsync($" == Deserialize next option '{Name}' ==");
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
		private static T GetRequiredValue<T>([NotNull] IDictionary<string, object> dict, [NotNull] string key)
		{
			T value = GetValue<T>(dict, key, required: true);
			
			return value == null
				? throw new InvalidOperationException("GetValue cannot return null for a required value.")
				: value;
		}

		[CanBeNull]
		private static T GetValueOrDefault<T>([NotNull] IDictionary<string, object> dict, [NotNull] string key) =>
			GetValue<T>(dict, key, required: false);

		
		
		
		
		
		
		
		
		
		
		
		
		
		
		
		
		
		
		
		
		
		[CanBeNull]
		private static T GetValue<T>([NotNull] IDictionary<string, object> dict, [NotNull] string key, bool required)
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
							string guidStr = Serializer.FixGuidString(valueStr);
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
					MethodInfo addMethod = list?.GetType().GetMethod(name: "Add");

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
			catch ( RuntimeBinderException ) when ( !required )
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
		public static ModComponent DeserializeTomlComponent([NotNull] string tomlString)
		{
			if ( tomlString is null )
				throw new ArgumentNullException(nameof(tomlString));

			tomlString = Serializer.FixWhitespaceIssues(tomlString);

			
			DocumentSyntax tomlDocument = Toml.Parse(tomlString);

			
			if ( tomlDocument.HasErrors )
			{
				foreach ( DiagnosticMessage message in tomlDocument.Diagnostics )
				{
					if ( message is null )
						continue;

					Logger.Log(message.Message);
				}

				return null;
			}

			
			TomlTable tomlTable = tomlDocument.ToModel();

			IList<TomlTable> componentTableThing = new List<TomlTable>();
			switch ( tomlTable["thisMod"] )
			{
				case TomlArray componentTable:
					componentTableThing.Add((TomlTable)componentTable[0]);
					break;
				case TomlTableArray componentTables:
					componentTableThing = componentTables;
					break;
			}

			
			var component = new ModComponent();
			foreach ( TomlTable tomlComponent in componentTableThing )
			{
				if ( tomlComponent is IDictionary<string, object> componentDict )
					component.DeserializeComponent(componentDict);
			}

			return component;
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

		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> ReadComponentsFromFile([NotNull] string filePath)
		{
			if ( filePath is null )
				throw new ArgumentNullException(nameof(filePath));

			try
			{
				if ( MainConfig.CaseInsensitivePathing )
					filePath = PathHelper.GetCaseSensitivePath(filePath, isFile: true).Item1;
				
				string tomlString = File.ReadAllText(filePath)
					
					
					.Replace(oldValue: "Instructions = []", string.Empty)
					.Replace(oldValue: "Options = []", string.Empty);

				if ( string.IsNullOrWhiteSpace(tomlString) )
				{
					throw new InvalidDataException(
						$"Expected an instructions file at '{filePath}' but the file was empty."
					);
				}

				tomlString = Serializer.FixWhitespaceIssues(tomlString);

				
				DocumentSyntax tomlDocument = Toml.Parse(tomlString);

				
				if ( tomlDocument.HasErrors )
				{
					foreach ( DiagnosticMessage message in tomlDocument.Diagnostics )
					{
						if ( message is null )
							continue;

						Logger.LogError(message.Message);
					}
				}

				TomlTable tomlTable = tomlDocument.ToModel();

				
				var componentTables = (IList<TomlTable>)tomlTable[key: "thisMod"];

				
				var components = new List<ModComponent>();

				foreach ( TomlTable tomlComponent in componentTables )
				{
					if ( tomlComponent is null )
						continue;

					var thisComponent = new ModComponent();
					thisComponent.DeserializeComponent(tomlComponent);

					components.Add(thisComponent);
				}

				return components;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, customMessage: "There was a problem serializing the components in the file.");
				throw;
			}
		}

		
		
		
		
		public bool TryGenerateInstructionsFromArchive()
		{
			try
			{
				
				if ( Instructions.Count > 0 )
					return false;

				
				if ( ModLink.Count == 0 )
					return false;

				
				if ( MainConfig.SourcePath == null || !MainConfig.SourcePath.Exists )
					return false;

				
				string firstModLink = ModLink[0];
				if ( string.IsNullOrWhiteSpace(firstModLink) )
					return false;

				
				
				string searchTerm;
				if ( firstModLink.Contains("://") )
				{
					
					Uri uri = new Uri(firstModLink);
					string lastSegment = uri.Segments.LastOrDefault()?.TrimEnd('/') ?? string.Empty;

					
					if ( !string.IsNullOrEmpty(lastSegment) && lastSegment.Contains('-') )
					{
						
						var match = System.Text.RegularExpressions.Regex.Match(lastSegment, @"^\d+-(.+)$");
						searchTerm = match.Success ? match.Groups[1].Value : lastSegment;
					}
					else
					{
						searchTerm = lastSegment;
					}

					
					if ( Path.HasExtension(searchTerm) )
					{
						searchTerm = Path.GetFileNameWithoutExtension(searchTerm);
					}
				}
				else
				{
					
					string fileName = Path.GetFileName(firstModLink);
					searchTerm = Path.HasExtension(fileName) ? Path.GetFileNameWithoutExtension(fileName) : fileName;
				}

				if ( string.IsNullOrWhiteSpace(searchTerm) )
				{
					
					searchTerm = Name;
				}

				Logger.LogVerbose($"[TryGenerateInstructions] Component '{Name}': Searching for archive matching '{searchTerm}'");

				
				string[] archiveExtensions = { "*.zip", "*.rar", "*.7z", "*.exe" };
				var allArchives = archiveExtensions
					.SelectMany(ext => MainConfig.SourcePath.GetFiles(ext, SearchOption.TopDirectoryOnly))
					.Where(f => f.Exists)
					.ToList();

				if ( allArchives.Count == 0 )
				{
					Logger.LogVerbose($"[TryGenerateInstructions] Component '{Name}': No archives found in directory");
					return false;
				}

				Logger.LogVerbose($"[TryGenerateInstructions] Component '{Name}': Found {allArchives.Count} archives to check");

				
				string searchTermLower = searchTerm.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
				FileInfo matchingArchive = allArchives
					.OrderByDescending(f =>
					{
						string fileWithoutExt = Path.GetFileNameWithoutExtension(f.Name);
						string fileNameNormalized = fileWithoutExt.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");

						
						if ( fileNameNormalized.Equals(searchTermLower) )
							return 100;

						
						if ( fileNameNormalized.Contains(searchTermLower) )
							return 50;

						
						if ( searchTermLower.Contains(fileNameNormalized) )
							return 25;

						return 0;
					})
					.ThenByDescending(f => f.LastWriteTime)
					.FirstOrDefault(f =>
					{
						
						string fileWithoutExt = Path.GetFileNameWithoutExtension(f.Name);
						string fileNameNormalized = fileWithoutExt.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
						return fileNameNormalized.Contains(searchTermLower) || searchTermLower.Contains(fileNameNormalized);
					});

				if ( matchingArchive == null )
				{
					Logger.LogVerbose($"[TryGenerateInstructions] Component '{Name}': No matching archive found for '{searchTerm}'");
					return false;
				}

				Logger.LogVerbose($"[TryGenerateInstructions] Component '{Name}': Selected archive '{matchingArchive.Name}'");

				
				bool generated = Services.AutoInstructionGenerator.GenerateInstructions(this, matchingArchive.FullName);
				if ( generated )
				{
					
					IsDownloaded = true;
				}
				return generated;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to auto-generate instructions for component '{Name}'");
				return false;
			}
		}

		
		
		
		
		public async Task<InstallExitCode> InstallAsync([NotNull] List<ModComponent> componentsList, CancellationToken cancellationToken)
		{
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			cancellationToken.ThrowIfCancellationRequested();
			EnsureCheckpointLoaded();
			InstallState = ComponentInstallState.Running;
			LastStartedUtc = DateTimeOffset.UtcNow;
			PersistCheckpoint();

			try
			{
				await s_checkpointSemaphore.WaitAsync(cancellationToken);
				try
				{
					
					var realFileSystem = new Services.FileSystem.RealFileSystemProvider();

					InstallExitCode exitCode = await ExecuteInstructionsAsync(
						Instructions,
						componentsList,
						cancellationToken,
						realFileSystem
					);
					await Logger.LogAsync((string)Utility.Utility.GetEnumDescription(exitCode));
					if ( exitCode == InstallExitCode.Success )
					{
						InstallState = ComponentInstallState.Completed;
						LastCompletedUtc = DateTimeOffset.UtcNow;
					}
					else if ( exitCode == InstallExitCode.DependencyViolation )
					{
						InstallState = ComponentInstallState.Blocked;
						LastCompletedUtc = DateTimeOffset.UtcNow;
					}
					else
					{
						InstallState = ComponentInstallState.Failed;
						LastCompletedUtc = DateTimeOffset.UtcNow;
					}
					PersistCheckpoint();
					return exitCode;
				}
				finally
				{
					_ = s_checkpointSemaphore.Release();
				}
			}
			catch ( InvalidOperationException ex )
			{
				await Logger.LogExceptionAsync(ex);
				InstallState = ComponentInstallState.Failed;
				LastCompletedUtc = DateTimeOffset.UtcNow;
				PersistCheckpoint();
			}
			catch ( OperationCanceledException )
			{
				InstallState = ComponentInstallState.Failed;
				PersistCheckpoint();
				throw;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				await Logger.LogErrorAsync(
					"The above exception is not planned and has not been experienced."
					+ " Please report this to the developer."
				);
				InstallState = ComponentInstallState.Failed;
				LastCompletedUtc = DateTimeOffset.UtcNow;
				PersistCheckpoint();
			}

			return InstallExitCode.UnknownError;
		}

		
		
		
		
		
		
		public async Task<InstallExitCode> ExecuteInstructionsAsync(
			[NotNull][ItemNotNull] ObservableCollection<Instruction> theseInstructions,
			[NotNull][ItemNotNull] List<ModComponent> componentsList,
			CancellationToken cancellationToken,
			[NotNull] Services.FileSystem.IFileSystemProvider fileSystemProvider
		)
		{
			if ( theseInstructions is null )
				throw new ArgumentNullException(nameof(theseInstructions));
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));
			if ( fileSystemProvider is null )
				throw new ArgumentNullException(nameof(fileSystemProvider));

			bool shouldInstall = ShouldInstallComponent(componentsList);
			if ( !shouldInstall )
				return InstallExitCode.DependencyViolation;

			InstallExitCode installExitCode = InstallExitCode.Success;

			for ( int instructionIndex = 1; instructionIndex <= theseInstructions.Count; instructionIndex++ )
			{
				cancellationToken.ThrowIfCancellationRequested();

				Instruction instruction = theseInstructions[instructionIndex - 1];

				
				instruction.SetFileSystemProvider(fileSystemProvider);

				if ( !ShouldRunInstruction(instruction, componentsList) )
				{
					continue;
				}

				

				Instruction.ActionExitCode exitCode = Instruction.ActionExitCode.Success;
				switch ( instruction.Action )
				{
					case Instruction.ActionType.Extract:
						instruction.SetRealPaths();
						exitCode = await instruction.ExtractFileAsync();
						break;
					case Instruction.ActionType.Delete:
						instruction.SetRealPaths(noValidate: true);
						exitCode = instruction.DeleteFile();
						break;
					case Instruction.ActionType.DelDuplicate:
						instruction.SetRealPaths(noParse: true);
						instruction.DeleteDuplicateFile(caseInsensitive: true);
						exitCode = Instruction.ActionExitCode.Success;
						break;
					case Instruction.ActionType.Copy:
						instruction.SetRealPaths();
						exitCode = await instruction.CopyFileAsync();
						break;
					case Instruction.ActionType.Move:
						instruction.SetRealPaths();
						exitCode = await instruction.MoveFileAsync();
						break;
					case Instruction.ActionType.Rename:
						instruction.SetRealPaths(noValidate: true);
						exitCode = instruction.RenameFile();
						break;
					case Instruction.ActionType.Patcher:
						instruction.SetRealPaths();
						exitCode = await instruction.ExecuteTSLPatcherAsync();
						break;
					case Instruction.ActionType.Execute:
					case Instruction.ActionType.Run:
						instruction.SetRealPaths(noValidate: true);
						exitCode = await instruction.ExecuteProgramAsync();
						break;
					case Instruction.ActionType.Choose:
						instruction.SetRealPaths(noParse: true);
						List<Option> list = instruction.GetChosenOptions();
						for ( int i = 0; i < list.Count; i++ )
						{
							Option thisOption = list[i];
							InstallExitCode optionExitCode = await ExecuteInstructionsAsync(
								thisOption.Instructions,
								componentsList,
								cancellationToken,
								fileSystemProvider  
							);
							installExitCode = optionExitCode;

							
							if ( optionExitCode != InstallExitCode.Success )
							{
								await Logger.LogErrorAsync($"Failed to install chosen option {i + 1} in main instruction index {instructionIndex}");
								exitCode = Instruction.ActionExitCode.OptionalInstallFailed;
								if ( optionExitCode == InstallExitCode.UserCancelledInstall )
									return optionExitCode;
								break;
							}
						}

						break;
					case Instruction.ActionType.Unset:
					default:
						
						await Logger.LogWarningAsync($"Unknown instruction '{instruction.ActionString}'");
						exitCode = Instruction.ActionExitCode.UnknownInstruction;
						break;
				}

				_ = Logger.LogVerboseAsync(
					$"Instruction #{instructionIndex} '{instruction.ActionString}' exited with code {exitCode}"
				);
				if ( exitCode != Instruction.ActionExitCode.Success )
				{
					await Logger.LogErrorAsync(
						$"FAILED Instruction #{instructionIndex} Action '{instruction.ActionString}'"
					);

					
					return InstallExitCode.UnknownError;
				}


				_ = Logger.LogAsync($"Successfully completed instruction #{instructionIndex} '{instruction.Action}'");
			}

			return installExitCode;
		}

		[NotNull]
		public static Dictionary<string, List<ModComponent>> GetConflictingComponents(
			[NotNull] List<Guid> dependencyGuids,
			[NotNull] List<Guid> restrictionGuids,
			[NotNull][ItemNotNull] List<ModComponent> componentsList
		)
		{
			if ( dependencyGuids is null )
				throw new ArgumentNullException(nameof(dependencyGuids));
			if ( restrictionGuids is null )
				throw new ArgumentNullException(nameof(restrictionGuids));
			if ( componentsList == null )
				throw new ArgumentNullException(nameof(componentsList));

			var conflicts = new Dictionary<string, List<ModComponent>>();
			if ( dependencyGuids.Count > 0 )
			{
				var dependencyConflicts = new List<ModComponent>();

				foreach ( Guid requiredGuid in dependencyGuids )
				{
					ModComponent checkComponent = FindComponentFromGuid(requiredGuid, componentsList);
					if ( checkComponent == null )
					{
						
						
						
						var componentGuidNotFound = new ModComponent
						{
							Name = "ModComponent Undefined with GUID.",
							Guid = requiredGuid,
						};
						dependencyConflicts.Add(componentGuidNotFound);
					}
					else if ( !checkComponent.IsSelected )
					{
						dependencyConflicts.Add(checkComponent);
					}
				}


				if ( dependencyConflicts.Count > 0 )
					conflicts["Dependency"] = dependencyConflicts;
			}

			
			if ( restrictionGuids.Count > 0 )
			{
				var restrictionConflicts = restrictionGuids
					.Select(requiredGuid => FindComponentFromGuid(requiredGuid, componentsList)).Where(
						checkComponent => checkComponent != null && checkComponent.IsSelected
					).ToList();

				if ( restrictionConflicts.Count > 0 )
					conflicts["Restriction"] = restrictionConflicts;
			}

			return conflicts;
		}

		
		
		
		
		public bool ShouldInstallComponent([NotNull][ItemNotNull] List<ModComponent> componentsList)
		{
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			Dictionary<string, List<ModComponent>> conflicts = GetConflictingComponents(
				Dependencies,
				Restrictions,
				componentsList
			);
			return conflicts.Count == 0;
		}

		
		
		
		
		public static bool ShouldRunInstruction(
			[NotNull] Instruction instruction,
			[NotNull] List<ModComponent> componentsList
		)
		{
			if ( instruction is null )
				throw new ArgumentNullException(nameof(instruction));
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			Dictionary<string, List<ModComponent>> conflicts = GetConflictingComponents(
				instruction.Dependencies,
				instruction.Restrictions,
				componentsList
			);
			return conflicts.Count == 0;
		}

		[CanBeNull]
		public static ModComponent FindComponentFromGuid(
			Guid guidToFind,
			[NotNull][ItemNotNull] List<ModComponent> componentsList
		)
		{
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			ModComponent foundComponent = null;
			foreach ( ModComponent component in componentsList )
			{
				if ( component.Guid == guidToFind )
				{
					foundComponent = component;
					break;
				}

				foreach ( Option thisOption in component.Options )
				{
					if ( thisOption.Guid == guidToFind )
					{
						foundComponent = thisOption;
						break;
					}
				}

				if ( foundComponent != null )
					break;
			}

			return foundComponent;
		}

		[NotNull]
		public static List<ModComponent> FindComponentsFromGuidList(
			[NotNull] List<Guid> guidsToFind,
			[NotNull] List<ModComponent> componentsList
		)
		{
			if ( guidsToFind is null )
				throw new ArgumentNullException(nameof(guidsToFind));
			if ( componentsList is null )
				throw new ArgumentNullException(nameof(componentsList));

			var foundComponents = new List<ModComponent>();
			foreach ( Guid guidToFind in guidsToFind )
			{
				ModComponent foundComponent = FindComponentFromGuid(guidToFind, componentsList);
				if ( foundComponent is null )
					continue;

				foundComponents.Add(foundComponent);
			}

			return foundComponents;
		}

		public static (bool isCorrectOrder, List<ModComponent> reorderedComponents) ConfirmComponentsInstallOrder(
			[NotNull][ItemNotNull] List<ModComponent> components
		)
		{
			if ( components is null )
				throw new ArgumentNullException(nameof(components));

			Dictionary<Guid, GraphNode> nodeMap = CreateDependencyGraph(components);

			
			var permanentMark = new HashSet<GraphNode>();
			var temporaryMark = new HashSet<GraphNode>();

			foreach ( GraphNode node in nodeMap.Values )
			{
				if ( !permanentMark.Contains(node) )
				{
					if ( HasCycle(node, permanentMark, temporaryMark) )
					{
						
						throw new KeyNotFoundException("Circular dependency detected in component ordering");
					}
				}
			}

			
			var visitedNodes = new HashSet<GraphNode>();
			var orderedComponents = new List<ModComponent>();

			foreach ( GraphNode node in nodeMap.Values )
			{
				if ( visitedNodes.Contains(node) )
					continue;

				DepthFirstSearch(node, visitedNodes, orderedComponents);
			}

			bool isCorrectOrder = orderedComponents.SequenceEqual(components);

			return (isCorrectOrder, orderedComponents);
		}

		
		private static bool HasCycle(
			[NotNull] GraphNode node,
			[NotNull] ISet<GraphNode> permanentMark,
			[NotNull] ISet<GraphNode> temporaryMark
		)
		{
			if ( permanentMark.Contains(node) )
				return false; 

			if ( temporaryMark.Contains(node) )
				return true; 

			_ = temporaryMark.Add(node);

			foreach ( GraphNode dependency in node.Dependencies )
			{
				if ( HasCycle(dependency, permanentMark, temporaryMark) )
					return true;
			}

			_ = temporaryMark.Remove(node);
			_ = permanentMark.Add(node);
			return false;
		}

		
		private static void DepthFirstSearch(
			[NotNull] GraphNode node,
			[NotNull] ISet<GraphNode> visitedNodes,
			[NotNull] ICollection<ModComponent> orderedComponents
		)
		{
			if ( node is null )
				throw new ArgumentNullException(nameof(node));
			if ( visitedNodes is null )
				throw new ArgumentNullException(nameof(visitedNodes));
			if ( orderedComponents is null )
				throw new ArgumentNullException(nameof(orderedComponents));

			_ = visitedNodes.Add(node);

			foreach ( GraphNode dependency in node.Dependencies )
			{
				if ( visitedNodes.Contains(dependency) )
					continue;

				DepthFirstSearch(dependency, visitedNodes, orderedComponents);
			}

			orderedComponents.Add(node.ModComponent);
		}

		private static Dictionary<Guid, GraphNode> CreateDependencyGraph(
			[NotNull][ItemNotNull] List<ModComponent> components
		)
		{
			if ( components is null )
				throw new ArgumentNullException(nameof(components));

			var nodeMap = new Dictionary<Guid, GraphNode>();

			foreach ( ModComponent component in components )
			{
				var node = new GraphNode(component);
				nodeMap[component.Guid] = node;
			}

			foreach ( ModComponent component in components )
			{
				GraphNode node = nodeMap[component.Guid];

				foreach ( Guid dependencyGuid in component.InstallAfter )
				{
					if ( !nodeMap.TryGetValue(dependencyGuid, out GraphNode dependencyNode) )
					{
						
						Logger.LogWarning($"ModComponent '{component.Name}' references InstallAfter GUID {dependencyGuid} which is not in the current component list");
						continue;
					}
					_ = node?.Dependencies?.Add(dependencyNode);
				}

				foreach ( Guid dependentGuid in component.InstallBefore )
				{
					if ( !nodeMap.TryGetValue(dependentGuid, out GraphNode dependentNode) )
					{
						
						Logger.LogWarning($"ModComponent '{component.Name}' references InstallBefore GUID {dependentGuid} which is not in the current component list");
						continue;
					}
					_ = dependentNode?.Dependencies?.Add(node);
				}
			}

			return nodeMap;
		}

		public void CreateInstruction(int index = 0)
		{
			var instruction = new Instruction();
			if ( Instructions.IsNullOrEmptyOrAllNull() )
			{
				if ( index != 0 )
				{
					Logger.LogError("Cannot create instruction at index when list is empty.");
					return;
				}

				Instructions.Add(instruction);
			}
			else
			{
				Instructions.Insert(index, instruction);
			}
			instruction.SetParentComponent(this);
		}

		public void DeleteInstruction(int index) => Instructions.RemoveAt(index);
		public void DeleteOption(int index) => Options.RemoveAt(index);

		public void MoveInstructionToIndex([NotNull] Instruction thisInstruction, int index)
		{
			if ( thisInstruction is null || index < 0 || index >= Instructions.Count )
				throw new ArgumentException("Invalid instruction or index.");

			int currentIndex = Instructions.IndexOf(thisInstruction);
			if ( currentIndex < 0 )
				throw new ArgumentException("Instruction does not exist in the list.");

			if ( index == currentIndex )
			{
				_ = Logger.LogAsync(
					$"Cannot move Instruction '{thisInstruction.Action}' from {currentIndex} to {index}. Reason: Indices are the same."
				);
				return;
			}

			Instructions.RemoveAt(currentIndex);
			Instructions.Insert(index, thisInstruction);

			_ = Logger.LogVerboseAsync($"Instruction '{thisInstruction.Action}' moved from {currentIndex} to {index}");
		}

		public void CreateOption(int index = 0)
		{
			var option = new Option
			{
				Name = Path.GetFileNameWithoutExtension(Path.GetTempFileName()),
				Guid = Guid.NewGuid(),
			};
			if ( Instructions.IsNullOrEmptyOrAllNull() )
			{
				if ( index != 0 )
				{
					Logger.LogError("Cannot create option at index when list is empty.");
					return;
				}

				Options.Add(option);
			}
			else
			{
				Options.Insert(index, option);
			}
		}

		public void MoveOptionToIndex([NotNull] Option thisOption, int index)
		{
			if ( thisOption is null || index < 0 || index >= Options.Count )
				throw new ArgumentException("Invalid option or index.");

			int currentIndex = Options.IndexOf(thisOption);
			if ( currentIndex < 0 )
				throw new ArgumentException("Option does not exist in the list.");

			if ( index == currentIndex )
			{
				_ = Logger.LogAsync(
					$"Cannot move Option '{thisOption.Name}' from {currentIndex} to {index}. Reason: Indices are the same."
				);
				return;
			}

			Options.RemoveAt(currentIndex);
			Options.Insert(index, thisOption);

			_ = Logger.LogVerboseAsync($"Option '{thisOption.Name}' moved from {currentIndex} to {index}");
		}

		public class GraphNode
		{
			public GraphNode([CanBeNull] ModComponent component)
			{
				ModComponent = component;
				Dependencies = new HashSet<GraphNode>();
			}

			public ModComponent ModComponent { get; }
			public HashSet<GraphNode> Dependencies { get; }
		}
	}
}
