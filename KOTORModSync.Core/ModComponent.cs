// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core.Utility;

using Newtonsoft.Json;

using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;
namespace KOTORModSync.Core
{
	public class ModComponent : INotifyPropertyChanged
	{
		public enum InstallExitCode
		{
			[Description( "Completed Successfully" )]
			Success,
			[Description( "A dependency or restriction violation between components has occurred." )]
			DependencyViolation,
			[Description( "User cancelled the installation." )]
			UserCancelledInstall,
			[Description( "An invalid operation was attempted." )]
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

		[NotNull] private string _author = string.Empty;
		[NotNull] private List<string> _category = new List<string>();
		[NotNull] private List<Guid> _dependencies = new List<Guid>();
		[NotNull] private List<string> _dependencyNames = new List<string>();
		[NotNull] private string _description = string.Empty;
		[NotNull] internal string _descriptionSpoilerFree = string.Empty;
		[NotNull] private string _directions = string.Empty;
		[NotNull] internal string _directionsSpoilerFree = string.Empty;
		[NotNull] private string _downloadInstructions = string.Empty;
		[NotNull] internal string _downloadInstructionsSpoilerFree = string.Empty;
		[NotNull] private string _usageWarning = string.Empty;
		[NotNull] internal string _usageWarningSpoilerFree = string.Empty;
		[NotNull] private string _screenshots = string.Empty;
		[NotNull] internal string _screenshotsSpoilerFree = string.Empty;
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


