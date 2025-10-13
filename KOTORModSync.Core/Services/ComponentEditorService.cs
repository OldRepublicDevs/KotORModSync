// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{

	public class ComponentEditorService
	{



		public static bool ComponentHasChanges([CanBeNull] ModComponent component, [CanBeNull] string rawText) => component != null
				&& !string.IsNullOrWhiteSpace(rawText)
				&& rawText != component.SerializeComponent();



		public static async Task<bool> SaveComponentChangesAsync([NotNull] ModComponent component, [NotNull] string rawText, [NotNull][ItemNotNull] List<ModComponent> allComponents)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( string.IsNullOrEmpty(rawText) )
				throw new ArgumentException("Raw text cannot be null or empty", nameof(rawText));
			if ( allComponents == null )
				throw new ArgumentNullException(nameof(allComponents));

			try
			{
				var newComponent = ModComponent.DeserializeTomlComponent(rawText);
				if ( newComponent is null )
				{
					await Logger.LogErrorAsync("Could not deserialize your raw config text into a ModComponent instance in memory. There may be syntax errors, check the output window for details.");
					return false;
				}

				int index = allComponents.IndexOf(component);
				if ( index == -1 )
				{
					string componentName = string.IsNullOrWhiteSpace(newComponent.Name)
						? "."
						: $" '{newComponent.Name}'.";
					string output = $"Could not find the index of component{componentName}"
						+ " Ensure you single-clicked on a component on the left before pressing save."
						+ " Please back up your work and try again.";
					await Logger.LogErrorAsync(output);
					return false;
				}

				allComponents[index] = newComponent;
				await Logger.LogAsync($"Saved '{newComponent.Name}' successfully. Refer to the output window for more information.");
				return true;
			}
			catch ( Exception ex )
			{
				string output = "An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";
				await Logger.LogExceptionAsync(ex);
				await Logger.LogErrorAsync(output + Environment.NewLine + ex.Message);
				return false;
			}
		}


		public static Instruction CreateNewInstruction([NotNull] ModComponent component, int index = 0)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			component.CreateInstruction(index);
			return component.Instructions[index];
		}



		public static void DeleteInstruction([NotNull] ModComponent component, [NotNull] Instruction instruction)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( instruction == null )
				throw new ArgumentNullException(nameof(instruction));

			int index = component.Instructions.IndexOf(instruction);
			if ( index >= 0 )
			{
				component.DeleteInstruction(index);
			}
		}



		public static void MoveInstructionUp([NotNull] ModComponent component, [NotNull] Instruction instruction)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( instruction == null )
				throw new ArgumentNullException(nameof(instruction));

			int index = component.Instructions.IndexOf(instruction);
			if ( index > 0 )
			{
				component.MoveInstructionToIndex(instruction, index - 1);
			}
		}



		public static void MoveInstructionDown([NotNull] ModComponent component, [NotNull] Instruction instruction)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( instruction == null )
				throw new ArgumentNullException(nameof(instruction));

			int index = component.Instructions.IndexOf(instruction);
			if ( index >= 0 && index < component.Instructions.Count - 1 )
			{
				component.MoveInstructionToIndex(instruction, index + 1);
			}
		}


		public static Option CreateNewOption([NotNull] ModComponent component, int index = 0)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			component.CreateOption(index);
			return component.Options[index];
		}



		public static void DeleteOption([NotNull] ModComponent component, [NotNull] Option option)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( option == null )
				throw new ArgumentNullException(nameof(option));

			int index = component.Options.IndexOf(option);
			if ( index >= 0 )
			{
				component.DeleteOption(index);
			}
		}



		public static void MoveOptionUp([NotNull] ModComponent component, [NotNull] Option option)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( option == null )
				throw new ArgumentNullException(nameof(option));

			int index = component.Options.IndexOf(option);
			if ( index > 0 )
			{
				component.MoveOptionToIndex(option, index - 1);
			}
		}



		public static void MoveOptionDown([NotNull] ModComponent component, [NotNull] Option option)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( option == null )
				throw new ArgumentNullException(nameof(option));

			int index = component.Options.IndexOf(option);
			if ( index >= 0 && index < component.Options.Count - 1 )
			{
				component.MoveOptionToIndex(option, index + 1);
			}
		}



		public void HandleComponentCheckboxChange([NotNull] ModComponent component, bool isChecked, [NotNull][ItemNotNull] List<ModComponent> allComponents, [CanBeNull] HashSet<ModComponent> visitedComponents = null)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( allComponents == null )
				throw new ArgumentNullException(nameof(allComponents));

			visitedComponents = visitedComponents ?? new HashSet<ModComponent>();

			if ( visitedComponents.Contains(component) )
			{
				Logger.LogError($"ModComponent '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
				return;
			}

			_ = visitedComponents.Add(component);

			if ( isChecked )
			{
				HandleComponentChecked(component, allComponents, visitedComponents);
			}
			else
			{
				HandleComponentUnchecked(component, allComponents, visitedComponents);
			}
		}

		private void HandleComponentChecked([NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> allComponents, [NotNull] HashSet<ModComponent> visitedComponents)
		{
			Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(
				component.Dependencies,
				component.Restrictions,
				allComponents
			);

			if ( conflicts.TryGetValue("Dependency", out List<ModComponent> dependencyConflicts) )
			{
				foreach ( ModComponent conflictComponent in dependencyConflicts )
				{
					if ( conflictComponent?.IsSelected == false )
					{
						conflictComponent.IsSelected = true;
						HandleComponentCheckboxChange(conflictComponent, true, allComponents, visitedComponents);
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
						HandleComponentCheckboxChange(conflictComponent, false, allComponents, visitedComponents);
					}
				}
			}

			foreach ( ModComponent c in allComponents )
			{
				if ( !c.IsSelected || !c.Restrictions.Contains(component.Guid) )
					continue;

				c.IsSelected = false;
				HandleComponentCheckboxChange(c, false, allComponents, visitedComponents);
			}
		}

		private void HandleComponentUnchecked([NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> allComponents, [NotNull] HashSet<ModComponent> visitedComponents)
		{

			foreach ( ModComponent c in allComponents )
			{
				if ( c.IsSelected && c.Dependencies.Contains(component.Guid) )
				{
					c.IsSelected = false;
					HandleComponentCheckboxChange(c, false, allComponents, visitedComponents);
				}
			}
		}
	}
}
