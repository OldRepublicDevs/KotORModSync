// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service for processing components and managing their state.
	/// </summary>
	public static class ComponentProcessingService
	{
		/// <summary>
		/// Processes a list of components and determines their processing state.
		/// </summary>
		/// <param name="componentsList">The list of components to process.</param>
		/// <returns>A result containing the processing state and any reordered components.</returns>
		/// <exception cref="ArgumentNullException">Thrown when componentsList is null.</exception>
		public static async Task<ComponentProcessingResult> ProcessComponentsAsync([NotNull][ItemNotNull] List<Component> componentsList)
		{
			if ( componentsList == null )
				throw new ArgumentNullException(nameof(componentsList));

			try
			{
				if ( componentsList.IsNullOrEmptyCollection() )
				{
					return new ComponentProcessingResult
					{
						IsEmpty = true,
						Success = true
					};
				}

				// Check for circular dependencies and reorder if needed
				try
				{
					(bool isCorrectOrder, List<Component> reorderedList) =
						Component.ConfirmComponentsInstallOrder(componentsList);
					if ( !isCorrectOrder )
					{
						await Logger.LogAsync("Reordered list to match dependency structure.");
						return new ComponentProcessingResult
						{
							IsEmpty = false,
							Success = true,
							ReorderedComponents = reorderedList,
							NeedsReordering = true
						};
					}
				}
				catch ( KeyNotFoundException )
				{
					await Logger.LogErrorAsync(
						"Cannot process order of components. " +
						"There are circular dependency conflicts that cannot be automatically resolved. " +
						"Please resolve these before attempting an installation."
					);
					return new ComponentProcessingResult
					{
						IsEmpty = false,
						Success = false,
						HasCircularDependencies = true
					};
				}

				return new ComponentProcessingResult
				{
					IsEmpty = false,
					Success = true,
					Components = componentsList
				};
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return new ComponentProcessingResult
				{
					IsEmpty = false,
					Success = false,
					Exception = ex
				};
			}
		}

		/// <summary>
		/// Validates that a component can be moved to a new position.
		/// </summary>
		/// <param name="component">The component to move.</param>
		/// <param name="components">The list of components.</param>
		/// <param name="relativeIndex">The relative index to move to.</param>
		/// <returns>True if the move is valid, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">Thrown when component or components is null.</exception>
		public static bool CanMoveComponent([NotNull] Component component, [NotNull][ItemNotNull] List<Component> components, int relativeIndex)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			int index = components.IndexOf(component);
			return index != -1 &&
				   !(index == 0 && relativeIndex < 0) &&
				   index + relativeIndex < components.Count &&
				   index + relativeIndex >= 0;
		}

		/// <summary>
		/// Moves a component to a new position in the list.
		/// </summary>
		/// <param name="component">The component to move.</param>
		/// <param name="components">The list of components.</param>
		/// <param name="relativeIndex">The relative index to move to.</param>
		/// <returns>True if the move was successful, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">Thrown when component or components is null.</exception>
		public static bool MoveComponent([NotNull] Component component, [NotNull][ItemNotNull] List<Component> components, int relativeIndex)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			if ( !CanMoveComponent(component, components, relativeIndex) )
				return false;

			int index = components.IndexOf(component);
			_ = components.Remove(component);
			components.Insert(index + relativeIndex, component);
			return true;
		}
	}

	/// <summary>
	/// Result of component processing operations.
	/// </summary>
	public class ComponentProcessingResult
	{
		/// <summary>
		/// Gets or sets whether the component list is empty.
		/// </summary>
		public bool IsEmpty { get; set; }

		/// <summary>
		/// Gets or sets whether the processing was successful.
		/// </summary>
		public bool Success { get; set; }

		/// <summary>
		/// Gets or sets the processed components.
		/// </summary>
		public List<Component> Components { get; set; }

		/// <summary>
		/// Gets or sets the reordered components (if reordering was needed).
		/// </summary>
		public List<Component> ReorderedComponents { get; set; }

		/// <summary>
		/// Gets or sets whether the components needed reordering.
		/// </summary>
		public bool NeedsReordering { get; set; }

		/// <summary>
		/// Gets or sets whether there are circular dependencies.
		/// </summary>
		public bool HasCircularDependencies { get; set; }

		/// <summary>
		/// Gets or sets any exception that occurred during processing.
		/// </summary>
		public Exception Exception { get; set; }
	}
}
