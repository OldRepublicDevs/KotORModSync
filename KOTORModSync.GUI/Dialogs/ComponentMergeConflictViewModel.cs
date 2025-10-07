// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using JetBrains.Annotations;
using KOTORModSync.Core;
using Component = KOTORModSync.Core.Component;

namespace KOTORModSync.Dialogs
{
	/// <summary>
	/// Tracks which source (Existing or Incoming) to use for each field when merging matched components
	/// </summary>
	public class FieldMergePreference : INotifyPropertyChanged
	{
		public enum FieldSource
		{
			UseExisting,
			UseIncoming,
			Merge // For lists - combine both
		}

		private FieldSource _name = FieldSource.UseIncoming;
		private FieldSource _author = FieldSource.UseIncoming;
		private FieldSource _description = FieldSource.UseIncoming;
		private FieldSource _directions = FieldSource.UseIncoming;
		private FieldSource _category = FieldSource.UseIncoming;
		private FieldSource _tier = FieldSource.UseIncoming;
		private FieldSource _installationMethod = FieldSource.UseIncoming;
		private FieldSource _instructions = FieldSource.UseIncoming;
		private FieldSource _dependencies = FieldSource.Merge;
		private FieldSource _restrictions = FieldSource.Merge;
		private FieldSource _installAfter = FieldSource.Merge;
		private FieldSource _options = FieldSource.UseIncoming;
		private FieldSource _modLink = FieldSource.Merge;
		private FieldSource _language = FieldSource.Merge;

		public FieldSource Name { get => _name; set { _name = value; OnPropertyChanged(); } }
		public FieldSource Author { get => _author; set { _author = value; OnPropertyChanged(); } }
		public FieldSource Description { get => _description; set { _description = value; OnPropertyChanged(); } }
		public FieldSource Directions { get => _directions; set { _directions = value; OnPropertyChanged(); } }
		public FieldSource Category { get => _category; set { _category = value; OnPropertyChanged(); } }
		public FieldSource Tier { get => _tier; set { _tier = value; OnPropertyChanged(); } }
		public FieldSource InstallationMethod { get => _installationMethod; set { _installationMethod = value; OnPropertyChanged(); } }
		public FieldSource Instructions { get => _instructions; set { _instructions = value; OnPropertyChanged(); } }
		public FieldSource Dependencies { get => _dependencies; set { _dependencies = value; OnPropertyChanged(); } }
		public FieldSource Restrictions { get => _restrictions; set { _restrictions = value; OnPropertyChanged(); } }
		public FieldSource InstallAfter { get => _installAfter; set { _installAfter = value; OnPropertyChanged(); } }
		public FieldSource Options { get => _options; set { _options = value; OnPropertyChanged(); } }
		public FieldSource ModLink { get => _modLink; set { _modLink = value; OnPropertyChanged(); } }
		public FieldSource Language { get => _language; set { _language = value; OnPropertyChanged(); } }

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public class ComponentMergeConflictViewModel : INotifyPropertyChanged
	{
		private bool _useIncomingOrder = true;
		private bool _selectAllExisting;
		private bool _selectAllIncoming = true;
		private bool _skipDuplicates = true;
		private ComponentConflictItem _selectedExistingItem;
		private ComponentConflictItem _selectedIncomingItem;
		private TomlDiffResult _selectedMergedTomlLine;
		private string _searchText = string.Empty;

		private readonly
			Dictionary<Tuple<ComponentConflictItem, ComponentConflictItem>, GuidConflictResolver.GuidResolution>
			_guidResolutions =
				new Dictionary<Tuple<ComponentConflictItem, ComponentConflictItem>,
					GuidConflictResolver.GuidResolution>();

		// Track field-level merge preferences for each matched pair
		private readonly Dictionary<Tuple<ComponentConflictItem, ComponentConflictItem>, FieldMergePreference>
			_fieldPreferences = new Dictionary<Tuple<ComponentConflictItem, ComponentConflictItem>, FieldMergePreference>();

		public ComponentMergeConflictViewModel(
			[NotNull] List<Component> existingComponents,
			[NotNull] List<Component> incomingComponents,
			[NotNull] string existingSource,
			[NotNull] string incomingSource,
			[NotNull] Func<Component, Component, bool> matchFunc)
		{
			ExistingComponents = new ObservableCollection<ComponentConflictItem>();
			IncomingComponents = new ObservableCollection<ComponentConflictItem>();
			PreviewComponents = new ObservableCollection<PreviewItem>();
			FilteredExistingComponents = new ObservableCollection<ComponentConflictItem>();
			FilteredIncomingComponents = new ObservableCollection<ComponentConflictItem>();
			RealtimeMergedComponents = new ObservableCollection<Component>();
			CurrentTomlDiff = new ObservableCollection<TomlDiffResult>();
			ExistingComponentsToml = new ObservableCollection<TomlDiffResult>();
			IncomingComponentsToml = new ObservableCollection<TomlDiffResult>();
			MergedComponentsToml = new ObservableCollection<TomlDiffResult>();

			ExistingSourceInfo = existingSource;
			IncomingSourceInfo = incomingSource;

			// Initialize commands
			SelectAllIncomingMatchesCommand = new RelayCommand(_ => SelectAllIncomingMatches());
			SelectAllExistingMatchesCommand = new RelayCommand(_ => SelectAllExistingMatches());
			KeepAllNewCommand = new RelayCommand(_ => KeepAllNew());
			KeepAllExistingUnmatchedCommand = new RelayCommand(_ => KeepAllExistingUnmatched());
			LinkSelectedCommand = new RelayCommand(_ => LinkSelectedItems(), _ => CanLinkSelected());
			UnlinkSelectedCommand = new RelayCommand(_ => UnlinkSelectedItems(), _ => CanUnlinkSelected());
			UseAllIncomingFieldsCommand = new RelayCommand(_ => UseAllIncomingFields());
			UseAllExistingFieldsCommand = new RelayCommand(_ => UseAllExistingFields());
			JumpToRawViewCommand = new RelayCommand(param => JumpToRawView(param as ComponentConflictItem), param => param is ComponentConflictItem);

			// Build conflict items
			BuildConflictItems(existingComponents, incomingComponents, matchFunc);

			// Wire up property changes to update preview
			PropertyChanged += (_, e) =>
			{
				switch ( e.PropertyName )
				{
					case nameof(UseIncomingOrder):
					case nameof(SelectAllExisting):
					case nameof(SelectAllIncoming):
					case nameof(SkipDuplicates):
						UpdatePreview();
						break;
					case nameof(SearchText):
						ApplySearchFilter();
						break;
				}
			};

			// Initial preview
			UpdatePreview();
			ApplySearchFilter();
		}

		// Commands for quick actions
		public RelayCommand SelectAllIncomingMatchesCommand { get; }
		public RelayCommand SelectAllExistingMatchesCommand { get; }
		public RelayCommand KeepAllNewCommand { get; }
		public RelayCommand KeepAllExistingUnmatchedCommand { get; }
		public RelayCommand LinkSelectedCommand { get; }
		public RelayCommand UnlinkSelectedCommand { get; }
		public RelayCommand UseAllIncomingFieldsCommand { get; }
		public RelayCommand UseAllExistingFieldsCommand { get; }

		private readonly List<(ComponentConflictItem Existing, ComponentConflictItem Incoming)> _matchedPairs =
			new List<(ComponentConflictItem, ComponentConflictItem)>();

		private readonly List<ComponentConflictItem> _existingOnly = new List<ComponentConflictItem>();
		private readonly List<ComponentConflictItem> _incomingOnly = new List<ComponentConflictItem>();

		public ObservableCollection<ComponentConflictItem> ExistingComponents { get; }
		public ObservableCollection<ComponentConflictItem> IncomingComponents { get; }
		public ObservableCollection<PreviewItem> PreviewComponents { get; }
		public ObservableCollection<ComponentConflictItem> FilteredExistingComponents { get; }
		public ObservableCollection<ComponentConflictItem> FilteredIncomingComponents { get; }
		public ObservableCollection<Component> RealtimeMergedComponents { get; }
		public ObservableCollection<TomlDiffResult> CurrentTomlDiff { get; }
		public ObservableCollection<TomlDiffResult> ExistingComponentsToml { get; set; }
		public ObservableCollection<TomlDiffResult> IncomingComponentsToml { get; set; }
		public ObservableCollection<TomlDiffResult> MergedComponentsToml { get; }

		// Commands for jumping to raw view
		public RelayCommand JumpToRawViewCommand { get; }

		public string ExistingSourceInfo { get; }
		public string IncomingSourceInfo { get; }

		public string SearchText
		{
			get => _searchText;
			set
			{
				if ( _searchText == value ) return;
				_searchText = value;
				OnPropertyChanged();
			}
		}

		public string ConflictDescription =>
			$"Found {_matchedPairs.Count} matching component(s), " +
			$"{_incomingOnly.Count} new component(s) in incoming list, " +
			$"and {_existingOnly.Count} component(s) only in existing list.";

		public string ConflictSummary
		{
			get
			{
				int existingSelected = ExistingComponents.Count(c => c.IsSelected);
				int incomingSelected = IncomingComponents.Count(c => c.IsSelected);
				return
					$"Selected: {existingSelected} from existing, {incomingSelected} from incoming → {PreviewComponents.Count} total components";
			}
		}

		// Statistics
		public int NewComponentsCount => _incomingOnly.Count(i => i.IsSelected);
		public int UpdatedComponentsCount => _matchedPairs.Count(p => p.Incoming.IsSelected && !p.Existing.IsSelected);

		public int KeptComponentsCount => _existingOnly.Count(e => e.IsSelected) +
										  _matchedPairs.Count(p => p.Existing.IsSelected && !p.Incoming.IsSelected);

		public int RemovedComponentsCount => _existingOnly.Count(e => !e.IsSelected) +
											 _matchedPairs.Count(p =>
												 p.Existing.IsSelected && !p.Incoming.IsSelected &&
												 !p.Incoming.IsSelected);

		public int TotalChanges => NewComponentsCount + UpdatedComponentsCount + RemovedComponentsCount;

		public string MergeImpactSummary =>
			$"📊 Merge Impact: {NewComponentsCount} new, {UpdatedComponentsCount} updated, {KeptComponentsCount} kept, {RemovedComponentsCount} removed";

		public bool HasMatchedPairSelected
		{
			get
			{
				if ( _selectedExistingItem == null && _selectedIncomingItem == null )
					return false;

				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = default;

				if ( _selectedExistingItem != null )
					matchedPair = _matchedPairs.FirstOrDefault(p => p.Existing == _selectedExistingItem);
				else if ( _selectedIncomingItem != null )
					matchedPair = _matchedPairs.FirstOrDefault(p => p.Incoming == _selectedIncomingItem);

				return matchedPair.Existing != null && matchedPair.Incoming != null;
			}
		}

		public FieldMergePreference CurrentFieldPreferences
		{
			get
			{
				// Get preferences for the currently selected matched pair
				if ( _selectedExistingItem == null && _selectedIncomingItem == null )
					return new FieldMergePreference(); // Return empty instead of null

				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = default;

				if ( _selectedExistingItem != null )
					matchedPair = _matchedPairs.FirstOrDefault(p => p.Existing == _selectedExistingItem);
				else if ( _selectedIncomingItem != null )
					matchedPair = _matchedPairs.FirstOrDefault(p => p.Incoming == _selectedIncomingItem);

				if ( matchedPair.Existing == null || matchedPair.Incoming == null )
					return new FieldMergePreference(); // Return empty instead of null

				var key = Tuple.Create(matchedPair.Existing, matchedPair.Incoming);

				// If preferences don't exist yet, create them with smart defaults and subscribe to changes
				if ( _fieldPreferences.TryGetValue(key, out FieldMergePreference prefs) )
					return prefs;
				prefs = CreateAndSubscribeFieldPreferences(matchedPair.Existing.Component, matchedPair.Incoming.Component);
				_fieldPreferences[key] = prefs;

				return prefs;
			}
		}

		/// <summary>
		/// Creates field preferences with smart defaults and subscribes to property changes - use whichever source has data when the other is empty
		/// </summary>
		private FieldMergePreference CreateAndSubscribeFieldPreferences(Component existing, Component incoming)
		{
			FieldMergePreference prefs = CreateSmartFieldPreferences(existing, incoming);

			// Subscribe to property changes to update preview
			prefs.PropertyChanged += (_, __) =>
			{
				UpdatePreview();
				OnPropertyChanged(nameof(PreviewName));
				OnPropertyChanged(nameof(PreviewAuthor));
				OnPropertyChanged(nameof(PreviewInstructionsCount));
			};

			return prefs;
		}

		/// <summary>
		/// Creates field preferences with smart defaults - use whichever source has data when the other is empty
		/// </summary>
		private FieldMergePreference CreateSmartFieldPreferences(Component existing, Component incoming)
		{
			var prefs = new FieldMergePreference();

			// For each field, if one source is empty/null and the other has data, use the one with data
			// Otherwise default to UseIncoming

			// Name
			bool existingHasName = !string.IsNullOrWhiteSpace(existing.Name);
			bool incomingHasName = !string.IsNullOrWhiteSpace(incoming.Name);
			if ( !existingHasName && incomingHasName )
				prefs.Name = FieldMergePreference.FieldSource.UseIncoming;
			else if ( existingHasName && !incomingHasName )
				prefs.Name = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.Name = FieldMergePreference.FieldSource.UseIncoming; // Default

			// Author
			bool existingHasAuthor = !string.IsNullOrWhiteSpace(existing.Author);
			bool incomingHasAuthor = !string.IsNullOrWhiteSpace(incoming.Author);
			if ( !existingHasAuthor && incomingHasAuthor )
				prefs.Author = FieldMergePreference.FieldSource.UseIncoming;
			else if ( existingHasAuthor && !incomingHasAuthor )
				prefs.Author = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.Author = FieldMergePreference.FieldSource.UseIncoming;

			// Instructions
			bool existingHasInstructions = existing.Instructions.Count > 0;
			bool incomingHasInstructions = incoming.Instructions.Count > 0;
			if ( !existingHasInstructions && incomingHasInstructions )
				prefs.Instructions = FieldMergePreference.FieldSource.UseIncoming;
			else if ( existingHasInstructions && !incomingHasInstructions )
				prefs.Instructions = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.Instructions = FieldMergePreference.FieldSource.UseIncoming;

			// Options
			bool existingHasOptions = existing.Options.Count > 0;
			bool incomingHasOptions = incoming.Options.Count > 0;
			if ( !existingHasOptions && incomingHasOptions )
				prefs.Options = FieldMergePreference.FieldSource.UseIncoming;
			else if ( existingHasOptions && !incomingHasOptions )
				prefs.Options = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.Options = FieldMergePreference.FieldSource.UseIncoming;

			// For lists that support merge, use Merge if both have data, otherwise use whichever has data
			// Dependencies
			bool existingHasDeps = existing.Dependencies.Count > 0;
			bool incomingHasDeps = incoming.Dependencies.Count > 0;
			if ( existingHasDeps && incomingHasDeps )
				prefs.Dependencies = FieldMergePreference.FieldSource.Merge;
			else if ( existingHasDeps )
				prefs.Dependencies = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.Dependencies = FieldMergePreference.FieldSource.UseIncoming;

			// Restrictions
			bool existingHasRestrictions = existing.Restrictions.Count > 0;
			bool incomingHasRestrictions = incoming.Restrictions.Count > 0;
			if ( existingHasRestrictions && incomingHasRestrictions )
				prefs.Restrictions = FieldMergePreference.FieldSource.Merge;
			else if ( existingHasRestrictions )
				prefs.Restrictions = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.Restrictions = FieldMergePreference.FieldSource.UseIncoming;

			// InstallAfter
			bool existingHasInstallAfter = existing.InstallAfter.Count > 0;
			bool incomingHasInstallAfter = incoming.InstallAfter.Count > 0;
			if ( existingHasInstallAfter && incomingHasInstallAfter )
				prefs.InstallAfter = FieldMergePreference.FieldSource.Merge;
			else if ( existingHasInstallAfter )
				prefs.InstallAfter = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.InstallAfter = FieldMergePreference.FieldSource.UseIncoming;

			// ModLink
			bool existingHasModLink = existing.ModLink.Count > 0;
			bool incomingHasModLink = incoming.ModLink.Count > 0;
			if ( existingHasModLink && incomingHasModLink )
				prefs.ModLink = FieldMergePreference.FieldSource.Merge;
			else if ( existingHasModLink )
				prefs.ModLink = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.ModLink = FieldMergePreference.FieldSource.UseIncoming;

			// Language
			bool existingHasLanguage = existing.Language.Count > 0;
			bool incomingHasLanguage = incoming.Language.Count > 0;
			if ( existingHasLanguage && incomingHasLanguage )
				prefs.Language = FieldMergePreference.FieldSource.Merge;
			else if ( existingHasLanguage )
				prefs.Language = FieldMergePreference.FieldSource.UseExisting;
			else
				prefs.Language = FieldMergePreference.FieldSource.UseIncoming;

			// Other string fields default to incoming
			prefs.Description = FieldMergePreference.FieldSource.UseIncoming;
			prefs.Directions = FieldMergePreference.FieldSource.UseIncoming;
			prefs.Category = FieldMergePreference.FieldSource.UseIncoming;
			prefs.Tier = FieldMergePreference.FieldSource.UseIncoming;
			prefs.InstallationMethod = FieldMergePreference.FieldSource.UseIncoming;

			return prefs;
		}

		// Preview properties for field merge display
		public string PreviewName
		{
			get
			{
				if ( !HasMatchedPairSelected ) return string.Empty;

				// Get the matched pair to access actual component data
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p =>
				p.Existing == _selectedExistingItem || p.Incoming == _selectedIncomingItem);

				return matchedPair.Existing == null || matchedPair.Incoming == null
				? string.Empty
				: CurrentFieldPreferences.Name == FieldMergePreference.FieldSource.UseExisting
				? matchedPair.Existing.Component.Name
				: matchedPair.Incoming.Component.Name;
			}
		}

