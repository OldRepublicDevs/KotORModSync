// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{
	
	
	
	public static class DryRunValidator
	{
		
		
		
		[NotNull]
		public static async Task<DryRunValidationResult> ValidateInstallationAsync(
			[NotNull][ItemNotNull] List<ModComponent> components,
			CancellationToken cancellationToken = default
		)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			var result = new DryRunValidationResult();
			var virtualFileSystem = new VirtualFileSystemProvider();

			await Logger.LogAsync("Starting dry-run validation of installation...");
			await Logger.LogAsync($"Validating {components.Count(c => c.IsSelected)} selected component(s)...");

			
			List<ModComponent> selectedComponents = components.Where(c => c.IsSelected).ToList();

			if ( selectedComponents.Count == 0 )
			{
				await Logger.LogAsync("No components selected for validation.");
				return result;
			}

			
			int componentIndex = 0;
			foreach ( ModComponent component in selectedComponents )
			{
				componentIndex++;
				cancellationToken.ThrowIfCancellationRequested();

				await Logger.LogAsync($"[{componentIndex}/{selectedComponents.Count}] Validating component '{component.Name}'...");

				try
				{
					
					if ( !component.ShouldInstallComponent(components) )
					{
						await Logger.LogWarningAsync(
							$"ModComponent '{component.Name}' has unmet dependencies or restriction conflicts. It will be skipped."
						);

						result.Issues.Add(new ValidationIssue
						{
							Severity = ValidationSeverity.Warning,
							Category = "DependencyValidation",
							Message = "ModComponent has unmet dependencies or restriction conflicts and will be skipped during installation.",
							AffectedComponent = component,
							Timestamp = DateTimeOffset.UtcNow
						});

						continue;
					}

					
					await ValidateComponentInstructionsAsync(
						component,
						components,
						virtualFileSystem,
						result,
						cancellationToken
					);
				}
				catch ( Exception ex )
				{
					await Logger.LogExceptionAsync(ex);

					result.Issues.Add(new ValidationIssue
					{
						Severity = ValidationSeverity.Critical,
						Category = "ValidationException",
						Message = $"Unexpected error during validation: {ex.Message}",
						AffectedComponent = component,
						Timestamp = DateTimeOffset.UtcNow
					});
				}
			}

			
			foreach ( ValidationIssue issue in virtualFileSystem.ValidationIssues )
			{
				result.Issues.Add(issue);
			}

			await Logger.LogAsync("Dry-run validation completed.");
			await Logger.LogAsync($"Results: {result.Issues.Count} issue(s) found " +
				$"({result.Issues.Count(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical)} errors, " +
				$"{result.Issues.Count(i => i.Severity == ValidationSeverity.Warning)} warnings)");

			return result;
		}

		
		
		
		
		
		private static async Task ValidateComponentInstructionsAsync(
			[NotNull] ModComponent component,
			[NotNull][ItemNotNull] List<ModComponent> allComponents,
			[NotNull] VirtualFileSystemProvider fileSystem,
			[NotNull] DryRunValidationResult result,
			CancellationToken cancellationToken
		)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( allComponents == null )
				throw new ArgumentNullException(nameof(allComponents));
			if ( fileSystem == null )
				throw new ArgumentNullException(nameof(fileSystem));
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			try
			{
				
				
				ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
					component.Instructions,
					allComponents,
					cancellationToken,
					fileSystem  
				);

				
				if ( exitCode != ModComponent.InstallExitCode.Success )
				{
					result.Issues.Add(new ValidationIssue
					{
						Severity = ValidationSeverity.Error,
						Category = "ExecutionError",
						Message = $"ModComponent failed validation with exit code: {exitCode}",
						AffectedComponent = component,
						Timestamp = DateTimeOffset.UtcNow
					});
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);

				result.Issues.Add(new ValidationIssue
				{
					Severity = ValidationSeverity.Error,
					Category = "ValidationException",
					Message = $"Exception during validation: {ex.Message}",
					AffectedComponent = component,
					Timestamp = DateTimeOffset.UtcNow
				});
			}
		}
	}
}

