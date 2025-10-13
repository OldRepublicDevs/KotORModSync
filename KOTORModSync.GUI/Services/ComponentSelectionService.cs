



using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	
	
	
	public class ComponentSelectionService
	{
		private readonly MainConfig _mainConfig;

		public ComponentSelectionService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}

		
		
		
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
				
				if ( visitedComponents.Contains(component) )
				{
					if ( !suppressErrors )
						Logger.LogError($"ModComponent '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
					return;
				}

				
				_ = visitedComponents.Add(component);

				Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(
					component.Dependencies,
					component.Restrictions,
					_mainConfig.allComponents
				);

				
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

				
				foreach ( ModComponent c in _mainConfig.allComponents )
				{
					if ( !c.IsSelected || !c.Restrictions.Contains(component.Guid) )
						continue;

					c.IsSelected = false;
					HandleComponentUnchecked(c, visitedComponents, suppressErrors, onComponentVisualRefresh);
				}

				
				if ( component.Options != null && component.Options.Count > 0 )
				{
					bool hasSelectedOption = component.Options.Any(opt => opt.IsSelected);
					if ( !hasSelectedOption )
					{
						bool optionSelected = TryAutoSelectFirstOption(component);

						
						
						if ( !optionSelected )
						{
							Logger.LogVerbose($"[ComponentSelectionService] No valid options available for '{component.Name}', unchecking component");
							component.IsSelected = false;
							
							
							onComponentVisualRefresh?.Invoke(component);
							return;
						}
					}
				}

				
				onComponentVisualRefresh?.Invoke(component);
			}
			catch ( Exception e )
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
			if ( component is null )
				throw new ArgumentNullException(nameof(component));

			visitedComponents = visitedComponents ?? new HashSet<ModComponent>();

			try
			{
				
				if ( visitedComponents.Contains(component) )
				{
					if ( !suppressErrors )
						Logger.LogError($"ModComponent '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
					return;
				}

				
				_ = visitedComponents.Add(component);

				
				foreach ( ModComponent c in _mainConfig.allComponents.Where(c => c.IsSelected && c.Dependencies.Contains(component.Guid)) )
				{
					c.IsSelected = false;
					HandleComponentUnchecked(c, visitedComponents, suppressErrors, onComponentVisualRefresh);
				}

				
				onComponentVisualRefresh?.Invoke(component);
			}
			catch ( Exception e )
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

				switch ( isChecked )
				{
					case true:
						
						foreach ( ModComponent component in _mainConfig.allComponents )
						{
							component.IsSelected = true;
							onComponentChecked?.Invoke(component, finishedComponents, true);
						}
						break;
					case false:
						
						foreach ( ModComponent component in _mainConfig.allComponents )
						{
							component.IsSelected = false;
							onComponentUnchecked?.Invoke(component, finishedComponents, true);
						}
						break;
					case null:
						
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
				
				bool allOptionsUnchecked = parentComponent.Options.All(opt => !opt.IsSelected);

				if ( allOptionsUnchecked && parentComponent.IsSelected )
				{
					
					
					Logger.LogVerbose($"[ComponentSelectionService] All options unchecked for '{parentComponent.Name}', unchecking component");
					parentComponent.IsSelected = false;

					
					var visitedComponents = new HashSet<ModComponent>();
					foreach ( ModComponent c in _mainConfig.allComponents.Where(c => c.IsSelected && c.Dependencies.Contains(parentComponent.Guid)) )
					{
						c.IsSelected = false;
						HandleComponentUnchecked(c, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
					}

					
					onComponentVisualRefresh?.Invoke(parentComponent);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e, "Error handling option unchecked");
			}
		}

		
		
		
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
				
				if ( !parentComponent.IsSelected )
				{
					Logger.LogVerbose($"[ComponentSelectionService] Option '{option.Name}' checked, auto-checking parent component '{parentComponent.Name}'");
					parentComponent.IsSelected = true;

					
					var visitedComponents = new HashSet<ModComponent>();
					HandleComponentChecked(parentComponent, visitedComponents, suppressErrors: true, onComponentVisualRefresh);
				}
			}
			catch ( Exception e )
			{
				Logger.LogException(e, "Error handling option checked");
			}
		}

		
		
		
		
		private bool TryAutoSelectFirstOption(ModComponent component)
		{
			if ( component?.Options == null || component.Options.Count == 0 )
				return false;

			try
			{
				
				
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

