



using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{
	
	
	
	public class DryRunValidationResult
	{
		[NotNull]
		[ItemNotNull]
		public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();

		public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);

		public bool HasWarnings => Issues.Any(i => i.Severity == ValidationSeverity.Warning);

		
		
		
		[NotNull]
		public string GetSummaryMessage()
		{
			if ( IsValid && !HasWarnings )
			{
				return "✓ Dry-run validation passed successfully. All instructions appear to be correct.";
			}

			var sb = new StringBuilder();
			int errorCount = Issues.Count(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);
			int warningCount = Issues.Count(i => i.Severity == ValidationSeverity.Warning);

			if ( errorCount > 0 )
			{
				_ = sb.AppendLine($"✗ Validation failed with {errorCount} error(s) and {warningCount} warning(s).");
				_ = sb.AppendLine();
				_ = sb.AppendLine("The following issues must be resolved before installation:");
			}
			else if ( warningCount > 0 )
			{
				_ = sb.AppendLine($"⚠ Validation passed with {warningCount} warning(s).");
				_ = sb.AppendLine();
				_ = sb.AppendLine("You may proceed, but review the following warnings:");
			}

			return sb.ToString();
		}

		
		
		
		[NotNull]
		public string GetEndUserMessage()
		{
			var sb = new StringBuilder();
			_ = sb.AppendLine(GetSummaryMessage());
			_ = sb.AppendLine();

			
			var componentIssues = Issues
				.Where(i => i.AffectedComponent != null)
				.GroupBy(i => i.AffectedComponent)
				.ToList();

			if ( componentIssues.Any() )
			{
				_ = sb.AppendLine("Issues by component:");
				_ = sb.AppendLine();

				foreach ( IGrouping<ModComponent, ValidationIssue> group in componentIssues )
				{
					ModComponent component = group.Key;
					_ = sb.AppendLine($"━━━ {component.Name} ━━━");

					foreach ( ValidationIssue issue in group )
					{
						string icon;
						if ( issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Critical )
							icon = "✗";
						else if ( issue.Severity == ValidationSeverity.Warning )
							icon = "⚠";
						else
							icon = "ℹ";

						_ = sb.AppendLine($"{icon} {issue.Message}");

						
						string advice = GetEndUserAdvice(issue);
						if ( !string.IsNullOrEmpty(advice) )
						{
							_ = sb.AppendLine($"   → {advice}");
						}

						_ = sb.AppendLine();
					}
				}
			}

			
			var genericIssues = Issues.Where(i => i.AffectedComponent == null).ToList();
			if ( genericIssues.Any() )
			{
				_ = sb.AppendLine("━━━ General Issues ━━━");
				foreach ( ValidationIssue issue in genericIssues )
				{
					string icon;
					if ( issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Critical )
						icon = "✗";
					else if ( issue.Severity == ValidationSeverity.Warning )
						icon = "⚠";
					else
						icon = "ℹ";

					_ = sb.AppendLine($"{icon} {issue.Message}");

					string advice = GetEndUserAdvice(issue);
					if ( !string.IsNullOrEmpty(advice) )
					{
						_ = sb.AppendLine($"   → {advice}");
					}

					_ = sb.AppendLine();
				}
			}

			return sb.ToString();
		}

		
		
		
		[NotNull]
		public string GetEditorMessage()
		{
			var sb = new StringBuilder();
			_ = sb.AppendLine(GetSummaryMessage());
			_ = sb.AppendLine();

			
			var componentIssues = Issues
				.Where(i => i.AffectedComponent != null)
				.GroupBy(i => i.AffectedComponent)
				.ToList();

			if ( componentIssues.Any() )
			{
				foreach ( IGrouping<ModComponent, ValidationIssue> group in componentIssues )
				{
					ModComponent component = group.Key;
					_ = sb.AppendLine($"━━━ ModComponent: {component.Name} (GUID: {component.Guid}) ━━━");
					_ = sb.AppendLine();

					IOrderedEnumerable<IGrouping<int, ValidationIssue>> instructionGroups = group.GroupBy(i => i.InstructionIndex).OrderBy(g => g.Key);

					foreach ( IGrouping<int, ValidationIssue> instrGroup in instructionGroups )
					{
						if ( instrGroup.Key > 0 )
						{
							_ = sb.AppendLine($"  Instruction #{instrGroup.Key}:");

							ValidationIssue firstIssue = instrGroup.First();
							if ( firstIssue.AffectedInstruction != null )
							{
								_ = sb.AppendLine($"    Action: {firstIssue.AffectedInstruction.Action}");
								if ( firstIssue.AffectedInstruction.Source.Count != 0 )
								{
									_ = sb.AppendLine($"    Source: {string.Join(", ", firstIssue.AffectedInstruction.Source)}");
								}
								if ( !string.IsNullOrEmpty(firstIssue.AffectedInstruction.Destination) )
								{
									_ = sb.AppendLine($"    Destination: {firstIssue.AffectedInstruction.Destination}");
								}
							}
							_ = sb.AppendLine();
						}

						foreach ( ValidationIssue issue in instrGroup )
						{
							string icon;
							if ( issue.Severity == ValidationSeverity.Error || issue.Severity == ValidationSeverity.Critical )
								icon = "✗";
							else if ( issue.Severity == ValidationSeverity.Warning )
								icon = "⚠";
							else
								icon = "ℹ";

							_ = sb.AppendLine($"  {icon} [{issue.Category}] {issue.Message}");

							string advice = GetEditorAdvice(issue);
							if ( !string.IsNullOrEmpty(advice) )
							{
								_ = sb.AppendLine($"     → {advice}");
							}
						}

						_ = sb.AppendLine();
					}
				}
			}

			return sb.ToString();
		}

		private static string GetEndUserAdvice([NotNull] ValidationIssue issue)
		{
			if ( issue.Category == "ArchiveValidation" || issue.Category == "ExtractArchive" )
				return "This archive may be missing, corrupted, or incompatible. Try re-downloading it from the mod link.";

			if ( (issue.Category == "MoveFile" || issue.Category == "CopyFile") && issue.Message.Contains("does not exist") )
				return "Required files are missing. This usually means a previous mod installation step failed, or the mod archive is incomplete.";

			if ( (issue.Category == "MoveFile" || issue.Category == "CopyFile") && issue.Message.Contains("already exists") )
				return "This file conflict may be expected. If you continue and see errors, try deselecting conflicting mods.";

			if ( issue.Category == "DeleteFile" )
				return "Attempting to delete a file that doesn't exist. This may indicate incorrect instruction order.";

			if ( issue.Category == "ExecuteProcess" )
				return "The required executable is missing. Check if the mod archive was extracted correctly.";

			if ( issue.Category == "FileSystemInitialization" )
				return "Could not verify all files. Ensure you have set the correct mod and KOTOR directories in Settings.";

			return string.Empty;
		}

		private static string GetEditorAdvice([NotNull] ValidationIssue issue)
		{
			if ( issue.Category == "ArchiveValidation" )
				return "Verify the archive path is correct and the file exists in the source directory.";

			if ( issue.Category == "ExtractArchive" )
				return "Check that the archive is valid and not corrupted. Consider using a different archive format.";

			if ( (issue.Category == "MoveFile" || issue.Category == "CopyFile") && issue.Message.Contains("does not exist") )
				return "Add an Extract instruction before this operation, or verify the source path is correct. Check if the file should come from a previous component's instructions.";

			if ( (issue.Category == "MoveFile" || issue.Category == "CopyFile") && issue.Message.Contains("already exists") )
				return "Set 'Overwrite' to true if you want to replace the existing file, or reorder instructions to avoid conflicts.";

			if ( issue.Category == "DeleteFile" )
				return "Move this instruction to after the file is created, or remove it if it's unnecessary.";

			if ( issue.Category == "RenameFile" )
				return "Ensure the source file exists at the time this instruction runs. Add Dependencies if this relies on another component.";

			if ( issue.Category == "ExecuteProcess" )
				return "Verify the executable path is correct and the file will exist at execution time.";

			return "Review the instruction parameters and execution order.";
		}

		
		
		
		[NotNull]
		[ItemNotNull]
		public List<ModComponent> GetAffectedComponents()
		{
			return Issues
				.Where(i => i.AffectedComponent != null &&
					(i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical))
				.Select(i => i.AffectedComponent)
				.Distinct()
				.ToList();
		}

		
		
		
		[NotNull]
		[ItemNotNull]
		public List<ModComponent> GetSuggestedComponentsToDisable()
		{
			
			List<ModComponent> affectedComponents = GetAffectedComponents();
			var allSelectedComponents = MainConfig.AllComponents.Where(c => c.IsSelected).ToList();

			return affectedComponents.Where(component =>
			{
				
				bool isRequiredDependency = allSelectedComponents.Any(c =>
					c != component && c.Dependencies.Contains(component.Guid));

				return !isRequiredDependency;
			}).ToList();
		}
	}
}

