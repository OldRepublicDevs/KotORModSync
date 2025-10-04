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

namespace KOTORModSync
{
	public enum ConflictFilter
	{
		All,
		ConflictsOnly,
		NewOnly,
		ExistingOnly,
		UpdatedOnly
	}

	public class ComponentMergeConflictViewModel : INotifyPropertyChanged
	{
		private bool _useIncomingOrder = true;
		private bool _selectAllExisting;
		private bool _selectAllIncoming = true;
		private bool _skipDuplicates = true;
		private ConflictFilter _currentFilter = ConflictFilter.All;
		private ComponentConflictItem _selectedExistingItem;
		private ComponentConflictItem _selectedIncomingItem;
		private string _searchText = string.Empty;
		private readonly Dictionary<Tuple<ComponentConflictItem, ComponentConflictItem>, GuidConflictResolver.GuidResolution> _guidResolutions =
			new Dictionary<Tuple<ComponentConflictItem, ComponentConflictItem>, GuidConflictResolver.GuidResolution>();

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

			ExistingSourceInfo = existingSource;
			IncomingSourceInfo = incomingSource;

			// Initialize commands
			SelectAllIncomingMatchesCommand = new RelayCommand(_ => SelectAllIncomingMatches());
			SelectAllExistingMatchesCommand = new RelayCommand(_ => SelectAllExistingMatches());
			KeepAllNewCommand = new RelayCommand(_ => KeepAllNew());
			KeepAllExistingUnmatchedCommand = new RelayCommand(_ => KeepAllExistingUnmatched());
			LinkSelectedCommand = new RelayCommand(_ => LinkSelectedItems(), _ => CanLinkSelected());
			UnlinkSelectedCommand = new RelayCommand(_ => UnlinkSelectedItems(), _ => CanUnlinkSelected());

			// Build conflict items
			BuildConflictItems(existingComponents, incomingComponents, matchFunc);

			// Wire up property changes to update preview
			PropertyChanged += (_, e) =>
			{
				if ( e.PropertyName == nameof(UseIncomingOrder) ||
					e.PropertyName == nameof(SelectAllExisting) ||
					e.PropertyName == nameof(SelectAllIncoming) ||
					e.PropertyName == nameof(SkipDuplicates) )
				{
					UpdatePreview();
				}
				else if ( e.PropertyName == nameof(SearchText) )
				{
					ApplySearchFilter();
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
		private readonly List<(ComponentConflictItem Existing, ComponentConflictItem Incoming)> _matchedPairs =
			new List<(ComponentConflictItem, ComponentConflictItem)>();
		private readonly List<ComponentConflictItem> _existingOnly = new List<ComponentConflictItem>();
		private readonly List<ComponentConflictItem> _incomingOnly = new List<ComponentConflictItem>();

		public ObservableCollection<ComponentConflictItem> ExistingComponents { get; }
		public ObservableCollection<ComponentConflictItem> IncomingComponents { get; }
		public ObservableCollection<PreviewItem> PreviewComponents { get; }
		public ObservableCollection<ComponentConflictItem> FilteredExistingComponents { get; }
		public ObservableCollection<ComponentConflictItem> FilteredIncomingComponents { get; }

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
				return $"Selected: {existingSelected} from existing, {incomingSelected} from incoming → {PreviewComponents.Count} total components";
			}
		}

		// Statistics
		public int NewComponentsCount => _incomingOnly.Count(i => i.IsSelected);
		public int UpdatedComponentsCount => _matchedPairs.Count(p => p.Incoming.IsSelected && !p.Existing.IsSelected);
		public int KeptComponentsCount => _existingOnly.Count(e => e.IsSelected) + _matchedPairs.Count(p => p.Existing.IsSelected && !p.Incoming.IsSelected);
		public int RemovedComponentsCount => _existingOnly.Count(e => !e.IsSelected) + _matchedPairs.Count(p => p.Existing.IsSelected && !p.Incoming.IsSelected && !p.Incoming.IsSelected);
		public int TotalChanges => NewComponentsCount + UpdatedComponentsCount + RemovedComponentsCount;

		public string MergeImpactSummary =>
			$"📊 Merge Impact: {NewComponentsCount} new, {UpdatedComponentsCount} updated, {KeptComponentsCount} kept, {RemovedComponentsCount} removed";