		public const string CheckpointFolderName = ".kotor_modsync";
		[NotNull][ItemNotNull] private List<string> _language = new List<string>();
		[NotNull] private Dictionary<string, Dictionary<string, bool?>> _modLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase );
		[NotNull] private Dictionary<string, ResourceMetadata> _resourceRegistry = new Dictionary<string, ResourceMetadata>( StringComparer.OrdinalIgnoreCase );
		[NotNull] private List<string> _excludedDownloads = new List<string>();
		[NotNull] private string _name = string.Empty;
		[NotNull] private string _nameFieldContent = string.Empty;
		[NotNull] private string _heading = string.Empty;
		[NotNull] private ObservableCollection<Option> _options = new ObservableCollection<Option>();
		[NotNull] private List<Guid> _restrictions = new List<Guid>();
		[NotNull] private string _tier = string.Empty;
		private bool _isDownloaded;
		private bool _isValidating;
		private bool _widescreenOnly;
		public Guid Guid
		{
			get => _guid;
			set
			{
				if (_guid == value) return;
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
				if (string.Equals( _name, value, StringComparison.Ordinal )) return;
				_name = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		[JsonIgnore]
		public string NameFieldContent
		{
			get => _nameFieldContent;


			set
			{
				if (string.Equals( _nameFieldContent, value, StringComparison.Ordinal )) return;
				_nameFieldContent = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string Heading
		{
			get => _heading;


			set
			{
				if (string.Equals( _heading, value, StringComparison.Ordinal )) return;
				_heading = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string Author
		{
			get => _author;


			set
			{
				if (string.Equals( _author, value, StringComparison.Ordinal )) return;
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
				if (_category == value) return;
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
				string normalizedValue = CategoryTierDefinitions.NormalizeTier( value );


				if (string.Equals( _tier, normalizedValue, StringComparison.Ordinal )) return;
				_tier = normalizedValue;
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
				if (_language == value) return;
				_language = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public Dictionary<string, Dictionary<string, bool?>> ModLinkFilenames
		{
			get => _modLinkFilenames;
			set
			{
				if (_modLinkFilenames == value) return;
				_modLinkFilenames = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public Dictionary<string, ResourceMetadata> ResourceRegistry
		{
			get => _resourceRegistry;
			set
			{
				if (_resourceRegistry == value) return;
				_resourceRegistry = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public List<string> ExcludedDownloads
		{
			get => _excludedDownloads;
			set
			{
				if (_excludedDownloads == value) return;
				_excludedDownloads = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string Description
		{
			get => _description;


			set
			{
				if (string.Equals( _description, value, StringComparison.Ordinal )) return;
				_description = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string DescriptionSpoilerFree
		{
			get => string.IsNullOrWhiteSpace( _descriptionSpoilerFree ) ? _description : _descriptionSpoilerFree;


			set
			{
				if (string.Equals( _descriptionSpoilerFree, value, StringComparison.Ordinal )) return;
				_descriptionSpoilerFree = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string InstallationMethod
		{
			get => _installationMethod;


			set
			{
				if (string.Equals( _installationMethod, value, StringComparison.Ordinal )) return;
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
				if (string.Equals( _directions, value, StringComparison.Ordinal )) return;
				_directions = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string DirectionsSpoilerFree
		{
			get => string.IsNullOrWhiteSpace( _directionsSpoilerFree ) ? _directions : _directionsSpoilerFree;


			set
			{
				if (string.Equals( _directionsSpoilerFree, value, StringComparison.Ordinal )) return;
				_directionsSpoilerFree = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string DownloadInstructions
		{
			get => _downloadInstructions;


			set
			{
				if (string.Equals( _downloadInstructions, value, StringComparison.Ordinal )) return;
				_downloadInstructions = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string DownloadInstructionsSpoilerFree
		{
			get => string.IsNullOrWhiteSpace( _downloadInstructionsSpoilerFree ) ? _downloadInstructions : _downloadInstructionsSpoilerFree;


			set
			{
				if (string.Equals( _downloadInstructionsSpoilerFree, value, StringComparison.Ordinal )) return;
				_downloadInstructionsSpoilerFree = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string UsageWarning
		{
			get => _usageWarning;


			set
			{
				if (string.Equals( _usageWarning, value, StringComparison.Ordinal )) return;
				_usageWarning = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string UsageWarningSpoilerFree
		{
			get => string.IsNullOrWhiteSpace( _usageWarningSpoilerFree ) ? _usageWarning : _usageWarningSpoilerFree;


			set
			{
				if (string.Equals( _usageWarningSpoilerFree, value, StringComparison.Ordinal )) return;
				_usageWarningSpoilerFree = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string Screenshots
		{
			get => _screenshots;


			set
			{
				if (string.Equals( _screenshots, value, StringComparison.Ordinal )) return;
				_screenshots = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string ScreenshotsSpoilerFree
		{
			get => string.IsNullOrWhiteSpace( _screenshotsSpoilerFree ) ? _screenshots : _screenshotsSpoilerFree;


			set
			{
				if (string.Equals( _screenshotsSpoilerFree, value, StringComparison.Ordinal )) return;
				_screenshotsSpoilerFree = value;
				OnPropertyChanged();
			}
		}
		[NotNull] private string _knownBugs = string.Empty;
		[NotNull]
		public string KnownBugs
		{
			get => _knownBugs;


			set
			{
				if (string.Equals( _knownBugs, value, StringComparison.Ordinal )) return;
				_knownBugs = value;
				OnPropertyChanged();
			}
		}
		[NotNull] private string _installationWarning = string.Empty;
		[NotNull] private string _compatibilityWarning = string.Empty;
		[NotNull] private string _steamNotes = string.Empty;
		[NotNull]
		public string InstallationWarning
		{
			get => _installationWarning;


			set
			{
				if (string.Equals( _installationWarning, value, StringComparison.Ordinal )) return;
				_installationWarning = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string CompatibilityWarning
		{
			get => _compatibilityWarning;


			set
			{
				if (string.Equals( _compatibilityWarning, value, StringComparison.Ordinal )) return;
				_compatibilityWarning = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string SteamNotes
		{
			get => _steamNotes;


			set
			{
				if (string.Equals( _steamNotes, value, StringComparison.Ordinal )) return;
				_steamNotes = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public List<Guid> Dependencies
		{
			get => _dependencies;
			set
			{
				if (_dependencies == value) return;
				_dependencies = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public List<string> DependencyNames
		{
			get => _dependencyNames;
			set
			{
				if (_dependencyNames == value) return;
				_dependencyNames = value;
				OnPropertyChanged();
			}
		}
		[NotNull] private Dictionary<Guid, string> _dependencyGuidToOriginalName = new Dictionary<Guid, string>();
		[NotNull]
		[JsonIgnore]
		public Dictionary<Guid, string> DependencyGuidToOriginalName
		{
			get => _dependencyGuidToOriginalName;
			set
			{
				_dependencyGuidToOriginalName = value;
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
				if (_isSelected == value) return;
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
				if (_instructions != value)
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
				if (_installState == value)
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
				if (_lastStartedUtc == value)
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
				if (_lastCompletedUtc == value)
					return;
				_lastCompletedUtc = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public ObservableCollection<Option> Options
		{
			get => _options;
			set
			{
				if (_options == value)
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
				if (_isDownloaded == value)
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
				if (_isValidating == value)
					return;
				_isValidating = value;
				OnPropertyChanged();
			}
		}
		public bool WidescreenOnly
		{
			get => _widescreenOnly;
			set
			{
				if (_widescreenOnly == value)
					return;
				_widescreenOnly = value;
				OnPropertyChanged();
			}
		}
		private bool? _aspyrExclusive;
		[JsonIgnore]
		public bool? AspyrExclusive
		{
			get => _aspyrExclusive;
			set
			{
				if (_aspyrExclusive == value)
					return;
				_aspyrExclusive = value;
				OnPropertyChanged();
			}
		}
		public event PropertyChangedEventHandler PropertyChanged;
		private void OptionsCollectionChanged( object sender, NotifyCollectionChangedEventArgs e )
		{
			OnPropertyChanged( nameof( Options ) );
		}
		private void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null ) =>
			PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
		[NotNull]
		public string SerializeComponent()
		{
			return Services.ModComponentSerializationService.SerializeSingleComponentAsTomlString( this );
		}

		[CanBeNull]
		public static ModComponent DeserializeTomlComponent( [NotNull] string tomlString )
		{
			if (tomlString is null)
				throw new ArgumentNullException( nameof( tomlString ) );

			// Use the unified deserialization service
			List<ModComponent> components = Services.ModComponentSerializationService.DeserializeModComponentFromTomlString( tomlString );
			return components?.FirstOrDefault();
		}
		public async Task<InstallExitCode> InstallAsync(
			[NotNull] List<ModComponent> componentsList,
			CancellationToken cancellationToken )
		{
			if (componentsList is null)
				throw new ArgumentNullException( nameof( componentsList ) );
			cancellationToken.ThrowIfCancellationRequested();

			InstallState = ComponentInstallState.Running;
			LastStartedUtc = DateTimeOffset.UtcNow;
			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

			try
			{
				Services.FileSystem.RealFileSystemProvider realFileSystem = new Services.FileSystem.RealFileSystemProvider();
				InstallExitCode exitCode = await ExecuteInstructionsAsync(
					Instructions,
					componentsList,
					cancellationToken,


					realFileSystem
				).ConfigureAwait( false );
				await Logger.LogAsync( (string)UtilityHelper.GetEnumDescription( exitCode ) ).ConfigureAwait( false );

				sw.Stop();
				bool success = exitCode == InstallExitCode.Success;
				Services.TelemetryService.Instance.RecordModInstallation(
					modName: Name,
					success: success,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: success ? null : exitCode.ToString()
				);

				if (exitCode == InstallExitCode.Success)
				{
					InstallState = ComponentInstallState.Completed;
					LastCompletedUtc = DateTimeOffset.UtcNow;
				}
				else if (exitCode == InstallExitCode.DependencyViolation)
				{
					InstallState = ComponentInstallState.Blocked;
					LastCompletedUtc = DateTimeOffset.UtcNow;
				}
				else
				{
					InstallState = ComponentInstallState.Failed;
					LastCompletedUtc = DateTimeOffset.UtcNow;
				}
				return exitCode;
			}
			catch (InvalidOperationException ex)


			{
				await Logger.LogExceptionAsync( ex ).ConfigureAwait( false );
				sw.Stop();
				Services.TelemetryService.Instance.RecordModInstallation(
					modName: Name,
					success: false,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: ex.Message
				);
				InstallState = ComponentInstallState.Failed;
				LastCompletedUtc = DateTimeOffset.UtcNow;
			}
			catch (OperationCanceledException)
			{
				sw.Stop();
				Services.TelemetryService.Instance.RecordModInstallation(
					modName: Name,
					success: false,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: "Cancelled"
				);
				InstallState = ComponentInstallState.Failed;
				throw;
			}
			catch (Exception ex)


			{
				await Logger.LogExceptionAsync( ex )

.ConfigureAwait( false );
				await Logger.LogErrorAsync(
					"The above exception is not planned and has not been experienced."
					+ " Please report this to the developer."
				).ConfigureAwait( false );
				sw.Stop();
				Services.TelemetryService.Instance.RecordModInstallation(
					modName: Name,
					success: false,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: ex.Message
				);
				InstallState = ComponentInstallState.Failed;
				LastCompletedUtc = DateTimeOffset.UtcNow;
			}
			return InstallExitCode.UnknownError;
		}
		/// <summary>
		/// Executes a single instruction using the unified instruction execution pipeline.
		/// This method is used by both real installations and dry-run validation.
		/// </summary>
		public async Task<Instruction.ActionExitCode> ExecuteSingleInstructionAsync(
			[NotNull] Instruction instruction,
			int instructionIndex,
			[NotNull][ItemNotNull] List<ModComponent> componentsList,
			[NotNull] Services.FileSystem.IFileSystemProvider fileSystemProvider,
			bool skipDependencyCheck = false,
			CancellationToken cancellationToken = default
		)
		{
			if (instruction is null)
				throw new ArgumentNullException( nameof( instruction ) );
			if (componentsList is null)
				throw new ArgumentNullException( nameof( componentsList ) );
			if (fileSystemProvider is null)
				throw new ArgumentNullException( nameof( fileSystemProvider ) );

			Instruction.ActionExitCode exitCode = Instruction.ActionExitCode.Success;
			switch (instruction.Action)
			{
				case Instruction.ActionType.Extract:
					instruction.SetRealPaths();


					exitCode = await instruction.ExtractFileAsync()













.ConfigureAwait( false );








					break;
				case Instruction.ActionType.Delete:
					instruction.SetRealPaths( skipExistenceCheck: true );
					exitCode = instruction.DeleteFile();
					break;
				case Instruction.ActionType.DelDuplicate:
					instruction.SetRealPaths( sourceIsNotFilePath: true );
					instruction.DeleteDuplicateFile( caseInsensitive: true );
					exitCode = Instruction.ActionExitCode.Success;
					break;
				case Instruction.ActionType.Copy:
					instruction.SetRealPaths();
					exitCode = await instruction.CopyFileAsync().ConfigureAwait( false );
					break;
				case Instruction.ActionType.Move:
					instruction.SetRealPaths();
					exitCode = await instruction.MoveFileAsync().ConfigureAwait( false );
					break;
				case Instruction.ActionType.Rename:
					instruction.SetRealPaths( skipExistenceCheck: true );
					exitCode = instruction.RenameFile();
					break;
				case Instruction.ActionType.Patcher:
					instruction.SetRealPaths();
					exitCode = await instruction.ExecuteTSLPatcherAsync().ConfigureAwait( false );
					break;
				case Instruction.ActionType.Execute:
				case Instruction.ActionType.Run:
					instruction.SetRealPaths( skipExistenceCheck: true );
					exitCode = await instruction.ExecuteProgramAsync().ConfigureAwait( false );
					break;
				case Instruction.ActionType.Choose:
					instruction.SetRealPaths( sourceIsNotFilePath: true );
					List<Option> list = instruction.GetChosenOptions();
					for (int i = 0; i < list.Count; i++)
					{
						Option thisOption = list[i];
						InstallExitCode optionExitCode = await ExecuteInstructionsAsync(
							thisOption.Instructions,
							componentsList,
							cancellationToken,
							fileSystemProvider,
							skipDependencyCheck
						).ConfigureAwait( false );
						if (optionExitCode != InstallExitCode.Success)
						{
							await Logger.LogErrorAsync( $"Failed to install chosen option {i + 1} in main instruction index {instructionIndex}" ).ConfigureAwait( false );
							exitCode = Instruction.ActionExitCode.OptionalInstallFailed;
							break;
						}
					}
					break;
				default:
					await Logger.LogWarningAsync( $"Unknown instruction '{instruction.ActionString}'" ).ConfigureAwait( false );
					exitCode = Instruction.ActionExitCode.UnknownInstruction;
					break;
			}
			return exitCode;
		}

		public async Task<InstallExitCode> ExecuteInstructionsAsync(
			[NotNull][ItemNotNull] ObservableCollection<Instruction> theseInstructions,
			[NotNull][ItemNotNull] List<ModComponent> componentsList,
			CancellationToken cancellationToken,
			[NotNull] Services.FileSystem.IFileSystemProvider fileSystemProvider,
			bool skipDependencyCheck = false
		)
		{
			if (theseInstructions is null)
				throw new ArgumentNullException( nameof( theseInstructions ) );
			if (componentsList is null)
				throw new ArgumentNullException( nameof( componentsList ) );
			if (fileSystemProvider is null)
				throw new ArgumentNullException( nameof( fileSystemProvider ) );

			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				if (!skipDependencyCheck)
				{
					bool shouldInstall = ShouldInstallComponent( componentsList );
					if (!shouldInstall)
						return InstallExitCode.DependencyViolation;
				}

				InstallExitCode installExitCode = InstallExitCode.Success;
				for (int instructionIndex = 1; instructionIndex <= theseInstructions.Count; instructionIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					Instruction instruction = theseInstructions[instructionIndex - 1];
					instruction.SetFileSystemProvider( fileSystemProvider );
					if (!ShouldRunInstruction( instruction, componentsList ))
						continue;

					Instruction.ActionExitCode exitCode = await ExecuteSingleInstructionAsync(
						instruction,
						instructionIndex,
						componentsList,
						fileSystemProvider,
						skipDependencyCheck,
						cancellationToken
					).ConfigureAwait( false );

					_ = Logger.LogVerboseAsync(
						$"Instruction #{instructionIndex} '{instruction.ActionString}' exited with code {exitCode}"
					);
					if (exitCode != Instruction.ActionExitCode.Success)
					{
						await Logger.LogErrorAsync(
							$"FAILED Instruction #{instructionIndex} Action '{instruction.ActionString}'"
						).ConfigureAwait( false );
						if (exitCode == Instruction.ActionExitCode.OptionalInstallFailed)
							return InstallExitCode.UserCancelledInstall;
						return InstallExitCode.UnknownError;
					}
					_ = Logger.LogVerboseAsync( $"Successfully completed instruction #{instructionIndex} '{instruction.Action}'" );
				}

				sw.Stop();
				Services.TelemetryService.Instance.RecordComponentExecution(
					componentName: Name,
					success: true,
					instructionCount: theseInstructions.Count,
					durationMs: sw.Elapsed.TotalMilliseconds
				);

				return installExitCode;
			}
			catch (Exception ex)
			{
				sw.Stop();
				Services.TelemetryService.Instance.RecordComponentExecution(
					componentName: Name,
					success: false,
					instructionCount: theseInstructions.Count,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: ex.Message
				);
				throw;
			}
		}
		[NotNull]
		public static Dictionary<string, List<ModComponent>> GetConflictingComponents(
			[NotNull] List<Guid> dependencyGuids,
			[NotNull] List<Guid> restrictionGuids,
			[NotNull][ItemNotNull] List<ModComponent> componentsList
		)
		{
			if (dependencyGuids is null)
				throw new ArgumentNullException( nameof( dependencyGuids ) );
			if (restrictionGuids is null)
				throw new ArgumentNullException( nameof( restrictionGuids ) );
			if (componentsList == null)
				throw new ArgumentNullException( nameof( componentsList ) );
			Dictionary<string, List<ModComponent>> conflicts = new Dictionary<string, List<ModComponent>>( StringComparer.Ordinal );
			if (dependencyGuids.Count > 0)
			{
				List<ModComponent> dependencyConflicts = new List<ModComponent>();
				foreach (Guid requiredGuid in dependencyGuids)
				{
					ModComponent checkComponent = FindComponentFromGuid( requiredGuid, componentsList );
					if (checkComponent == null)
					{
						ModComponent componentGuidNotFound = new ModComponent
						{
							Name = "ModComponent Undefined with GUID.",
							Guid = requiredGuid,
						};
						dependencyConflicts.Add( componentGuidNotFound );
					}
					else if (!checkComponent.IsSelected)
					{
						dependencyConflicts.Add( checkComponent );
					}
				}
				if (dependencyConflicts.Count > 0)
					conflicts["Dependency"] = dependencyConflicts;
			}
			if (restrictionGuids.Count > 0)
			{
				List<ModComponent> restrictionConflicts = restrictionGuids
					.Select( requiredGuid => FindComponentFromGuid( requiredGuid, componentsList ) ).Where(
						checkComponent => checkComponent?.IsSelected ?? false
					).ToList();
				if (restrictionConflicts.Count > 0)
					conflicts["Restriction"] = restrictionConflicts;
			}
			return conflicts;
		}
		public bool ShouldInstallComponent( [NotNull][ItemNotNull] List<ModComponent> componentsList )
		{
			if (componentsList is null)
				throw new ArgumentNullException( nameof( componentsList ) );
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
			if (instruction is null)
				throw new ArgumentNullException( nameof( instruction ) );
			if (componentsList is null)
				throw new ArgumentNullException( nameof( componentsList ) );
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
			if (componentsList is null)
				throw new ArgumentNullException( nameof( componentsList ) );
			ModComponent foundComponent = null;
			foreach (ModComponent component in componentsList)
			{
				if (component.Guid == guidToFind)
				{
					foundComponent = component;
					break;
				}
				foreach (Option thisOption in component.Options)
				{
					if (thisOption.Guid == guidToFind)
					{
						foundComponent = thisOption;
						break;
					}
				}
				if (foundComponent != null)
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
			if (guidsToFind is null)
				throw new ArgumentNullException( nameof( guidsToFind ) );
			if (componentsList is null)
				throw new ArgumentNullException( nameof( componentsList ) );
			List<ModComponent> foundComponents = new List<ModComponent>();
			foreach (Guid guidToFind in guidsToFind)
			{
				ModComponent foundComponent = FindComponentFromGuid( guidToFind, componentsList );
				if (foundComponent is null)
					continue;
				foundComponents.Add( foundComponent );
			}
			return foundComponents;
		}
		public static (bool isCorrectOrder, List<ModComponent> reorderedComponents) ConfirmComponentsInstallOrder(
			[NotNull][ItemNotNull] List<ModComponent> components
		)
		{
			if (components is null)
				throw new ArgumentNullException( nameof( components ) );
			Dictionary<Guid, GraphNode> nodeMap = CreateDependencyGraph( components );
			HashSet<GraphNode> permanentMark = new HashSet<GraphNode>();
			HashSet<GraphNode> temporaryMark = new HashSet<GraphNode>();
			if (nodeMap.Values.Where( node => !permanentMark.Contains( node ) ).Any( node => HasCycle( node, permanentMark, temporaryMark ) ))
				throw new KeyNotFoundException( "Circular dependency detected in component ordering" );
			HashSet<GraphNode> visitedNodes = new HashSet<GraphNode>();
			List<ModComponent> orderedComponents = new List<ModComponent>();
			foreach (GraphNode node in nodeMap.Values.Where( node => !visitedNodes.Contains( node ) ))
			{
				DepthFirstSearch( node, visitedNodes, orderedComponents );
			}
			bool isCorrectOrder = orderedComponents.SequenceEqual( components );
			return (isCorrectOrder, orderedComponents);
		}
		private static bool HasCycle(
			[NotNull] GraphNode node,
			[NotNull] ISet<GraphNode> permanentMark,
			[NotNull] ISet<GraphNode> temporaryMark
		)
		{
			if (permanentMark.Contains( node ))
				return false;
			if (temporaryMark.Contains( node ))
				return true;
			_ = temporaryMark.Add( node );
			foreach (GraphNode dependency in node.Dependencies)
			{
				if (HasCycle( dependency, permanentMark, temporaryMark ))
					return true;
			}
			_ = temporaryMark.Remove( node );
			_ = permanentMark.Add( node );
			return false;
		}
		private static void DepthFirstSearch(
			[NotNull] GraphNode node,
			[NotNull] ISet<GraphNode> visitedNodes,
			[NotNull] ICollection<ModComponent> orderedComponents
		)
		{
			if (node is null)
				throw new ArgumentNullException( nameof( node ) );
			if (visitedNodes is null)
				throw new ArgumentNullException( nameof( visitedNodes ) );
			if (orderedComponents is null)
				throw new ArgumentNullException( nameof( orderedComponents ) );
			_ = visitedNodes.Add( node );
			foreach (GraphNode dependency in node.Dependencies.Where( dependency => !visitedNodes.Contains( dependency ) ))
			{
				DepthFirstSearch( dependency, visitedNodes, orderedComponents );
			}
			orderedComponents.Add( node.ModComponent );
		}
		private static Dictionary<Guid, GraphNode> CreateDependencyGraph(
			[NotNull][ItemNotNull] List<ModComponent> components
		)
		{
			if (components is null)
				throw new ArgumentNullException( nameof( components ) );
			Dictionary<Guid, GraphNode> nodeMap = new Dictionary<Guid, GraphNode>();
			foreach (ModComponent component in components)
			{
				GraphNode node = new GraphNode( component );
				nodeMap[component.Guid] = node;
			}
			foreach (ModComponent component in components)
			{
				GraphNode node = nodeMap[component.Guid];
				foreach (Guid dependencyGuid in component.InstallAfter)
				{
					if (!nodeMap.TryGetValue( dependencyGuid, out GraphNode dependencyNode ))
					{
						Logger.LogWarning( $"ModComponent '{component.Name}' references InstallAfter GUID {dependencyGuid} which is not in the current component list" );
						continue;
					}
					_ = node?.Dependencies?.Add( dependencyNode );
				}
				foreach (Guid dependentGuid in component.InstallBefore)
				{
					if (!nodeMap.TryGetValue( dependentGuid, out GraphNode dependentNode ))
					{
						Logger.LogWarning( $"ModComponent '{component.Name}' references InstallBefore GUID {dependentGuid} which is not in the current component list" );
						continue;
					}
					_ = dependentNode?.Dependencies?.Add( node );
				}
			}
			return nodeMap;
		}
		public void CreateInstruction( int index = 0 )
		{
			Instruction instruction = new Instruction();
			if (Instructions.IsNullOrEmptyOrAllNull())
			{
				if (index != 0)
				{
					Logger.LogError( "Cannot create instruction at index when list is empty." );
					return;
				}
				Instructions.Add( instruction );
			}
			else
			{
				Instructions.Insert( index, instruction );
			}
			instruction.SetParentComponent( this );
		}
		public void DeleteInstruction( int index ) => Instructions.RemoveAt( index );
		public void DeleteOption( int index ) => Options.RemoveAt( index );
		public void MoveInstructionToIndex( [NotNull] Instruction thisInstruction, int index )
		{
			if (thisInstruction is null || index < 0 || index >= Instructions.Count)
				throw new ArgumentException( "Invalid instruction or index." );
			int currentIndex = Instructions.IndexOf( thisInstruction );
			if (currentIndex < 0)
				throw new ArgumentException( "Instruction does not exist in the list." );
			if (index == currentIndex)
			{
				_ = Logger.LogAsync(
					$"Cannot move Instruction '{thisInstruction.Action}' from {currentIndex} to {index}. Reason: Indices are the same."
				);
				return;
			}
			Instructions.RemoveAt( currentIndex );
			Instructions.Insert( index, thisInstruction );
			_ = Logger.LogVerboseAsync( $"Instruction '{thisInstruction.Action}' moved from {currentIndex} to {index}" );
		}
		public void CreateOption( int index = 0 )
		{
			Option option = new Option
			{
				Name = Path.GetFileNameWithoutExtension( Path.GetTempFileName() ),
				Guid = Guid.NewGuid(),
			};
			if (Instructions.IsNullOrEmptyOrAllNull())
			{
				if (index != 0)
				{
					Logger.LogError( "Cannot create option at index when list is empty." );
					return;
				}
				Options.Add( option );
			}
			else
			{
				Options.Insert( index, option );
			}
		}
		public void MoveOptionToIndex( [NotNull] Option thisOption, int index )
		{
			if (thisOption is null || index < 0 || index >= Options.Count)
				throw new ArgumentException( "Invalid option or index." );
			int currentIndex = Options.IndexOf( thisOption );
			if (currentIndex < 0)
				throw new ArgumentException( "Option does not exist in the list." );
			if (index == currentIndex)
			{
				_ = Logger.LogAsync(
					$"Cannot move Option '{thisOption.Name}' from {currentIndex} to {index}. Reason: Indices are the same."
				);
				return;
			}
			Options.RemoveAt( currentIndex );
			Options.Insert( index, thisOption );
			_ = Logger.LogVerboseAsync( $"Option '{thisOption.Name}' moved from {currentIndex} to {index}" );
		}
		public class GraphNode
		{
			public GraphNode( [CanBeNull] ModComponent component )
			{
				ModComponent = component;
				Dependencies = new HashSet<GraphNode>();
			}
			public ModComponent ModComponent { get; }
			public HashSet<GraphNode> Dependencies { get; }
		}
	}

	/// <summary>
	/// Metadata for content-addressable resource tracking.
	/// Supports dual-key lookup (MetadataHash before download, ContentId after).
	/// </summary>
	public class ResourceMetadata
	{
		/// <summary>Current lookup key (MetadataHash initially, ContentId after download)</summary>
		public string ContentKey { get; set; }

		/// <summary>SHA-1 of bencoded info dict (BitTorrent infohash) - null pre-download</summary>
		public string ContentId { get; set; }

		/// <summary>SHA-256 of file bytes - CANONICAL for integrity (null pre-download)</summary>
		public string ContentHashSHA256 { get; set; }

		/// <summary>SHA-256 of canonical provider metadata</summary>
		public string MetadataHash { get; set; }

		/// <summary>Primary download URL</summary>
		public string PrimaryUrl { get; set; }

		/// <summary>Provider-specific metadata (normalized)</summary>
		[NotNull]
		public Dictionary<string, object> HandlerMetadata { get; set; } = new Dictionary<string, object>( StringComparer.Ordinal );

		/// <summary>Files contained in this resource (filename -> exists in archive)</summary>
		[NotNull]
		public Dictionary<string, bool?> Files { get; set; } = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase );

		/// <summary>File size in bytes</summary>
		public long FileSize { get; set; }

		/// <summary>Bytes per piece (from DeterminePieceSize)</summary>
		public int PieceLength { get; set; }

		/// <summary>Hex-encoded concatenated SHA-1 hashes (20 bytes per piece)</summary>
		public string PieceHashes { get; set; }

		/// <summary>First time this resource was observed</summary>
		public DateTime? FirstSeen { get; set; }

		/// <summary>Last time integrity was verified</summary>
		public DateTime? LastVerified { get; set; }

		/// <summary>Schema version for migration</summary>
		public int SchemaVersion { get; set; } = 1;

		/// <summary>Trust level for mapping verification</summary>
		public MappingTrustLevel TrustLevel { get; set; } = MappingTrustLevel.Unverified;
	}

	/// <summary>
	/// Trust level for MetadataHash → ContentId mappings.
	/// Elevated through independent verification.
	/// </summary>
	public enum MappingTrustLevel
	{
		/// <summary>Initial state - not yet verified</summary>
		Unverified = 0,

		/// <summary>Seen from one source</summary>
		ObservedOnce = 1,

		/// <summary>Verified from 2+ independent sources</summary>
		Verified = 2
	}
}