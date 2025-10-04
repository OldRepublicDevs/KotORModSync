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

namespace KOTORModSync
{
	public class DependencyUnlinkViewModel : INotifyPropertyChanged
	{
		private string _statusText;

		public ObservableCollection<DependentComponentItem> DependentComponents { get; }
		public ObservableCollection<QuickActionItem> QuickActions { get; }
		public Component ComponentToDelete { get; }
		public string SummaryText { get; }
		public string DetailedDependencyInfo { get; }
		public ICommand ApplyQuickActionCommand { get; }

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

		public DependencyUnlinkViewModel(Component componentToDelete, List<Component> dependentComponents)
		{
			ComponentToDelete = componentToDelete;
			DependentComponents = new ObservableCollection<DependentComponentItem>();
			QuickActions = new ObservableCollection<QuickActionItem>();
			ApplyQuickActionCommand = new RelayCommand(ApplyQuickAction);

			// Build summary
			int dependentCount = dependentComponents.Count;
			SummaryText = $"Cannot delete '{componentToDelete.Name}' because {dependentCount} component{(dependentCount > 1 ? "s" : "")} depend on it. " +
			              "You must first unlink these dependencies by unchecking the dependent components below.";

			// Build detailed dependency info
			DetailedDependencyInfo = BuildDetailedDependencyInfo(componentToDelete, dependentComponents);

			// Build dependent component items
			foreach (Component component in dependentComponents)
			{
				var item = new DependentComponentItem(component, componentToDelete);
				item.PropertyChanged += OnComponentSelectionChanged;
				DependentComponents.Add(item);
			}

			// Build quick actions
			BuildQuickActions(dependentComponents);

			UpdateStatus();
		}

		private string BuildDetailedDependencyInfo(Component componentToDelete, List<Component> dependentComponents)
		{
			var info = new List<string>
			{
				$"Component to delete: {componentToDelete.Name} (GUID: {componentToDelete.Guid})", "",
				"Dependent components:"
			};

			foreach (Component dependent in dependentComponents)
			{
				var dependencyTypes = new List<string>();

				// Check different types of dependencies
				if (dependent.Dependencies.Contains(componentToDelete.Guid))
					dependencyTypes.Add("Dependency");
				if (dependent.Restrictions.Contains(componentToDelete.Guid))
					dependencyTypes.Add("Restriction");
				if (dependent.InstallBefore.Contains(componentToDelete.Guid))
					dependencyTypes.Add("InstallBefore");
				if (dependent.InstallAfter.Contains(componentToDelete.Guid))
					dependencyTypes.Add("InstallAfter");

				info.Add($"  • {dependent.Name} (GUID: {dependent.Guid})");
				info.Add($"    Dependency types: {string.Join(", ", dependencyTypes)}");
			}

			return string.Join(Environment.NewLine, info);
		}

		private void BuildQuickActions(List<Component> dependentComponents)
		{
			// Add "Uncheck All" action
			QuickActions.Add(new QuickActionItem
			{
				ActionType = QuickActionType.UncheckAll,
				Text = "❌ Uncheck All Dependencies"
			});

			// Add "Uncheck Selected Only" action
			QuickActions.Add(new QuickActionItem
			{
				ActionType = QuickActionType.UncheckSelectedOnly,
				Text = "☑️ Uncheck Only Selected Dependencies"
			});

			// Add individual component actions
			foreach (Component component in dependentComponents.Take(5)) // Limit to first 5 for UI space
			{
				QuickActions.Add(new QuickActionItem
				{
					ActionType = QuickActionType.UncheckSpecific,
					Component = component,
					Text = $"❌ Uncheck: {component.Name}"
				});
			}
		}

		private void OnComponentSelectionChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(DependentComponentItem.IsSelected))
				UpdateStatus();
		}

		private void UpdateStatus()
		{
			int selectedCount = DependentComponents.Count(c => c.IsSelected);
			int totalCount = DependentComponents.Count;
			int uncheckedCount = totalCount - selectedCount;

			StatusText = uncheckedCount > 0
			             ? $"✅ {uncheckedCount}/{totalCount} dependencies will be unlinked. Click 'Unlink & Delete' to proceed."
						 : $"⚠️ {totalCount}/{totalCount} dependencies still linked. Uncheck at least one dependent component to proceed.";
		}

		private void ApplyQuickAction(object parameter)
		{
			if (!(parameter is QuickActionItem action))
				return;

			switch (action.ActionType)
			{
				case QuickActionType.UncheckAll:
					foreach (DependentComponentItem item in DependentComponents)
					    item.IsSelected = false;
					break;

				case QuickActionType.UncheckSelectedOnly:
					// Uncheck only components that are currently selected for installation
					foreach (DependentComponentItem item in DependentComponents.Where(c => c.Component.IsSelected))
					{
						item.IsSelected = false;
					}
					break;

				case QuickActionType.UncheckSpecific:
					if (action.Component != null)
					{
						DependentComponentItem componentItem = DependentComponents.FirstOrDefault(c => c.Component == action.Component);
						if (componentItem != null)
						{
							componentItem.IsSelected = false;
						}
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public class DependentComponentItem : INotifyPropertyChanged
	{
		private bool _isSelected;

		public Component Component { get; }
		public string Name => Component.Name;
		public string Author => Component.Author;
		public string DependencyInfo { get; }
		public IBrush DependencyInfoColor { get; }
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

		public DependentComponentItem(Component component, Component componentToDelete)
		{
			Component = component;
			_isSelected = true; // Start with all dependencies selected (will be unlinked)

			// Build dependency info
			var dependencyTypes = new List<string>();
			if (component.Dependencies.Contains(componentToDelete.Guid))
				dependencyTypes.Add("Dependency");
			if (component.Restrictions.Contains(componentToDelete.Guid))
				dependencyTypes.Add("Restriction");
			if (component.InstallBefore.Contains(componentToDelete.Guid))
				dependencyTypes.Add("InstallBefore");
			if (component.InstallAfter.Contains(componentToDelete.Guid))
				dependencyTypes.Add("InstallAfter");

			DependencyInfo = $"🔗 Depends on: {string.Join(", ", dependencyTypes)}";
			DependencyInfoColor = ThemeResourceHelper.DependencyWarningForeground;
			StatusIcon = "🔗";
			StatusTooltip = "This component depends on the component you want to delete. Uncheck to unlink the dependency.";
			BackgroundBrush = ThemeResourceHelper.DependencyWarningBackground;
			BorderBrush = ThemeResourceHelper.DependencyWarningBorder;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public class QuickActionItem
	{
		public QuickActionType ActionType { get; set; }
		public Component Component { get; set; }
		public string Text { get; set; }
	}

	public enum QuickActionType
	{
		UncheckAll,
		UncheckSelectedOnly,
		UncheckSpecific
	}
}
