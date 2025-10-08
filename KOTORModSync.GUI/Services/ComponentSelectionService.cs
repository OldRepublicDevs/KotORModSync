// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
	}
}

