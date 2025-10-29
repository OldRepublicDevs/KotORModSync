// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{
	public static class ValidationResultPresenter
	{
		[NotNull]
		public static string GetDialogTitle( [NotNull] DryRunValidationResult result )
		{
			if (result == null)
				throw new ArgumentNullException( nameof( result ) );

			if (result.IsValid && !result.HasWarnings)
			{
				return "✓ Validation Passed";
			}

			if (result.IsValid && result.HasWarnings)
			{
				return "⚠ Validation Passed with Warnings";
			}

			return "✗ Validation Failed";
		}

		[NotNull]
		public static string GetMainMessage( [NotNull] DryRunValidationResult result, bool isEditorMode )
		{
			if (result == null)
				throw new ArgumentNullException( nameof( result ) );

			return isEditorMode
				? result.GetEditorMessage()
				: result.GetEndUserMessage();
		}

		[NotNull]
		[ItemNotNull]
		public static List<ActionableStep> GetActionableSteps( [NotNull] DryRunValidationResult result, bool isEditorMode )
		{
			if (result == null)
				throw new ArgumentNullException( nameof( result ) );

			List<ActionableStep> steps = new List<ActionableStep>();

			if (result.IsValid)
			{
				return steps;
			}


			List<IGrouping<ModComponent, ValidationIssue>> componentIssues = result.Issues
				.Where( i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical )
				.Where( i => i.AffectedComponent != null )
				.GroupBy( i => i.AffectedComponent )
				.ToList();

			foreach (IGrouping<ModComponent, ValidationIssue> group in componentIssues)
			{
				ModComponent component = group.Key;
				List<ValidationIssue> issues = group.ToList();


				bool hasArchiveIssues = issues.Exists( i => string.Equals( i.Category, "ArchiveValidation", StringComparison.Ordinal ) || string.Equals( i.Category, "ExtractArchive", StringComparison.Ordinal ) );
				bool hasOrderIssues = issues.Exists( i => i.Message.Contains( "does not exist" ) && !hasArchiveIssues );

				if (hasArchiveIssues && !isEditorMode)
				{

					steps.Add( new ActionableStep
					{
						ActionType = ActionType.DownloadMod,
						ModComponent = component,
						Title = $"Download missing mod: {component.Name}",
						Description = "This mod's archive file is missing, corrupted, or incompatible.",
						Instructions = new List<string>
						{
							"1. Check if the mod file exists in your mod directory",
							"2. If missing, download it from the mod link",
							"3. Place it in your mod directory",
							"4. Run validation again"
						},
						CanAutoResolve = false
					} );
				}
				else if (hasOrderIssues && isEditorMode)
				{

					steps.Add( new ActionableStep
					{
						ActionType = ActionType.ReorderInstructions,
						ModComponent = component,
						Title = $"Fix instruction order: {component.Name}",
						Description = "Instructions are attempting to access files that don't exist yet.",
						Instructions = new List<string>
						{
							"1. Review the instruction order for this component",
							"2. Ensure Extract instructions come before Move/Copy instructions",
							"3. Check that files are created before they are used",
							"4. Add Dependencies if this component relies on files from another component"
						},
						CanAutoResolve = false
					} );
				}
				else if (!isEditorMode)
				{

					bool isRequiredDependency = MainConfig.AllComponents
						.Exists( c => c != component && c.IsSelected && c.Dependencies.Contains( component.Guid ) );

					if (!isRequiredDependency)
					{
						steps.Add( new ActionableStep
						{
							ActionType = ActionType.DisableComponent,
							ModComponent = component,
							Title = $"Disable problematic mod: {component.Name}",
							Description = "This mod has configuration issues. Disabling it will allow other mods to install.",
							Instructions = new List<string>
							{
								"Click the button below to automatically deselect this mod",
								"Then run validation again"
							},
							CanAutoResolve = true
						} );
					}
					else
					{
						steps.Add( new ActionableStep
						{
							ActionType = ActionType.ContactSupport,
							ModComponent = component,
							Title = $"Report issue with: {component.Name}",
							Description = "This mod has issues and is required by other selected mods.",
							Instructions = new List<string>
							{
								"This mod cannot be disabled as other mods depend on it",
								"Please report this issue to the mod build creator",
								"Include the validation log from the Output window"
							},
							CanAutoResolve = false
						} );
					}
				}
				else
				{

					steps.Add( new ActionableStep
					{
						ActionType = ActionType.EditInstructions,
						ModComponent = component,
						Title = $"Edit instructions: {component.Name}",
						Description = "Review and fix the instructions for this component.",
						Instructions = new List<string>
						{
							"1. Select the component in the left list",
							"2. Review the instructions in the editor",
							"3. Fix the issues based on the error messages",
							"4. Save and run validation again"
						},
						CanAutoResolve = false
					} );
				}
			}

			return steps;
		}




		public static bool CanAutoResolve( [NotNull] DryRunValidationResult result )
		{
			if (result == null)
				throw new ArgumentNullException( nameof( result ) );

			List<ModComponent> componentsToDisable = result.GetSuggestedComponentsToDisable();
			return componentsToDisable.Count > 0;
		}





		public static int AutoResolveIssues( [NotNull] DryRunValidationResult result )
		{
			if (result == null)
				throw new ArgumentNullException( nameof( result ) );

			List<ModComponent> componentsToDisable = result.GetSuggestedComponentsToDisable();

			foreach (ModComponent component in componentsToDisable)
			{
				component.IsSelected = false;
			}

			return componentsToDisable.Count;
		}




		[NotNull]
		[ItemNotNull]
		public static List<ModComponent> GetComponentsToHighlight( [NotNull] DryRunValidationResult result )
		{
			if (result == null)
				throw new ArgumentNullException( nameof( result ) );

			return result.GetAffectedComponents();
		}
	}




	public class ActionableStep
	{
		public ActionType ActionType { get; set; }
		public ModComponent ModComponent { get; set; }

		[NotNull]
		public string Title { get; set; } = string.Empty;

		[NotNull]
		public string Description { get; set; } = string.Empty;

		[NotNull]
		[ItemNotNull]
		public List<string> Instructions { get; set; } = new List<string>();

		public bool CanAutoResolve { get; set; }
	}




	public enum ActionType
	{
		DownloadMod,
		DisableComponent,
		ReorderInstructions,
		EditInstructions,
		ContactSupport
	}
}