// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
	public class RegexImportDialogViewModel : INotifyPropertyChanged
	{
		private readonly string _markdown;
		private string _previewSummary;
		private string _previewMarkdown;
		private ObservableCollection<Inline> _highlightedPreview;
		private int _selectedTabIndex;

		public MergeHeuristicsOptions Heuristics { get; private set; }

		public MarkdownImportProfile ConfiguredProfile => Profile;

		public RegexImportDialogViewModel( [NotNull] string markdown, [NotNull] MarkdownImportProfile profile )
		{
			_markdown = markdown;
			Profile = profile;
			Profile.PropertyChanged += OnProfilePropertyChanged;
			Heuristics = MergeHeuristicsOptions.CreateDefault();
			PreviewMarkdown = _markdown;
			RecomputePreview();
		}

		public void ResetDefaults()
		{
			Profile.PropertyChanged -= OnProfilePropertyChanged;
			Profile = MarkdownImportProfile.CreateDefault();
			Profile.PropertyChanged += OnProfilePropertyChanged;
			OnPropertyChanged( nameof( Profile ) );
			RecomputePreview();
		}

		public MarkdownImportProfile Profile { get; private set; }

		public string PreviewSummary
		{
			get => _previewSummary;
			private set
			{
				if (string.Equals( _previewSummary, value, StringComparison.Ordinal )) return;
				_previewSummary = value;
				OnPropertyChanged();
			}
		}

		public string PreviewMarkdown
		{
			get => _previewMarkdown;
			set
			{
				if (string.Equals( _previewMarkdown, value, StringComparison.Ordinal )) return;
				_previewMarkdown = value;
				OnPropertyChanged();
			}
		}

		public ObservableCollection<Inline> HighlightedPreview
		{
			get => _highlightedPreview;
			set
			{
				if (_highlightedPreview == value) return;
				_highlightedPreview = value;
				Logger.LogVerbose( $"HighlightedPreview set with {value?.Count ?? 0} inlines" );
				OnPropertyChanged();
			}
		}

		[UsedImplicitly]
		public int SelectedTabIndex
		{
			get => _selectedTabIndex;
			set
			{
				if (_selectedTabIndex == value) return;
				_selectedTabIndex = value;
				OnPropertyChanged();

				Profile.Mode = value == 0 ? RegexMode.Individual : RegexMode.Raw;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void RecomputePreview()
		{
			// Preview should only show regex matches, not parse components
			// Actual component parsing only happens when user clicks "Load"
			
			// Count regex matches for preview summary without actually parsing
			int componentMatches = 0;
			int linkMatches = 0;
			
			try
			{
				if (Profile.Mode == RegexMode.Raw)
				{
					// Count component sections in raw mode
					if (!string.IsNullOrWhiteSpace(Profile.RawRegexPattern))
					{
						var regex = new Regex(Profile.RawRegexPattern, Profile.RawRegexOptions);
						componentMatches = regex.Matches(_markdown).Count;
					}
				}
				else
				{
					// Count component sections in individual mode
					if (!string.IsNullOrWhiteSpace(Profile.ComponentSectionPattern))
					{
						var regex = new Regex(Profile.ComponentSectionPattern, Profile.ComponentSectionOptions);
						componentMatches = regex.Matches(_markdown).Count;
					}
				}
				
				// Count ModLink matches
				if (!string.IsNullOrWhiteSpace(Profile.ModLinkPattern))
				{
					var linkRegex = new Regex(Profile.ModLinkPattern, Profile.GetRegexOptions());
					linkMatches = linkRegex.Matches(_markdown).Count;
				}
			}
			catch (Exception ex)
			{
				Logger.LogVerbose($"Preview regex error: {ex.Message}");
			}
			
			PreviewSummary = $"Component Matches: {componentMatches} | Link Matches: {linkMatches}";
			GenerateHighlightedPreview();
		}

		private void GenerateHighlightedPreview()
		{
			var inlines = new ObservableCollection<Inline>();

			try
			{
				// Helper function to get theme resources
				IBrush GetResource( string key, IBrush fallback ) =>
					Application.Current?.TryGetResource( key, Application.Current?.ActualThemeVariant, out object value ) == true && value is IBrush b ? b : fallback;

				string patternToUse = Profile.Mode == RegexMode.Raw
					? Profile.RawRegexPattern
					: Profile.ComponentSectionPattern;

				if (string.IsNullOrWhiteSpace( patternToUse ))
				{
					// Use theme resource for default text color
					inlines.Add( new Run( _markdown ) { Foreground = GetResource( "RegexHighlight.Default", Brushes.Black ) } );
					HighlightedPreview = inlines;
					return;
				}
				// Get all theme resource colors - GetResource will use theme-specific colors if available, 
				// or fallback to FluentLightStyle colors if not (which happen to match standard syntax highlighting)
				var groupColors = new Dictionary<string, IBrush>( StringComparer.Ordinal )
				{
					["heading"] = GetResource( "RegexHighlight.Heading", new SolidColorBrush( Color.FromRgb( 0x00, 0x78, 0xD4 ) ) ),
					["name"] = GetResource( "RegexHighlight.Name", new SolidColorBrush( Color.FromRgb( 0x00, 0x78, 0xD4 ) ) ),
					["name_link"] = GetResource( "RegexHighlight.Name", new SolidColorBrush( Color.FromRgb( 0x00, 0x78, 0xD4 ) ) ),
					["name_plain"] = GetResource( "RegexHighlight.Name", new SolidColorBrush( Color.FromRgb( 0x00, 0x78, 0xD4 ) ) ),
					["author"] = GetResource( "RegexHighlight.Author", new SolidColorBrush( Color.FromRgb( 0x00, 0x5A, 0x9E ) ) ),
					["description"] = GetResource( "RegexHighlight.Description", new SolidColorBrush( Color.FromRgb( 0xCA, 0x50, 0x10 ) ) ),
					["masters"] = GetResource( "RegexHighlight.Masters", new SolidColorBrush( Color.FromRgb( 0xF7, 0x63, 0x0C ) ) ),
					["category_tier"] = GetResource( "RegexHighlight.CategoryTier", new SolidColorBrush( Color.FromRgb( 0x7B, 0x2E, 0xBF ) ) ),
					["non_english"] = GetResource( "RegexHighlight.NonEnglish", new SolidColorBrush( Color.FromRgb( 0x60, 0x5E, 0x5C ) ) ),
					["installation_method"] = GetResource( "RegexHighlight.InstallationMethod", new SolidColorBrush( Color.FromRgb( 0x10, 0x7C, 0x10 ) ) ),
					["installation_instructions"] = GetResource( "RegexHighlight.InstallationInstructions", new SolidColorBrush( Color.FromRgb( 0xD8, 0x3B, 0x01 ) ) )
				};

				Logger.LogVerbose( $"GenerateHighlightedPreview: Mode={Profile.Mode}" );

				// Get default text color from theme
				IBrush defaultTextColor = GetResource( "RegexHighlight.Default", Brushes.Black );

				if (Profile.Mode == RegexMode.Raw)
				{
					var regex = new Regex( patternToUse, Profile.RawRegexOptions );
					MatchCollection matches = regex.Matches( _markdown );
					Logger.LogVerbose( $"Found {matches.Count} RAW matches with pattern: {patternToUse.Substring( 0, Math.Min( 80, patternToUse.Length ) )}..." );
					int lastIndex = 0;

					foreach (Match match in matches)
					{
						if (match.Index > lastIndex)
						{
							string beforeText = _markdown.Substring( lastIndex, match.Index - lastIndex );
							inlines.Add( new Run( beforeText ) { Foreground = defaultTextColor } );
						}

						var processedRanges = new List<(int start, int end)>();
						foreach (string groupName in groupColors.Keys)
						{
							Group group = match.Groups[groupName];
							if (!group.Success || group.Length <= 0)
								continue;
							Logger.LogVerbose( $"RAW group '{groupName}': '{group.Value}'" );
							bool overlaps = processedRanges.Exists( range => !(group.Index >= range.end || group.Index + group.Length <= range.start) );
							if (overlaps)
								continue;
							int relativeStart = group.Index - match.Index;
							if (relativeStart > 0)
							{
								string beforeGroup = match.Value.Substring( 0, relativeStart );
								if (!string.IsNullOrEmpty( beforeGroup )) inlines.Add( new Run( beforeGroup ) { Foreground = defaultTextColor } );
							}
							inlines.Add( new Run( group.Value ) { Foreground = groupColors[groupName], FontWeight = FontWeight.Bold } );
							processedRanges.Add( (group.Index, group.Index + group.Length) );
						}

						if (processedRanges.Count == 0) inlines.Add( new Run( match.Value ) { Foreground = defaultTextColor } );
						lastIndex = match.Index + match.Length;
					}

					if (lastIndex < _markdown.Length)
					{
						string remainingText = _markdown.Substring( lastIndex );
						inlines.Add( new Run( remainingText ) { Foreground = defaultTextColor } );
					}
				}
				else
				{

					var sectionRegex = new Regex( Profile.ComponentSectionPattern, Profile.ComponentSectionOptions );
					MatchCollection sections = sectionRegex.Matches( _markdown );
					Logger.LogVerbose( $"Found {sections.Count} section matches (INDIVIDUAL mode)" );
					int cursor = 0;

					foreach (Match section in sections)
					{
						if (section.Index > cursor)
							inlines.Add( new Run( _markdown.Substring( cursor, section.Index - cursor ) ) { Foreground = defaultTextColor } );

						string sectionText;
						int sectionTextStartInDoc;
						Group contentGroup = section.Groups["content"];
						if (contentGroup.Success)
						{
							sectionText = contentGroup.Value;
							sectionTextStartInDoc = contentGroup.Index;
						}
						else
						{
							sectionText = section.Value;
							sectionTextStartInDoc = section.Index;
						}

						Logger.LogVerbose( $"Processing section at {sectionTextStartInDoc} length {sectionText.Length}" );

						var ranges = new List<(int start, int end, IBrush brush)>();

						void AddGroupRange( string pattern, string groupName, IBrush brush )
						{
							if (string.IsNullOrWhiteSpace( pattern )) return;
							var r = new Regex( pattern, Profile.GetRegexOptions() );
							foreach (Match m in r.Matches( sectionText ))
							{
								Group g = m.Groups[groupName];
								if (g.Success && g.Length > 0)
								{
									int absStart = sectionTextStartInDoc + g.Index;
									int absEnd = absStart + g.Length;
									ranges.Add( (absStart, absEnd, brush) );
									Logger.LogVerbose( $"IND group '{groupName}' at {absStart}-{absEnd}: '{g.Value}'" );
								}
							}
						}

						AddGroupRange( Profile.HeadingPattern, "heading", groupColors["heading"] );
						AddGroupRange( Profile.NamePattern, "name", groupColors["name"] );
						AddGroupRange( Profile.AuthorPattern, "author", groupColors["author"] );
						AddGroupRange( Profile.DescriptionPattern, "description", groupColors["description"] );
						AddGroupRange( Profile.DependenciesPattern, "masters", groupColors["masters"] );
						AddGroupRange( Profile.CategoryTierPattern, "category_tier", groupColors["category_tier"] );
						AddGroupRange( Profile.NonEnglishPattern, "non_english", groupColors["non_english"] );
						AddGroupRange( Profile.InstallationMethodPattern, "installation_method", groupColors["installation_method"] );
						AddGroupRange( Profile.InstallationInstructionsPattern, "installation_instructions", groupColors["installation_instructions"] );

						ranges = ranges.OrderBy( r => r.start ).ThenByDescending( r => r.end - r.start ).ToList();

						int pos = sectionTextStartInDoc;
						foreach ((int start, int end, IBrush brush) in ranges)
						{
							if (start < pos) continue;
							if (start > pos)
								inlines.Add( new Run( _markdown.Substring( pos, start - pos ) ) { Foreground = defaultTextColor } );
							inlines.Add( new Run( _markdown.Substring( start, end - start ) ) { Foreground = brush, FontWeight = FontWeight.Bold } );
							pos = end;
						}

						int sectionEnd = section.Index + section.Length;
						if (pos < sectionEnd)
							inlines.Add( new Run( _markdown.Substring( pos, sectionEnd - pos ) ) { Foreground = defaultTextColor } );

						cursor = section.Index + section.Length;
					}

					if (cursor < _markdown.Length)
						inlines.Add( new Run( _markdown.Substring( cursor ) ) { Foreground = defaultTextColor } );
				}
			}
			catch (Exception ex)
			{
				inlines.Clear();
				IBrush GetResource( string key, IBrush fallback ) =>
					Application.Current?.TryGetResource( key, Application.Current?.ActualThemeVariant, out object value ) == true && value is IBrush b ? b : fallback;
				IBrush defaultTextColor = GetResource( "RegexHighlight.Default", Brushes.Black );
				inlines.Add( new Run( _markdown ) { Foreground = defaultTextColor } );
				inlines.Add( new Run( $"\n\n[Regex Error: {ex.Message}]" ) { Foreground = Brushes.Red } );
			}

			HighlightedPreview = inlines;
		}

		public void OnProfileChanged() => RecomputePreview();

		private void OnProfilePropertyChanged( object sender, PropertyChangedEventArgs e ) => RecomputePreview();

		public MarkdownParserResult ConfirmLoad()
		{
			// Parse the markdown with the configured profile when user clicks "Load"
			// This is the ONLY time we actually parse into ModComponent objects
			var parser = new MarkdownParser( Profile,
				logInfo => Logger.Log( logInfo ),
				Logger.LogVerbose );
			return parser.Parse( _markdown );
		}

		private void OnPropertyChanged( [CallerMemberName][CanBeNull] string name = null ) =>
			PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( name ) );
	}
}