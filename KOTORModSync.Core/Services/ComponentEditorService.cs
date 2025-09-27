// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service for handling component editing operations
	/// </summary>
	public class ComponentEditorService
	{
		/// <summary>
		/// Checks if a component has changes compared to its serialized form
		/// </summary>
		/// <param name="component">Component to check</param>
		/// <param name="rawText">Raw text to compare against</param>
		/// <returns>True if component has changes</returns>
		public static bool ComponentHasChanges([CanBeNull] Component component, [CanBeNull] string rawText) => component != null
				&& !string.IsNullOrWhiteSpace(rawText)
				&& rawText != component.SerializeComponent();

		/// <summary>
		/// Saves changes to a component from raw text
		/// </summary>
		/// <param name="component">Component to update</param>
		/// <param name="rawText">Raw text containing component data</param>
		/// <param name="allComponents">All components list for updating</param>
		/// <returns>True if save was successful</returns>
		public async Task<bool> SaveComponentChangesAsync([NotNull] Component component, [NotNull] string rawText, [NotNull][ItemNotNull] List<Component> allComponents)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (string.IsNullOrEmpty(rawText))
				throw new ArgumentException("Raw text cannot be null or empty", nameof(rawText));
			if (allComponents == null)
				throw new ArgumentNullException(nameof(allComponents));

			try
			{
				var newComponent = Component.DeserializeTomlComponent(rawText);
				if (newComponent is null)
				{
					await Logger.LogErrorAsync("Could not deserialize your raw config text into a Component instance in memory. There may be syntax errors, check the output window for details.");
					return false;
				}

				// Find the corresponding component in the collection
				int index = allComponents.IndexOf(component);
				if (index == -1)
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

				// Update the properties of the component
				allComponents[index] = newComponent;
				await Logger.LogAsync($"Saved '{newComponent.Name}' successfully. Refer to the output window for more information.");
				return true;
			}
			catch (Exception ex)
			{
				string output = "An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";
				await Logger.LogExceptionAsync(ex);
				await Logger.LogErrorAsync(output + Environment.NewLine + ex.Message);
				return false;
			}
		}

		/// <summary>
		/// Creates a new instruction for a component
		/// </summary>
		/// <param name="component">Component to add instruction to</param>
		/// <param name="index">Index where to insert the instruction</param>
		/// <returns>Created instruction</returns>
		public Instruction CreateNewInstruction([NotNull] Component component, int index = 0)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));

			component.CreateInstruction(index);
			return component.Instructions[index];
		}

		/// <summary>
		/// Deletes an instruction from a component
		/// </summary>
		/// <param name="component">Component to remove instruction from</param>
		/// <param name="instruction">Instruction to remove</param>
		public void DeleteInstruction([NotNull] Component component, [NotNull] Instruction instruction)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (instruction == null)
				throw new ArgumentNullException(nameof(instruction));

			int index = component.Instructions.IndexOf(instruction);
			if (index >= 0)
			{
				component.DeleteInstruction(index);
			}
		}

		/// <summary>
		/// Moves an instruction up in the list
		/// </summary>
		/// <param name="component">Component containing the instruction</param>
		/// <param name="instruction">Instruction to move</param>
		public void MoveInstructionUp([NotNull] Component component, [NotNull] Instruction instruction)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (instruction == null)
				throw new ArgumentNullException(nameof(instruction));

			int index = component.Instructions.IndexOf(instruction);
			if (index > 0)
			{
				component.MoveInstructionToIndex(instruction, index - 1);
			}
		}

		/// <summary>
		/// Moves an instruction down in the list
		/// </summary>
		/// <param name="component">Component containing the instruction</param>
		/// <param name="instruction">Instruction to move</param>
		public void MoveInstructionDown([NotNull] Component component, [NotNull] Instruction instruction)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (instruction == null)
				throw new ArgumentNullException(nameof(instruction));

			int index = component.Instructions.IndexOf(instruction);
			if (index >= 0 && index < component.Instructions.Count - 1)
			{
				component.MoveInstructionToIndex(instruction, index + 1);
			}
		}

		/// <summary>
		/// Creates a new option for a component
		/// </summary>
		/// <param name="component">Component to add option to</param>
		/// <param name="index">Index where to insert the option</param>
		/// <returns>Created option</returns>
		public Option CreateNewOption([NotNull] Component component, int index = 0)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));

			component.CreateOption(index);
			return component.Options[index];
		}

		/// <summary>
		/// Deletes an option from a component
		/// </summary>
		/// <param name="component">Component to remove option from</param>
		/// <param name="option">Option to remove</param>
		public void DeleteOption([NotNull] Component component, [NotNull] Option option)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (option == null)
				throw new ArgumentNullException(nameof(option));

			int index = component.Options.IndexOf(option);
			if (index >= 0)
			{
				component.DeleteOption(index);
			}
		}

		/// <summary>
		/// Moves an option up in the list
		/// </summary>
		/// <param name="component">Component containing the option</param>
		/// <param name="option">Option to move</param>
		public void MoveOptionUp([NotNull] Component component, [NotNull] Option option)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (option == null)
				throw new ArgumentNullException(nameof(option));

			int index = component.Options.IndexOf(option);
			if (index > 0)
			{
				component.MoveOptionToIndex(option, index - 1);
			}
		}

		/// <summary>
		/// Moves an option down in the list
		/// </summary>
		/// <param name="component">Component containing the option</param>
		/// <param name="option">Option to move</param>
		public void MoveOptionDown([NotNull] Component component, [NotNull] Option option)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (option == null)
				throw new ArgumentNullException(nameof(option));

			int index = component.Options.IndexOf(option);
			if (index >= 0 && index < component.Options.Count - 1)
			{
				component.MoveOptionToIndex(option, index + 1);
			}
		}

		/// <summary>
		/// Handles component checkbox state changes with dependency resolution
		/// </summary>
		/// <param name="component">Component that was checked/unchecked</param>
		/// <param name="isChecked">New checked state</param>
		/// <param name="allComponents">All components for dependency resolution</param>
		/// <param name="visitedComponents">Set of already processed components to prevent circular dependencies</param>
		public void HandleComponentCheckboxChange([NotNull] Component component, bool isChecked, [NotNull][ItemNotNull] List<Component> allComponents, [CanBeNull] HashSet<Component> visitedComponents = null)
		{
			if (component == null)
				throw new ArgumentNullException(nameof(component));
			if (allComponents == null)
				throw new ArgumentNullException(nameof(allComponents));

			visitedComponents = visitedComponents ?? new HashSet<Component>();

			// Check if the component has already been visited
			if (visitedComponents.Contains(component))
			{
				Logger.LogError($"Component '{component.Name}' has dependencies/restrictions that cannot be resolved automatically!");
				return;
			}

			// Add the component to the visited set
			visitedComponents.Add(component);

			if (isChecked)
			{
				HandleComponentChecked(component, allComponents, visitedComponents);
			}
			else
			{
				HandleComponentUnchecked(component, allComponents, visitedComponents);
			}
		}

		private void HandleComponentChecked([NotNull] Component component, [NotNull][ItemNotNull] List<Component> allComponents, [NotNull] HashSet<Component> visitedComponents)
		{
			Dictionary<string, List<Component>> conflicts = Component.GetConflictingComponents(
				component.Dependencies,
				component.Restrictions,
				allComponents
			);

			// Handling conflicts based on what's defined for THIS component
			if (conflicts.TryGetValue("Dependency", out List<Component> dependencyConflicts))
			{
				foreach (Component conflictComponent in dependencyConflicts)
				{
					if (conflictComponent?.IsSelected == false)
					{
						conflictComponent.IsSelected = true;
						HandleComponentCheckboxChange(conflictComponent, true, allComponents, visitedComponents);
					}
				}
			}

			if (conflicts.TryGetValue("Restriction", out List<Component> restrictionConflicts))
			{
				foreach (Component conflictComponent in restrictionConflicts)
				{
					if (conflictComponent?.IsSelected == true)
					{
						conflictComponent.IsSelected = false;
						HandleComponentCheckboxChange(conflictComponent, false, allComponents, visitedComponents);
					}
				}
			}

			// Handling OTHER component's defined restrictions based on the change to THIS component.
			foreach (Component c in allComponents)
			{
				if (!c.IsSelected || !c.Restrictions.Contains(component.Guid))
					continue;

				c.IsSelected = false;
				HandleComponentCheckboxChange(c, false, allComponents, visitedComponents);
			}
		}

		private void HandleComponentUnchecked([NotNull] Component component, [NotNull][ItemNotNull] List<Component> allComponents, [NotNull] HashSet<Component> visitedComponents)
		{
			// Handling OTHER component's defined dependencies based on the change to THIS component.
			foreach (Component c in allComponents)
			{
				if (c.IsSelected && c.Dependencies.Contains(component.Guid))
				{
					c.IsSelected = false;
					HandleComponentCheckboxChange(c, false, allComponents, visitedComponents);
				}
			}
		}
	}
}
