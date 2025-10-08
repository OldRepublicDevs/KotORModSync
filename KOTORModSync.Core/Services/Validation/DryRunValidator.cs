// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
	/// <summary>
	/// Service for performing dry-run validation of component installations.
	/// </summary>
	public static class DryRunValidator
	{
		/// <summary>
		/// Validates all selected components using a virtual file system.
		/// </summary>
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

			// Get only selected components
			List<ModComponent> selectedComponents = components.Where(c => c.IsSelected).ToList();

			if ( selectedComponents.Count == 0 )
			{
				await Logger.LogAsync("No components selected for validation.");
				return result;
			}

			// Validate each component in installation order
			int componentIndex = 0;
			foreach ( ModComponent component in selectedComponents )
			{
				componentIndex++;
				cancellationToken.ThrowIfCancellationRequested();

				await Logger.LogAsync($"[{componentIndex}/{selectedComponents.Count}] Validating component '{component.Name}'...");

				try
				{
					// Check if component should be installed (dependencies/restrictions)
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

					// Validate component by executing with virtual file system
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

			// Add all issues from the virtual file system
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

		/// <summary>
		/// Validates component instructions by calling the ACTUAL ExecuteInstructionsAsync method
		/// with a virtual file system provider. This ensures 100% code reuse between validation
		/// and real installation - the ONLY difference is VirtualFileSystemProvider vs RealFileSystemProvider.
		/// </summary>
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
				// THIS IS THE KEY: We call the ACTUAL installation method with a virtual file system!
				// No duplicate logic, no separate validation path. Just swap the file system provider.
				ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
					component.Instructions,
					allComponents,
					cancellationToken,
					fileSystem  // Virtual file system provider
				);

				// Check for errors
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

