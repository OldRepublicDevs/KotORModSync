// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for component selection logic including dependency resolution
	/// </summary>
	public class ComponentSelectionService
	{
		private readonly MainConfig _mainConfig;

		public ComponentSelectionService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

		/// <summary>
		/// Handles component checkbox being checked - resolves dependencies and restrictions
		/// </summary>
		public void HandleComponentChecked(
			ModComponent component,
			HashSet<ModComponent> visitedComponents,
			bool suppressErrors = false,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));
			if ( visitedComponents is null )
				throw new ArgumentNullException(nameof(visitedComponents));

			try
			{
				// Check if the component has already been visited
				if ( visitedComponents.Contains(component) )
				{
					if ( !suppressErrors )
						Logger.LogError($"ModComponent '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
					return;
				}

				// Add the component to the visited set
				_ = visitedComponents.Add(component);

				Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(
					component.Dependencies,
					component.Restrictions,
					_mainConfig.allComponents
				);

				// Auto-select dependencies
				if ( conflicts.TryGetValue("Dependency", out List<ModComponent> dependencyConflicts) )
				{
					foreach ( ModComponent conflictComponent in dependencyConflicts )
					{
						if ( conflictComponent?.IsSelected == false )
						{
							conflictComponent.IsSelected = true;
							HandleComponentChecked(conflictComponent, visitedComponents, suppressErrors, onComponentVisualRefresh);
						}
					}
				}

				// Auto-deselect restrictions
				if ( conflicts.TryGetValue("Restriction", out List<ModComponent> restrictionConflicts) )
				{
					foreach ( ModComponent conflictComponent in restrictionConflicts )
					{
						if ( conflictComponent?.IsSelected == true )
						{
							conflictComponent.IsSelected = false;
							HandleComponentUnchecked(conflictComponent, visitedComponents, suppressErrors, onComponentVisualRefresh);
						}
					}
				}

				// Handle OTHER components' restrictions on THIS component
				foreach ( ModComponent c in _mainConfig.allComponents )
				{
					if ( !c.IsSelected || !c.Restrictions.Contains(component.Guid) )
						continue;

					c.IsSelected = false;
					HandleComponentUnchecked(c, visitedComponents, suppressErrors, onComponentVisualRefresh);
				}

				// Auto-select at least one option if the component has options and none are selected
				if ( component.Options != null && component.Options.Count > 0 )
				{
					bool hasSelectedOption = component.Options.Any(opt => opt.IsSelected);
					if ( !hasSelectedOption )
					{
						bool optionSelected = TryAutoSelectFirstOption(component);

						// If no option could be selected, uncheck the component
						// This creates a dependency: component selection depends on at least one option being selected
						if ( !optionSelected )
						{
							Logger.LogVerbose($"[ComponentSelectionService] No valid options available for '{component.Name}', unchecking component");
							component.IsSelected = false;
							// Don't call HandleComponentUnchecked here to avoid infinite loop
							// Just refresh the visual
							onComponentVisualRefresh?.Invoke(component);
							return;
						}
					}
				}

				// Trigger visual refresh
				onComponentVisualRefresh?.Invoke(component);
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
			}
		}

		/// <summary>
		/// Handles component checkbox being unchecked - resolves dependencies
		/// </summary>
		public void HandleComponentUnchecked(
			ModComponent component,
			HashSet<ModComponent> visitedComponents,
			bool suppressErrors = false,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));

			visitedComponents = visitedComponents ?? new HashSet<ModComponent>();

			try
			{
				// Check if already visited
				if ( visitedComponents.Contains(component) )
				{
					if ( !suppressErrors )
						Logger.LogError($"ModComponent '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
					return;
				}

				// Add to visited set
				_ = visitedComponents.Add(component);

				// Uncheck components that depend on THIS component
				foreach ( ModComponent c in _mainConfig.allComponents.Where(c => c.IsSelected && c.Dependencies.Contains(component.Guid)) )
				{
					c.IsSelected = false;
					HandleComponentUnchecked(c, visitedComponents, suppressErrors, onComponentVisualRefresh);
				}

				// Trigger visual refresh
				onComponentVisualRefresh?.Invoke(component);
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
			}
		}

		/// <summary>
		/// Handles SelectAll checkbox state changes
		/// </summary>
		public void HandleSelectAllCheckbox(
			bool? isChecked,
			Action<ModComponent, HashSet<ModComponent>, bool> onComponentChecked,
			Action<ModComponent, HashSet<ModComponent>, bool> onComponentUnchecked)
		{
			try
			{
				var finishedComponents = new HashSet<ModComponent>();

				switch ( isChecked )
				{
					case true:
						// Select all
						foreach ( ModComponent component in _mainConfig.allComponents )
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, finishedComponents, true);
						}
						break;
					case false:
						// Deselect all
						foreach ( ModComponent component in _mainConfig.allComponents )
						{
							component.IsSelected = false;
							onComponentUnchecked?.Invoke(component, finishedComponents, true);
						}
						break;
					case null:
						// Indeterminate state - select all (common UI pattern)
						foreach ( ModComponent component in _mainConfig.allComponents )
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, finishedComponents, true);
						}
						break;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error handling select all checkbox");
			}
		}

		/// <summary>
		/// Handles option checkbox being unchecked - enforces that at least one option must be selected
		/// </summary>
		public void HandleOptionUnchecked(
			Option option,
			ModComponent parentComponent,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if ( option is null )
				throw new ArgumentNullException(nameof(option));
			if ( parentComponent is null )
				throw new ArgumentNullException(nameof(parentComponent));

			try
			{
				// Check if all options are now unchecked
				bool allOptionsUnchecked = parentComponent.Options.All(opt => !opt.IsSelected);

				if ( allOptionsUnchecked && parentComponent.IsSelected )
				{
					// If all options are unchecked, the component must also be unchecked
					// This enforces the dependency: component selection depends on at least one option being selected
					Logger.LogVerbose($"[ComponentSelectionService] All options unchecked for '{parentComponent.Name}', unchecking component");
					parentComponent.IsSelected = false;

					// Handle cascading unchecks for components that depend on this one
					var visitedComponents = new HashSet<ModComponent>();
					foreach ( ModComponent c in _mainConfig.allComponents.Where(c => c.IsSelected && c.Dependencies.Contains(parentComponent.Guid)) )
					{
						c.IsSelected = false;
						HandleComponentUnchecked(c, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
					}

					// Trigger visual refresh for the parent component
					onComponentVisualRefresh?.Invoke(parentComponent);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e, "Error handling option unchecked");
			}
		}

		/// <summary>
		/// Handles option checkbox being checked - ensures parent component is also checked
		/// </summary>
		public void HandleOptionChecked(
			Option option,
			ModComponent parentComponent,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if ( option is null )
				throw new ArgumentNullException(nameof(option));
			if ( parentComponent is null )
				throw new ArgumentNullException(nameof(parentComponent));

			try
			{
				// If an option is checked, ensure the parent component is also checked
				if ( !parentComponent.IsSelected )
				{
					Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' checked, auto-checking parent component '{parentComponent.Name}'");
					parentComponent.IsSelected = true;

					// Handle dependencies/restrictions for the parent component
					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentChecked(parentComponent, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e, "Error handling option checked");
			}
		}

		/// <summary>
		/// Attempts to auto-select the first available option for a component.
		/// This ensures that when a component with options is selected, at least one option is also selected.
		/// </summary>
		private bool TryAutoSelectFirstOption(ModComponent component)
		{
			if ( component?.Options == null || component.Options.Count == 0 )
				return false;

			try
			{
				// Simply select the first option - options themselves don't have dependencies/restrictions
				// The component's dependencies/restrictions have already been resolved at this point
				Option firstOption = component.Options[0];
				firstOption.IsSelected = true;
				Logger.LogVerbose($"[ComponentSelectionService] Auto-selected option '{firstOption.Name}' for component '{component.Name}'");
				return true;
			}
			catch ( Exception e )
			{
				Logger.LogException(e, $"Error auto-selecting first option for component '{component.Name}'");
				return false;
			}
		}
	}
}

