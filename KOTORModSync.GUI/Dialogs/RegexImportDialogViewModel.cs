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
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using JetBrains.Annotations;
using KOTORModSync;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
	public class RegexImportDialogViewModel : INotifyPropertyChanged
	{
		private string _previewSummary;
		private string _previewMarkdown;
		private ObservableCollection<Inline> _highlightedPreview;
		private int _selectedTabIndex;
		private readonly Dictionary<string, (int start, int end, string groupName)> _highlightedRanges;
		private readonly Dictionary<int, MatchTrace> _positionToMatch;
		private readonly Dictionary<int, SectionTrace> _positionToSection;
		private MarkdownParserResult _lastParseResult;

		public MergeHeuristicsOptions Heuristics { get; private set; }

		public MarkdownImportProfile ConfiguredProfile => Profile;
		public ICommand FindCommand { get; private set; }

		public RegexImportDialogViewModel([NotNull] string markdown, [NotNull] MarkdownImportProfile profile)
		{
			Profile = profile;
			Profile.PropertyChanged += OnProfilePropertyChanged;
			Heuristics = MergeHeuristicsOptions.CreateDefault();
			PreviewMarkdown = markdown;
			_highlightedRanges = new Dictionary<string, (int start, int end, string groupName)>(StringComparer.Ordinal);
			_positionToMatch = new Dictionary<int, MatchTrace>();
			_positionToSection = new Dictionary<int, SectionTrace>();

			FindCommand = new RelayCommand(_ => ShowFindDialog());
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
				if (string.Equals(_previewSummary, value, StringComparison.Ordinal)) return;
				_previewSummary = value;
				OnPropertyChanged();
			}
		}

		public string PreviewMarkdown
		{
			get => _previewMarkdown;
			set
			{
				if (string.Equals(_previewMarkdown, value, StringComparison.Ordinal)) return;
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

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Preview logic requires using actual MarkdownParser")]
		private void RecomputePreview()
		{
			// Use the ACTUAL parser to show exactly what will be loaded
			// NO SIMPLIFIED LOGIC - MUST BE 100% ACCURATE
			try
			{
				// Parse using the exact same MarkdownParser that ConfirmLoad() uses
				var parser = new MarkdownParser(Profile,
					logInfo => Logger.LogVerbose(logInfo),
					Logger.LogVerbose);
				MarkdownParserResult result = parser.Parse(PreviewMarkdown);
				_lastParseResult = result;

				int componentMatches = result.Components.Count;

				// Count actual ModLink matches from parsed components
				int linkMatches = result.Components.Sum(c => c.ModLinkFilenames?.Count ?? 0);

				PreviewSummary = string.Format(
					System.Globalization.CultureInfo.InvariantCulture,
					"Component Matches: {0} | Link Matches: {1}",
					componentMatches,
					linkMatches
				);

				// Generate highlighted preview using trace data from MarkdownParser
				GenerateHighlightedPreview();
			}
			catch (Exception ex)
			{
				PreviewSummary = $"Error: {ex.Message}";
				Logger.LogVerbose($"Preview error: {ex.Message}");
			}
		}


		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "MA0009:Add regex evaluation timeout", Justification = "<Pending>")]

		private void GenerateHighlightedPreview()
		{
			var inlines = new ObservableCollection<Inline>();

			try
			{
				// Use the ACTUAL MarkdownParser to get parsed components
				var parser = new MarkdownParser(Profile,
					logInfo => Logger.LogVerbose(logInfo),
					Logger.LogVerbose);
				MarkdownParserResult result = parser.Parse(PreviewMarkdown);
				_lastParseResult = result;

				// Build highlighting based on actual parsing
				BuildHighlightingFromParsedResult(result, inlines);
			}
			catch (Exception ex)
			{
				inlines.Clear();
				IBrush GetResource(string key, IBrush fallback) =>
					Application.Current?.TryGetResource(key, Application.Current?.ActualThemeVariant, out object value) == true && value is IBrush b ? b : fallback;
				IBrush defaultTextColor = GetResource("RegexHighlight.Default", Brushes.Black);
				inlines.Add(new Run(PreviewMarkdown) { Foreground = defaultTextColor });
				inlines.Add(new Run($"\n\n[Regex Error: {ex.Message}]") { Foreground = Brushes.Red });
			}

			HighlightedPreview = inlines;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Color mapping dictionary is extensive but necessary")]
		private void BuildHighlightingFromParsedResult(MarkdownParserResult result, ObservableCollection<Inline> inlines)
		{
			// EXCLUSIVELY use MarkdownParser's trace data - NO independent logic
			if (result?.Trace == null)
			{
				inlines.Add(new Run(PreviewMarkdown));
				return;
			}

			// Helper function to get theme resources
			IBrush GetResource(string key, IBrush fallback) =>
				Application.Current?.TryGetResource(key, Application.Current?.ActualThemeVariant, out object value) == true && value is IBrush b ? b : fallback;

			// Get all theme resource colors
			var groupColors = new Dictionary<string, IBrush>(StringComparer.Ordinal)
			{
				// Core patterns
				["heading"] = GetResource("RegexHighlight.Heading", new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))),
				["name"] = GetResource("RegexHighlight.Name", new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))),
				["name_link"] = GetResource("RegexHighlight.NameLink", new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))),
				["name_plain"] = GetResource("RegexHighlight.NamePlain", new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))),
				["author"] = GetResource("RegexHighlight.Author", new SolidColorBrush(Color.FromRgb(0x00, 0x5A, 0x9E))),
				["description"] = GetResource("RegexHighlight.Description", new SolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10))),
				["masters"] = GetResource("RegexHighlight.Masters", new SolidColorBrush(Color.FromRgb(0xF7, 0x63, 0x0C))),
				// Category & Tier - supports both combined and individual groups
				["category_tier"] = GetResource("RegexHighlight.CategoryTier", new SolidColorBrush(Color.FromRgb(0x7B, 0x2E, 0xBF))),
				["category"] = GetResource("RegexHighlight.Category", new SolidColorBrush(Color.FromRgb(0x7B, 0x2E, 0xBF))),
				["tier"] = GetResource("RegexHighlight.Tier", new SolidColorBrush(Color.FromRgb(0x9C, 0x27, 0xB0))),
				["non_english"] = GetResource("RegexHighlight.NonEnglish", new SolidColorBrush(Color.FromRgb(0x60, 0x5E, 0x5C))),
				["value"] = GetResource("RegexHighlight.Value", new SolidColorBrush(Color.FromRgb(0x60, 0x5E, 0x5C))),
				// Installation patterns
				["installation_method"] = GetResource("RegexHighlight.InstallationMethod", new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10))),
				["method"] = GetResource("RegexHighlight.Method", new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10))),
				["installation_instructions"] = GetResource("RegexHighlight.InstallationInstructions", new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01))),
				["directions"] = GetResource("RegexHighlight.Directions", new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01))),
				["download"] = GetResource("RegexHighlight.DownloadInstructions", new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22))),
				// ModLink patterns
				["label"] = GetResource("RegexHighlight.ModLinkLabel", new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2))),
				["link"] = GetResource("RegexHighlight.ModLink", new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3))),
				// Warning patterns
				["warning"] = GetResource("RegexHighlight.UsageWarning", new SolidColorBrush(Color.FromRgb(0xE9, 0x1E, 0x63))),
				["installwarning"] = GetResource("RegexHighlight.InstallationWarning", new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00))),
				["compatwarning"] = GetResource("RegexHighlight.CompatibilityWarning", new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07))),
				// Other patterns
				["screenshots"] = GetResource("RegexHighlight.Screenshots", new SolidColorBrush(Color.FromRgb(0x00, 0x96, 0x88))),
				["bugs"] = GetResource("RegexHighlight.KnownBugs", new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x22))),
				["steamnotes"] = GetResource("RegexHighlight.SteamNotes", new SolidColorBrush(Color.FromRgb(0x79, 0x55, 0x48))),
			};

			// Get default text color from theme
			IBrush defaultTextColor = GetResource("RegexHighlight.Default", Brushes.Black);

			// EXCLUSIVELY use trace data from MarkdownParser - convert matches to ranges
			_highlightedRanges.Clear();
			_positionToMatch.Clear();
			_positionToSection.Clear();
			
			var allRanges = new List<(int start, int end, string groupName)>();

			// Store matches for position lookup
			foreach (var match in result.Trace.Matches.Where(m => m.WasUsed))
			{
				allRanges.Add((match.StartIndex, match.EndIndex, match.GroupName));
				
				// Store for hover detection
				string key = $"{match.StartIndex}_{match.EndIndex}";
				_highlightedRanges[key] = (match.StartIndex, match.EndIndex, match.GroupName);
				
				// Store for position-to-match lookup (middle of range for tooltip display)
				int middlePos = (match.StartIndex + match.EndIndex) / 2;
				_positionToMatch[middlePos] = match;
			}
			
			// Store sections for position lookup
			foreach (var section in result.Trace.Sections)
			{
				int middlePos = (section.StartIndex + section.EndIndex) / 2;
				_positionToSection[middlePos] = section;
			}

			// Sort ranges by start position, then by length (longest first for overlaps)
			allRanges = allRanges.OrderBy(r => r.start).ThenByDescending(r => r.end - r.start).ToList();

			// Build inlines from trace matches
			string markdown = PreviewMarkdown;
			int pos = 0;

			foreach (var (start, end, groupName) in allRanges)
			{
				// Skip if this range starts before current position (overlap handling)
				if (start < pos) continue;

				// Add unmatched text before this range
				if (start > pos)
				{
					inlines.Add(new Run(markdown.Substring(pos, start - pos)) { Foreground = defaultTextColor });
				}

				// Add highlighted match
				IBrush brush = groupColors.TryGetValue(groupName, out IBrush color) ? color : defaultTextColor;
				inlines.Add(new Run(markdown.Substring(start, end - start)) 
				{ 
					Foreground = brush, 
					FontWeight = FontWeight.Bold 
				});

				pos = end;
			}

			// Add remaining text
			if (pos < markdown.Length)
			{
				inlines.Add(new Run(markdown.Substring(pos)) { Foreground = defaultTextColor });
			}

			Logger.LogVerbose($"Built highlighting from {result.Trace.Matches.Count} trace matches, {result.Trace.Sections.Count} sections");
		}

		public void OnProfileChanged() => RecomputePreview();

		private void OnProfilePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			// Don't auto-update on every property change - wait for explicit trigger
		}

		public void UpdatePreviewFromTextBox(TextBox textBox)
		{
			if (textBox == null) return;

			// Update the bound property from the TextBox using exact name matching
			string textBoxName = textBox.Name ?? "";
			switch (textBoxName)
			{
				case "RawRegexTextBox":
					Profile.RawRegexPattern = textBox.Text ?? "";
					break;
				case "ComponentSectionTextBox":
					Profile.ComponentSectionPattern = textBox.Text ?? "";
					break;
				case "HeadingTextBox":
					Profile.HeadingPattern = textBox.Text ?? "";
					break;
				case "NameTextBox":
					Profile.NamePattern = textBox.Text ?? "";
					break;
				case "AuthorTextBox":
					Profile.AuthorPattern = textBox.Text ?? "";
					break;
				case "DescriptionTextBox":
					Profile.DescriptionPattern = textBox.Text ?? "";
					break;
				case "MastersTextBox":
					Profile.DependenciesPattern = textBox.Text ?? "";
					break;
				case "CategoryTextBox":
					Profile.CategoryTierPattern = textBox.Text ?? "";
					break;
				case "NonEnglishTextBox":
					Profile.NonEnglishPattern = textBox.Text ?? "";
					break;
				case "InstallationMethodTextBox":
					Profile.InstallationMethodPattern = textBox.Text ?? "";
					break;
				case "InstallationInstructionsTextBox":
					Profile.InstallationInstructionsPattern = textBox.Text ?? "";
					break;
				case "ModLinkTextBox":
					Profile.ModLinkPattern = textBox.Text ?? "";
					break;
			}

			RecomputePreview();
		}

		public MarkdownParserResult ConfirmLoad()
		{
			// Parse the CURRENT preview markdown (which may have been edited) with the configured profile
			// This is the ONLY time we actually parse into ModComponent objects
			var parser = new MarkdownParser(Profile,
				logInfo => Logger.Log(logInfo),
				Logger.LogVerbose);
			return parser.Parse(PreviewMarkdown);
		}

		private void ShowFindDialog()
		{
			// Trigger find dialog event - will be handled in code-behind to show find UI
			OnPropertyChanged("ShowFindDialog");
		}

		public string GetGroupNameForPosition(int position)
		{
			// Use ranges already populated from trace data
			foreach (var range in _highlightedRanges.Values)
			{
				if (position >= range.start && position <= range.end)
				{
					return range.groupName;
				}
			}

			return null;
		}

		public string GetComponentInfoForPosition(int position)
		{
			if (_lastParseResult?.Trace == null)
				return null;

			// Find the section that contains this position
			var containingSection = _lastParseResult.Trace.Sections.Find(s => position >= s.StartIndex && position <= s.EndIndex);
			if (containingSection == null)
				return null;

			// Get the component for this section
			if (containingSection.ResultedInComponent && _lastParseResult.Components.Count > 0)
			{
				// Find component by matching ComponentIndex (1-based)
				var component = _lastParseResult.Components.FirstOrDefault(c => 
					_lastParseResult.Trace.Sections.IndexOf(containingSection) + 1 == containingSection.ComponentIndex
				);

				if (component == null && containingSection.ComponentIndex > 0 && containingSection.ComponentIndex <= _lastParseResult.Components.Count)
				{
					// Fallback to ComponentIndex
					component = _lastParseResult.Components[containingSection.ComponentIndex - 1];
				}

				if (component != null)
				{
					var sb = new System.Text.StringBuilder();
					sb.AppendLine($"ModComponent #{containingSection.ComponentIndex}");
					sb.AppendLine($"GUID: {component.Guid}");
					sb.AppendLine($"Name: {component.Name ?? "<null>"}");
					sb.AppendLine($"Author: {component.Author ?? "<null>"}");
					sb.AppendLine($"Category: {string.Join(" & ", component.Category ?? new List<string>())}");
					sb.AppendLine($"Tier: {component.Tier ?? "<null>"}");
				if (!string.IsNullOrEmpty(component.Description))
					sb.AppendLine($"Description: {component.Description.Substring(0, Math.Min(100, component.Description.Length))}...");
				if (component.ModLinkFilenames?.Count > 0)
					sb.AppendLine($"Links: {component.ModLinkFilenames.Count}");
				if (component.Dependencies?.Count > 0)
					sb.AppendLine($"Dependencies: {component.Dependencies.Count} mod(s)");
					
					return sb.ToString();
				}
			}

			// Show section info if no component
			if (containingSection.WasSkipped)
			{
				return $"Section #{containingSection.ComponentIndex} - SKIPPED\nReason: {containingSection.SkipReason}";
			}

			return $"Section #{containingSection.ComponentIndex} - No component created";
		}

		public static string GetTextBoxNameForGroupName(string groupName)
		{
			// Map group names to the corresponding TextBox names in the Simple tab
			var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["heading"] = "Heading",
				["name"] = "Name",
				["name_link"] = "Name",
				["name_plain"] = "Name",
				["author"] = "Author",
				["description"] = "Description",
				["masters"] = "Masters",
				["category"] = "Category",
				["tier"] = "Category",
				["category_tier"] = "Category",
				["non_english"] = "NonEnglish",
				["value"] = "NonEnglish",
				["installation_method"] = "InstallationMethod",
				["method"] = "InstallationMethod",
				["installation_instructions"] = "InstallationInstructions",
				["directions"] = "InstallationInstructions",
				["download"] = "Download",
				["label"] = "ModLink",
				["link"] = "ModLink"
			};

			return mapping.TryGetValue(groupName, out string textBoxName) ? textBoxName : null;
		}

		private void OnPropertyChanged([CallerMemberName][CanBeNull] string name = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}