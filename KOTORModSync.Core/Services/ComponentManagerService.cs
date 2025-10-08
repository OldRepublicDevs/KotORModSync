// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service for managing components and their operations
	/// </summary>
	public class ComponentManagerService
	{
		/// <summary>
		/// Finds and resolves duplicate components in the list
		/// </summary>
		/// <param name="components">List of components to check</param>
		/// <param name="promptUser">Whether to prompt user for confirmation</param>
		/// <returns>True if all duplicates were resolved, false otherwise</returns>
		public static async Task<bool> FindDuplicateComponentsAsync([NotNull][ItemNotNull] List<ModComponent> components, bool promptUser = true)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			// Check for duplicate GUID
			bool duplicatesFixed = true;
			bool continuePrompting = promptUser;

			foreach ( ModComponent component in components )
			{
				ModComponent duplicateComponent = components.Find(c => c.Guid == component.Guid && c != component);

				if ( duplicateComponent is null )
					continue;

				if ( !Guid.TryParse(duplicateComponent.Guid.ToString(), out Guid _) )
				{
					await Logger.LogWarningAsync(
						$"Invalid GUID for component '{component.Name}'. Got '{component.Guid}'"
					);

					if ( MainConfig.AttemptFixes )
					{
						await Logger.LogVerboseAsync("Fixing the above issue automatically...");
						duplicateComponent.Guid = Guid.NewGuid();
					}
				}

				string message = $"ModComponent '{component.Name}' has a duplicate GUID with component '{duplicateComponent.Name}'";
				await Logger.LogAsync(message);

				bool? confirm = true;
				if ( continuePrompting )
				{
					// This would need to be handled by the UI layer
					// For now, we'll auto-fix if AttemptFixes is enabled
					if ( MainConfig.AttemptFixes )
					{
						confirm = true;
					}
					else
					{
						// Return false to indicate user interaction is needed
						return false;
					}
				}

				switch ( confirm )
				{
					case true:
						duplicateComponent.Guid = Guid.NewGuid();
						await Logger.LogAsync($"Replaced GUID of component '{duplicateComponent.Name}'");
						break;
					case false:
						await Logger.LogVerboseAsync($"User canceled GUID replacement for component '{duplicateComponent.Name}'");
						duplicatesFixed = false;
						break;
					case null:
						continuePrompting = false;
						break;
				}
			}

			return duplicatesFixed;
		}

		/// <summary>
		/// Validates all selected components for installation
		/// </summary>
		/// <param name="components">List of components to validate</param>
		/// <returns>True if all components are valid, false otherwise</returns>
		public static async Task<bool> ValidateComponentsForInstallationAsync([NotNull][ItemNotNull] List<ModComponent> components)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			await Logger.LogAsync("Validating individual components, this might take a while...");
			bool individuallyValidated = true;

			foreach ( ModComponent component in components )
			{
				if ( !component.IsSelected )
					continue;

				if ( component.Restrictions.Count > 0 && component.IsSelected )
				{
					List<ModComponent> restrictedComponentsList = ModComponent.FindComponentsFromGuidList(
						component.Restrictions,
						components
					);
					foreach ( ModComponent restrictedComponent in restrictedComponentsList )
					{
						if ( restrictedComponent?.IsSelected == true )
						{
							await Logger.LogErrorAsync($"Cannot install '{component.Name}' due to '{restrictedComponent.Name}' being selected for install.");
							individuallyValidated = false;
						}
					}
				}

				if ( component.Dependencies.Count > 0 && component.IsSelected )
				{
					List<ModComponent> dependencyComponentsList = ModComponent.FindComponentsFromGuidList(component.Dependencies, components);
					foreach ( ModComponent dependencyComponent in dependencyComponentsList )
					{
						if ( dependencyComponent?.IsSelected != true )
						{
							await Logger.LogErrorAsync($"Cannot install '{component.Name}' due to '{dependencyComponent?.Name}' not being selected for install.");
							individuallyValidated = false;
						}
					}
				}

				var validator = new ComponentValidation(component, components);
				await Logger.LogVerboseAsync($" == Validating '{component.Name}' == ");
				individuallyValidated &= validator.Run();
			}

			await Logger.LogVerboseAsync("Finished validating all components.");
			return individuallyValidated;
		}

		/// <summary>
		/// Creates a new component with default values
		/// </summary>
		/// <returns>New component instance</returns>
		public static ModComponent CreateNewComponent() => new ModComponent
		{
			Guid = Guid.NewGuid(),
			Name = "new mod_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()),
		};

		/// <summary>
		/// Removes a component from the list, checking for dependencies
		/// </summary>
		/// <param name="component">ModComponent to remove</param>
		/// <param name="components">List of all components</param>
		/// <returns>True if component can be removed, false if dependencies exist</returns>
		public static bool CanRemoveComponent([NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> components)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			return !components.Any(c => c.Dependencies.Any(g => g == component.Guid));
		}

		/// <summary>
		/// Moves a component to a new position in the list
		/// </summary>
		/// <param name="component">ModComponent to move</param>
		/// <param name="components">List of components</param>
		/// <param name="relativeIndex">Relative index to move (-1 for up, 1 for down)</param>
		public static void MoveComponent([NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> components, int relativeIndex)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			int index = components.IndexOf(component);
			if ( component is null
				|| (index == 0 && relativeIndex < 0)
				|| index == -1
				|| index + relativeIndex == components.Count )
			{
				return;
			}

			_ = components.Remove(component);
			components.Insert(index + relativeIndex, component);
		}
	}
}
