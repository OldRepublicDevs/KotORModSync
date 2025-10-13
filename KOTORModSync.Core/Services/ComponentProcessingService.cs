// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;
using KOTORModSync.Core;

namespace KOTORModSync.Core.Services
{

	public static class ComponentProcessingService
	{

	public static async Task<int> TryAutoGenerateInstructionsForComponentsAsync(List<ModComponent> components)
	{
		if ( components == null || components.Count == 0 )
			return 0;

		try
		{

			return await TryGenerateFromLocalArchivesAsync(components);
		}
		catch ( Exception ex )
		{
			await Logger.LogExceptionAsync(ex);
			return 0;
		}
	}

	public static async Task<int> TryGenerateFromLocalArchivesAsync(List<ModComponent> components)
		{
			int generatedCount = 0;

			foreach ( ModComponent component in components )
			{

				int initialInstructionCount = component.Instructions.Count;

				bool success = component.TryGenerateInstructionsFromArchive();
				if ( !success )
					continue;

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


	public class ComponentProcessingResult
	{

		public bool IsEmpty { get; set; }

		public bool Success { get; set; }

		public List<ModComponent> Components { get; set; }

		public List<ModComponent> ReorderedComponents { get; set; }

		public bool NeedsReordering { get; set; }

		public bool HasCircularDependencies { get; set; }

		public Exception Exception { get; set; }
	}
}
