// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{
	/// <summary>
	/// Helper class to present validation results to users with actionable information.
	/// Used by GUI layer to create dialogs and user controls.
	/// </summary>
	public static class ValidationResultPresenter
	{
		/// <summary>
		/// Gets a user-friendly title for the validation result dialog.
		/// </summary>
		[NotNull]
		public static string GetDialogTitle([NotNull] DryRunValidationResult result)
		{
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			if ( result.IsValid && !result.HasWarnings )
			{
				return "✓ Validation Passed";
			}

			if ( result.IsValid && result.HasWarnings )
			{
				return "⚠ Validation Passed with Warnings";
			}

			return "✗ Validation Failed";
		}

		/// <summary>
		/// Gets the main message to display to the user.
		/// </summary>
		[NotNull]
		public static string GetMainMessage([NotNull] DryRunValidationResult result, bool isEditorMode)
		{
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			return isEditorMode
				? result.GetEditorMessage()
				: result.GetEndUserMessage();
		}

		/// <summary>
		/// Gets actionable steps the user can take to resolve issues.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<ActionableStep> GetActionableSteps([NotNull] DryRunValidationResult result, bool isEditorMode)
		{
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			var steps = new List<ActionableStep>();

			if ( result.IsValid )
			{
				return steps; // No actions needed
			}

			// Group issues by component
			var componentIssues = result.Issues
				.Where(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical)
				.Where(i => i.AffectedComponent != null)
				.GroupBy(i => i.AffectedComponent)
				.ToList();

			foreach ( var group in componentIssues )
			{
				Component component = group.Key;
				List<ValidationIssue> issues = group.ToList();

				// Determine if issues are fixable
				bool hasArchiveIssues = issues.Any(i => i.Category == "ArchiveValidation" || i.Category == "ExtractArchive");
				bool hasOrderIssues = issues.Any(i => i.Message.Contains("does not exist") && !hasArchiveIssues);

				if ( hasArchiveIssues && !isEditorMode )
				{
					// End user: suggest downloading missing mods
					steps.Add(new ActionableStep
					{
						ActionType = ActionType.DownloadMod,
						Component = component,
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
					});
				}
				else if ( hasOrderIssues && isEditorMode )
				{
					// Editor mode: suggest reordering instructions
					steps.Add(new ActionableStep
					{
						ActionType = ActionType.ReorderInstructions,
						Component = component,
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
					});
				}
				else if ( !isEditorMode )
				{
					// End user: suggest disabling problematic component
					bool isRequiredDependency = MainConfig.AllComponents
						.Any(c => c != component && c.IsSelected && c.Dependencies.Contains(component.Guid));

					if ( !isRequiredDependency )
					{
						steps.Add(new ActionableStep
						{
							ActionType = ActionType.DisableComponent,
							Component = component,
							Title = $"Disable problematic mod: {component.Name}",
							Description = "This mod has configuration issues. Disabling it will allow other mods to install.",
							Instructions = new List<string>
							{
								"Click the button below to automatically deselect this mod",
								"Then run validation again"
							},
							CanAutoResolve = true
						});
					}
					else
					{
						steps.Add(new ActionableStep
						{
							ActionType = ActionType.ContactSupport,
							Component = component,
							Title = $"Report issue with: {component.Name}",
							Description = "This mod has issues and is required by other selected mods.",
							Instructions = new List<string>
							{
								"This mod cannot be disabled as other mods depend on it",
								"Please report this issue to the mod build creator",
								"Include the validation log from the Output window"
							},
							CanAutoResolve = false
						});
					}
				}
				else
				{
					// Editor mode: general fix instruction
					steps.Add(new ActionableStep
					{
						ActionType = ActionType.EditInstructions,
						Component = component,
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
					});
				}
			}

			return steps;
		}

		/// <summary>
		/// Checks if auto-resolution is available for any issues.
		/// </summary>
		public static bool CanAutoResolve([NotNull] DryRunValidationResult result)
		{
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			List<Component> componentsToDisable = result.GetSuggestedComponentsToDisable();
			return componentsToDisable.Count > 0;
		}

		/// <summary>
		/// Auto-resolves issues by disabling problematic components.
		/// </summary>
		/// <returns>Number of components that were disabled.</returns>
		public static int AutoResolveIssues([NotNull] DryRunValidationResult result)
		{
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			List<Component> componentsToDisable = result.GetSuggestedComponentsToDisable();

			foreach ( Component component in componentsToDisable )
			{
				component.IsSelected = false;
			}

			return componentsToDisable.Count;
		}

		/// <summary>
		/// Gets components that should be highlighted in the UI.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		public static List<Component> GetComponentsToHighlight([NotNull] DryRunValidationResult result)
		{
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			return result.GetAffectedComponents();
		}
	}

	/// <summary>
	/// Represents an actionable step the user can take to resolve validation issues.
	/// </summary>
	public class ActionableStep
	{
		public ActionType ActionType { get; set; }
		public Component Component { get; set; }

		[NotNull]
		public string Title { get; set; } = string.Empty;

		[NotNull]
		public string Description { get; set; } = string.Empty;

		[NotNull]
		[ItemNotNull]
		public List<string> Instructions { get; set; } = new List<string>();

		public bool CanAutoResolve { get; set; }
	}

	/// <summary>
	/// Type of action the user can take.
	/// </summary>
	public enum ActionType
	{
		DownloadMod,
		DisableComponent,
		ReorderInstructions,
		EditInstructions,
		ContactSupport
	}
}