		public string PreviewAuthor
		{
			get
			{
				if ( !HasMatchedPairSelected ) return string.Empty;

				// Get the matched pair to access actual component data
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p =>
					p.Existing == _selectedExistingItem || p.Incoming == _selectedIncomingItem);

				return matchedPair.Existing == null || matchedPair.Incoming == null
					? string.Empty
					: CurrentFieldPreferences.Author == FieldMergePreference.FieldSource.UseExisting
					? matchedPair.Existing.Component.Author
					: matchedPair.Incoming.Component.Author;
			}
		}

		public string PreviewInstructionsCount
		{
			get
			{
				if ( !HasMatchedPairSelected ) return "0 instructions";

				// Get the matched pair to access actual component data
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p =>
					p.Existing == _selectedExistingItem || p.Incoming == _selectedIncomingItem);

				if ( matchedPair.Existing == null || matchedPair.Incoming == null )
					return "0 instructions";

				int count = CurrentFieldPreferences.Instructions == FieldMergePreference.FieldSource.UseExisting
					? matchedPair.Existing.Component.Instructions.Count
					: matchedPair.Incoming.Component.Instructions.Count;

				return $"{count} instruction{(count != 1 ? "s" : "")}";
			}
		}

		public ComponentConflictItem SelectedExistingItem
		{
			get => _selectedExistingItem;
			set
			{
				if ( _selectedExistingItem == value ) return;

				// Clear previous selection highlight
				if ( _selectedExistingItem != null )
					_selectedExistingItem.IsVisuallySelected = false;

				_selectedExistingItem = value;

				// Set new selection highlight
				if ( _selectedExistingItem != null )
					_selectedExistingItem.IsVisuallySelected = true;

				OnPropertyChanged();
				OnPropertyChanged(nameof(ComparisonVisible));
				OnPropertyChanged(nameof(ComparisonText));
				OnPropertyChanged(nameof(CanLinkItems));
				OnPropertyChanged(nameof(ExistingComponentsToml));
				OnPropertyChanged(nameof(HasMatchedPairSelected));
				OnPropertyChanged(nameof(CurrentFieldPreferences));
				OnPropertyChanged(nameof(PreviewName));
				OnPropertyChanged(nameof(PreviewAuthor));
				OnPropertyChanged(nameof(PreviewInstructionsCount));

				// Auto-highlight matching item in incoming list
				if ( value == null )
					return;
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
					_matchedPairs.FirstOrDefault(p => p.Existing == value);

				// If no match found, clear incoming selection
				if ( matchedPair.Incoming == null )
				{
					if ( _selectedIncomingItem != null )
					{
						_selectedIncomingItem.IsVisuallySelected = false;
						_selectedIncomingItem = null;
						OnPropertyChanged(nameof(SelectedIncomingItem));
					}
					return;
				}

				// Clear other incoming highlights first
				foreach ( ComponentConflictItem item in IncomingComponents )
				{
					if ( item != matchedPair.Incoming )
						item.IsVisuallySelected = false;
				}

				_selectedIncomingItem = matchedPair.Incoming;
				matchedPair.Incoming.IsVisuallySelected = true;
				OnPropertyChanged(nameof(SelectedIncomingItem));
				OnPropertyChanged(nameof(IncomingComponentsToml));
				OnPropertyChanged(nameof(CurrentFieldPreferences));
				OnPropertyChanged(nameof(PreviewName));
				OnPropertyChanged(nameof(PreviewAuthor));
				OnPropertyChanged(nameof(PreviewInstructionsCount));

				// Fire sync event for UI scrolling
				SyncSelectionRequested?.Invoke(this, new SyncSelectionEventArgs { SelectedItem = value, MatchedItem = matchedPair.Incoming });
			}
		}

		public ComponentConflictItem SelectedIncomingItem
		{
			get => _selectedIncomingItem;
			set
			{
				if ( _selectedIncomingItem == value ) return;

				// Clear previous selection highlight
				if ( _selectedIncomingItem != null )
					_selectedIncomingItem.IsVisuallySelected = false;

				_selectedIncomingItem = value;

				// Set new selection highlight
				if ( _selectedIncomingItem != null )
					_selectedIncomingItem.IsVisuallySelected = true;

				OnPropertyChanged();
				OnPropertyChanged(nameof(ComparisonVisible));
				OnPropertyChanged(nameof(ComparisonText));
				OnPropertyChanged(nameof(CanLinkItems));
				OnPropertyChanged(nameof(IncomingComponentsToml));
				OnPropertyChanged(nameof(HasMatchedPairSelected));
				OnPropertyChanged(nameof(CurrentFieldPreferences));
				OnPropertyChanged(nameof(PreviewName));
				OnPropertyChanged(nameof(PreviewAuthor));
				OnPropertyChanged(nameof(PreviewInstructionsCount));

				// Auto-highlight matching item in existing list
				if ( value == null )
					return;
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
					_matchedPairs.FirstOrDefault(p => p.Incoming == value);

				// If no match found, clear existing selection
				if ( matchedPair.Existing == null )
				{
					if ( _selectedExistingItem != null )
					{
						_selectedExistingItem.IsVisuallySelected = false;
						_selectedExistingItem = null;
						OnPropertyChanged(nameof(SelectedExistingItem));
					}
					return;
				}

				// Clear other existing highlights first
				foreach ( ComponentConflictItem item in ExistingComponents )
				{
					if ( item != matchedPair.Existing )
						item.IsVisuallySelected = false;
				}

				_selectedExistingItem = matchedPair.Existing;
				matchedPair.Existing.IsVisuallySelected = true;
				OnPropertyChanged(nameof(SelectedExistingItem));
				OnPropertyChanged(nameof(ExistingComponentsToml));
				OnPropertyChanged(nameof(CurrentFieldPreferences));
				OnPropertyChanged(nameof(PreviewName));
				OnPropertyChanged(nameof(PreviewAuthor));
				OnPropertyChanged(nameof(PreviewInstructionsCount));

				// Fire sync event for UI scrolling
				SyncSelectionRequested?.Invoke(this, new SyncSelectionEventArgs { SelectedItem = value, MatchedItem = matchedPair.Existing });
			}
		}

		public TomlDiffResult SelectedMergedTomlLine
		{
			get => _selectedMergedTomlLine;
			set
			{
				if ( _selectedMergedTomlLine == value ) return;
				_selectedMergedTomlLine = value;
				OnPropertyChanged();

				// TODO: Sync selection to preview list and/or source components
			}
		}

		public bool ComparisonVisible => _selectedExistingItem != null || _selectedIncomingItem != null;

		public bool CanLinkItems => _selectedExistingItem != null && _selectedIncomingItem != null &&
									!_matchedPairs.Any(p =>
										p.Existing == _selectedExistingItem || p.Incoming == _selectedIncomingItem);

		public string LinkButtonText
		{
			get
			{
				if ( _selectedExistingItem == null || _selectedIncomingItem == null )
					return "Select one from each list to link";
				if ( CanLinkItems )
					return $"🔗 Link \"{_selectedExistingItem.Name}\" ↔ \"{_selectedIncomingItem.Name}\"";
				return "Already linked or part of another link";

			}
		}

		public string ComparisonText
		{
			get
			{
				if ( _selectedExistingItem == null && _selectedIncomingItem == null )
					return "Select a component to see details";

				ComponentConflictItem item = _selectedExistingItem ?? _selectedIncomingItem;
				Component component = item.Component;

				var sb = new System.Text.StringBuilder();
				_ = sb.AppendLine($"Component: {component.Name}");
				_ = sb.AppendLine($"Author: {component.Author}");
				string categoryStr = component.Category != null && component.Category.Count > 0
					? string.Join(", ", component.Category)
					: "No category";
				_ = sb.AppendLine($"Category: {categoryStr} / {component.Tier}");
				_ = sb.AppendLine($"Instructions: {component.Instructions.Count}");
				_ = sb.AppendLine($"Options: {component.Options.Count}");
				_ = sb.AppendLine($"Dependencies: {component.Dependencies.Count}");
				_ = sb.AppendLine($"Links: {component.ModLink.Count}");

				// If there's a match, show differences
				if ( _selectedExistingItem != null )
				{
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
						_matchedPairs.FirstOrDefault(p => p.Existing == _selectedExistingItem);
					if ( matchedPair.Incoming == null )
						return sb.ToString();
					_ = sb.AppendLine("\n🔄 DIFFERENCES FROM INCOMING:");
					CompareComponents(component, matchedPair.Incoming.Component, sb);
				}
				else if ( _selectedIncomingItem != null )
				{
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
						_matchedPairs.FirstOrDefault(p => p.Incoming == _selectedIncomingItem);
					if ( matchedPair.Existing != null )
					{
						_ = sb.AppendLine("\n🔄 DIFFERENCES FROM EXISTING:");
						CompareComponents(component, matchedPair.Existing.Component, sb);
					}
				}

				return sb.ToString();
			}
		}

		private static void CompareComponents(Component a, Component b, System.Text.StringBuilder sb)
		{
			if ( a.Name != b.Name ) _ = sb.AppendLine($"  Name: '{a.Name}' vs '{b.Name}'");
			if ( a.Author != b.Author ) _ = sb.AppendLine($"  Author: '{a.Author}' vs '{b.Author}'");
			if ( a.Category != b.Category ) _ = sb.AppendLine($"  Category: '{a.Category}' vs '{b.Category}'");
			if ( a.Tier != b.Tier ) _ = sb.AppendLine($"  Tier: '{a.Tier}' vs '{b.Tier}'");
			if ( a.Instructions.Count != b.Instructions.Count )
				_ = sb.AppendLine($"  Instructions: {a.Instructions.Count} vs {b.Instructions.Count}");
			if ( a.Options.Count != b.Options.Count )
				_ = sb.AppendLine($"  Options: {a.Options.Count} vs {b.Options.Count}");
			if ( a.ModLink.Count != b.ModLink.Count )
				_ = sb.AppendLine($"  Links: {a.ModLink.Count} vs {b.ModLink.Count}");
		}

		public bool UseIncomingOrder
		{
			get => _useIncomingOrder;
			set
			{
				if ( _useIncomingOrder == value ) return;
				_useIncomingOrder = value;
				OnPropertyChanged();
			}
		}

		public bool SelectAllExisting
		{
			get => _selectAllExisting;
			set
			{
				if ( _selectAllExisting == value ) return;
				_selectAllExisting = value;

				foreach ( ComponentConflictItem item in ExistingComponents )
				{
					item.PropertyChanged -= OnItemSelectionChanged;
					item.IsSelected = value;
					item.PropertyChanged += OnItemSelectionChanged;
				}

				OnPropertyChanged();
				UpdatePreview();
			}
		}

		public bool SelectAllIncoming
		{
			get => _selectAllIncoming;
			set
			{
				if ( _selectAllIncoming == value ) return;
				_selectAllIncoming = value;

				foreach ( ComponentConflictItem item in IncomingComponents )
				{
					item.PropertyChanged -= OnItemSelectionChanged;
					item.IsSelected = value;
					item.PropertyChanged += OnItemSelectionChanged;
				}

				OnPropertyChanged();
				UpdatePreview();
			}
		}

		public bool SkipDuplicates
		{
			get => _skipDuplicates;
			set
			{
				if ( _skipDuplicates == value ) return;
				_skipDuplicates = value;
				OnPropertyChanged();
			}
		}

		private void BuildConflictItems(
			List<Component> existingComponents,
			List<Component> incomingComponents,
			Func<Component, Component, bool> matchFunc)
		{
			var existingSet = new HashSet<Component>();
			var incomingSet = new HashSet<Component>();

			// Build a list of all potential matches with scores
			var potentialMatches = (from existing in existingComponents from incoming in incomingComponents where matchFunc(existing, incoming) let score = FuzzyMatcher.GetComponentMatchScore(existing, incoming) select (existing, incoming, score)).ToList();

			// Sort by score descending to prioritize best matches
			potentialMatches = potentialMatches.OrderByDescending(m => m.score).Cast<(Component, Component, double)>().ToList();

			// Create a dictionary to track matches
			var existingToIncomingMatch = new Dictionary<Component, Component>();
			var incomingToExistingMatch = new Dictionary<Component, Component>();
			var existingItemLookup = new Dictionary<Component, ComponentConflictItem>();
			var incomingItemLookup = new Dictionary<Component, ComponentConflictItem>();

			// Create one-to-one matches, picking best matches first
			foreach ( (Component existing, Component incoming, double _) in potentialMatches )
			{
				// Skip if either component is already matched
				if ( existingSet.Contains(existing) || incomingSet.Contains(incoming) )
					continue;

				// Record the match
				existingToIncomingMatch[existing] = incoming;
				incomingToExistingMatch[incoming] = existing;

				_ = existingSet.Add(existing);
				_ = incomingSet.Add(incoming);
			}

			// Now add existing components in their original order
			foreach ( Component existing in existingComponents )
			{
				ComponentConflictItem existingItem;

				if ( existingToIncomingMatch.ContainsKey(existing) )
				{
					// This is a matched component
					existingItem = new ComponentConflictItem(existing, true, ComponentConflictStatus.Matched);
					existingItem.PropertyChanged += OnItemSelectionChanged;
					existingItem.IsSelected = false; // Default: prefer incoming for matches

					existingItemLookup[existing] = existingItem;
				}
				else
				{
					// Unmatched existing item
					existingItem = new ComponentConflictItem(existing, true, ComponentConflictStatus.ExistingOnly);
					existingItem.PropertyChanged += OnItemSelectionChanged;
					existingItem.IsSelected = true; // Keep unmatched existing
					_existingOnly.Add(existingItem);
				}

				ExistingComponents.Add(existingItem);
			}

			// Now add incoming components in their original order
			foreach ( Component incoming in incomingComponents )
			{
				ComponentConflictItem incomingItem;

				if ( incomingToExistingMatch.ContainsKey(incoming) )
				{
					// This is a matched component
					incomingItem = new ComponentConflictItem(incoming, false, ComponentConflictStatus.Matched);
					incomingItem.PropertyChanged += OnItemSelectionChanged;
					incomingItem.IsSelected = true; // Default: prefer incoming for matches

					incomingItemLookup[incoming] = incomingItem;
				}
				else
				{
					// Incoming-only item (new)
					incomingItem = new ComponentConflictItem(incoming, false, ComponentConflictStatus.New);
					incomingItem.PropertyChanged += OnItemSelectionChanged;
					incomingItem.IsSelected = true;
					_incomingOnly.Add(incomingItem);
				}

				IncomingComponents.Add(incomingItem);
			}

			// Build matched pairs list using the original match information
			foreach ( KeyValuePair<Component, Component> kvp in existingToIncomingMatch )
			{
				Component existing = kvp.Key;
				Component incoming = kvp.Value;

				ComponentConflictItem existingItem = existingItemLookup[existing];
				ComponentConflictItem incomingItem = incomingItemLookup[incoming];

				var pair = Tuple.Create(existingItem, incomingItem);
				_matchedPairs.Add((existingItem, incomingItem));

				// Field preferences will be created lazily with smart defaults when accessed

				// Resolve GUID conflict intelligently
				GuidConflictResolver.GuidResolution guidResolution =
					GuidConflictResolver.ResolveGuidConflict(existing, incoming);
				if ( guidResolution != null )
				{
					_guidResolutions[pair] = guidResolution;

					// Mark items if they have unresolved GUID conflicts
					if ( guidResolution.RequiresManualResolution )
					{
						existingItem.HasGuidConflict = true;
						incomingItem.HasGuidConflict = true;
						existingItem.GuidConflictTooltip = guidResolution.ConflictReason;
						incomingItem.GuidConflictTooltip = guidResolution.ConflictReason;
					}
				}
			}
		}

		// Quick action methods for power users
		public void SelectAllIncomingMatches()
		{
			foreach ( (ComponentConflictItem Existing, ComponentConflictItem Incoming) pair in _matchedPairs )
			{
				pair.Existing.IsSelected = false;
				pair.Incoming.IsSelected = true;
			}
			// Preview is auto-updated via OnItemSelectionChanged
		}

		public void SelectAllExistingMatches()
		{
			foreach ( (ComponentConflictItem Existing, ComponentConflictItem Incoming) pair in _matchedPairs )
			{
				pair.Existing.IsSelected = true;
				pair.Incoming.IsSelected = false;
			}
			// Preview is auto-updated via OnItemSelectionChanged
		}

		public void KeepAllNew()
		{
			// Toggle: if any are unselected, select all; if all are selected, deselect all
			bool anyUnselected = _incomingOnly.Any(item => !item.IsSelected);
			foreach ( ComponentConflictItem item in _incomingOnly )
				item.IsSelected = anyUnselected;
			// Preview is auto-updated via OnItemSelectionChanged
		}

		public void KeepAllExistingUnmatched()
		{
			// Toggle: if any are unselected, select all; if all are selected, deselect all
			bool anyUnselected = _existingOnly.Any(item => !item.IsSelected);
			foreach ( ComponentConflictItem item in _existingOnly )
				item.IsSelected = anyUnselected;
			// Preview is auto-updated via OnItemSelectionChanged
		}

		/// <summary>
		/// Sets all field preferences for all matched pairs to use incoming fields (where incoming has data)
		/// Respects smart merge logic - only sets to incoming if incoming has data, keeps existing if incoming is empty
		/// </summary>
		public void UseAllIncomingFields()
		{
			foreach ( (ComponentConflictItem Existing, ComponentConflictItem Incoming) pair in _matchedPairs )
			{
				var key = Tuple.Create(pair.Existing, pair.Incoming);

				// Ensure preferences exist by accessing CurrentFieldPreferences
				// This will lazily create them with smart defaults if they don't exist yet
				if ( !_fieldPreferences.ContainsKey(key) )
				{
					_ = CurrentFieldPreferences; // Access to trigger creation
				}

				// Get the actual stored preferences
				if ( _fieldPreferences.TryGetValue(key, out FieldMergePreference fieldPrefs) )
				{
					Component existing = pair.Existing.Component;
					Component incoming = pair.Incoming.Component;

					// For each field, prefer incoming if it has data, otherwise keep existing if that has data
					// Name
					if ( !string.IsNullOrWhiteSpace(incoming.Name) )
						fieldPrefs.Name = FieldMergePreference.FieldSource.UseIncoming;
					else if ( !string.IsNullOrWhiteSpace(existing.Name) )
						fieldPrefs.Name = FieldMergePreference.FieldSource.UseExisting;

					// Author
					if ( !string.IsNullOrWhiteSpace(incoming.Author) )
						fieldPrefs.Author = FieldMergePreference.FieldSource.UseIncoming;
					else if ( !string.IsNullOrWhiteSpace(existing.Author) )
						fieldPrefs.Author = FieldMergePreference.FieldSource.UseExisting;

					// Instructions
					if ( incoming.Instructions != null && incoming.Instructions.Count > 0 )
						fieldPrefs.Instructions = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existing.Instructions != null && existing.Instructions.Count > 0 )
						fieldPrefs.Instructions = FieldMergePreference.FieldSource.UseExisting;

					// Options
					if ( incoming.Options != null && incoming.Options.Count > 0 )
						fieldPrefs.Options = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existing.Options != null && existing.Options.Count > 0 )
						fieldPrefs.Options = FieldMergePreference.FieldSource.UseExisting;

					// For mergeable fields, use Merge if both have data, otherwise prefer incoming
					// Dependencies
					bool existingHasDeps = existing.Dependencies != null && existing.Dependencies.Count > 0;
					bool incomingHasDeps = incoming.Dependencies != null && incoming.Dependencies.Count > 0;
					if ( existingHasDeps && incomingHasDeps )
						fieldPrefs.Dependencies = FieldMergePreference.FieldSource.Merge;
					else if ( incomingHasDeps )
						fieldPrefs.Dependencies = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existingHasDeps )
						fieldPrefs.Dependencies = FieldMergePreference.FieldSource.UseExisting;

					// Restrictions
					bool existingHasRestrictions = existing.Restrictions != null && existing.Restrictions.Count > 0;
					bool incomingHasRestrictions = incoming.Restrictions != null && incoming.Restrictions.Count > 0;
					if ( existingHasRestrictions && incomingHasRestrictions )
						fieldPrefs.Restrictions = FieldMergePreference.FieldSource.Merge;
					else if ( incomingHasRestrictions )
						fieldPrefs.Restrictions = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existingHasRestrictions )
						fieldPrefs.Restrictions = FieldMergePreference.FieldSource.UseExisting;

					// InstallAfter
					bool existingHasInstallAfter = existing.InstallAfter != null && existing.InstallAfter.Count > 0;
					bool incomingHasInstallAfter = incoming.InstallAfter != null && incoming.InstallAfter.Count > 0;
					if ( existingHasInstallAfter && incomingHasInstallAfter )
						fieldPrefs.InstallAfter = FieldMergePreference.FieldSource.Merge;
					else if ( incomingHasInstallAfter )
						fieldPrefs.InstallAfter = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existingHasInstallAfter )
						fieldPrefs.InstallAfter = FieldMergePreference.FieldSource.UseExisting;

					// ModLink
					bool existingHasModLink = existing.ModLink != null && existing.ModLink.Count > 0;
					bool incomingHasModLink = incoming.ModLink != null && incoming.ModLink.Count > 0;
					if ( existingHasModLink && incomingHasModLink )
						fieldPrefs.ModLink = FieldMergePreference.FieldSource.Merge;
					else if ( incomingHasModLink )
						fieldPrefs.ModLink = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existingHasModLink )
						fieldPrefs.ModLink = FieldMergePreference.FieldSource.UseExisting;

					// Language
					bool existingHasLanguage = existing.Language != null && existing.Language.Count > 0;
					bool incomingHasLanguage = incoming.Language != null && incoming.Language.Count > 0;
					if ( existingHasLanguage && incomingHasLanguage )
						fieldPrefs.Language = FieldMergePreference.FieldSource.Merge;
					else if ( incomingHasLanguage )
						fieldPrefs.Language = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existingHasLanguage )
						fieldPrefs.Language = FieldMergePreference.FieldSource.UseExisting;

					// Other string fields - prefer incoming if it has data
					if ( !string.IsNullOrWhiteSpace(incoming.Description) )
						fieldPrefs.Description = FieldMergePreference.FieldSource.UseIncoming;
					else if ( !string.IsNullOrWhiteSpace(existing.Description) )
						fieldPrefs.Description = FieldMergePreference.FieldSource.UseExisting;

					if ( !string.IsNullOrWhiteSpace(incoming.Directions) )
						fieldPrefs.Directions = FieldMergePreference.FieldSource.UseIncoming;
					else if ( !string.IsNullOrWhiteSpace(existing.Directions) )
						fieldPrefs.Directions = FieldMergePreference.FieldSource.UseExisting;

					if ( incoming.Category != null && incoming.Category.Count > 0 )
						fieldPrefs.Category = FieldMergePreference.FieldSource.UseIncoming;
					else if ( existing.Category != null && existing.Category.Count > 0 )
						fieldPrefs.Category = FieldMergePreference.FieldSource.UseExisting;

					if ( !string.IsNullOrWhiteSpace(incoming.Tier) )
						fieldPrefs.Tier = FieldMergePreference.FieldSource.UseIncoming;
					else if ( !string.IsNullOrWhiteSpace(existing.Tier) )
						fieldPrefs.Tier = FieldMergePreference.FieldSource.UseExisting;

					if ( !string.IsNullOrWhiteSpace(incoming.InstallationMethod) )
						fieldPrefs.InstallationMethod = FieldMergePreference.FieldSource.UseIncoming;
					else if ( !string.IsNullOrWhiteSpace(existing.InstallationMethod) )
						fieldPrefs.InstallationMethod = FieldMergePreference.FieldSource.UseExisting;
				}
			}

			// Update preview for currently selected item
			OnPropertyChanged(nameof(CurrentFieldPreferences));
			OnPropertyChanged(nameof(PreviewName));
			OnPropertyChanged(nameof(PreviewAuthor));
			OnPropertyChanged(nameof(PreviewInstructionsCount));
			UpdatePreview();
		}

		/// <summary>
		/// Sets all field preferences for all matched pairs to use existing fields (where existing has data)
		/// Respects smart merge logic - only sets to existing if existing has data, keeps incoming if existing is empty
		/// </summary>
		public void UseAllExistingFields()
		{
			foreach ( (ComponentConflictItem Existing, ComponentConflictItem Incoming) pair in _matchedPairs )
			{
				var key = Tuple.Create(pair.Existing, pair.Incoming);

				// Ensure preferences exist by accessing CurrentFieldPreferences
				// This will lazily create them with smart defaults if they don't exist yet
				if ( !_fieldPreferences.ContainsKey(key) )
				{
					_ = CurrentFieldPreferences; // Access to trigger creation
				}

				// Get the actual stored preferences
				if ( _fieldPreferences.TryGetValue(key, out FieldMergePreference fieldPrefs) )
				{
					Component existing = pair.Existing.Component;
					Component incoming = pair.Incoming.Component;

					// For each field, prefer existing if it has data, otherwise keep incoming if that has data
					// Name
					if ( !string.IsNullOrWhiteSpace(existing.Name) )
						fieldPrefs.Name = FieldMergePreference.FieldSource.UseExisting;
					else if ( !string.IsNullOrWhiteSpace(incoming.Name) )
						fieldPrefs.Name = FieldMergePreference.FieldSource.UseIncoming;

					// Author
					if ( !string.IsNullOrWhiteSpace(existing.Author) )
						fieldPrefs.Author = FieldMergePreference.FieldSource.UseExisting;
					else if ( !string.IsNullOrWhiteSpace(incoming.Author) )
						fieldPrefs.Author = FieldMergePreference.FieldSource.UseIncoming;

					// Instructions
					if ( existing.Instructions != null && existing.Instructions.Count > 0 )
						fieldPrefs.Instructions = FieldMergePreference.FieldSource.UseExisting;
					else if ( incoming.Instructions != null && incoming.Instructions.Count > 0 )
						fieldPrefs.Instructions = FieldMergePreference.FieldSource.UseIncoming;

					// Options
					if ( existing.Options != null && existing.Options.Count > 0 )
						fieldPrefs.Options = FieldMergePreference.FieldSource.UseExisting;
					else if ( incoming.Options != null && incoming.Options.Count > 0 )
						fieldPrefs.Options = FieldMergePreference.FieldSource.UseIncoming;

					// For mergeable fields, use Merge if both have data, otherwise prefer existing
					// Dependencies
					bool existingHasDeps = existing.Dependencies != null && existing.Dependencies.Count > 0;
					bool incomingHasDeps = incoming.Dependencies != null && incoming.Dependencies.Count > 0;
					if ( existingHasDeps && incomingHasDeps )
						fieldPrefs.Dependencies = FieldMergePreference.FieldSource.Merge;
					else if ( existingHasDeps )
						fieldPrefs.Dependencies = FieldMergePreference.FieldSource.UseExisting;
					else if ( incomingHasDeps )
						fieldPrefs.Dependencies = FieldMergePreference.FieldSource.UseIncoming;

					// Restrictions
					bool existingHasRestrictions = existing.Restrictions != null && existing.Restrictions.Count > 0;
					bool incomingHasRestrictions = incoming.Restrictions != null && incoming.Restrictions.Count > 0;
					if ( existingHasRestrictions && incomingHasRestrictions )
						fieldPrefs.Restrictions = FieldMergePreference.FieldSource.Merge;
					else if ( existingHasRestrictions )
						fieldPrefs.Restrictions = FieldMergePreference.FieldSource.UseExisting;
					else if ( incomingHasRestrictions )
						fieldPrefs.Restrictions = FieldMergePreference.FieldSource.UseIncoming;

					// InstallAfter
					bool existingHasInstallAfter = existing.InstallAfter != null && existing.InstallAfter.Count > 0;
					bool incomingHasInstallAfter = incoming.InstallAfter != null && incoming.InstallAfter.Count > 0;
					if ( existingHasInstallAfter && incomingHasInstallAfter )
						fieldPrefs.InstallAfter = FieldMergePreference.FieldSource.Merge;
					else if ( existingHasInstallAfter )
						fieldPrefs.InstallAfter = FieldMergePreference.FieldSource.UseExisting;
					else if ( incomingHasInstallAfter )
						fieldPrefs.InstallAfter = FieldMergePreference.FieldSource.UseIncoming;

					// ModLink
					bool existingHasModLink = existing.ModLink != null && existing.ModLink.Count > 0;
					bool incomingHasModLink = incoming.ModLink != null && incoming.ModLink.Count > 0;
					if ( existingHasModLink && incomingHasModLink )
						fieldPrefs.ModLink = FieldMergePreference.FieldSource.Merge;
					else if ( existingHasModLink )
						fieldPrefs.ModLink = FieldMergePreference.FieldSource.UseExisting;
					else if ( incomingHasModLink )
						fieldPrefs.ModLink = FieldMergePreference.FieldSource.UseIncoming;

					// Language
					bool existingHasLanguage = existing.Language != null && existing.Language.Count > 0;
					bool incomingHasLanguage = incoming.Language != null && incoming.Language.Count > 0;
					if ( existingHasLanguage && incomingHasLanguage )
						fieldPrefs.Language = FieldMergePreference.FieldSource.Merge;
					else if ( existingHasLanguage )
						fieldPrefs.Language = FieldMergePreference.FieldSource.UseExisting;
					else if ( incomingHasLanguage )
						fieldPrefs.Language = FieldMergePreference.FieldSource.UseIncoming;

					// Other string fields - prefer existing if it has data
					if ( !string.IsNullOrWhiteSpace(existing.Description) )
						fieldPrefs.Description = FieldMergePreference.FieldSource.UseExisting;
					else if ( !string.IsNullOrWhiteSpace(incoming.Description) )
						fieldPrefs.Description = FieldMergePreference.FieldSource.UseIncoming;

					if ( !string.IsNullOrWhiteSpace(existing.Directions) )
						fieldPrefs.Directions = FieldMergePreference.FieldSource.UseExisting;
					else if ( !string.IsNullOrWhiteSpace(incoming.Directions) )
						fieldPrefs.Directions = FieldMergePreference.FieldSource.UseIncoming;

					if ( existing.Category != null && existing.Category.Count > 0 )
						fieldPrefs.Category = FieldMergePreference.FieldSource.UseExisting;
					else if ( incoming.Category != null && incoming.Category.Count > 0 )
						fieldPrefs.Category = FieldMergePreference.FieldSource.UseIncoming;

					if ( !string.IsNullOrWhiteSpace(existing.Tier) )
						fieldPrefs.Tier = FieldMergePreference.FieldSource.UseExisting;
					else if ( !string.IsNullOrWhiteSpace(incoming.Tier) )
						fieldPrefs.Tier = FieldMergePreference.FieldSource.UseIncoming;

					if ( !string.IsNullOrWhiteSpace(existing.InstallationMethod) )
						fieldPrefs.InstallationMethod = FieldMergePreference.FieldSource.UseExisting;
					else if ( !string.IsNullOrWhiteSpace(incoming.InstallationMethod) )
						fieldPrefs.InstallationMethod = FieldMergePreference.FieldSource.UseIncoming;
				}
			}

			// Update preview for currently selected item
			OnPropertyChanged(nameof(CurrentFieldPreferences));
			OnPropertyChanged(nameof(PreviewName));
			OnPropertyChanged(nameof(PreviewAuthor));
			OnPropertyChanged(nameof(PreviewInstructionsCount));
			UpdatePreview();
		}

		private void ApplySearchFilter()
		{
			FilteredExistingComponents.Clear();
			FilteredIncomingComponents.Clear();

			string searchLower = (SearchText ?? string.Empty).ToLowerInvariant().Trim();
			bool hasSearch = !string.IsNullOrEmpty(searchLower);

			foreach ( ComponentConflictItem item in ExistingComponents )
			{
				if ( !hasSearch ||
					 item.Name.IndexOf(searchLower, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
					 item.Author.IndexOf(searchLower, StringComparison.InvariantCultureIgnoreCase) >= 0 )
				{
					FilteredExistingComponents.Add(item);
				}
			}

			foreach ( ComponentConflictItem item in IncomingComponents )
			{
				if ( !hasSearch ||
					 item.Name.IndexOf(searchLower, StringComparison.InvariantCultureIgnoreCase) >= 0 ||
					 item.Author.IndexOf(searchLower, StringComparison.InvariantCultureIgnoreCase) >= 0 )
				{
					FilteredIncomingComponents.Add(item);
				}
			}
		}

		// Manual linking of components
		private bool CanLinkSelected() => _selectedExistingItem != null && _selectedIncomingItem != null &&
										  !_matchedPairs.Any(p =>
											  p.Existing == _selectedExistingItem ||
											  p.Incoming == _selectedIncomingItem);

		/// <summary>
		/// Manually chooses the GUID from the specified item when there's a conflict
		/// </summary>
		public void ChooseGuidForItem(ComponentConflictItem item)
		{
			// Find the matched pair for this item
			(ComponentConflictItem existing, ComponentConflictItem incoming) =
				_matchedPairs.FirstOrDefault(p => p.Existing == item || p.Incoming == item);

			if ( existing == null || incoming == null )
				return;

			var pairKey = Tuple.Create(existing, incoming);

			if ( !_guidResolutions.TryGetValue(pairKey, out GuidConflictResolver.GuidResolution resolution) )
				return;

			// User clicked on this item, so use this item's GUID
			if ( item == existing )
			{
				resolution.ChosenGuid = existing.Component.Guid;
				resolution.RejectedGuid = incoming.Component.Guid;
			}
			else
			{
				resolution.ChosenGuid = incoming.Component.Guid;
				resolution.RejectedGuid = existing.Component.Guid;
			}

			resolution.RequiresManualResolution = false; // User has resolved it

			// Clear the conflict indicators
			existing.HasGuidConflict = false;
			incoming.HasGuidConflict = false;

			// Update preview to reflect the change
			UpdatePreview();
		}

		private bool CanUnlinkSelected()
		{
			if ( _selectedExistingItem != null )
				return _matchedPairs.Any(p => p.Existing == _selectedExistingItem);
			if ( _selectedIncomingItem != null )
				return _matchedPairs.Any(p => p.Incoming == _selectedIncomingItem);
			return false;
		}

		private void LinkSelectedItems()
		{
			if ( !CanLinkSelected() ) return;

			// Remove from "only" lists if present
			_existingOnly.Remove(_selectedExistingItem);
			_incomingOnly.Remove(_selectedIncomingItem);

			// Update statuses
			_selectedExistingItem.UpdateStatus(ComponentConflictStatus.Matched);
			_selectedIncomingItem.UpdateStatus(ComponentConflictStatus.Matched);

			// Add to matched pairs
			_matchedPairs.Add((_selectedExistingItem, _selectedIncomingItem));

			// Field preferences will be created lazily with smart defaults when accessed

			// Default: prefer incoming for manual links
			_selectedExistingItem.IsSelected = false;
			_selectedIncomingItem.IsSelected = true;

			UpdatePreview();
			OnPropertyChanged(nameof(ConflictDescription));
			OnPropertyChanged(nameof(MergeImpactSummary));
			OnPropertyChanged(nameof(LinkButtonText));
			OnPropertyChanged(nameof(HasMatchedPairSelected));
			OnPropertyChanged(nameof(CurrentFieldPreferences));
			LinkSelectedCommand.RaiseCanExecuteChanged();
			UnlinkSelectedCommand.RaiseCanExecuteChanged();
		}

		private void UnlinkSelectedItems()
		{
			// Find the pair to unlink
			(ComponentConflictItem Existing, ComponentConflictItem Incoming) pairToRemove = default;

			if ( _selectedExistingItem != null )
				pairToRemove = _matchedPairs.FirstOrDefault(p => p.Existing == _selectedExistingItem);
			else if ( _selectedIncomingItem != null )
				pairToRemove = _matchedPairs.FirstOrDefault(p => p.Incoming == _selectedIncomingItem);

			if ( pairToRemove.Existing == null )
				return;
			ComponentConflictItem existingToUnlink = pairToRemove.Existing;
			ComponentConflictItem incomingToUnlink = pairToRemove.Incoming;

			// Remove from matched pairs
			_matchedPairs.Remove(pairToRemove);

			// Update statuses
			existingToUnlink.UpdateStatus(ComponentConflictStatus.ExistingOnly);
			incomingToUnlink.UpdateStatus(ComponentConflictStatus.New);

			// Add to "only" lists
			if ( !_existingOnly.Contains(existingToUnlink) )
				_existingOnly.Add(existingToUnlink);
			if ( !_incomingOnly.Contains(incomingToUnlink) )
				_incomingOnly.Add(incomingToUnlink);

			// Keep both selected by default
			existingToUnlink.IsSelected = true;
			incomingToUnlink.IsSelected = true;

			UpdatePreview();
			OnPropertyChanged(nameof(ConflictDescription));
			OnPropertyChanged(nameof(MergeImpactSummary));
			OnPropertyChanged(nameof(LinkButtonText));
			LinkSelectedCommand.RaiseCanExecuteChanged();
			UnlinkSelectedCommand.RaiseCanExecuteChanged();
		}

		private void OnItemSelectionChanged(object sender, PropertyChangedEventArgs e)
		{
			if ( e.PropertyName != nameof(ComponentConflictItem.IsSelected) )
				return;
			UpdatePreview();
			OnPropertyChanged(nameof(ConflictSummary));
			OnPropertyChanged(nameof(NewComponentsCount));
			OnPropertyChanged(nameof(UpdatedComponentsCount));
			OnPropertyChanged(nameof(KeptComponentsCount));
			OnPropertyChanged(nameof(RemovedComponentsCount));
			OnPropertyChanged(nameof(TotalChanges));
			OnPropertyChanged(nameof(MergeImpactSummary));
		}

		private void UpdatePreview()
		{
			PreviewComponents.Clear();

			var result = new List<PreviewItem>();

			if ( UseIncomingOrder )
			{
				// Incoming order takes priority
				int order = 1;

				foreach ( ComponentConflictItem incomingItem in IncomingComponents )
				{
					if ( !incomingItem.IsSelected ) continue;

					// Check if there's a matching existing item selected
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
						_matchedPairs.FirstOrDefault(p => p.Incoming == incomingItem);
					if ( matchedPair.Existing != null && matchedPair.Existing.IsSelected )
					{
						// Conflict: both selected, prefer incoming
						if ( !SkipDuplicates )
						{
							result.Add(new PreviewItem
							{
								OrderNumber = $"{order++}.",
								Name = incomingItem.Name,
								Source = "From: Incoming (Updated)",
								SourceColor = ThemeResourceHelper.MergeSourceIncomingBrush,
								Component = incomingItem.Component,
								StatusIcon = "⬆️",
								PositionChange = "UPDATED",
								PositionChangeColor = ThemeResourceHelper.MergePositionChangedBrush
							});
						}
						else
						{
							result.Add(new PreviewItem
							{
								OrderNumber = $"{order++}.",
								Name = incomingItem.Name,
								Source = "From: Incoming",
								SourceColor = ThemeResourceHelper.MergeSourceIncomingBrush,
								Component = incomingItem.Component,
								StatusIcon = "🔄",
								PositionChange = "MATCH",
								PositionChangeColor = ThemeResourceHelper.MergePositionNewBrush
							});
						}
					}
					else
					{
						result.Add(new PreviewItem
						{
							OrderNumber = $"{order++}.",
							Name = incomingItem.Name,
							Source = "From: Incoming",
							SourceColor = ThemeResourceHelper.MergeSourceIncomingBrush,
							Component = incomingItem.Component,
							StatusIcon = incomingItem.Status == ComponentConflictStatus.New ? "✨" : "🔄",
							PositionChange = incomingItem.Status == ComponentConflictStatus.New ? "NEW" : "MATCH",
							PositionChangeColor = incomingItem.Status == ComponentConflictStatus.New
								? ThemeResourceHelper.MergeStatusNewBrush
								: ThemeResourceHelper.MergePositionNewBrush
						});
					}
				}

				// Add existing-only items that are selected
				foreach ( ComponentConflictItem existingItem in _existingOnly.Where(e => e.IsSelected) )
				{
					// Find insertion point based on position in original existing list
					int insertAt = FindInsertionPoint(result, existingItem);
					result.Insert(insertAt,
						new PreviewItem
						{
							OrderNumber = $"{insertAt + 1}.",
							Name = existingItem.Name,
							Source = "From: Existing (Kept)",
							SourceColor = ThemeResourceHelper.MergeSourceExistingBrush,
							Component = existingItem.Component,
							StatusIcon = "📦",
							PositionChange = "KEPT",
							PositionChangeColor = ThemeResourceHelper.MergeStatusExistingOnlyBrush
						});
				}

				// Renumber
				for ( int i = 0; i < result.Count; i++ )
				{
					result[i].OrderNumber = $"{i + 1}.";
				}
			}
			else
			{
				// Existing order takes priority
				int order = 1;

				foreach ( ComponentConflictItem existingItem in ExistingComponents )
				{
					if ( !existingItem.IsSelected ) continue;

					// Check if there's a matching incoming item selected
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
						_matchedPairs.FirstOrDefault(p => p.Existing == existingItem);
					if ( matchedPair.Incoming != null && matchedPair.Incoming.IsSelected )
					{
						// Conflict: both selected, prefer existing
						if ( !SkipDuplicates )
						{
							result.Add(new PreviewItem
							{
								OrderNumber = $"{order++}.",
								Name = existingItem.Name,
								Source = "From: Existing (Kept)",
								SourceColor = ThemeResourceHelper.MergeSourceExistingBrush,
								Component = existingItem.Component,
								StatusIcon = "🔄",
								PositionChange = "MATCH",
								PositionChangeColor = ThemeResourceHelper.MergePositionNewBrush
							});
						}
						else
						{
							result.Add(new PreviewItem
							{
								OrderNumber = $"{order++}.",
								Name = existingItem.Name,
								Source = "From: Existing",
								SourceColor = ThemeResourceHelper.MergeSourceExistingBrush,
								Component = existingItem.Component,
								StatusIcon = "📦",
								PositionChange = "KEPT",
								PositionChangeColor = ThemeResourceHelper.MergeStatusExistingOnlyBrush
							});
						}
					}
					else
					{
						result.Add(new PreviewItem
						{
							OrderNumber = $"{order++}.",
							Name = existingItem.Name,
							Source = "From: Existing",
							SourceColor = ThemeResourceHelper.MergeSourceExistingBrush,
							Component = existingItem.Component,
							StatusIcon = "📦",
							PositionChange = "KEPT",
							PositionChangeColor = ThemeResourceHelper.MergeStatusExistingOnlyBrush
						});
					}
				}

				// Add incoming-only items that are selected
				result.AddRange(_incomingOnly.Where(i => i.IsSelected)
				.Select(incomingItem => new PreviewItem
				{
					OrderNumber = $"{order++}.",
					Name = incomingItem.Name,
					Source = "From: Incoming (New)",
					SourceColor = ThemeResourceHelper.MergeSourceIncomingBrush,
					Component = incomingItem.Component,
					StatusIcon = "✨",
					PositionChange = "NEW",
					PositionChangeColor = ThemeResourceHelper.MergeStatusNewBrush
				}));
			}

			foreach ( PreviewItem item in result )
			{
				PreviewComponents.Add(item);
			}

			// Update real-time merged components
			UpdateRealtimeMergedComponents();

			OnPropertyChanged(nameof(ConflictSummary));
		}

		private int FindInsertionPoint(List<PreviewItem> result, ComponentConflictItem itemToInsert)
		{
			// Find the position in the original existing list
			int originalIndex = ExistingComponents.ToList().FindIndex(c => c == itemToInsert);
			if ( originalIndex < 0 ) return result.Count;

			// Look for the nearest component after this one that's already in the result
			for ( int i = originalIndex + 1; i < ExistingComponents.Count; i++ )
			{
				ComponentConflictItem afterComponent = ExistingComponents[i];
				int afterIndexInResult = result.FindIndex(p => p.Component == afterComponent.Component);
				if ( afterIndexInResult >= 0 )
					return afterIndexInResult;
			}

			return result.Count;
		}

		private void UpdateRealtimeMergedComponents()
		{
			try
			{
				RealtimeMergedComponents.Clear();

				var mergedComponents = new List<Component>();
				var guidMap = new Dictionary<Guid, Guid>(); // Maps old GUIDs to new GUIDs

				foreach ( PreviewItem previewItem in PreviewComponents )
				{
					Component component = previewItem.Component;

					// If this component came from a match, actually merge the data
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
						_matchedPairs.FirstOrDefault(p =>
							p.Existing.Component == component || p.Incoming.Component == component);

					if ( matchedPair.Existing != null && matchedPair.Incoming != null )
					{
						// This is a matched pair - merge ALL fields from both components
						// Use field-level preferences if available
						var pair = Tuple.Create(matchedPair.Existing, matchedPair.Incoming);

						// Get field preferences - create with smart defaults if not exists
						if ( !_fieldPreferences.ContainsKey(pair) )
						{
							_fieldPreferences[pair] = CreateAndSubscribeFieldPreferences(
								matchedPair.Existing.Component,
								matchedPair.Incoming.Component
							);
						}
						FieldMergePreference fieldPrefs = _fieldPreferences[pair];

						// Merge using field preferences
						Component mergedComponent = ComponentMergeConflictViewModel.MergeComponentData(
							matchedPair.Existing.Component,
							matchedPair.Incoming.Component,
							fieldPrefs
						);

						// Apply intelligent GUID resolution
						if ( _guidResolutions.TryGetValue(pair, out GuidConflictResolver.GuidResolution resolution) )
						{
							Guid chosenGuid = resolution.ChosenGuid;
							Guid rejectedGuid = resolution.RejectedGuid;

							// Apply the chosen GUID to the merged component
							mergedComponent.Guid = chosenGuid;

							// Track the mapping for updating references
							if ( chosenGuid != rejectedGuid )
								guidMap[rejectedGuid] = chosenGuid;
						}

						mergedComponents.Add(mergedComponent);
					}
					else
					{
						// Not a matched pair, just use the component as-is
						mergedComponents.Add(component);
					}
				}

				// Update all GUID references in dependencies, restrictions, and install-after
				foreach ( Component component in mergedComponents )
				{
					// Update Dependencies
					for ( int i = 0; i < component.Dependencies.Count; i++ )
					{
						if ( guidMap.TryGetValue(component.Dependencies[i], out Guid newGuid) )
							component.Dependencies[i] = newGuid;
					}

					// Update Restrictions
					for ( int i = 0; i < component.Restrictions.Count; i++ )
					{
						if ( guidMap.TryGetValue(component.Restrictions[i], out Guid newGuid) )
							component.Restrictions[i] = newGuid;
					}

					// Update InstallAfter
					for ( int i = 0; i < component.InstallAfter.Count; i++ )
					{
						if ( guidMap.TryGetValue(component.InstallAfter[i], out Guid newGuid) )
							component.InstallAfter[i] = newGuid;
					}
				}

				// Add to the observable collection
				foreach ( Component component in mergedComponents )
				{
					RealtimeMergedComponents.Add(component);
				}

				// Update TOML diff if we have a selected component
				UpdateCurrentTomlDiff();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating real-time merged components");
			}
		}

		public List<Component> GetMergedComponents()
		{
			var mergedComponents = new List<Component>();
			var guidMap = new Dictionary<Guid, Guid>(); // Maps old GUIDs to new GUIDs

			foreach ( PreviewItem previewItem in PreviewComponents )
			{
				Component component = previewItem.Component;

				// If this component came from a match, actually merge the data
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
					_matchedPairs.FirstOrDefault(p =>
						p.Existing.Component == component || p.Incoming.Component == component);

				if ( matchedPair.Existing != null && matchedPair.Incoming != null )
				{
					// This is a matched pair - merge ALL fields from both components
					// Use field-level preferences if available
					var pair = Tuple.Create(matchedPair.Existing, matchedPair.Incoming);

					// Get field preferences - create with smart defaults if not exists
					if ( !_fieldPreferences.TryGetValue(pair, out FieldMergePreference fieldPrefs) )
					{
						fieldPrefs = CreateAndSubscribeFieldPreferences(
							matchedPair.Existing.Component,
							matchedPair.Incoming.Component
						);
						_fieldPreferences[pair] = fieldPrefs;
					}

					// Merge using field preferences
					Component mergedComponent = ComponentMergeConflictViewModel.MergeComponentData(
						matchedPair.Existing.Component,
						matchedPair.Incoming.Component,
						fieldPrefs
					);

					// Apply intelligent GUID resolution
					if ( _guidResolutions.TryGetValue(pair, out GuidConflictResolver.GuidResolution resolution) )
					{
						Guid chosenGuid = resolution.ChosenGuid;
						Guid rejectedGuid = resolution.RejectedGuid;

						// Apply the chosen GUID to the merged component
						mergedComponent.Guid = chosenGuid;

						// Track the mapping for updating references
						if ( chosenGuid != rejectedGuid )
							guidMap[rejectedGuid] = chosenGuid;
					}

					mergedComponents.Add(mergedComponent);
				}
				else
				{
					// Not a matched pair, just use the component as-is
					mergedComponents.Add(component);
				}
			}

			// Update all GUID references in dependencies, restrictions, and install-after
			foreach ( Component component in mergedComponents )
			{
				// Update Dependencies
				for ( int i = 0; i < component.Dependencies.Count; i++ )
				{
					if ( guidMap.TryGetValue(component.Dependencies[i], out Guid newGuid) )
						component.Dependencies[i] = newGuid;
				}

				// Update Restrictions
				for ( int i = 0; i < component.Restrictions.Count; i++ )
				{
					if ( guidMap.TryGetValue(component.Restrictions[i], out Guid newGuid) )
						component.Restrictions[i] = newGuid;
				}

				// Update InstallAfter
				for ( int i = 0; i < component.InstallAfter.Count; i++ )
				{
					if ( guidMap.TryGetValue(component.InstallAfter[i], out Guid newGuid) )
						component.InstallAfter[i] = newGuid;
				}
			}

			// Don't do cycle detection here - that only happens when Component.ConfirmComponentsInstallOrder fails
			// This merge is just combining two lists, not processing install order
			return mergedComponents;
		}

		/// <summary>
		/// Intelligently merges two components using field-level preferences.
		/// For each field: uses non-null/non-empty value if only one exists.
		/// For conflicts (both have values): uses the field preference to determine which source to use.
		/// </summary>
		private static Component MergeComponentData(Component existing, Component incoming, FieldMergePreference fieldPrefs)
		{
			// Use default preferences if none provided (prefer incoming)
			if ( fieldPrefs == null )
				fieldPrefs = new FieldMergePreference();

			// Create merged component
			var merged = new Component
			{
				// GUID will be set by caller based on intelligent GUID resolution
				Guid = existing.Guid,

				// Merge all string fields using preferences
				Name = MergeStringField(existing.Name, incoming.Name, fieldPrefs.Name),
				Author = MergeStringField(existing.Author, incoming.Author, fieldPrefs.Author),
				Description = MergeStringField(existing.Description, incoming.Description, fieldPrefs.Description),
				Directions = MergeStringField(existing.Directions, incoming.Directions, fieldPrefs.Directions),
				Category = MergeListField(existing.Category, incoming.Category, fieldPrefs.Category),
				Tier = MergeStringField(existing.Tier, incoming.Tier, fieldPrefs.Tier),
				InstallationMethod = MergeStringField(existing.InstallationMethod, incoming.InstallationMethod, fieldPrefs.InstallationMethod),

				// Merge lists using preferences
				Instructions = MergeListField(existing.Instructions, incoming.Instructions, fieldPrefs.Instructions),
				Options = MergeListField(existing.Options, incoming.Options, fieldPrefs.Options),

				// For Dependencies, Restrictions, InstallAfter, ModLink, Language - support merging
				Dependencies = fieldPrefs.Dependencies == FieldMergePreference.FieldSource.Merge
					? MergeLists(existing.Dependencies, incoming.Dependencies, deduplicate: true)
					: MergeListField(existing.Dependencies, incoming.Dependencies, fieldPrefs.Dependencies),

				Restrictions = fieldPrefs.Restrictions == FieldMergePreference.FieldSource.Merge
					? MergeLists(existing.Restrictions, incoming.Restrictions, deduplicate: true)
					: MergeListField(existing.Restrictions, incoming.Restrictions, fieldPrefs.Restrictions),

				InstallAfter = fieldPrefs.InstallAfter == FieldMergePreference.FieldSource.Merge
					? MergeLists(existing.InstallAfter, incoming.InstallAfter, deduplicate: true)
					: MergeListField(existing.InstallAfter, incoming.InstallAfter, fieldPrefs.InstallAfter),

				ModLink = fieldPrefs.ModLink == FieldMergePreference.FieldSource.Merge
					? MergeLists(existing.ModLink, incoming.ModLink, deduplicate: true)
					: MergeListField(existing.ModLink, incoming.ModLink, fieldPrefs.ModLink),

				Language = fieldPrefs.Language == FieldMergePreference.FieldSource.Merge
					? MergeLists(existing.Language, incoming.Language, deduplicate: true)
					: MergeListField(existing.Language, incoming.Language, fieldPrefs.Language),

				// State fields - always preserve existing state
				IsSelected = existing.IsSelected,
				InstallState = existing.InstallState,
				IsDownloaded = existing.IsDownloaded
			};

			return merged;

			// Helper to merge list fields based on preference
			T MergeListField<T>(T existingList, T incomingList, FieldMergePreference.FieldSource preference) where T : System.Collections.ICollection, new()
			{
				bool existingHasValues = existingList != null && existingList.Count > 0;
				bool incomingHasValues = incomingList != null && incomingList.Count > 0;

				switch ( existingHasValues )
				{
					// If only one has values, use it
					case false when !incomingHasValues:
						return new T();
					case false:
						return incomingList;
				}

				if ( !incomingHasValues )
					return existingList;

				// Both have values - use preference
				if ( preference == FieldMergePreference.FieldSource.UseExisting )
					return existingList;
				if ( preference == FieldMergePreference.FieldSource.UseIncoming )
					return incomingList;
				// Merge option for certain lists
				return existingList; // Default fallback
			}

			// Helper to merge string fields based on preference
			string MergeStringField(string existingVal, string incomingVal, FieldMergePreference.FieldSource preference)
			{
				bool existingHasValue = !string.IsNullOrWhiteSpace(existingVal);
				bool incomingHasValue = !string.IsNullOrWhiteSpace(incomingVal);

				switch ( existingHasValue )
				{
					// If only one has a value, use it
					case false when !incomingHasValue:
						return null;
					case false:
						return incomingVal;
				}

				if ( !incomingHasValue )
					return existingVal;

				// Both have values - use preference
				return preference == FieldMergePreference.FieldSource.UseExisting ? existingVal : incomingVal;
			}
		}

		/// <summary>
		/// Merges two lists, optionally deduplicating.
		/// </summary>
		private static List<T> MergeLists<T>(List<T> existingList, List<T> incomingList, bool deduplicate = false)
		{
			if ( existingList == null && incomingList == null )
				return new List<T>();

			if ( existingList == null || existingList.Count == 0 )
				return incomingList != null ? new List<T>(incomingList) : new List<T>();

			if ( incomingList == null || incomingList.Count == 0 )
				return new List<T>(existingList);

			// Both have data - combine them
			var merged = new List<T>(existingList);

			if ( deduplicate )
			{
				// Add only unique items from incoming
				foreach ( T item in incomingList )
				{
					if ( !merged.Contains(item) )
						merged.Add(item);
				}
			}
			else
			{
				// Add all items from incoming
				merged.AddRange(incomingList);
			}

			return merged;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		// Events for UI synchronization
		public event EventHandler<JumpToRawViewEventArgs> JumpToRawViewRequested;
		public event EventHandler<SyncSelectionEventArgs> SyncSelectionRequested;

		private void JumpToRawView(ComponentConflictItem item)
		{
			if ( item == null ) return;
			JumpToRawViewRequested?.Invoke(this, new JumpToRawViewEventArgs { Item = item });
		}

		public void UpdateExistingTomlView()
		{
			try
			{
				var selectedComponents = ExistingComponents.Where(c => c.IsSelected).Select(c => c.Component).ToList();

				// Build line number map as we generate TOML
				_existingComponentLineNumbers.Clear();
				int currentLine = 1;

				var newCollection = new ObservableCollection<TomlDiffResult>
				{
					// Header
					new TomlDiffResult
					{
						DiffType = DiffType.Unchanged, Text = "# Component List", LineNumber = currentLine++
					},
					new TomlDiffResult { DiffType = DiffType.Unchanged, Text = "", LineNumber = currentLine++ }
				};

				for ( int i = 0; i < selectedComponents.Count; i++ )
				{
					Component component = selectedComponents[i];
					string componentGuid = component.Guid.ToString();

					if ( i > 0 )
						newCollection.Add(new TomlDiffResult { DiffType = DiffType.Unchanged, Text = "", LineNumber = currentLine++, ComponentGuid = componentGuid });

					// Store starting line for this component
					_existingComponentLineNumbers[component] = currentLine;
					// Determine diff type for this component based on selection state
					ComponentConflictItem conflictItem = ExistingComponents.FirstOrDefault(ci => ci.Component == component);
					DiffType componentDiffType = DiffType.Removed; // Default: not selected = will be removed

					if ( conflictItem != null )
					{
						if ( conflictItem.IsSelected )
						{
							// Selected: will be used in final merge
							(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Existing == conflictItem);

							if ( matchedPair.Incoming != null && matchedPair.Incoming.IsSelected )
							{
								// Both selected: will be merged/updated
								componentDiffType = DiffType.Modified;
							}
							else
							{
								// Only existing selected: will be kept as-is
								componentDiffType = DiffType.Added; // Show as green (being kept/used)
							}
						}
						// else: not selected = Removed (red)
					}

					// Component header
					newCollection.Add(new TomlDiffResult
					{
						DiffType = componentDiffType,
						Text = $"# Component {i + 1}: {component.Name}",
						LineNumber = currentLine++,
						ComponentGuid = componentGuid
					});

					// Component TOML lines
					string componentToml = component.SerializeComponent();
					string[] lines = componentToml.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

					foreach ( string line in lines )
					{
						newCollection.Add(new TomlDiffResult { DiffType = componentDiffType, Text = line, LineNumber = currentLine++, ComponentGuid = componentGuid });
					}
				}

				// Replace the entire collection reference to avoid selection issues
				ExistingComponentsToml = newCollection;
				OnPropertyChanged(nameof(ExistingComponentsToml));
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating existing TOML view");
			}
		}

		public void UpdateIncomingTomlView()
		{
			try
			{
				var selectedComponents = IncomingComponents.Where(c => c.IsSelected).Select(c => c.Component).ToList();

				// Build line number map as we generate TOML
				_incomingComponentLineNumbers.Clear();
				int currentLine = 1;

				var newCollection = new ObservableCollection<TomlDiffResult>
				{
					// Header
					new TomlDiffResult { DiffType = DiffType.Unchanged, Text = "# Component List", LineNumber = currentLine++ },
					new TomlDiffResult { DiffType = DiffType.Unchanged, Text = "", LineNumber = currentLine++ }
				};

				for ( int i = 0; i < selectedComponents.Count; i++ )
				{
					Component component = selectedComponents[i];
					string componentGuid = component.Guid.ToString();

					if ( i > 0 )
						newCollection.Add(new TomlDiffResult { DiffType = DiffType.Unchanged, Text = "", LineNumber = currentLine++, ComponentGuid = componentGuid });

					// Store starting line for this component
					_incomingComponentLineNumbers[component] = currentLine;

					// Determine diff type for this component based on what it will do
					ComponentConflictItem conflictItem = IncomingComponents.FirstOrDefault(ci => ci.Component == component);
					DiffType componentDiffType = DiffType.Added; // Default: new component

					if ( conflictItem != null )
					{
						// Check if there's a matching existing component
						ComponentConflictItem existing = _matchedPairs.FirstOrDefault(p => p.Incoming == conflictItem).Existing;

						if ( existing != null )
						{
							// There's a matching existing component
							if ( existing.IsSelected && conflictItem.IsSelected )
							{
								// Both selected: will merge/update the existing one
								componentDiffType = DiffType.Modified;
							}
							else if ( conflictItem.IsSelected )
							{
								// Only incoming selected: will add/replace
								componentDiffType = DiffType.Added;
							}
							else
							{
								// Neither selected or only existing selected
								componentDiffType = DiffType.Unchanged;
							}
						}
						else
						{
							// No matching existing component: brand new
							componentDiffType = DiffType.Added;
						}
					}

					// Component header
					newCollection.Add(new TomlDiffResult
					{
						DiffType = componentDiffType,
						Text = $"# Component {i + 1}: {component.Name}",
						LineNumber = currentLine++,
						ComponentGuid = componentGuid
					});

					// Component TOML lines
					string componentToml = component.SerializeComponent();
					string[] lines = componentToml.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

					foreach ( string line in lines )
					{
						newCollection.Add(new TomlDiffResult
						{
							DiffType = componentDiffType,
							Text = line,
							LineNumber = currentLine++,
							ComponentGuid = componentGuid
						});
					}
				}

				// Replace the entire collection reference to avoid selection issues
				IncomingComponentsToml = newCollection;
				OnPropertyChanged(nameof(IncomingComponentsToml));
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating incoming TOML view");
			}
		}

		public void UpdateMergedTomlView()
		{
			try
			{
				MergedComponentsToml.Clear();

				// Get the existing and merged TOML
				string existingToml = GenerateFullToml(ExistingComponents.Where(c => c.IsSelected).Select(c => c.Component).ToList());
				string mergedToml = GenerateFullToml(RealtimeMergedComponents.ToList());

				// Generate diff between existing and merged
				List<TomlDiffResult> diffResults = GenerateTomlDiff(existingToml, mergedToml);
				foreach ( TomlDiffResult line in diffResults )
				{
					MergedComponentsToml.Add(line);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating merged TOML view");
			}
		}

		public int GetComponentLineNumber(ComponentConflictItem item)
		{
			if ( item == null ) return 0;

			Dictionary<Component, int> map = item.IsFromExisting ? _existingComponentLineNumbers : _incomingComponentLineNumbers;
			return map.ContainsKey(item.Component) ? map[item.Component] : 0;
		}

		// Track which line each component starts at in the TOML views
		private readonly Dictionary<Component, int> _existingComponentLineNumbers = new Dictionary<Component, int>();
		private readonly Dictionary<Component, int> _incomingComponentLineNumbers = new Dictionary<Component, int>();

		/// <summary>
		/// Generates TOML string from components (for diff generation)
		/// </summary>
		private static string GenerateFullToml(List<Component> components)
		{
			if ( components == null || components.Count == 0 )
				return "# No components selected";

			var sb = new System.Text.StringBuilder();
			_ = sb.AppendLine("# Component List");
			_ = sb.AppendLine();

			for ( int i = 0; i < components.Count; i++ )
			{
				if ( i > 0 )
					_ = sb.AppendLine();

				_ = sb.AppendLine($"# Component {i + 1}: {components[i].Name}");
				_ = sb.Append(components[i].SerializeComponent());
			}

			return sb.ToString();
		}

		private void UpdateCurrentTomlDiff()
		{
			try
			{
				CurrentTomlDiff.Clear();

				// If no component is selected, don't show diff
				if ( _selectedExistingItem == null && _selectedIncomingItem == null )
					return;

				ComponentConflictItem selectedItem = _selectedExistingItem ?? _selectedIncomingItem;
				if ( selectedItem == null )
					return;

				Component component = selectedItem.Component;

				// Find the corresponding merged component
				Component mergedComponent = RealtimeMergedComponents.FirstOrDefault(c => c.Name == component.Name);
				if ( mergedComponent == null )
					return;

				// Generate diff between original and merged component
				string originalToml = component.SerializeComponent();
				string mergedToml = mergedComponent.SerializeComponent();

				List<TomlDiffResult> diffResults = GenerateTomlDiff(originalToml, mergedToml);

				foreach ( TomlDiffResult result in diffResults )
				{
					CurrentTomlDiff.Add(result);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating TOML diff");
			}
		}

		private static List<TomlDiffResult> GenerateTomlDiff(string original, string merged)
		{
			var results = new List<TomlDiffResult>();

			if ( string.IsNullOrEmpty(original) && string.IsNullOrEmpty(merged) )
				return results;

			if ( string.IsNullOrEmpty(original) )
			{
				// Everything in merged is added
				string[] lines = merged.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
				results.AddRange(lines.Select((t, i) => new TomlDiffResult { DiffType = DiffType.Added, Text = t, LineNumber = i + 1 }));

				return results;
			}

			if ( string.IsNullOrEmpty(merged) )
			{
				// Everything in original is removed
				string[] lines = original.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
				results.AddRange(lines.Select((t, i) => new TomlDiffResult { DiffType = DiffType.Removed, Text = t, LineNumber = i + 1 }));

				return results;
			}

			// Both have content - do line-by-line comparison
			string[] originalLines = original.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			string[] mergedLines = merged.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

			int originalIndex = 0;
			int mergedIndex = 0;

			while ( originalIndex < originalLines.Length && mergedIndex < mergedLines.Length )
			{
				string originalLine = originalLines[originalIndex];
				string mergedLine = mergedLines[mergedIndex];

				if ( originalLine == mergedLine )
				{
					// Lines are identical
					results.Add(new TomlDiffResult
					{
						DiffType = DiffType.Unchanged,
						Text = originalLine,
						LineNumber = mergedIndex + 1
					});
					originalIndex++;
					mergedIndex++;
				}
				else if ( mergedIndex + 1 < mergedLines.Length && originalLine == mergedLines[mergedIndex + 1] )
				{
					// Current line was added
					results.Add(new TomlDiffResult
					{
						DiffType = DiffType.Added,
						Text = mergedLine,
						LineNumber = mergedIndex + 1
					});
					mergedIndex++;
				}
				else if ( originalIndex + 1 < originalLines.Length && mergedLine == originalLines[originalIndex + 1] )
				{
					// Current original line was removed
					results.Add(new TomlDiffResult
					{
						DiffType = DiffType.Removed,
						Text = originalLine,
						LineNumber = mergedIndex + 1
					});
					originalIndex++;
				}
				else
				{
					// Lines are different - treat as modified
					results.Add(new TomlDiffResult
					{
						DiffType = DiffType.Modified,
						Text = mergedLine,
						LineNumber = mergedIndex + 1
					});
					originalIndex++;
					mergedIndex++;
				}
			}

			// Handle remaining lines in original (removed)
			while ( originalIndex < originalLines.Length )
			{
				results.Add(new TomlDiffResult
				{
					DiffType = DiffType.Removed,
					Text = originalLines[originalIndex],
					LineNumber = mergedIndex + 1
				});
				originalIndex++;
			}

			// Handle remaining lines in merged (added)
			while ( mergedIndex < mergedLines.Length )
			{
				results.Add(new TomlDiffResult
				{
					DiffType = DiffType.Added,
					Text = mergedLines[mergedIndex],
					LineNumber = mergedIndex + 1
				});
				mergedIndex++;
			}

			return results;
		}

		public enum DiffType
		{
			Unchanged,
			Added,
			Removed,
			Modified
		}

		public class TomlDiffResult : INotifyPropertyChanged
		{
			private DiffType _diffType;
			private string _text;
			private int _lineNumber;
			private string _componentGuid;

			public DiffType DiffType
			{
				get => _diffType;
				set
				{
					if ( _diffType == value )
						return;
					_diffType = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(DiffColor));
				}
			}

			public string Text
			{
				get => _text;
				set
				{
					if ( _text == value )
						return;
					_text = value;
					OnPropertyChanged();
				}
			}

			public int LineNumber
			{
				get => _lineNumber;
				set
				{
					if ( _lineNumber == value )
						return;
					_lineNumber = value;
					OnPropertyChanged();
				}
			}

			/// <summary>
			/// GUID of the component this line belongs to (for selection tracking)
			/// </summary>
			public string ComponentGuid
			{
				get => _componentGuid;
				set
				{
					if ( _componentGuid == value )
						return;
					_componentGuid = value;
					OnPropertyChanged();
				}
			}

			public IBrush DiffColor
			{
				get
				{
					switch ( DiffType )
					{
						case DiffType.Added:
							return ThemeResourceHelper.MergeDiffAddedBrush;
						case DiffType.Removed:
							return ThemeResourceHelper.MergeDiffRemovedBrush;
						case DiffType.Modified:
							return ThemeResourceHelper.MergeDiffModifiedBrush;
						case DiffType.Unchanged:
						default:
							return ThemeResourceHelper.MergeDiffUnchangedBrush;
					}
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public enum ComponentConflictStatus
		{
			New, // Only in incoming list
			ExistingOnly, // Only in existing list
			Matched, // Exists in both lists
			Updated // Matched but has differences
		}

		public class ComponentConflictItem : INotifyPropertyChanged
		{
			private bool _isSelected;
			private bool _isVisuallySelected;
			private ComponentConflictStatus _status;
			private string _statusIcon;
			private IBrush _statusColor;
			private bool _hasGuidConflict;
			private string _guidConflictTooltip;

			public ComponentConflictItem([NotNull] Component component, bool isFromExisting,
				ComponentConflictStatus status)
			{
				Component = component;
				Name = component.Name;
				Author = string.IsNullOrWhiteSpace(component.Author) ? "Unknown Author" : component.Author;
				DateInfo = "Modified: N/A"; // Could add last modified tracking
				SizeInfo = $"{component.Instructions.Count} instruction(s)";
				IsFromExisting = isFromExisting;
				_status = status;
				_statusIcon = GetStatusIcon(status);
				_statusColor = GetStatusColor(status);
			}

			public Component Component { get; }
			public string Name { get; }
			public string Author { get; }
			public string DateInfo { get; }
			public string SizeInfo { get; }
			public bool IsFromExisting { get; }

			public ComponentConflictStatus Status
			{
				get => _status;
				private set
				{
					if ( _status == value ) return;
					_status = value;
					OnPropertyChanged();
				}
			}

			public string StatusIcon
			{
				get => _statusIcon;
				private set
				{
					if ( _statusIcon == value ) return;
					_statusIcon = value;
					OnPropertyChanged();
				}
			}

			public IBrush StatusColor
			{
				get => _statusColor;
				private set
				{
					if ( Equals(_statusColor, value) ) return;
					_statusColor = value;
					OnPropertyChanged();
				}
			}

			public bool IsVisuallySelected
			{
				get => _isVisuallySelected;
				set
				{
					if ( _isVisuallySelected == value ) return;
					_isVisuallySelected = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(SelectionBorderBrush));
					OnPropertyChanged(nameof(SelectionBackground));
				}
			}

			public IBrush SelectionBorderBrush => _isVisuallySelected
				? ThemeResourceHelper.MergeSelectionBorderBrush
				: Brushes.Transparent;

			public IBrush SelectionBackground => _isVisuallySelected
				? ThemeResourceHelper.MergeSelectionBackgroundBrush
				: Brushes.Transparent;

			public bool HasGuidConflict
			{
				get => _hasGuidConflict;
				set
				{
					if ( _hasGuidConflict == value ) return;
					_hasGuidConflict = value;
					OnPropertyChanged();
				}
			}

			public string GuidConflictTooltip
			{
				get => _guidConflictTooltip;
				set
				{
					if ( _guidConflictTooltip == value ) return;
					_guidConflictTooltip = value;
					OnPropertyChanged();
				}
			}

			/// <summary>
			/// Rich tooltip providing comprehensive component information
			/// </summary>
			public string RichTooltip
			{
				get
				{
					var sb = new System.Text.StringBuilder();
					Component component = Component;

					// Header with component name
					_ = sb.AppendLine($"📦 {component.Name}");
					_ = sb.AppendLine();

					// Author info
					if ( !string.IsNullOrEmpty(component.Author) )
						_ = sb.AppendLine($"👤 Author: {component.Author}");

					// Category and Tier
					if ( component.Category.Count > 0 )
						_ = sb.AppendLine($"📁 Category: {string.Join(", ", component.Category)}");

					if ( !string.IsNullOrEmpty(component.Tier) )
						_ = sb.AppendLine($"⭐ Tier: {component.Tier}");

					// Description
					if ( !string.IsNullOrEmpty(component.Description) )
					{
						_ = sb.AppendLine();
						_ = sb.AppendLine("📝 Description:");
						string desc = component.Description.Length > 200
							? component.Description.Substring(0, 200) + "..."
							: component.Description;
						_ = sb.AppendLine(desc);
					}

					// Installation info
					_ = sb.AppendLine();
					_ = sb.AppendLine($"🔧 Instructions: {component.Instructions.Count}");

					if ( !string.IsNullOrEmpty(component.InstallationMethod) )
						_ = sb.AppendLine($"⚙️ Method: {component.InstallationMethod}");

					// Dependencies
					if ( component.Dependencies.Count > 0 )
					{
						_ = sb.AppendLine();
						_ = sb.AppendLine($"✓ Requires: {component.Dependencies.Count} mod(s)");
					}

					// Restrictions
					if ( component.Restrictions.Count > 0 )
						_ = sb.AppendLine($"✗ Conflicts with: {component.Restrictions.Count} mod(s)");

					// Options
					if ( component.Options.Count > 0 )
						_ = sb.AppendLine($"⚙️ Has {component.Options.Count} optional component(s)");

					// Status
					_ = sb.AppendLine();
					_ = sb.AppendLine($"📊 Status: {Status}");

					// Download status
					if ( !component.IsDownloaded )
					{
						_ = sb.AppendLine();
						_ = sb.AppendLine("⚠️ Mod archive not downloaded");
						if ( component.ModLink.Count > 0 )
							_ = sb.AppendLine($"🔗 Download: {component.ModLink[0]}");
					}

					// GUID conflict warning if applicable
					if ( HasGuidConflict && !string.IsNullOrEmpty(GuidConflictTooltip) )
					{
						_ = sb.AppendLine();
						_ = sb.AppendLine("⚠️ GUID CONFLICT ⚠️");
						_ = sb.AppendLine(GuidConflictTooltip);
					}

					return sb.ToString();
				}
			}

			public void UpdateStatus(ComponentConflictStatus newStatus)
			{
				if ( Status == newStatus ) return;

				Status = newStatus;
				StatusIcon = GetStatusIcon(newStatus);
				StatusColor = GetStatusColor(newStatus);
			}

			private static string GetStatusIcon(ComponentConflictStatus status)
			{
				if ( status == ComponentConflictStatus.New )
					return "✨";
				if ( status == ComponentConflictStatus.ExistingOnly )
					return "📦";
				if ( status == ComponentConflictStatus.Matched )
					return "🔄";
				if ( status == ComponentConflictStatus.Updated )
					return "⬆️";
				return "";
			}

			private static IBrush GetStatusColor(ComponentConflictStatus status)
			{
				if ( status == ComponentConflictStatus.New )
					return ThemeResourceHelper.MergeStatusNewBrush;
				if ( status == ComponentConflictStatus.ExistingOnly )
					return ThemeResourceHelper.MergeStatusExistingOnlyBrush;
				if ( status == ComponentConflictStatus.Matched )
					return ThemeResourceHelper.MergeStatusMatchedBrush;
				if ( status == ComponentConflictStatus.Updated )
					return ThemeResourceHelper.MergeStatusUpdatedBrush;
				return ThemeResourceHelper.MergeStatusDefaultBrush;
			}

			public bool IsSelected
			{
				get => _isSelected;
				set
				{
					if ( _isSelected == value ) return;
					_isSelected = value;
					OnPropertyChanged();
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public class PreviewItem
		{
			public string OrderNumber { get; set; }
			public string Name { get; set; }
			public string Source { get; set; }
			public IBrush SourceColor { get; set; }
			public Component Component { get; set; }
			public string StatusIcon { get; set; }
			public string PositionChange { get; set; } // e.g., "↑3" or "↓2" or "NEW"
			public IBrush PositionChangeColor { get; set; }
		}
	}

	// Event args for jumping to raw view
	public class JumpToRawViewEventArgs : EventArgs
	{
		public ComponentMergeConflictViewModel.ComponentConflictItem Item { get; set; }
	}

	// Event args for syncing selection between lists
	public class SyncSelectionEventArgs : EventArgs
	{
		public ComponentMergeConflictViewModel.ComponentConflictItem SelectedItem { get; set; }
		public ComponentMergeConflictViewModel.ComponentConflictItem MatchedItem { get; set; }
	}
}