		public ConflictFilter CurrentFilter
		{
			get => _currentFilter;
			set
			{
				if ( _currentFilter == value ) return;
				_currentFilter = value;
				OnPropertyChanged();
				ApplyFilter();
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

				// Auto-highlight matching item in incoming list
				if ( value == null )
					return;
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Existing == value);
				if ( matchedPair.Incoming == null )
					return;
				// Clear other incoming highlights first
				foreach ( ComponentConflictItem item in IncomingComponents )
				{
					if ( item != matchedPair.Incoming )
						item.IsVisuallySelected = false;
				}

				_selectedIncomingItem = matchedPair.Incoming;
				matchedPair.Incoming.IsVisuallySelected = true;
				OnPropertyChanged(nameof(SelectedIncomingItem));
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

				// Auto-highlight matching item in existing list
				if ( value == null )
					return;
				(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Incoming == value);
				if ( matchedPair.Existing == null )
					return;
				// Clear other existing highlights first
				foreach ( ComponentConflictItem item in ExistingComponents )
				{
					if ( item != matchedPair.Existing )
						item.IsVisuallySelected = false;
				}

				_selectedExistingItem = matchedPair.Existing;
				matchedPair.Existing.IsVisuallySelected = true;
				OnPropertyChanged(nameof(SelectedExistingItem));
			}
		}

		public bool ComparisonVisible => _selectedExistingItem != null || _selectedIncomingItem != null;

		public bool CanLinkItems => _selectedExistingItem != null && _selectedIncomingItem != null &&
									!_matchedPairs.Any(p => p.Existing == _selectedExistingItem || p.Incoming == _selectedIncomingItem);

		public string LinkButtonText
		{
			get
			{
				if ( _selectedExistingItem != null && _selectedIncomingItem != null )
				{
					if ( CanLinkItems )
						return $"🔗 Link \"{_selectedExistingItem.Name}\" ↔ \"{_selectedIncomingItem.Name}\"";
					return "Already linked or part of another link";
				}
				return "Select one from each list to link";
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
				_ = sb.AppendLine($"Category: {component.Category} / {component.Tier}");
				_ = sb.AppendLine($"Instructions: {component.Instructions.Count}");
				_ = sb.AppendLine($"Options: {component.Options.Count}");
				_ = sb.AppendLine($"Dependencies: {component.Dependencies.Count}");
				_ = sb.AppendLine($"Links: {component.ModLink.Count}");

				// If there's a match, show differences
				if ( _selectedExistingItem != null )
				{
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Existing == _selectedExistingItem);
					if ( matchedPair.Incoming == null )
						return sb.ToString();
					_ = sb.AppendLine("\n🔄 DIFFERENCES FROM INCOMING:");
					CompareComponents(component, matchedPair.Incoming.Component, sb);
				}
				else if ( _selectedIncomingItem != null )
				{
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Incoming == _selectedIncomingItem);
					if ( matchedPair.Existing != null )
					{
						_ = sb.AppendLine("\n🔄 DIFFERENCES FROM EXISTING:");
						CompareComponents(component, matchedPair.Existing.Component, sb);
					}
				}

