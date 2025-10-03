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
				if ( value != null )
				{
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Existing == value);
					if ( matchedPair.Incoming != null )
					{
						// Clear other incoming highlights first
						foreach ( ComponentConflictItem item in IncomingComponents )
							if ( item != matchedPair.Incoming )
								item.IsVisuallySelected = false;

						_selectedIncomingItem = matchedPair.Incoming;
						matchedPair.Incoming.IsVisuallySelected = true;
						OnPropertyChanged(nameof(SelectedIncomingItem));
					}
				}
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
				if ( value != null )
				{
					(ComponentConflictItem Existing, ComponentConflictItem Incoming) matchedPair = _matchedPairs.FirstOrDefault(p => p.Incoming == value);
					if ( matchedPair.Existing != null )
					{
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
					if ( matchedPair.Incoming != null )
					{
						_ = sb.AppendLine("\n🔄 DIFFERENCES FROM INCOMING:");
						CompareComponents(component, matchedPair.Incoming.Component, sb);
					}
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

			// Find matches
			foreach ( Component existing in existingComponents )
			{
				Component match = incomingComponents.FirstOrDefault(inc => matchFunc(existing, inc));

				var existingItem = new ComponentConflictItem(existing, true, match != null ? ComponentConflictStatus.Matched : ComponentConflictStatus.ExistingOnly);
				existingItem.PropertyChanged += OnItemSelectionChanged;

				if ( match != null )
				{
					var incomingItem = new ComponentConflictItem(match, false, ComponentConflictStatus.Matched);
					incomingItem.PropertyChanged += OnItemSelectionChanged;
					incomingItem.IsSelected = true; // Default: prefer incoming for matches
					existingItem.IsSelected = false;

					_matchedPairs.Add((existingItem, incomingItem));
					_ = existingSet.Add(existing);
					_ = incomingSet.Add(match);

					ExistingComponents.Add(existingItem);
					IncomingComponents.Add(incomingItem);
				}
				else
				{
					existingItem.IsSelected = true; // Keep unmatched existing
					_existingOnly.Add(existingItem);
					_ = existingSet.Add(existing);
					ExistingComponents.Add(existingItem);
				}
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
			{
				pairToRemove = _matchedPairs.FirstOrDefault(p => p.Existing == _selectedExistingItem);
			}
			else if ( _selectedIncomingItem != null )
			{
				pairToRemove = _matchedPairs.FirstOrDefault(p => p.Incoming == _selectedIncomingItem);
			}

			if ( pairToRemove.Existing != null )
			{
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
				{
					return afterIndexInResult;
				}
			}

			return result.Count;
		}

		public List<Component> GetMergedComponents() => PreviewComponents.Select(p => p.Component).ToList();

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

		public ComponentConflictItem([NotNull] Component component, bool isFromExisting, ComponentConflictStatus status)
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

		public void UpdateStatus(ComponentConflictStatus newStatus)
		{
			if ( Status == newStatus ) return;

			Status = newStatus;
			StatusIcon = GetStatusIcon(newStatus);
			StatusColor = GetStatusColor(newStatus);
		}

		private string GetStatusIcon(ComponentConflictStatus status)
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

		private IBrush GetStatusColor(ComponentConflictStatus status)
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

