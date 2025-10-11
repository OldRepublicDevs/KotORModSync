// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;
using KOTORModSync.Core;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service for processing components and managing their state.
	/// </summary>
	public static class ComponentProcessingService
	{
	/// <summary>
	/// Attempts to auto-generate instructions for components without any.
	/// ONLY processes LOCAL archives already in the download directory.
	/// Does NOT trigger downloads - downloads must be explicitly requested via button clicks.
	/// </summary>
	public static async Task<int> TryAutoGenerateInstructionsForComponentsAsync(List<ModComponent> components)
	{
		if ( components == null || components.Count == 0 )
			return 0;

		try
		{
			// ONLY process local archives - no downloads
			// Downloads are triggered ONLY by user clicking "Fetch Downloads" button
			return await TryGenerateFromLocalArchivesAsync(components);
		}
		catch ( Exception ex )
		{
			await Logger.LogExceptionAsync(ex);
			return 0;
		}
	}

	/// <summary>
	/// Tries to generate instructions from archives already in the local mod directory.
	/// This is the ONLY method called during file loading - it does NOT download files.
	/// Processes ALL ModLinks even if instructions exist (avoiding duplicates).
	/// </summary>
	public static async Task<int> TryGenerateFromLocalArchivesAsync(List<ModComponent> components)
		{
			int generatedCount = 0;

			foreach ( ModComponent component in components )
			{
				// Process ALL components, even if they already have instructions
				// TryGenerateInstructionsFromArchive will handle avoiding duplicates internally
				int initialInstructionCount = component.Instructions.Count;

				// Try to generate instructions from local archives
				bool success = component.TryGenerateInstructionsFromArchive();
				if ( !success )
					continue;

				// Check if new instructions were added
				if ( component.Instructions.Count > initialInstructionCount )
				{
					generatedCount++;
					int newInstructions = component.Instructions.Count - initialInstructionCount;
					await Logger.LogAsync($"Added {newInstructions} instruction(s) from local archive for '{component.Name}': {component.InstallationMethod}");
				}
			}

			if ( generatedCount > 0 )
				await Logger.LogAsync($"Processed local archives and generated/updated instructions for {generatedCount} component(s).");

			return generatedCount;
		}
		/// <summary>
		/// Processes a list of components and determines their processing state.
		/// </summary>
		/// <param name="componentsList">The list of components to process.</param>
		/// <returns>A result containing the processing state and any reordered components.</returns>
		/// <exception cref="ArgumentNullException">Thrown when componentsList is null.</exception>
		public static async Task<ComponentProcessingResult> ProcessComponentsAsync([NotNull][ItemNotNull] List<ModComponent> componentsList)
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
					(bool isCorrectOrder, List<ModComponent> reorderedList) =
						ModComponent.ConfirmComponentsInstallOrder(componentsList);
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
		public static bool CanMoveComponent([NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> components, int relativeIndex)
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
		public static bool MoveComponent([NotNull] ModComponent component, [NotNull][ItemNotNull] List<ModComponent> components, int relativeIndex)
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
	/// <summary>
	/// Result of component processing operation
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
		public List<ModComponent> Components { get; set; }

		/// <summary>
		/// Gets or sets the reordered components (if reordering was needed).
		/// </summary>
		public List<ModComponent> ReorderedComponents { get; set; }

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
