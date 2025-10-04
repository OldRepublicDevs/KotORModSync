using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace KOTORModSync.Core.Parsing
{
	public enum RegexMode
	{
		Individual, // Use individual pattern fields
		Raw,         // Use single raw regex pattern
	}

	public sealed class MarkdownImportProfile : INotifyPropertyChanged
	{
		private string _rawRegexPattern = string.Empty;
		public string RawRegexPattern
		{
			get => _rawRegexPattern;
			set { _rawRegexPattern = value; OnPropertyChanged(); }
		}

		private RegexOptions _rawRegexOptions = RegexOptions.Multiline;
		public RegexOptions RawRegexOptions
		{
			get => _rawRegexOptions;
			set { _rawRegexOptions = value; OnPropertyChanged(); }
		}

		private RegexMode _mode = RegexMode.Raw;
		public RegexMode Mode
		{
			get => _mode;
			set { _mode = value; OnPropertyChanged(); }
		}

		private bool _globalFlag = true;
		public bool GlobalFlag
		{
			get => _globalFlag;
			set { _globalFlag = value; OnPropertyChanged(); }
		}

		private bool _multilineFlag = true;
		public bool MultilineFlag
		{
			get => _multilineFlag;
			set { _multilineFlag = value; OnPropertyChanged(); }
		}

		private bool _ignoreCaseFlag;
		public bool IgnoreCaseFlag
		{
			get => _ignoreCaseFlag;
			set { _ignoreCaseFlag = value; OnPropertyChanged(); }
		}

		private bool _singlelineFlag = true;
		public bool SinglelineFlag
		{
			get => _singlelineFlag;
			set { _singlelineFlag = value; OnPropertyChanged(); }
		}

		private string _headingPattern = string.Empty;
		public string HeadingPattern
		{
			get => _headingPattern;
			set { _headingPattern = value; OnPropertyChanged(); }
		}

		private string _componentSectionPattern = string.Empty;
		public string ComponentSectionPattern
		{
			get => _componentSectionPattern;
			set { _componentSectionPattern = value; OnPropertyChanged(); }
		}

		private RegexOptions _componentSectionOptions = RegexOptions.Multiline | RegexOptions.Singleline;
		public RegexOptions ComponentSectionOptions
		{
			get => _componentSectionOptions;
			set { _componentSectionOptions = value; OnPropertyChanged(); }
		}

		private string _namePattern = string.Empty;
		public string NamePattern
		{
			get => _namePattern;
			set { _namePattern = value; OnPropertyChanged(); }
		}

		private string _authorPattern = string.Empty;
		public string AuthorPattern
		{
			get => _authorPattern;
			set { _authorPattern = value; OnPropertyChanged(); }
		}

		private string _descriptionPattern = string.Empty;
		public string DescriptionPattern
		{
			get => _descriptionPattern;
			set { _descriptionPattern = value; OnPropertyChanged(); }
		}

		private string _modLinkPattern = string.Empty;
		public string ModLinkPattern
		{
			get => _modLinkPattern;
			set { _modLinkPattern = value; OnPropertyChanged(); }
		}

		private string _categoryTierPattern = string.Empty;
		public string CategoryTierPattern
		{
			get => _categoryTierPattern;
			set { _categoryTierPattern = value; OnPropertyChanged(); }
		}

		private string _installationMethodPattern = string.Empty;
		public string InstallationMethodPattern
		{
			get => _installationMethodPattern;
			set { _installationMethodPattern = value; OnPropertyChanged(); }
		}

		private string _installationInstructionsPattern = string.Empty;
		public string InstallationInstructionsPattern
		{
			get => _installationInstructionsPattern;
			set { _installationInstructionsPattern = value; OnPropertyChanged(); }
		}

		private string _nonEnglishPattern = string.Empty;
		public string NonEnglishPattern
		{
			get => _nonEnglishPattern;
			set { _nonEnglishPattern = value; OnPropertyChanged(); }
		}

		private string _dependenciesPattern = string.Empty;
		public string DependenciesPattern
		{
			get => _dependenciesPattern;
			set { _dependenciesPattern = value; OnPropertyChanged(); }
		}

		private string _restrictionsPattern = string.Empty;
		public string RestrictionsPattern
		{
			get => _restrictionsPattern;
			set { _restrictionsPattern = value; OnPropertyChanged(); }
		}

		private string _optionPattern = string.Empty;
		public string OptionPattern
		{
			get => _optionPattern;
			set { _optionPattern = value; OnPropertyChanged(); }
		}


		private string _instructionPattern = string.Empty;
		public string InstructionPattern
		{
			get => _instructionPattern;
			set { _instructionPattern = value; OnPropertyChanged(); }
		}


		public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();

		public RegexOptions GetRegexOptions()
		{
			RegexOptions options = RegexOptions.Compiled;

			if ( MultilineFlag ) options |= RegexOptions.Multiline;
			if ( SinglelineFlag ) options |= RegexOptions.Singleline;
			if ( IgnoreCaseFlag ) options |= RegexOptions.IgnoreCase;
			// Note: Global flag is handled differently in .NET (using Matches vs Match)

			return options;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		public MarkdownImportProfile Clone()
		{
			var clone = (MarkdownImportProfile)MemberwiseClone();
			clone.Metadata.Clear();
			foreach ( KeyValuePair<string, object> pair in Metadata )
			{
				clone.Metadata[pair.Key] = pair.Value;
			}
			clone.PropertyChanged = null; // Don't copy event handlers
			return clone;
		}

		public static MarkdownImportProfile CreateDefault()
		{
			// Matches a mod section starting with ### heading and ending before the next ___ separator or end of string
			const string defaultRawPattern = @"(?ms)^###\s*(?<heading>.+?)\s*\r?\n(?:[\s\S]*?\*\*Name:\*\*\s*(?:\[(?<name>(?<name_link>[^\]]+))\]\([^)]+\)|(?<name_plain>.*?))(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?(?:[\s\S]*?\*\*Author:\*\*\s*(?<author>.*?)(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?(?:[\s\S]*?\*\*Description:\*\*\s*(?<description>.*?)(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?(?:[\s\S]*?\*\*Masters:\*\*\s*(?<masters>.*?)(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?(?:[\s\S]*?\*\*Category\s*&\s*Tier:\*\*\s*(?<category_tier>.*?)(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?(?:[\s\S]*?\*\*Non-English Functionality:\*\*\s*(?<non_english>.*?)(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?(?:[\s\S]*?\*\*Installation Method:\*\*\s*(?<installation_method>.*?)(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?(?:[\s\S]*?\*\*Installation Instructions:\*\*\s*(?<installation_instructions>.*?)(?=\r?\n\s*\*\*[^:\n]{1,100}:\*\*|\r?\n\s*(?:-{3,}|_{3,})|\Z))?[\s\S]*?(?=\r?\n\s*(?:-{3,}|_{3,})|\Z)";
			// Outer pattern: Match ### heading until ___
			const string defaultOuterPattern = @"(?m)^###\s*.+?$[\s\S]*?(?=^___\s*$)";

			return new MarkdownImportProfile
			{
				Mode = RegexMode.Individual,
				ComponentSectionPattern = defaultOuterPattern,
				ComponentSectionOptions = RegexOptions.Multiline,
				RawRegexPattern = defaultRawPattern,
				RawRegexOptions = RegexOptions.Multiline | RegexOptions.Singleline,
				NamePattern = @"\*\*Name:\*\*\s*(?:\[(?<name>(?<name_link>[^\]]+))\]\([^)]+\)|(?<name_plain>[^\r\n]+))",
				AuthorPattern = @"\*\*Author:\*\*\s*(?<author>[^\r\n]+)",
				DescriptionPattern = @"\*\*Description:\*\*\s*(?<description>(?:(?!\r?\n\s*(?:\*\*\w+[^:]*:\*\*|_{3,}|-{3,}|##)).)*)",
				ModLinkPattern = @"\[(?<label>[^]]+)\]\((?<link>[^)]+)\)",
				CategoryTierPattern = @"\*\*Category\s*&\s*Tier:\*\*\s*(?<category>[^/\r\n]+)/\s*(?<tier>[^\r\n]+)",
				InstallationMethodPattern = @"\*\*Installation Method:\*\*\s*(?<method>[^\r\n]+)",
				InstallationInstructionsPattern = @"\*\*Installation Instructions:\*\*\s*(?<directions>(?:(?!\r?\n\s*(?:\*\*\w+[^:]*:\*\*|_{3,}|-{3,}|##)).)*)",
				NonEnglishPattern = @"\*\*Non-English Functionality:\*\*\s*(?<value>[^\r\n]+)",
				DependenciesPattern = @"\*\*Masters:\*\*\s*(?<masters>[^\r\n]+)",
				RestrictionsPattern = string.Empty,
				OptionPattern = string.Empty,
				InstructionPattern = string.Empty,
				// Regex flags (defaulting to /gms)
				GlobalFlag = true,
				MultilineFlag = true,
				SinglelineFlag = true,
				IgnoreCaseFlag = false,
			};
		}
	}
}

