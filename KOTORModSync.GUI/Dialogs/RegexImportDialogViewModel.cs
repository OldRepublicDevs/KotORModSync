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
		private readonly string _markdown;
		private string _previewSummary;
		private string _previewMarkdown;
		private ObservableCollection<Inline> _highlightedPreview;
		private int _selectedTabIndex;
		private readonly Dictionary<string, (int start, int end, string groupName)> _highlightedRanges;

		public MergeHeuristicsOptions Heuristics { get; private set; }

		public MarkdownImportProfile ConfiguredProfile => Profile;
		public ICommand FindCommand { get; private set; }

		public RegexImportDialogViewModel([NotNull] string markdown, [NotNull] MarkdownImportProfile profile)
		{
			_markdown = markdown;
			Profile = profile;
			Profile.PropertyChanged += OnProfilePropertyChanged;
			Heuristics = MergeHeuristicsOptions.CreateDefault();
			PreviewMarkdown = _markdown;
			_highlightedRanges = new Dictionary<string, (int start, int end, string groupName)>(StringComparer.Ordinal);

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

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Preview logic requires comprehensive regex matching and error handling")]
		private void RecomputePreview()
		{
			// Lightweight preview - just count regex matches without full parsing
			// This is fast and doesn't lag the UI as much
			try
			{
				int componentMatches = 0;
				int linkMatches = 0;

				if (Profile.Mode == RegexMode.Raw)
				{
					if (!string.IsNullOrWhiteSpace(Profile.RawRegexPattern))
					{
						try
						{
							var regex = new Regex(Profile.RawRegexPattern, Profile.RawRegexOptions, TimeSpan.FromSeconds(2));
							componentMatches = regex.Matches(PreviewMarkdown).Count;
						}
						catch (Exception ex)
						{
							PreviewSummary = $"Regex Error: {ex.Message}";
							Logger.LogVerbose($"Preview regex error: {ex.Message}");
							return;
						}
					}
				}
				else
				{
					// For Individual mode, count component section matches
					if (!string.IsNullOrWhiteSpace(Profile.ComponentSectionPattern))
					{
						try
						{
							var regex = new Regex(Profile.ComponentSectionPattern, Profile.GetRegexOptions(), TimeSpan.FromSeconds(2));
							componentMatches = regex.Matches(PreviewMarkdown).Count;
						}
						catch (Exception ex)
						{
							PreviewSummary = $"Regex Error: {ex.Message}";
							Logger.LogVerbose($"Preview regex error: {ex.Message}");
							return;
						}
					}
				}

				// Count ModLink matches
				if (!string.IsNullOrWhiteSpace(Profile.ModLinkPattern))
				{
					try
					{
						var linkRegex = new Regex(Profile.ModLinkPattern, Profile.GetRegexOptions(), TimeSpan.FromSeconds(2));
						linkMatches = linkRegex.Matches(PreviewMarkdown).Count;
					}
					catch (Exception ex)
					{
						// Non-critical error, just log it
						Logger.LogVerbose($"Link pattern error: {ex.Message}");
					}
				}

				PreviewSummary = string.Format(
					System.Globalization.CultureInfo.InvariantCulture,
					"Component Matches: {0} | Link Matches: {1}",
					componentMatches,
					linkMatches
				);

				// Build highlighted ranges for hover detection
				BuildHighlightedRanges();
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
				// Helper function to get theme resources
				IBrush GetResource(string key, IBrush fallback) =>
					Application.Current?.TryGetResource(key, Application.Current?.ActualThemeVariant, out object value) == true && value is IBrush b ? b : fallback;

				string patternToUse = Profile.Mode == RegexMode.Raw
					? Profile.RawRegexPattern
					: Profile.ComponentSectionPattern;

				if (string.IsNullOrWhiteSpace(patternToUse))
				{
					// Use theme resource for default text color
					inlines.Add(new Run(_markdown) { Foreground = GetResource("RegexHighlight.Default", Brushes.Black) });
					HighlightedPreview = inlines;
					return;
				}
				// Get all theme resource colors - GetResource will use theme-specific colors if available, 
				// or fallback to FluentLightStyle colors if not (which happen to match standard syntax highlighting)
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

				Logger.LogVerbose($"GenerateHighlightedPreview: Mode={Profile.Mode}");

				// Get default text color from theme
				IBrush defaultTextColor = GetResource("RegexHighlight.Default", Brushes.Black);

				if (Profile.Mode == RegexMode.Raw)
				{
					var regex = new Regex(patternToUse, Profile.RawRegexOptions, TimeSpan.FromSeconds(2));
					MatchCollection matches = regex.Matches(_markdown);
					Logger.LogVerbose($"Found {matches.Count} RAW matches with pattern: {patternToUse.Substring(0, Math.Min(80, patternToUse.Length))}...");
					int lastIndex = 0;

					foreach (Match match in matches)
					{
						if (match.Index > lastIndex)
						{
							string beforeText = _markdown.Substring(lastIndex, match.Index - lastIndex);
							inlines.Add(new Run(beforeText) { Foreground = defaultTextColor });
						}

						var processedRanges = new List<(int start, int end)>();
						var groupRanges = new List<(int start, int end, IBrush brush, string name)>();

						// First, collect all groups from the match that are in our color dictionary
						foreach (string groupName in groupColors.Keys)
						{
							Group group = match.Groups[groupName];
							if (!group.Success || group.Length <= 0)
								continue;
							Logger.LogVerbose($"RAW group '{groupName}': '{group.Value}'");
							groupRanges.Add((group.Index, group.Index + group.Length, groupColors[groupName], groupName));
						}

						// Also check for any other named groups in the match (fallback coloring)
						foreach (Group group in match.Groups.Cast<Group>())
						{
							// Skip if already processed or is a numbered group (group 0 is the full match)
							if (
								group.Index < 0
								|| group.Length <= 0
								|| string.IsNullOrEmpty(group.Name)
								|| int.TryParse(group.Name, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)
							)
								continue;
							if (groupColors.ContainsKey(group.Name))
								continue; // Already processed above
							Logger.LogVerbose($"RAW unknown group '{group.Name}': '{group.Value}'");
							// Use a fallback color for unknown groups
							groupRanges.Add((group.Index, group.Index + group.Length, groupColors["description"], group.Name));
						}

						// Sort by start position, then by length (longer first) to prioritize more specific matches
						groupRanges = groupRanges.OrderBy(r => r.start).ThenByDescending(r => r.end - r.start).ToList();

						// Process groups, avoiding overlaps
						int relativePos = 0;
						foreach ((int start, int end, IBrush brush, string name) in groupRanges)
						{
							// Check for overlaps with already processed ranges
							if (processedRanges.Exists(range => !(start >= range.end || end <= range.start)))
								continue;

							int relativeStart = start - match.Index;
							if (relativeStart > relativePos)
							{
								string beforeGroup = match.Value.Substring(relativePos, relativeStart - relativePos);
								if (!string.IsNullOrEmpty(beforeGroup))
									inlines.Add(new Run(beforeGroup) { Foreground = defaultTextColor });
							}

							string groupValue = _markdown.Substring(start, end - start);
							inlines.Add(new Run(groupValue) { Foreground = brush, FontWeight = FontWeight.Bold });
							processedRanges.Add((start, end));
							relativePos = end - match.Index;
						}

						// Add any remaining text from the match
						if (relativePos < match.Length)
						{
							string remainingText = match.Value.Substring(relativePos);
							if (!string.IsNullOrEmpty(remainingText))
								inlines.Add(new Run(remainingText) { Foreground = defaultTextColor });
						}
						else if (processedRanges.Count == 0)
						{
							// Fallback: if no groups were processed, add the entire match as default color
							inlines.Add(new Run(match.Value) { Foreground = defaultTextColor });
						}

						lastIndex = match.Index + match.Length;
					}

					if (lastIndex < _markdown.Length)
					{
						string remainingText = _markdown.Substring(lastIndex);
						inlines.Add(new Run(remainingText) { Foreground = defaultTextColor });
					}
				}
				else
				{

					var sectionRegex = new Regex(Profile.ComponentSectionPattern, Profile.ComponentSectionOptions, TimeSpan.FromSeconds(2));
					MatchCollection sections = sectionRegex.Matches(_markdown);
					Logger.LogVerbose($"Found {sections.Count} section matches (INDIVIDUAL mode)");
					int cursor = 0;

					foreach (Match section in sections)
					{
						if (section.Index > cursor)
							inlines.Add(new Run(_markdown.Substring(cursor, section.Index - cursor)) { Foreground = defaultTextColor });

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

						Logger.LogVerbose($"Processing section at {sectionTextStartInDoc} length {sectionText.Length}");

						var ranges = new List<(int start, int end, IBrush brush)>();

						void AddGroupRange(string pattern, string groupName, IBrush brush)
						{
							if (string.IsNullOrWhiteSpace(pattern)) return;
							var r = new Regex(pattern, Profile.GetRegexOptions(), TimeSpan.FromSeconds(2));
							foreach (Match m in r.Matches(sectionText))
							{
								Group g = m.Groups[groupName];
								if (g.Success && g.Length > 0)
								{
									int absStart = sectionTextStartInDoc + g.Index;
									int absEnd = absStart + g.Length;
									ranges.Add((absStart, absEnd, brush));
									Logger.LogVerbose($"IND group '{groupName}' at {absStart}-{absEnd}: '{g.Value}'");
								}
							}
						}

						// Core patterns
						AddGroupRange(Profile.HeadingPattern, "heading", groupColors["heading"]);
						AddGroupRange(Profile.NamePattern, "name", groupColors["name"]);
						AddGroupRange(Profile.NamePattern, "name_link", groupColors["name_link"]);
						AddGroupRange(Profile.NamePattern, "name_plain", groupColors["name_plain"]);
						AddGroupRange(Profile.AuthorPattern, "author", groupColors["author"]);
						AddGroupRange(Profile.DescriptionPattern, "description", groupColors["description"]);
						AddGroupRange(Profile.DependenciesPattern, "masters", groupColors["masters"]);

						// Category & Tier - handle both combined and individual groups
						if (!string.IsNullOrWhiteSpace(Profile.CategoryTierPattern))
						{
							var catTierRegex = new Regex(Profile.CategoryTierPattern, Profile.GetRegexOptions(), TimeSpan.FromSeconds(2));
							foreach (Match m in catTierRegex.Matches(sectionText))
							{
								// Try category_tier first (if pattern uses it)
								if (m.Groups["category_tier"].Success && m.Groups["category_tier"].Length > 0)
								{
									int absStart = sectionTextStartInDoc + m.Groups["category_tier"].Index;
									int absEnd = absStart + m.Groups["category_tier"].Length;
									ranges.Add((absStart, absEnd, groupColors["category_tier"]));
								}
								// Then try individual category and tier groups
								if (m.Groups["category"].Success && m.Groups["category"].Length > 0)
								{
									int absStart = sectionTextStartInDoc + m.Groups["category"].Index;
									int absEnd = absStart + m.Groups["category"].Length;
									ranges.Add((absStart, absEnd, groupColors["category"]));
								}
								if (m.Groups["tier"].Success && m.Groups["tier"].Length > 0)
								{
									int absStart = sectionTextStartInDoc + m.Groups["tier"].Index;
									int absEnd = absStart + m.Groups["tier"].Length;
									ranges.Add((absStart, absEnd, groupColors["tier"]));
								}
							}
						}

						// Non-English
						AddGroupRange(Profile.NonEnglishPattern, "non_english", groupColors["non_english"]);
						AddGroupRange(Profile.NonEnglishPattern, "value", groupColors["value"]);

						// Installation patterns
						AddGroupRange(Profile.InstallationMethodPattern, "installation_method", groupColors["installation_method"]);
						AddGroupRange(Profile.InstallationMethodPattern, "method", groupColors["method"]);
						AddGroupRange(Profile.InstallationInstructionsPattern, "installation_instructions", groupColors["installation_instructions"]);
						AddGroupRange(Profile.InstallationInstructionsPattern, "directions", groupColors["directions"]);
						AddGroupRange(Profile.DownloadInstructionsPattern, "download", groupColors["download"]);

						// ModLink patterns
						AddGroupRange(Profile.ModLinkPattern, "label", groupColors["label"]);
						AddGroupRange(Profile.ModLinkPattern, "link", groupColors["link"]);

						// Warning patterns
						AddGroupRange(Profile.UsageWarningPattern, "warning", groupColors["warning"]);
						AddGroupRange(Profile.InstallationWarningPattern, "installwarning", groupColors["installwarning"]);
						AddGroupRange(Profile.CompatibilityWarningPattern, "compatwarning", groupColors["compatwarning"]);

						// Other patterns
						AddGroupRange(Profile.ScreenshotsPattern, "screenshots", groupColors["screenshots"]);
						AddGroupRange(Profile.KnownBugsPattern, "bugs", groupColors["bugs"]);
						AddGroupRange(Profile.SteamNotesPattern, "steamnotes", groupColors["steamnotes"]);

						ranges = ranges.OrderBy(r => r.start).ThenByDescending(r => r.end - r.start).ToList();

						int pos = sectionTextStartInDoc;
						foreach ((int start, int end, IBrush brush) in ranges)
						{
							if (start < pos)
								continue;
							if (start > pos)
								inlines.Add(new Run(_markdown.Substring(pos, start - pos)) { Foreground = defaultTextColor });
							inlines.Add(new Run(_markdown.Substring(start, end - start)) { Foreground = brush, FontWeight = FontWeight.Bold });
							pos = end;
						}

						int sectionEnd = section.Index + section.Length;
						if (pos < sectionEnd)
							inlines.Add(new Run(_markdown.Substring(pos, sectionEnd - pos)) { Foreground = defaultTextColor });

						cursor = section.Index + section.Length;
					}

					if (cursor < _markdown.Length)
						inlines.Add(new Run(_markdown.Substring(cursor)) { Foreground = defaultTextColor });
				}
			}
			catch (Exception ex)
			{
				inlines.Clear();
				IBrush GetResource(string key, IBrush fallback) =>
					Application.Current?.TryGetResource(key, Application.Current?.ActualThemeVariant, out object value) == true && value is IBrush b ? b : fallback;
				IBrush defaultTextColor = GetResource("RegexHighlight.Default", Brushes.Black);
				inlines.Add(new Run(_markdown) { Foreground = defaultTextColor });
				inlines.Add(new Run($"\n\n[Regex Error: {ex.Message}]") { Foreground = Brushes.Red });
			}

			HighlightedPreview = inlines;
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
			// Rebuild highlighted ranges if needed
			if (_highlightedRanges.Count == 0)
			{
				BuildHighlightedRanges();
			}

			// Find the range that contains this position
			foreach (var range in _highlightedRanges.Values)
			{
				if (position >= range.start && position <= range.end)
				{
					return range.groupName;
				}
			}

			return null;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Range building logic requires comprehensive pattern matching for both modes")]
		private void BuildHighlightedRanges()
		{
			_highlightedRanges.Clear();

			try
			{
				if (Profile.Mode == RegexMode.Raw)
				{
					if (!string.IsNullOrWhiteSpace(Profile.RawRegexPattern))
					{
						var regex = new Regex(Profile.RawRegexPattern, Profile.RawRegexOptions, TimeSpan.FromSeconds(2));
						MatchCollection matches = regex.Matches(PreviewMarkdown);

						int rangeIndex = 0;
						foreach (Match match in matches)
						{
							// Add ranges for each named group
							foreach (string groupName in new[] { "heading", "name", "name_link", "name_plain", "author", "description",
																  "masters", "category", "tier", "non_english", "value",
																  "method", "installation_method", "directions", "installation_instructions",
																  "download", "label", "link" })
							{
								Group group = match.Groups[groupName];
								if (group.Success)
								{
									_highlightedRanges[$"{rangeIndex}_{groupName}"] = (group.Index, group.Index + group.Length, groupName);
									rangeIndex++;
								}
							}
						}
					}
				}
				else
				{
					// Build ranges for individual patterns
					var patterns = new Dictionary<string, string>(StringComparer.Ordinal)
					{
						["heading"] = Profile.HeadingPattern,
						["name"] = Profile.NamePattern,
						["author"] = Profile.AuthorPattern,
						["description"] = Profile.DescriptionPattern,
						["masters"] = Profile.DependenciesPattern,
						["category"] = Profile.CategoryTierPattern,
						["non_english"] = Profile.NonEnglishPattern,
						["method"] = Profile.InstallationMethodPattern,
						["directions"] = Profile.InstallationInstructionsPattern,
						["link"] = Profile.ModLinkPattern
					};

					int rangeIndex = 0;
					foreach (var kvp in patterns)
					{
						if (string.IsNullOrWhiteSpace(kvp.Value)) continue;

						var regex = new Regex(kvp.Value, Profile.GetRegexOptions(), TimeSpan.FromSeconds(2));
						MatchCollection matches = regex.Matches(PreviewMarkdown);

						foreach (Match match in matches)
						{
							// Try to find the named group
							Group group = match.Groups[kvp.Key];
							if (!group.Success) group = match.Groups["name_link"];
							if (!group.Success) group = match.Groups["name_plain"];
							if (!group.Success) group = match.Groups["value"];

							if (group.Success)
							{
								_highlightedRanges[$"{rangeIndex}_{kvp.Key}"] = (group.Index, group.Index + group.Length, kvp.Key);
								rangeIndex++;
							}
						}
					}
				}

				Logger.LogVerbose($"Built {_highlightedRanges.Count} highlighted ranges for hover detection");
			}
			catch (Exception ex)
			{
				Logger.LogVerbose($"Error building highlighted ranges: {ex.Message}");
			}
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