



using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core
{
	
	
	
	[SuppressMessage(
		category: "Performance",
		checkId: "CA1822:Mark members as static",
		Justification = "unique naming scheme used for class"
	)]
	[SuppressMessage(
		category: "CodeQuality",
		checkId: "IDE0079:Remove unnecessary suppression",
		Justification = "<Pending>"
	)]
	[SuppressMessage(category: "ReSharper", checkId: "MemberCanBeMadeStatic.Global")]
	[SuppressMessage(category: "ReSharper", checkId: "InconsistentNaming")]
	[SuppressMessage(category: "ReSharper", checkId: "MemberCanBePrivate.Global")]
	[SuppressMessage(category: "ReSharper", checkId: "UnusedMember.Global")]
	public sealed class MainConfig : INotifyPropertyChanged
	{

		public MainConfig()
		{
			debugLogging = false;
			attemptFixes = true;
			noAdmin = false;
			caseInsensitivePathing = true;
			validateAndReplaceInvalidArchives = true;
			filterDownloadsByResolution = true;
		}

		[NotNull]
		public static string CurrentVersion => "2.0.0b1";


		public static bool NoAdmin { get; private set; }

		public bool noAdmin
		{
			get => NoAdmin;
			set => NoAdmin = value;
		}

		public bool useCopyForMoveActions
		{
			get => UseCopyForMoveActions;
			set => UseCopyForMoveActions = value;
		}

		public static bool UseCopyForMoveActions { get; private set; }

		public static bool UseMultiThreadedIO { get; private set; }
		public bool useMultiThreadedIO { get => UseMultiThreadedIO; set => UseMultiThreadedIO = value; }

		public static bool CaseInsensitivePathing { get; private set; }

		public bool caseInsensitivePathing
		{
			get => CaseInsensitivePathing;
			set => CaseInsensitivePathing = Utility.Utility.GetOperatingSystem() != OSPlatform.Windows;
		}

		public static bool DebugLogging { get; private set; }
		public bool debugLogging { get => DebugLogging; set => DebugLogging = value; }

		public static DirectoryInfo LastOutputDirectory { get; private set; }

		[CanBeNull]
		public DirectoryInfo lastOutputDirectory
		{
			get => LastOutputDirectory;
			set => LastOutputDirectory = value;
		}

		public static bool AttemptFixes { get; private set; }
		public bool attemptFixes { get => AttemptFixes; set => AttemptFixes = value; }

		public static bool ArchiveDeepCheck { get; private set; }
		public bool archiveDeepCheck { get => ArchiveDeepCheck; set => ArchiveDeepCheck = value; }

		public static bool ValidateAndReplaceInvalidArchives { get; private set; }
		public bool validateAndReplaceInvalidArchives { get => ValidateAndReplaceInvalidArchives; set => ValidateAndReplaceInvalidArchives = value; }

		public static bool FilterDownloadsByResolution { get; private set; }
		public bool filterDownloadsByResolution { get => FilterDownloadsByResolution; set => FilterDownloadsByResolution = value; }

		public static string NexusModsApiKey { get; private set; }
		public string nexusModsApiKey { get => NexusModsApiKey; set => NexusModsApiKey = value; }

		[NotNull][ItemNotNull] public static List<ModComponent> AllComponents { get; set; } = new List<ModComponent>();

		[NotNull]
		[ItemNotNull]
		public List<ModComponent> allComponents
		{
			get => AllComponents;
			set => AllComponents = value ?? throw new ArgumentNullException(nameof(value));
		}

		[CanBeNull] public static DirectoryInfo SourcePath { get; private set; }

		[CanBeNull]
		public DirectoryInfo sourcePath
		{
			get => SourcePath;
			set
			{
				SourcePath = value;
				OnPropertyChanged(nameof(sourcePathFullName));
			}
		}

		[CanBeNull] public string sourcePathFullName => SourcePath?.FullName;

		[CanBeNull] public static DirectoryInfo DestinationPath { get; private set; }

		[CanBeNull]
		public DirectoryInfo destinationPath
		{
			get => DestinationPath;
			set
			{
				DestinationPath = value;
				OnPropertyChanged(nameof(destinationPathFullName));
			}
		}

		[CanBeNull] public string destinationPathFullName => DestinationPath?.FullName;

		
		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
