// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Services
{

	public class ComponentSelectionService
	{
		private readonly MainConfig _mainConfig;
		private bool? _isAspyrVersion;

		public ComponentSelectionService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

		/// <summary>
		/// Detects if the game installation is the Aspyr version and caches the result
		/// </summary>
		public void DetectGameVersion()
		{
			try
			{
				if (_mainConfig.destinationPath?.Exists == true)
				{
					PathUtilities.DetectedGame gameType = PathUtilities.DetectGame(_mainConfig.destinationPath.FullName);
					_isAspyrVersion = gameType == PathUtilities.DetectedGame.Kotor2Aspyr;
					Logger.Log($"Detected game version: {gameType}, IsAspyr: {_isAspyrVersion}");
				}
				else
				{
					_isAspyrVersion = null;
					Logger.LogVerbose("No destination path set, cannot detect game version");
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error detecting game version");
				_isAspyrVersion = null;
			}
		}

		/// <summary>
		/// Checks if a component should be selectable based on Aspyr exclusivity
		/// </summary>
		public bool IsComponentSelectable(ModComponent component)
		{
			// If component is Aspyr-exclusive, only allow if we detected Aspyr version
			if (component.AspyrExclusive == true)
			{
				return _isAspyrVersion == true;
			}

			// All other components are selectable
			return true;
		}

		public void HandleComponentChecked(
			ModComponent component,
			HashSet<ModComponent> visitedComponents,
			bool suppressErrors = false,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if (component is null)
				throw new ArgumentNullException(nameof(component));
			if (visitedComponents is null)
				throw new ArgumentNullException(nameof(visitedComponents));

			try
			{

				if (visitedComponents.Contains(component))
				{
					if (!suppressErrors)
						Logger.LogError($"ModComponent '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
					return;
				}

				_ = visitedComponents.Add(component);

				Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(
					component.Dependencies,
					component.Restrictions,
					_mainConfig.allComponents
				);

				if (conflicts.TryGetValue("Dependency", out List<ModComponent> dependencyConflicts))
				{
					foreach (ModComponent conflictComponent in dependencyConflicts)
					{
						if (conflictComponent?.IsSelected == false)
						{
							conflictComponent.IsSelected = true;
							HandleComponentChecked(conflictComponent, visitedComponents, suppressErrors, onComponentVisualRefresh);
						}
					}
				}

				if (conflicts.TryGetValue("Restriction", out List<ModComponent> restrictionConflicts))
				{
					foreach (ModComponent conflictComponent in restrictionConflicts)
					{
						if (conflictComponent?.IsSelected == true)
						{
							conflictComponent.IsSelected = false;
							HandleComponentUnchecked(conflictComponent, visitedComponents, suppressErrors, onComponentVisualRefresh);
						}
					}
				}

				foreach (ModComponent c in _mainConfig.allComponents)
				{
					if (!c.IsSelected || !c.Restrictions.Contains(component.Guid))
						continue;

					c.IsSelected = false;
					HandleComponentUnchecked(c, visitedComponents, suppressErrors, onComponentVisualRefresh);
				}

				// Handle component-to-option dependencies and restrictions
				HandleComponentToOptionDependencies(component, visitedComponents, suppressErrors, onComponentVisualRefresh);

				// Note: Removed automatic first option selection
				// Options should only be selected based on their own dependencies/restrictions
				// and user interaction, not automatically when parent component is selected

				onComponentVisualRefresh?.Invoke(component);
			}
			catch (Exception e)
			{
				Logger.LogException(e);
			}
		}

		public void HandleComponentUnchecked(
			ModComponent component,
			HashSet<ModComponent> visitedComponents,
			bool suppressErrors = false,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if (component is null)
				throw new ArgumentNullException(nameof(component));

			visitedComponents = visitedComponents ?? new HashSet<ModComponent>();

			try
			{

				if (visitedComponents.Contains(component))
				{
					if (!suppressErrors)
						Logger.LogError($"ModComponent '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
					return;
				}

				_ = visitedComponents.Add(component);

				foreach (ModComponent c in _mainConfig.allComponents.Where(c => c.IsSelected && c.Dependencies.Contains(component.Guid)))
				{
					c.IsSelected = false;
					HandleComponentUnchecked(c, visitedComponents, suppressErrors, onComponentVisualRefresh);
				}

				onComponentVisualRefresh?.Invoke(component);
			}
			catch (Exception e)
			{
				Logger.LogException(e);
			}
		}

		public void HandleSelectAllCheckbox(
			bool? isChecked,
			Action<ModComponent, HashSet<ModComponent>, bool> onComponentChecked,
			Action<ModComponent, HashSet<ModComponent>, bool> onComponentUnchecked)
		{
			try
			{
				var finishedComponents = new HashSet<ModComponent>();

				switch (isChecked)
				{
					case true:

						foreach (ModComponent component in _mainConfig.allComponents)
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, finishedComponents, true);
						}
						break;
					case false:

						foreach (ModComponent component in _mainConfig.allComponents)
						{
							component.IsSelected = false;
							onComponentUnchecked?.Invoke(component, finishedComponents, true);
						}
						break;
					case null:

						foreach (ModComponent component in _mainConfig.allComponents)
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, finishedComponents, true);
						}
						break;
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error handling select all checkbox");
			}
		}

		public void HandleOptionUnchecked(
			Option option,
			ModComponent parentComponent,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if (option is null)
				throw new ArgumentNullException(nameof(option));
			if (parentComponent is null)
				throw new ArgumentNullException(nameof(parentComponent));

			try
			{
				Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' unchecked, handling cascading effects");

				// Handle cascading effects for option dependencies
				var visitedOptions = new HashSet<Option>();
				HandleOptionDependencyCascading(option, parentComponent, visitedOptions, onComponentVisualRefresh);

				// Check if all options are unchecked and handle parent component accordingly
				bool allOptionsUnchecked = parentComponent.Options.All(opt => !opt.IsSelected);

				if (allOptionsUnchecked && parentComponent.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] All options unchecked for '{parentComponent.Name}', unchecking component");
					parentComponent.IsSelected = false;

					var visitedComponents = new HashSet<ModComponent>();
					foreach (ModComponent c in _mainConfig.allComponents.Where(c => c.IsSelected && c.Dependencies.Contains(parentComponent.Guid)))
					{
						c.IsSelected = false;
						HandleComponentUnchecked(c, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
					}

					onComponentVisualRefresh?.Invoke(parentComponent);
				}
			}
			catch (Exception e)
			{
				Logger.LogException(e, "Error handling option unchecked");
			}
		}

		public void HandleOptionChecked(
			Option option,
			ModComponent parentComponent,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if (option is null)
				throw new ArgumentNullException(nameof(option));
			if (parentComponent is null)
				throw new ArgumentNullException(nameof(parentComponent));

			try
			{
				Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' checked, validating dependencies and restrictions");

				// First, ensure parent component is selected (options require their parent to be selected)
				if (!parentComponent.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' checked, auto-checking parent component '{parentComponent.Name}'");
					parentComponent.IsSelected = true;

					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentChecked(parentComponent, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}

				// Now validate option's own dependencies and restrictions
				var visitedOptions = new HashSet<Option>();
				ValidateAndResolveOptionDependencies(option, visitedOptions, onComponentVisualRefresh);
			}
			catch (Exception e)
			{
				Logger.LogException(e, "Error handling option checked");
			}
		}

		/// <summary>
		/// Validates and resolves dependencies for an option when it's checked.
		/// Handles option-to-component, option-to-option, and component-to-option dependencies.
		/// </summary>
		private void ValidateAndResolveOptionDependencies(
			Option option,
			HashSet<Option> visitedOptions,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if (visitedOptions.Contains(option))
				return;

			visitedOptions.Add(option);

			// Handle option-to-component dependencies
			foreach (Guid dependencyGuid in option.Dependencies)
			{
				ModComponent dependencyComponent = ModComponent.FindComponentFromGuid(dependencyGuid, _mainConfig.allComponents);
				if (dependencyComponent != null && !dependencyComponent.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' requires component '{dependencyComponent.Name}', selecting it");
					dependencyComponent.IsSelected = true;
					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentChecked(dependencyComponent, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}
			}

			// Handle option-to-option dependencies
			foreach (Guid dependencyGuid in option.Dependencies)
			{
				Option dependencyOption = FindOptionByGuid(dependencyGuid);
				if (dependencyOption != null && !dependencyOption.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' requires option '{dependencyOption.Name}', selecting it");
					dependencyOption.IsSelected = true;
					ValidateAndResolveOptionDependencies(dependencyOption, visitedOptions, onComponentVisualRefresh);
				}
			}

			// Handle option restrictions (conflicts)
			foreach (Guid restrictionGuid in option.Restrictions)
			{
				ModComponent restrictionComponent = ModComponent.FindComponentFromGuid(restrictionGuid, _mainConfig.allComponents);
				if (restrictionComponent != null && restrictionComponent.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' conflicts with component '{restrictionComponent.Name}', deselecting it");
					restrictionComponent.IsSelected = false;
					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentUnchecked(restrictionComponent, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}

				Option restrictionOption = FindOptionByGuid(restrictionGuid);
				if (restrictionOption != null && restrictionOption.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' conflicts with option '{restrictionOption.Name}', deselecting it");
					restrictionOption.IsSelected = false;
					ModComponent restParentComponent = FindParentComponent(restrictionOption);
					if (restParentComponent != null)
					{
						HandleOptionDependencyCascading(restrictionOption, restParentComponent, visitedOptions, onComponentVisualRefresh);
					}
				}
			}

			// Handle component-to-option dependencies (components that depend on this option)
			foreach (ModComponent component in _mainConfig.allComponents)
			{
				if (component.Dependencies.Contains(option.Guid) && !component.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Component '{component.Name}' requires option '{option.Name}', selecting component");
					component.IsSelected = true;
					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentChecked(component, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}
			}

			// Handle component restrictions (components that conflict with this option)
			foreach (ModComponent component in _mainConfig.allComponents)
			{
				if (component.Restrictions.Contains(option.Guid) && component.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Component '{component.Name}' conflicts with option '{option.Name}', deselecting component");
					component.IsSelected = false;
					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentUnchecked(component, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}
			}
		}

		/// <summary>
		/// Handles cascading effects when an option is unchecked.
		/// Deselects dependent options and components.
		/// </summary>
		private void HandleOptionDependencyCascading(
			Option option,
			ModComponent restParentComponent,
			HashSet<Option> visitedOptions,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			if (visitedOptions.Contains(option))
				return;

			visitedOptions.Add(option);

			// Find and deselect options that depend on this option
			foreach (ModComponent component in _mainConfig.allComponents)
			{
				foreach (Option componentOption in component.Options)
				{
					if (componentOption.Dependencies.Contains(option.Guid) && componentOption.IsSelected)
					{
						Logger.LogVerbose($"[ComponentSelectionService] Option '{componentOption.Name}' depends on '{option.Name}', deselecting it");
						componentOption.IsSelected = false;
						HandleOptionDependencyCascading(componentOption, component, visitedOptions, onComponentVisualRefresh);
					}
				}
			}

			// Find and deselect components that depend on this option
			foreach (ModComponent component in _mainConfig.allComponents)
			{
				if (component.Dependencies.Contains(option.Guid) && component.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Component '{component.Name}' depends on option '{option.Name}', deselecting it");
					component.IsSelected = false;
					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentUnchecked(component, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}
			}

			// Check if all options are unchecked in the parent component
			if (restParentComponent != null)
			{
				bool allOptionsUnchecked = restParentComponent.Options.All(opt => !opt.IsSelected);
				if (allOptionsUnchecked && restParentComponent.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] All options unchecked for '{restParentComponent.Name}', unchecking component");
					restParentComponent.IsSelected = false;

					var visitedComponents = new HashSet<ModComponent>();
					foreach (ModComponent c in _mainConfig.allComponents.Where(c => c.IsSelected && c.Dependencies.Contains(restParentComponent.Guid)))
					{
						c.IsSelected = false;
						HandleComponentUnchecked(c, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
					}

					onComponentVisualRefresh?.Invoke(restParentComponent);
				}
			}
		}

		/// <summary>
		/// Finds an option by its GUID across all components.
		/// </summary>
		private Option FindOptionByGuid(Guid optionGuid)
		{
			foreach (ModComponent component in _mainConfig.allComponents)
			{
				foreach (Option option in component.Options)
				{
					if (option.Guid == optionGuid)
						return option;
				}
			}
			return null;
		}

		/// <summary>
		/// Finds the parent component of an option.
		/// </summary>
		private ModComponent FindParentComponent(Option option)
		{
			foreach (ModComponent component in _mainConfig.allComponents)
			{
				if (component.Options.Contains(option))
					return component;
			}
			return null;
		}

		/// <summary>
		/// Handles component-to-option dependencies and restrictions.
		/// When a component is selected, it may require or conflict with specific options.
		/// </summary>
		private void HandleComponentToOptionDependencies(
			ModComponent component,
			HashSet<ModComponent> visitedComponents,
			bool suppressErrors = false,
			Action<ModComponent> onComponentVisualRefresh = null)
		{
			// Handle component dependencies on options
			foreach (Guid dependencyGuid in component.Dependencies)
			{
				Option dependencyOption = FindOptionByGuid(dependencyGuid);
				if (dependencyOption != null && !dependencyOption.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Component '{component.Name}' requires option '{dependencyOption.Name}', selecting it");
					dependencyOption.IsSelected = true;
					var visitedOptions = new HashSet<Option>();
					ValidateAndResolveOptionDependencies(dependencyOption, visitedOptions, onComponentVisualRefresh);
				}
			}

			// Handle component restrictions on options
			foreach (Guid restrictionGuid in component.Restrictions)
			{
				Option restrictionOption = FindOptionByGuid(restrictionGuid);
				if (restrictionOption != null && restrictionOption.IsSelected)
				{
					Logger.LogVerbose($"[ComponentSelectionService] Component '{component.Name}' conflicts with option '{restrictionOption.Name}', deselecting it");
					restrictionOption.IsSelected = false;
					ModComponent restParentComponent = FindParentComponent(restrictionOption);
					if (restParentComponent != null)
					{
						var visitedOptions = new HashSet<Option>();
						HandleOptionDependencyCascading(restrictionOption, restParentComponent, visitedOptions, onComponentVisualRefresh);
					}
				}
			}
		}

	}
}