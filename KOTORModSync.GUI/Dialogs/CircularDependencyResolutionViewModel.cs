// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media;
using JetBrains.Annotations;
using Component = KOTORModSync.Core.Component;

namespace KOTORModSync.Dialogs
{
	public class CircularDependencyResolutionViewModel : INotifyPropertyChanged
	{
		private string _statusText;

		public ObservableCollection<ComponentItem> Components { get; }
		public ObservableCollection<SuggestionItem> Suggestions { get; }
		public string SummaryText { get; }
		public string DetailedCycleInfo { get; }
		public ICommand ApplySuggestionCommand { get; }

		public string StatusText
		{
			get => _statusText;
			set
			{
				if (_statusText == value) return;
				_statusText = value;
				OnPropertyChanged();
			}
		}

		public CircularDependencyResolutionViewModel(
			List<Component> components,
			CircularDependencyDetector.CircularDependencyResult cycleInfo)
		{
			Components = new ObservableCollection<ComponentItem>();
			Suggestions = new ObservableCollection<SuggestionItem>();
			ApplySuggestionCommand = new RelayCommand(ApplySuggestion);

			// Build summary
			int cycleCount = cycleInfo.Cycles.Count;
			SummaryText = $"Found {cycleCount} circular dependency cycle{(cycleCount > 1 ? "s" : "")} that prevent installation. " +
			              "These components have conflicting dependencies that cannot be automatically resolved.";

			// Build detailed cycle info
			DetailedCycleInfo = cycleInfo.DetailedErrorMessage;

			// Get components involved in cycles
			var componentsInCycles = new HashSet<Guid>();
			foreach ( List<Guid> cycle in cycleInfo.Cycles)
			{
				foreach ( Guid guid in cycle)
				{
					componentsInCycles.Add(guid);
				}
			}

			// Build component items
			foreach ( Component component in components)
			{
				bool isInCycle = componentsInCycles.Contains(component.Guid);
				var item = new ComponentItem(component, isInCycle);
				item.PropertyChanged += OnComponentSelectionChanged;
				Components.Add(item);
			}

			// Build suggestions
			List<Component> suggestedComponents = CircularDependencyDetector.SuggestComponentsToRemove(cycleInfo);
			foreach ( Component suggestion in suggestedComponents)
			{
				Suggestions.Add(new SuggestionItem
				{
					Component = suggestion,
					Text = $"❌ Uncheck: {suggestion.Name}" +
					       (!string.IsNullOrWhiteSpace(suggestion.Author) ? $" by {suggestion.Author}" : "")
				});
			}

			UpdateStatus();
		}

		private void OnComponentSelectionChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ComponentItem.IsSelected))
			{
				UpdateStatus();
			}
		}

		private void UpdateStatus()
		{
			int selectedCount = Components.Count(c => c.IsSelected);
			int totalCount = Components.Count;
			int uncheckedInCycle = Components.Count(c => c.IsInCycle && !c.IsSelected);

			if (uncheckedInCycle > 0)
			{
				StatusText = $"✅ {selectedCount}/{totalCount} components selected. {uncheckedInCycle} cycle component(s) unchecked. Click 'Retry' to continue.";
			}
			else
			{
				StatusText = $"⚠️ {selectedCount}/{totalCount} components selected. Uncheck at least one component involved in the cycle to proceed.";
			}
		}

		private void ApplySuggestion(object parameter)
		{
			if (!(parameter is SuggestionItem suggestion))
				return;

			ComponentItem componentItem = Components.FirstOrDefault(c => c.Component == suggestion.Component);
			if (componentItem != null)
			{
				componentItem.IsSelected = false;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class ComponentItem : INotifyPropertyChanged
	{
		private bool _isSelected;

		public Component Component { get; }
		public string Name => Component.Name;
		public string Author => Component.Author;
		public bool IsInCycle { get; }
		public string CycleInfo { get; }
		public IBrush CycleInfoColor { get; }
		public string StatusIcon { get; }
		public string StatusTooltip { get; }
		public IBrush BackgroundBrush { get; }
		public IBrush BorderBrush { get; }

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

		public ComponentItem(Component component, bool isInCycle)
		{
			Component = component;
			_isSelected = component.IsSelected;
			IsInCycle = isInCycle;

			if (isInCycle)
			{
				CycleInfo = "⚠️ Involved in circular dependency";
				CycleInfoColor = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Warning yellow
				StatusIcon = "⚠️";
				StatusTooltip = "This component is involved in a circular dependency. Consider unchecking it.";
				BackgroundBrush = new SolidColorBrush(Color.FromArgb(30, 255, 193, 7));
				BorderBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));
			}
			else
			{
				CycleInfo = "";
				CycleInfoColor = Brushes.Transparent;
				StatusIcon = "✓";
				StatusTooltip = "This component is not involved in any cycles.";
				BackgroundBrush = Brushes.Transparent;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public class SuggestionItem
	{
		public Component Component { get; set; }
		public string Text { get; set; }
	}
}

