



using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Services
{

	
	
	
	public class ComponentProcessingService
	{
		
		
		
		public static async Task<ComponentProcessingResult> ProcessComponentsAsync(List<ModComponent> components)
		{
			var result = new ComponentProcessingResult
			{
				Components = components,
				IsEmpty = components == null || components.Count == 0,
				Success = false,
				HasCircularDependencies = false
			};

			try
			{
				if ( result.IsEmpty )
				{
					result.Success = true;
					return result;
				}

				
				await Logger.LogVerboseAsync($"Processing {components.Count} components");

			
			try
			{
				(bool isCorrectOrder, List<ModComponent> reorderedList) =
					ModComponent.ConfirmComponentsInstallOrder(components);
				if ( !isCorrectOrder )
				{
					await Logger.LogAsync("Reordered list to match dependency structure.");
					result.ReorderedComponents = reorderedList;
					result.NeedsReordering = true;
				}
			}
			catch ( KeyNotFoundException )
			{
				await Logger.LogErrorAsync(
					"Cannot process order of components. " +
					"There are circular dependency conflicts that cannot be automatically resolved. " +
					"Please resolve these before attempting an installation."
				);
				result.HasCircularDependencies = true;
				result.Success = false;
				return result;
			}

			result.Success = true;
			return result;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error processing components");
				return result;
			}
		}

		
		
		
		public static async Task<int> TryAutoGenerateInstructionsForComponentsAsync(List<ModComponent> components)
		{
			if ( components == null || components.Count == 0 )
				return 0;

			try
			{
				int generatedCount = 0;
				int skippedCount = 0;

				foreach ( ModComponent component in components )
				{
					
					if ( component.Instructions.Count > 0 )
					{
						skippedCount++;
						continue;
					}

					
					bool success = component.TryGenerateInstructionsFromArchive();
					if ( !success )
						continue;

					generatedCount++;
					await Logger.LogAsync($"Auto-generated instructions for '{component.Name}': {component.InstallationMethod}");
				}

				if ( generatedCount > 0 )
					await Logger.LogAsync($"Auto-generated instructions for {generatedCount} component(s). Skipped {skippedCount} component(s) that already had instructions.");

				return generatedCount;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return 0;
			}
		}
	}
}

