// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

		public RegexImportDialogViewModel([NotNull] string markdown, [NotNull] MarkdownImportProfile profile)
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
			OnPropertyChanged(nameof(Profile));
			RecomputePreview();
		}

		public MarkdownImportProfile Profile { get; private set; }

		public string PreviewSummary
		{
			get => _previewSummary;
			private set
			{
				if (_previewSummary == value) return;
				_previewSummary = value;
				OnPropertyChanged();
			}
		}

		public string PreviewMarkdown
		{
			get => _previewMarkdown;
			set
			{
				if ( _previewMarkdown == value ) return;
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
				Logger.LogVerbose($"HighlightedPreview set with {value?.Count ?? 0} inlines");
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
			var parser = new MarkdownParser(Profile,
				logInfo => Logger.Log(logInfo),
				Logger.LogVerbose);
			MarkdownParserResult result = parser.Parse(_markdown);
			int comp = result.Components?.Count ?? 0;
			Debug.Assert(result.Components != null, "result.Components != null");
			int links = result.Components.Sum(c => c.ModLinkFilenames.Count);
			PreviewSummary = $"Components: {comp} | Links: {links}";

			GenerateHighlightedPreview();
			UpdateCounts();
		}

		private void GenerateHighlightedPreview()
		{
			var inlines = new ObservableCollection<Inline>();

			try
			{
				string patternToUse = Profile.Mode == RegexMode.Raw
					? Profile.RawRegexPattern
					: Profile.ComponentSectionPattern;

				if ( string.IsNullOrWhiteSpace(patternToUse) )
				{
					inlines.Add(new Run(_markdown) { Foreground = Brushes.White });
					HighlightedPreview = inlines;
					return;
				}

				IBrush GetResource(string key, IBrush fallback) =>
					Application.Current?.TryGetResource(key, Application.Current?.ActualThemeVariant, out object value) == true && value is IBrush b ? b : fallback;
				var groupColors = new Dictionary<string, IBrush>
				{
					["heading"] = GetResource("RegexHighlight.Heading", Brushes.LightBlue),
					["name"] = GetResource("RegexHighlight.Name", Brushes.LightGreen),
					["name_link"] = GetResource("RegexHighlight.Name", Brushes.LightGreen),
					["name_plain"] = GetResource("RegexHighlight.Name", Brushes.LightGreen),
					["author"] = GetResource("RegexHighlight.Author", Brushes.LightCoral),
					["description"] = GetResource("RegexHighlight.Description", Brushes.LightYellow),
					["masters"] = GetResource("RegexHighlight.Masters", Brushes.LightPink),
					["category_tier"] = GetResource("RegexHighlight.CategoryTier", Brushes.LightCyan),
					["non_english"] = GetResource("RegexHighlight.NonEnglish", Brushes.LightGray),
					["installation_method"] = GetResource("RegexHighlight.InstallationMethod", Brushes.LightSalmon),
					["installation_instructions"] = GetResource("RegexHighlight.InstallationInstructions", Brushes.LightSeaGreen)
				};

				Logger.LogVerbose($"GenerateHighlightedPreview: Mode={Profile.Mode}");

				if ( Profile.Mode == RegexMode.Raw )
				{
					var regex = new Regex(patternToUse, Profile.RawRegexOptions);
					MatchCollection matches = regex.Matches(_markdown);
					Logger.LogVerbose($"Found {matches.Count} RAW matches with pattern: {patternToUse.Substring(0, Math.Min(80, patternToUse.Length))}...");
					int lastIndex = 0;

					foreach ( Match match in matches )
					{
						if ( match.Index > lastIndex )
						{
							string beforeText = _markdown.Substring(lastIndex, match.Index - lastIndex);
							inlines.Add(new Run(beforeText) { Foreground = Brushes.White });
						}

						var processedRanges = new List<(int start, int end)>();
						foreach ( string groupName in groupColors.Keys )
						{
							Group group = match.Groups[groupName];
							if ( !group.Success || group.Length <= 0 )
								continue;
							Logger.LogVerbose($"RAW group '{groupName}': '{group.Value}'");
							bool overlaps = processedRanges.Any(range => !(group.Index >= range.end || group.Index + group.Length <= range.start));
							if ( overlaps )
								continue;
							int relativeStart = group.Index - match.Index;
							if ( relativeStart > 0 )
							{
								string beforeGroup = match.Value.Substring(0, relativeStart);
								if ( !string.IsNullOrEmpty(beforeGroup) ) inlines.Add(new Run(beforeGroup) { Foreground = Brushes.White });
							}
							inlines.Add(new Run(group.Value) { Foreground = groupColors[groupName], FontWeight = FontWeight.Bold });
							processedRanges.Add((group.Index, group.Index + group.Length));
						}

						if ( processedRanges.Count == 0 ) inlines.Add(new Run(match.Value) { Foreground = Brushes.White });
						lastIndex = match.Index + match.Length;
					}

					if ( lastIndex < _markdown.Length )
					{
						string remainingText = _markdown.Substring(lastIndex);
						inlines.Add(new Run(remainingText) { Foreground = Brushes.White });
					}
				}
				else
				{

					var sectionRegex = new Regex(Profile.ComponentSectionPattern, Profile.ComponentSectionOptions);
					MatchCollection sections = sectionRegex.Matches(_markdown);
					Logger.LogVerbose($"Found {sections.Count} section matches (INDIVIDUAL mode)");
					int cursor = 0;

					foreach ( Match section in sections )
					{
						if ( section.Index > cursor )
							inlines.Add(new Run(_markdown.Substring(cursor, section.Index - cursor)) { Foreground = Brushes.White });

						string sectionText;
						int sectionTextStartInDoc;
						Group contentGroup = section.Groups["content"];
						if ( contentGroup.Success )
						{
							sectionText = contentGroup.Value;
							sectionTextStartInDoc = contentGroup.Index;
						}
						else
						{
							sectionText = section.Value;
							sectionTextStartInDoc = section.Index;
						}

						Logger.LogVerbose($"Processing section at {sectionTextStartInDoc} length {sectionText.Length}");

						var ranges = new List<(int start, int end, IBrush brush)>();

						void AddGroupRange(string pattern, string groupName, IBrush brush)
						{
							if ( string.IsNullOrWhiteSpace(pattern) ) return;
							var r = new Regex(pattern, Profile.GetRegexOptions());
							foreach ( Match m in r.Matches(sectionText) )
							{
								Group g = m.Groups[groupName];
								if ( g.Success && g.Length > 0 )
								{
									int absStart = sectionTextStartInDoc + g.Index;
									int absEnd = absStart + g.Length;
									ranges.Add((absStart, absEnd, brush));
									Logger.LogVerbose($"IND group '{groupName}' at {absStart}-{absEnd}: '{g.Value}'");
								}
							}
						}

						AddGroupRange(Profile.HeadingPattern, "heading", groupColors["heading"]);
						AddGroupRange(Profile.NamePattern, "name", groupColors["name"]);
						AddGroupRange(Profile.AuthorPattern, "author", groupColors["author"]);
						AddGroupRange(Profile.DescriptionPattern, "description", groupColors["description"]);
						AddGroupRange(Profile.DependenciesPattern, "masters", groupColors["masters"]);
						AddGroupRange(Profile.CategoryTierPattern, "category_tier", groupColors["category_tier"]);
						AddGroupRange(Profile.NonEnglishPattern, "non_english", groupColors["non_english"]);
						AddGroupRange(Profile.InstallationMethodPattern, "installation_method", groupColors["installation_method"]);
						AddGroupRange(Profile.InstallationInstructionsPattern, "installation_instructions", groupColors["installation_instructions"]);

						ranges = ranges.OrderBy(r => r.start).ThenByDescending(r => r.end - r.start).ToList();

						int pos = sectionTextStartInDoc;
						foreach ( (int start, int end, IBrush brush) in ranges )
						{
							if ( start < pos ) continue;
							if ( start > pos )
								inlines.Add(new Run(_markdown.Substring(pos, start - pos)) { Foreground = Brushes.White });
							inlines.Add(new Run(_markdown.Substring(start, end - start)) { Foreground = brush, FontWeight = FontWeight.Bold });
							pos = end;
						}

						int sectionEnd = section.Index + section.Length;
						if ( pos < sectionEnd )
							inlines.Add(new Run(_markdown.Substring(pos, sectionEnd - pos)) { Foreground = Brushes.White });

						cursor = section.Index + section.Length;
					}

					if ( cursor < _markdown.Length )
						inlines.Add(new Run(_markdown.Substring(cursor)) { Foreground = Brushes.White });
				}
			}
			catch ( Exception ex )
			{

				inlines.Clear();
				inlines.Add(new Run(_markdown) { Foreground = Brushes.White });
				inlines.Add(new Run($"\n\n[Regex Error: {ex.Message}]") { Foreground = Brushes.Red });
			}

			HighlightedPreview = inlines;
		}

		private void UpdateCounts()
		{

			var parser = new MarkdownParser(Profile,
				logInfo => Logger.Log(logInfo),
				Logger.LogVerbose);
			MarkdownParserResult result = parser.Parse(_markdown);
			int comp = result.Components.Count;
			int links = result.Components.Sum(c => c.ModLinkFilenames.Count);
			PreviewSummary = $"Components: {comp} | Links: {links}";
		}

		public void OnProfileChanged() => RecomputePreview();

		private void OnProfilePropertyChanged(object sender, PropertyChangedEventArgs e) => RecomputePreview();

		public MarkdownParserResult ConfirmLoad()
		{
			var parser = new MarkdownParser(Profile,
				logInfo => Logger.Log(logInfo),
				Logger.LogVerbose);
			return parser.Parse(_markdown);
		}

		private void OnPropertyChanged([CallerMemberName][CanBeNull] string name = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