				return sb.ToString();
			}
		}

		private void CompareComponents(Component a, Component b, System.Text.StringBuilder sb)
		{
			if ( a.Name != b.Name ) _ = sb.AppendLine($"  Name: '{a.Name}' vs '{b.Name}'");
			if ( a.Author != b.Author ) _ = sb.AppendLine($"  Author: '{a.Author}' vs '{b.Author}'");
			if ( a.Category != b.Category ) _ = sb.AppendLine($"  Category: '{a.Category}' vs '{b.Category}'");
			if ( a.Tier != b.Tier ) _ = sb.AppendLine($"  Tier: '{a.Tier}' vs '{b.Tier}'");
			if ( a.Instructions.Count != b.Instructions.Count ) _ = sb.AppendLine($"  Instructions: {a.Instructions.Count} vs {b.Instructions.Count}");
			if ( a.Options.Count != b.Options.Count ) _ = sb.AppendLine($"  Options: {a.Options.Count} vs {b.Options.Count}");
			if ( a.ModLink.Count != b.ModLink.Count ) _ = sb.AppendLine($"  Links: {a.ModLink.Count} vs {b.ModLink.Count}");
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
			var potentialMatches = new List<(Component Existing, Component Incoming, double Score)>();

			foreach ( Component existing in existingComponents )
			{
				foreach ( Component incoming in incomingComponents )
				{
					if ( !matchFunc(existing, incoming) )
						continue;
					double score = FuzzyMatcher.GetComponentMatchScore(existing, incoming);
					potentialMatches.Add((existing, incoming, score));
				}
			}

			// Sort by score descending to prioritize best matches
			potentialMatches = potentialMatches.OrderByDescending(m => m.Score).ToList();

			// Create one-to-one matches, picking best matches first
			foreach ( (Component existing, Component incoming, double _) in potentialMatches )
			{
				// Skip if either component is already matched
				if ( existingSet.Contains(existing) || incomingSet.Contains(incoming) )
					continue;

				var existingItem = new ComponentConflictItem(existing, true, ComponentConflictStatus.Matched);
				var incomingItem = new ComponentConflictItem(incoming, false, ComponentConflictStatus.Matched);

				existingItem.PropertyChanged += OnItemSelectionChanged;
				incomingItem.PropertyChanged += OnItemSelectionChanged;

				incomingItem.IsSelected = true; // Default: prefer incoming for matches
				existingItem.IsSelected = false;

				var pair = Tuple.Create(existingItem, incomingItem);
				_matchedPairs.Add((existingItem, incomingItem));

				// Resolve GUID conflict intelligently
				GuidConflictResolver.GuidResolution guidResolution = GuidConflictResolver.ResolveGuidConflict(existing, incoming);
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

				_ = existingSet.Add(existing);
				_ = incomingSet.Add(incoming);

				ExistingComponents.Add(existingItem);
				IncomingComponents.Add(incomingItem);
			}

			// Add unmatched existing items
			foreach ( Component existing in existingComponents )
			{
				if ( existingSet.Contains(existing) )
					continue;

				var existingItem = new ComponentConflictItem(existing, true, ComponentConflictStatus.ExistingOnly);
				existingItem.PropertyChanged += OnItemSelectionChanged;
				existingItem.IsSelected = true; // Keep unmatched existing
				_existingOnly.Add(existingItem);
				_ = existingSet.Add(existing);
				ExistingComponents.Add(existingItem);
			}

			// Add incoming-only items
			foreach ( Component incoming in incomingComponents )
			{
				if ( incomingSet.Contains(incoming) )
					continue;
				var incomingItem = new ComponentConflictItem(incoming, false, ComponentConflictStatus.New);
				incomingItem.PropertyChanged += OnItemSelectionChanged;
				incomingItem.IsSelected = true;
				_incomingOnly.Add(incomingItem);
				IncomingComponents.Add(incomingItem);
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
			foreach ( ComponentConflictItem item in _incomingOnly )
				item.IsSelected = true;
			// Preview is auto-updated via OnItemSelectionChanged
		}

		public void KeepAllExistingUnmatched()
		{
			foreach ( ComponentConflictItem item in _existingOnly )
				item.IsSelected = true;
			// Preview is auto-updated via OnItemSelectionChanged
		}

		private void ApplyFilter()
		{
			// Filter items based on current filter
			UpdatePreview();
			ApplySearchFilter();
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
										  !_matchedPairs.Any(p => p.Existing == _selectedExistingItem || p.Incoming == _selectedIncomingItem);

		/// <summary>
		/// Manually chooses the GUID from the specified item when there's a conflict
		/// </summary>
		public void ChooseGuidForItem(ComponentConflictItem item)
		{
			// Find the matched pair for this item
			(ComponentConflictItem Existing, ComponentConflictItem Incoming) pair =
				_matchedPairs.FirstOrDefault(p => p.Existing == item || p.Incoming == item);

			if ( pair.Existing == null || pair.Incoming == null )
				return;

			var pairKey = Tuple.Create(pair.Existing, pair.Incoming);

			if ( !_guidResolutions.TryGetValue(pairKey, out GuidConflictResolver.GuidResolution resolution) )
				return;

			// User clicked on this item, so use this item's GUID
			if ( item == pair.Existing )
			{
				resolution.ChosenGuid = pair.Existing.Component.Guid;
				resolution.RejectedGuid = pair.Incoming.Component.Guid;
				resolution.RequiresManualResolution = false; // User has resolved it
			}
			else
			{
				resolution.ChosenGuid = pair.Incoming.Component.Guid;
				resolution.RejectedGuid = pair.Existing.Component.Guid;
				resolution.RequiresManualResolution = false; // User has resolved it
			}

			// Clear the conflict indicators
			pair.Existing.HasGuidConflict = false;
			pair.Incoming.HasGuidConflict = false;

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

			// Default: prefer incoming for manual links
			_selectedExistingItem.IsSelected = false;
			_selectedIncomingItem.IsSelected = true;

			UpdatePreview();
			OnPropertyChanged(nameof(ConflictDescription));
			OnPropertyChanged(nameof(MergeImpactSummary));
			OnPropertyChanged(nameof(LinkButtonText));
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
			if ( e.PropertyName == nameof(ComponentConflictItem.IsSelected) )
			{
				UpdatePreview();
				OnPropertyChanged(nameof(ConflictSummary));
				OnPropertyChanged(nameof(NewComponentsCount));
				OnPropertyChanged(nameof(UpdatedComponentsCount));
				OnPropertyChanged(nameof(KeptComponentsCount));
				OnPropertyChanged(nameof(RemovedComponentsCount));
				OnPropertyChanged(nameof(TotalChanges));
				OnPropertyChanged(nameof(MergeImpactSummary));
			}
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
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Incoming == incomingItem);
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
							PositionChangeColor = incomingItem.Status == ComponentConflictStatus.New ? ThemeResourceHelper.MergeStatusNewBrush : ThemeResourceHelper.MergePositionNewBrush
						});
					}
				}

				// Add existing-only items that are selected
				foreach ( ComponentConflictItem existingItem in _existingOnly.Where(e => e.IsSelected) )
				{
					// Find insertion point based on position in original existing list
					int insertAt = FindInsertionPoint(result, existingItem);
					result.Insert(insertAt, new PreviewItem
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
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Existing == existingItem);
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
				foreach ( ComponentConflictItem incomingItem in _incomingOnly.Where(i => i.IsSelected) )
				{
					result.Add(new PreviewItem
					{
						OrderNumber = $"{order++}.",
						Name = incomingItem.Name,
						Source = "From: Incoming (New)",
						SourceColor = ThemeResourceHelper.MergeSourceIncomingBrush,
						Component = incomingItem.Component,
						StatusIcon = "✨",
						PositionChange = "NEW",
						PositionChangeColor = ThemeResourceHelper.MergeStatusNewBrush
					});
				}
			}

			foreach ( PreviewItem item in result )
			{
				PreviewComponents.Add(item);
			}

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

	public List<Component> GetMergedComponents()
		{
			var mergedComponents = new List<Component>();
			var guidMap = new Dictionary<Guid, Guid>(); // Maps old GUIDs to new GUIDs

			foreach ( PreviewItem previewItem in PreviewComponents )
			{
				Component component = previewItem.Component;

			// If this component came from a match, actually merge the data
			(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair =
				_matchedPairs.FirstOrDefault(p => p.Existing.Component == component || p.Incoming.Component == component);

			if ( matchedPair.Existing != null && matchedPair.Incoming != null )
			{
				// This is a matched pair - merge ALL fields from both components
				// User's checkbox selection determines which wins for conflicts
				var pair = Tuple.Create(matchedPair.Existing, matchedPair.Incoming);

				// Merge: take non-empty values, user's selection wins conflicts
				Component mergedComponent = MergeComponentData(
					matchedPair.Existing.Component,
					matchedPair.Incoming.Component,
					matchedPair.Incoming.IsSelected // Use incoming if it's selected
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
	/// Intelligently merges two components.
	/// For each field: uses non-null/non-empty value if only one exists.
	/// For conflicts (both have values): uses the selected component's value based on user's checkbox choice.
	/// </summary>
	private Component MergeComponentData(Component existing, Component incoming, bool useIncomingForConflicts)
	{
		// Helper to merge string fields
		string MergeString(string existingVal, string incomingVal)
		{
			bool existingHasValue = !string.IsNullOrWhiteSpace(existingVal);
			bool incomingHasValue = !string.IsNullOrWhiteSpace(incomingVal);

			if ( !existingHasValue && !incomingHasValue ) return null;
			if ( !existingHasValue ) return incomingVal;
			if ( !incomingHasValue ) return existingVal;

			// Both have values - CONFLICT - use user's selection
			return useIncomingForConflicts ? incomingVal : existingVal;
		}

		// Create merged component
		var merged = new Component
		{
			// GUID will be set by caller based on intelligent GUID resolution
			Guid = existing.Guid,

			// Merge all string fields
			Name = MergeString(existing.Name, incoming.Name),
			Author = MergeString(existing.Author, incoming.Author),
			Description = MergeString(existing.Description, incoming.Description),
			Directions = MergeString(existing.Directions, incoming.Directions),
			Category = MergeString(existing.Category, incoming.Category),
			Tier = MergeString(existing.Tier, incoming.Tier),
			InstallationMethod = MergeString(existing.InstallationMethod, incoming.InstallationMethod),

			// Merge lists - ALWAYS keep existing Instructions/Dependencies/Restrictions/InstallAfter if they exist
			// These are structural and shouldn't be lost
			Instructions = existing.Instructions.Count > 0 ? existing.Instructions :
						   incoming.Instructions.Count > 0 ? incoming.Instructions :
						   new ObservableCollection<Instruction>(),

			Dependencies = existing.Dependencies.Count > 0 ? existing.Dependencies :
						   incoming.Dependencies.Count > 0 ? incoming.Dependencies :
						   new List<Guid>(),

			Restrictions = existing.Restrictions.Count > 0 ? existing.Restrictions :
						   incoming.Restrictions.Count > 0 ? incoming.Restrictions :
						   new List<Guid>(),

			InstallAfter = existing.InstallAfter.Count > 0 ? existing.InstallAfter :
						   incoming.InstallAfter.Count > 0 ? incoming.InstallAfter :
						   new List<Guid>(),

			Options = existing.Options.Count > 0 ? existing.Options :
					  incoming.Options.Count > 0 ? incoming.Options :
					  new ObservableCollection<Option>(),

			// ModLink - combine both if both exist, otherwise use whichever has data
			ModLink = MergeLists(existing.ModLink, incoming.ModLink, deduplicate: true),

			// Language - prefer whichever has data
			Language = incoming.Language.Count > 0 ? incoming.Language :
					   existing.Language.Count > 0 ? existing.Language :
					   new List<string>(),

			// State fields - always preserve existing state
			IsSelected = existing.IsSelected,
			InstallState = existing.InstallState,
			IsDownloaded = existing.IsDownloaded
		};

		return merged;
	}

	/// <summary>
	/// Merges two lists, optionally deduplicating.
	/// </summary>
	private List<T> MergeLists<T>(List<T> existingList, List<T> incomingList, bool deduplicate = false)
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

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public enum ComponentConflictStatus
	{
		New,           // Only in incoming list
		ExistingOnly,  // Only in existing list
		Matched,       // Exists in both lists
		Updated        // Matched but has differences
	}

	public class ComponentConflictItem : INotifyPropertyChanged
	{
		private bool _isSelected;
		private bool _isHighlighted;
		private bool _isVisuallySelected;
		private ComponentConflictStatus _status;
		private string _statusIcon;
		private IBrush _statusColor;
		private bool _hasGuidConflict;
		private string _guidConflictTooltip;

		public ComponentConflictItem([NotNull] Component component, bool isFromExisting, ComponentConflictStatus status)
		{
			Component = component;
			Name = component.Name;
			Author = string.IsNullOrWhiteSpace(component.Author) ? "Unknown Author" : component.Author;
			DateInfo = "Modified: N/A"; // Could add last modified tracking
			SizeInfo = $"{component.Instructions.Count} instruction(s)";
			IsFromExisting = isFromExisting;
			_status = status;
			_statusIcon = ComponentConflictItem.GetStatusIcon(status);
			_statusColor = ComponentConflictItem.GetStatusColor(status);
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

		public bool IsHighlighted
		{
			get => _isHighlighted;
			set
			{
				if ( _isHighlighted == value ) return;
				_isHighlighted = value;
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

		public IBrush SelectionBorderBrush => _isVisuallySelected ? ThemeResourceHelper.MergeSelectionBorderBrush : Brushes.Transparent;
		public IBrush SelectionBackground => _isVisuallySelected ? ThemeResourceHelper.MergeSelectionBackgroundBrush : Brushes.Transparent;

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

		public void UpdateStatus(ComponentConflictStatus newStatus)
		{
			if ( Status == newStatus ) return;

			Status = newStatus;
			StatusIcon = ComponentConflictItem.GetStatusIcon(newStatus);
			StatusColor = ComponentConflictItem.GetStatusColor(newStatus);
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

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

