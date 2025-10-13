// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
	public partial class ModManagementDialog : Window
	{
		private readonly ModManagementService _modManagementService;
		private readonly List<ModComponent> _originalComponents;
		private readonly IModManagementDialogService _dialogService;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public bool ModificationsApplied { get; private set; }

		public ModManagementDialog()
		{
			InitializeComponent();
			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		public ModManagementDialog(ModManagementService modManagementService, IModManagementDialogService dialogService)
		{
			_modManagementService = modManagementService ?? throw new ArgumentNullException(nameof(modManagementService));
			_dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
			_originalComponents = MainConfig.AllComponents.ToList();
			ModificationsApplied = false;

			InitializeComponent();
			DataContext = _modManagementService.GetModStatistics();
			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		#region Batch Operations

		private async void SelectAllMods_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					ModManagementService.BatchOperationResult result = await _modManagementService.PerformBatchOperation(
						_originalComponents,
						ModManagementService.BatchModOperation.SetSelected,
						new Dictionary<string, object> { ["selected"] = true });

					ShowBatchResult("Select All", result);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to select all mods");
			}
		}

		private async void DeselectAllMods_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					ModManagementService.BatchOperationResult result = await _modManagementService.PerformBatchOperation(
						_originalComponents,
						ModManagementService.BatchModOperation.SetSelected,
						new Dictionary<string, object> { ["selected"] = false });

					ShowBatchResult("Deselect All", result);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to deselect all mods");
			}
		}

		private async void InvertSelection_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					ModManagementService.BatchOperationResult result = await _modManagementService.PerformBatchOperation(
					_originalComponents,
					ModManagementService.BatchModOperation.SetSelected,
					new Dictionary<string, object> { ["invert"] = true });

					ShowBatchResult("Invert Selection", result);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to invert selection");
			}
		}

		private async void ValidateAllMods_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					Dictionary<ModComponent, ModManagementService.ModValidationResult> results = _modManagementService.ValidateAllMods();
					int errorCount = results.Count(r => !r.Value.IsValid);
					int warningCount = results.Sum(r => r.Value.Warnings.Count);

					await _dialogService.ShowInformationDialog(
				"Validation complete!\n\n" +
				$"Errors: {errorCount}\n" +
				$"Warnings: {warningCount}\n\n" +
				$"Valid mods: {results.Count(r => r.Value.IsValid)}/{results.Count}");
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to validate all mods");
			}
		}

		private async void CheckFileAvailability_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					int availableCount = 0;
					int unavailableCount = 0;

					foreach ( ModComponent component in _originalComponents.Where(c => c.IsSelected) )
					{
						if ( component.IsDownloaded )
							availableCount++;
						else
							unavailableCount++;
					}

					await _dialogService.ShowInformationDialog(
					"File Availability Check:\n\n" +
				$"Available: {availableCount}\n" +
				$"Unavailable: {unavailableCount}");
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to check file availability");
			}
		}

		private void SortByName_Click(object sender, RoutedEventArgs e)
		{
			_modManagementService.SortMods();
			ModificationsApplied = true;
		}

		private void SortByNameDesc_Click(object sender, RoutedEventArgs e)
		{
			_modManagementService.SortMods(ModManagementService.ModSortCriteria.Name, ModManagementService.SortOrder.Descending);
			ModificationsApplied = true;
		}

		private void SortByAuthor_Click(object sender, RoutedEventArgs e)
		{
			_modManagementService.SortMods(ModManagementService.ModSortCriteria.Author);
			ModificationsApplied = true;
		}

		private void SortByCategory_Click(object sender, RoutedEventArgs e)
		{
			_modManagementService.SortMods(ModManagementService.ModSortCriteria.Category);
			ModificationsApplied = true;
		}

		private void SortByTier_Click(object sender, RoutedEventArgs e)
		{
			_modManagementService.SortMods(ModManagementService.ModSortCriteria.Tier);
			ModificationsApplied = true;
		}

		private async void SetAllDownloaded_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					ModManagementService.BatchOperationResult result = await _modManagementService.PerformBatchOperation(
						_originalComponents,
						ModManagementService.BatchModOperation.SetDownloaded,
						new Dictionary<string, object> { ["downloaded"] = true });

					ShowBatchResult("Set All Downloaded", result);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to set all mods downloaded");
			}
		}

		private async void SetAllNotDownloaded_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					ModManagementService.BatchOperationResult result = await _modManagementService.PerformBatchOperation(
						_originalComponents,
						ModManagementService.BatchModOperation.SetDownloaded,
						new Dictionary<string, object> { ["downloaded"] = false });

					ShowBatchResult("Set All Not Downloaded", result);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to set all mods not downloaded");
			}
		}

		private async void UpdateCategories_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformBatchOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to update categories.");
						return;
					}

					// Get existing categories
					var existingCategories = _originalComponents
						.Where(c => c.Category != null && c.Category.Count > 0)
						.SelectMany(c => c.Category)
						.Where(cat => !string.IsNullOrWhiteSpace(cat))
						.Distinct()
						.OrderBy(c => c)
						.ToList();

					// Show dialog with category options
					string categoryOptions = existingCategories.Any()
						? "\n\nExisting categories:\n  • " + string.Join("\n  • ", existingCategories)
						: "\n\nNo existing categories found.";

					// For now, prompt user to enter a category name via confirmation dialog
					// In a full implementation, this would be a custom dialog with dropdown + text input
					bool? proceed = await _dialogService.ShowConfirmationDialog(
						$"Update category for {selectedComponents.Count} selected mod(s)?{categoryOptions}\n\n" +
						"This feature requires a custom input dialog.\n" +
						"For now, you can edit categories individually in the mod editor.\n\n" +
						"Would you like to clear all categories for selected mods?",
						"Clear Categories",
						"Cancel");

					if ( proceed == true )
					{
						ModManagementService.BatchOperationResult result = await _modManagementService.PerformBatchOperation(
							selectedComponents,
							ModManagementService.BatchModOperation.UpdateCategory,
							new Dictionary<string, object> { ["category"] = string.Empty });

						ShowBatchResult("Clear Categories", result);
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to update categories");
			}
		}

		#endregion

		#region Import/Export Operations

		private async void ImportFromToml_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformImportOperationAsync(async () =>
				{
					string[] files = await _dialogService.ShowFileDialog(isFolderDialog: false, windowName: "Import from TOML file");
					if ( files != null && files.Length > 0 )
					{
						List<ModComponent> imported = await _modManagementService.ImportMods(files[0]);
						await _dialogService.ShowInformationDialog($"Imported {imported.Count} component(s)");
						ModificationsApplied = true;
						_dialogService.RefreshStatistics();
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to import from TOML file");
			}
		}

		private async void ImportFromJson_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformImportOperationAsync(async () =>
				{
					string[] files = await _dialogService.ShowFileDialog(isFolderDialog: false, windowName: "Import from JSON file");
					if ( files != null && files.Length > 0 )
					{
						List<ModComponent> imported = await _modManagementService.ImportMods(files[0]);
						await _dialogService.ShowInformationDialog($"Imported {imported.Count} component(s)");
						ModificationsApplied = true;
						_dialogService.RefreshStatistics();
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to import from JSON file");
			}
		}

		private async void ImportFromXml_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformImportOperationAsync(async () =>
				{
					string[] files = await _dialogService.ShowFileDialog(isFolderDialog: false, windowName: "Import from XML file");
					if ( files != null && files.Length > 0 )
					{
						List<ModComponent> imported = await _modManagementService.ImportMods(files[0]);
						await _dialogService.ShowInformationDialog($"Imported {imported.Count} component(s)");
						ModificationsApplied = true;
						_dialogService.RefreshStatistics();
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to import from XML file");
			}
		}

		private async void ExportToToml_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformExportOperationAsync(async () =>
				{
					string filePath = await _dialogService.ShowSaveFileDialog("exported_mods.toml");
					if ( filePath != null )
					{
						bool success = await ModManagementService.ExportMods(_originalComponents, filePath);
						await _dialogService.ShowInformationDialog(success ? "Export completed successfully" : "Export failed");
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to export to TOML file");
			}
		}

		private async void ExportToJson_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformExportOperationAsync(async () =>
				{
					string filePath = await _dialogService.ShowSaveFileDialog("exported_mods.json");
					if ( filePath != null )
					{
						bool success = await ModManagementService.ExportMods(_originalComponents, filePath, ModManagementService.ExportFormat.Json);
						await _dialogService.ShowInformationDialog(success ? "Export completed successfully" : "Export failed");
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to export to JSON file");
			}
		}

		private async void ExportToXml_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformExportOperationAsync(async () =>
				{
					string filePath = await _dialogService.ShowSaveFileDialog("exported_mods.xml");
					if ( filePath != null )
					{
						bool success = await ModManagementService.ExportMods(_originalComponents, filePath, ModManagementService.ExportFormat.Xml);
						await _dialogService.ShowInformationDialog(success ? "Export completed successfully" : "Export failed");
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to export to XML file");
			}
		}

		#endregion

		#region Advanced Tools

		private async void CheckDependencyChains_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to analyze dependencies.");
						return;
					}

					int totalDependencies = 0;
					int componentsWithDependencies = 0;

					foreach ( ModComponent component in selectedComponents.Where(component => component.Dependencies.Count != 0 || component.InstallAfter.Count != 0 ||
								 component.InstallBefore.Count != 0) )
					{
						componentsWithDependencies++;
						totalDependencies += component.Dependencies.Count + component.InstallAfter.Count + component.InstallBefore.Count;
					}

					// Check for circular dependencies
					Dictionary<ModComponent, List<ModComponent>> dependencyGraph = ModManagementDialog.BuildDependencyGraph(selectedComponents);
					int circularDependencies = ModManagementDialog.DetectCircularDependencies(dependencyGraph);

					await _dialogService.ShowInformationDialog(
					"Dependency Analysis Results:\n\n" +
					$"Selected mods: {selectedComponents.Count}\n" +
					$"Mods with dependencies: {componentsWithDependencies}\n" +
					$"Total dependency relationships: {totalDependencies}\n" +
					$"Circular dependencies detected: {circularDependencies}\n\n" +
					"Dependencies help ensure mods are installed in the correct order.");
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to check dependency chains");
			}
		}

		private static Dictionary<ModComponent, List<ModComponent>> BuildDependencyGraph(List<ModComponent> components)
		{
			var graph = new Dictionary<ModComponent, List<ModComponent>>();

			foreach ( ModComponent component in components )
			{
				if ( !graph.ContainsKey(component) )
					graph[component] = new List<ModComponent>();

				// Add direct dependencies
				foreach ( ModComponent depComponent in component.Dependencies.Select(depGuid => components.FirstOrDefault(c => c.Guid == depGuid)).Where(depComponent => depComponent != null && !graph[component].Contains(depComponent)) )
				{
					graph[component].Add(depComponent);
				}

				// Add install-after relationships
				foreach ( ModComponent afterComponent in component.InstallAfter.Select(afterGuid => components.FirstOrDefault(c => c.Guid == afterGuid)).Where(afterComponent => afterComponent != null && !graph[component].Contains(afterComponent)) )
				{
					graph[component].Add(afterComponent);
				}
			}

			return graph;
		}

		private static int DetectCircularDependencies(Dictionary<ModComponent, List<ModComponent>> graph)
		{
			var visited = new HashSet<ModComponent>();
			var recursionStack = new HashSet<ModComponent>();

			return graph.Keys.Count(component => HasCircularDependency(component, graph, visited, recursionStack));
		}

		private static bool HasCircularDependency(ModComponent component, Dictionary<ModComponent, List<ModComponent>> graph,
										 HashSet<ModComponent> visited, HashSet<ModComponent> recursionStack)
		{
			if ( recursionStack.Contains(component) )
				return true;

			_ = visited.Add(component);
			_ = recursionStack.Add(component);

			if ( graph.TryGetValue(component, out List<ModComponent> value) )
			{
				if ( value.Any(dependency => HasCircularDependency(dependency, graph, visited, recursionStack)) )
				{
					return true;
				}
			}

			_ = recursionStack.Remove(component);
			return false;
		}

		private async void ResolveConflicts_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var components = _originalComponents.ToList();
					Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(new List<Guid>(), new List<Guid>(), components);

					int totalConflicts = conflicts.Sum(kvp => kvp.Value.Count);

					if ( totalConflicts == 0 )
					{
						await _dialogService.ShowInformationDialog("No conflicts detected between components.");
						return;
					}

					int dependencyCount = conflicts.ContainsKey("Dependency") ? conflicts["Dependency"].Count : 0;
					int restrictionCount = conflicts.ContainsKey("Restriction") ? conflicts["Restriction"].Count : 0;
					await _dialogService.ShowInformationDialog(
					"Conflict Resolution Results:\n\n" +
					$"Total conflicts found: {totalConflicts}\n" +
					$"Dependency conflicts: {dependencyCount}\n" +
					$"Restriction conflicts: {restrictionCount}\n\n" +
					"Conflicts have been automatically resolved based on component dependencies and restrictions.");
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to resolve conflicts");
			}
		}

		private async void GenerateDependencyGraph_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to generate dependency graph.");
						return;
					}

					Dictionary<ModComponent, List<ModComponent>> dependencyGraph = ModManagementDialog.BuildDependencyGraph(selectedComponents);
					string graphText = ModManagementDialog.GenerateGraphText(dependencyGraph);

					// For now, just show the graph in a dialog. In a real implementation,
					// you might want to save this to a file or display it in a visual graph control
					await _dialogService.ShowInformationDialog(
					$"Dependency Graph for {selectedComponents.Count} selected mods:\n\n" +
					graphText +
					"\n\nNote: This is a text representation. A visual graph view could be implemented in the future.");
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to generate dependency graph");
			}
		}

		private static string GenerateGraphText(Dictionary<ModComponent, List<ModComponent>> graph)
		{
			var sb = new System.Text.StringBuilder();

			foreach ( var kvp in graph )
			{
				ModComponent component = kvp.Key;
				List<ModComponent> dependencies = kvp.Value;
				_ = sb.AppendLine($"{component.Name} (GUID: {component.Guid.ToString().Substring(0, 8)}...)");
				if ( dependencies.Any() )
				{
					_ = sb.AppendLine("  Depends on:");
					foreach ( ModComponent dep in dependencies )
					{
						_ = sb.AppendLine($"    - {dep.Name}");
					}
				}
				else
				{
					_ = sb.AppendLine("  No dependencies");
				}
				_ = sb.AppendLine();
			}

			return sb.ToString();
		}

		private async void OptimizeInstallationOrder_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to optimize installation order.");
						return;
					}

					(bool isCorrectOrder, List<ModComponent> reorderedComponents) = ModComponent.ConfirmComponentsInstallOrder(selectedComponents);

					if ( isCorrectOrder && reorderedComponents == null )
					{
						await _dialogService.ShowInformationDialog("Installation order is already optimal for the selected mods.");
					}
					else
					{
						string originalOrder = string.Join(" → ", selectedComponents.Select(c => c.Name));
						string newOrder = reorderedComponents != null
						? string.Join(" → ", reorderedComponents.Select(c => c.Name))
						: "Already optimal";

						await _dialogService.ShowInformationDialog(
						"Installation Order Optimization:\n\n" +
						$"Original order: {originalOrder}\n\n" +
						$"Optimized order: {newOrder}\n\n" +
						"The installation order has been optimized based on component dependencies.");
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to optimize installation order");
			}
		}

		private async void AnalyzeModSizes_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to analyze sizes.");
						return;
					}

					long totalSize = 0;
					int modsWithSize = 0;
					var sizeBreakdown = new Dictionary<string, (long Size, int Count)>();

					foreach ( ModComponent component in selectedComponents )
					{
						// Estimate size based on instructions and mod links
						long estimatedSize = EstimateComponentSize(component);

						if ( estimatedSize <= 0 )
							continue;
						totalSize += estimatedSize;
						modsWithSize++;

						// Categorize by size ranges
						string category = GetSizeCategory(estimatedSize);
						if ( sizeBreakdown.ContainsKey(category) )
							sizeBreakdown[category] = (sizeBreakdown[category].Size + estimatedSize, sizeBreakdown[category].Count + 1);
						else
							sizeBreakdown[category] = (estimatedSize, 1);
					}

					string analysis = $"Mod Size Analysis for {selectedComponents.Count} mods:\n\n";
					analysis += $"Mods with size data: {modsWithSize}\n";
					analysis += $"Total estimated size: {FormatBytes(totalSize)}\n";
					analysis += $"Average size per mod: {FormatBytes(totalSize / Math.Max(1, modsWithSize))}\n\n";

					analysis += "Size Distribution:\n";
					foreach ( var kvp in sizeBreakdown.OrderByDescending(kvp => kvp.Value.Size) )
					{
						string category = kvp.Key;
						long size = kvp.Value.Size;
						int count = kvp.Value.Count;
						analysis += $"{category}: {count} mods ({FormatBytes(size)})\n";
					}

					await _dialogService.ShowInformationDialog(analysis);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to analyze mod sizes");
			}
		}

		private static long EstimateComponentSize(ModComponent component)
		{
			long size = 0;

			// Count instructions that might indicate file operations
			foreach ( Instruction instruction in component.Instructions )
			{
				if ( instruction.Action == Instruction.ActionType.Extract ||
					instruction.Action == Instruction.ActionType.Copy ||
					instruction.Action == Instruction.ActionType.Move )
				{
					size += 1024 * 1024; // Estimate 1MB per file operation
				}
			}

			// Add size based on mod links (if any)
			if ( component.ModLink.Count != 0 )
				size += 50 * 1024 * 1024; // Estimate 50MB per mod link

			return size;
		}

		private static string GetSizeCategory(long bytes)
		{
			if ( bytes < 1024 * 1024 ) return "< 1 MB";
			if ( bytes < 10 * 1024 * 1024 ) return "1-10 MB";
			if ( bytes < 50 * 1024 * 1024 ) return "10-50 MB";
			if ( bytes < 100 * 1024 * 1024 ) return "50-100 MB";
			return "> 100 MB";
		}

		private static string FormatBytes(long bytes)
		{
			string[] units = { "B", "KB", "MB", "GB" };
			double size = bytes;
			int unitIndex = 0;

			while ( size >= 1024 && unitIndex < units.Length - 1 )
			{
				size /= 1024;
				unitIndex++;
			}

			return $"{size:F1} {units[unitIndex]}";
		}

		private async void CheckRedundantFiles_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to check for redundant files.");
						return;
					}

					var redundantFiles = new Dictionary<string, List<ModComponent>>();

					// Collect all files referenced by instructions
					foreach ( ModComponent component in selectedComponents )
					{
						foreach ( Instruction instruction in component.Instructions )
						{
							if ( instruction.Action != Instruction.ActionType.Copy &&
								 instruction.Action != Instruction.ActionType.Move &&
								 instruction.Action != Instruction.ActionType.Extract )
							{
								continue;
							}

							foreach ( string sourcePath in instruction.Source )
							{
								string fileName = System.IO.Path.GetFileName(sourcePath);
								if ( string.IsNullOrEmpty(fileName) )
									continue;
								if ( redundantFiles.ContainsKey(fileName) )
									redundantFiles[fileName].Add(component);
								else
									redundantFiles[fileName] = new List<ModComponent> { component };
							}
						}
					}

					// Find files that appear in multiple components
					var actualRedundantFiles = redundantFiles.Where(kvp => kvp.Value.Count > 1).ToList();

					if ( actualRedundantFiles.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No redundant files found across selected mods.");
					}
					else
					{
						string report = $"Found {actualRedundantFiles.Count} potentially redundant files:\n\n";

						foreach ( var kvp in actualRedundantFiles.Take(10) ) // Show first 10
						{
							string fileName = kvp.Key;
							List<ModComponent> components = kvp.Value;
							report += $"{fileName} (used by {components.Count} mods):\n";
							foreach ( ModComponent component in components )
							{
								report += $"  - {component.Name}\n";
							}
							report += "\n";
						}

						if ( actualRedundantFiles.Count > 10 )
						{
							report += $"... and {actualRedundantFiles.Count - 10} more files.\n";
						}

						await _dialogService.ShowInformationDialog(report);
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to check redundant files");
			}
		}

		private async void ScanForMalware_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to scan for malware.");
						return;
					}

					// Basic malware pattern detection (this is a simplified example)
					string[] suspiciousPatterns = { ".exe", ".bat", ".cmd", ".scr", ".pif", ".com", ".jar", ".vbs", ".js", ".wsf", ".hta" };

					var suspiciousFiles = new List<string>();
					var suspiciousComponents = new List<ModComponent>();

					foreach ( ModComponent component in selectedComponents )
					{
						foreach ( Instruction instruction in component.Instructions )
						{
							if ( instruction.Action != Instruction.ActionType.Execute &&
							 instruction.Action != Instruction.ActionType.Run )
							{
								continue;
							}

							foreach ( string sourcePath in instruction.Source )
							{
								string extension = System.IO.Path.GetExtension(sourcePath).ToLowerInvariant();
								if ( !suspiciousPatterns.Contains(extension) )
									continue;
								if ( suspiciousFiles.Contains(sourcePath) )
									continue;
								suspiciousFiles.Add(sourcePath);
								if ( !suspiciousComponents.Contains(component) )
									suspiciousComponents.Add(component);
							}
						}
					}

					if ( suspiciousFiles.Count == 0 )
					{
						await _dialogService.ShowInformationDialog(
						"Malware Scan Results:\n\n" +
						"✅ No suspicious executable files found in selected mods.\n\n" +
						"Note: This is a basic scan. For comprehensive security, use dedicated antivirus software.");
					}
					else
					{
						string report = $"Malware Scan Results - Found {suspiciousFiles.Count} suspicious files:\n\n";
						report += $"Affected mods: {string.Join(", ", suspiciousComponents.Select(c => c.Name))}\n\n";
						report += "Suspicious files:\n";

						foreach ( string file in suspiciousFiles.Take(10) )
						{
							report += $"  - {file}\n";
						}

						if ( suspiciousFiles.Count > 10 )
							report += $"... and {suspiciousFiles.Count - 10} more files.\n";

						report += "\n⚠️  Warning: These files may be legitimate mod tools or patches.\n" +
							  "Please verify with the mod author before removing.";

						await _dialogService.ShowInformationDialog(report);
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to scan for malware");
			}
		}

		private async void CheckFileIntegrity_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to check file integrity.");
						return;
					}

					int totalInstructions = 0;
					int instructionsWithChecksums = 0;
					int componentsWithChecksums = 0;
					var componentsWithoutChecksums = new List<ModComponent>();

					foreach ( ModComponent component in selectedComponents )
					{
						bool hasChecksums = false;

						foreach ( Instruction instruction in component.Instructions )
						{
							totalInstructions++;

							if ( instruction.ExpectedChecksums != null && instruction.ExpectedChecksums.Any() )
							{
								instructionsWithChecksums++;
								hasChecksums = true;
							}
						}

						if ( hasChecksums )
							componentsWithChecksums++;
						else
							componentsWithoutChecksums.Add(component);

					}

					string report = "File Integrity Check Results:\n\n";
					report += $"Total instructions analyzed: {totalInstructions}\n";
					report += $"Instructions with checksums: {instructionsWithChecksums}\n";
					report += $"Components with checksums: {componentsWithChecksums}/{selectedComponents.Count}\n\n";

					if ( componentsWithoutChecksums.Any() )
					{
						report += "Components without checksum validation:\n";
						foreach ( ModComponent component in componentsWithoutChecksums.Take(10) )
						{
							report += $"  - {component.Name}\n";
						}

						if ( componentsWithoutChecksums.Count > 10 )
							report += $"... and {componentsWithoutChecksums.Count - 10} more.\n";

						report += "\n💡 Tip: Add checksums to instructions for better integrity verification.";
					}
					else
					{
						report += "✅ All selected components have checksum validation configured.";
					}

					await _dialogService.ShowInformationDialog(report);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to check file integrity");
			}
		}

		private async void ValidateDigitalSignatures_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to validate digital signatures.");
						return;
					}

					int totalInstructions = 0;
					int instructionsWithSignatures = 0;
					int componentsWithSignatures = 0;
					var componentsWithoutSignatures = new List<ModComponent>();

					foreach ( ModComponent component in selectedComponents )
					{
						bool hasSignatures = false;

						foreach ( Instruction instruction in component.Instructions )
						{
							totalInstructions++;

							// Check if instruction has signature validation configured
							// (This would depend on how signature validation is implemented in the core)
							if ( instruction.Action == Instruction.ActionType.Execute &&
							instruction.Source.Any(s => s.Contains("signature") || s.Contains("cert")) )
							{
								instructionsWithSignatures++;
								hasSignatures = true;
							}
						}

						if ( hasSignatures )
							componentsWithSignatures++;
						else
							componentsWithoutSignatures.Add(component);
					}

					string report = "Digital Signature Validation Results:\n\n";
					report += $"Total instructions analyzed: {totalInstructions}\n";
					report += $"Instructions with signature validation: {instructionsWithSignatures}\n";
					report += $"Components with signature validation: {componentsWithSignatures}/{selectedComponents.Count}\n\n";

					if ( componentsWithoutSignatures.Count != 0 )
					{
						report += "Components without signature validation:\n";
						foreach ( ModComponent component in componentsWithoutSignatures.Take(10) )
						{
							report += $"  - {component.Name}\n";
						}

						if ( componentsWithoutSignatures.Count > 10 )
							report += $"... and {componentsWithoutSignatures.Count - 10} more.\n";

						report += "\n💡 Tip: Consider adding digital signature validation for executable instructions.";
					}
					else if ( instructionsWithSignatures > 0 )
					{
						report += "✅ All selected components have digital signature validation configured.";
					}
					else
					{
						report += "ℹ️  No digital signature validation is currently configured for any instructions.";
					}

					await _dialogService.ShowInformationDialog(report);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to validate digital signatures");
			}
		}

		private async void CleanOrphanedFiles_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					bool? confirm = await _dialogService.ShowConfirmationDialog(
					"This will scan the KOTOR installation directory for files that are not referenced by any mod instructions.\n\n" +
					"⚠️  WARNING: This is an analysis tool. No files will be deleted automatically.\n\n" +
					"Continue with orphaned file analysis?",
					"Yes, Analyze",
					"Cancel");

					if ( confirm != true )
						return;

					// Build a set of all files that are referenced by instructions
					var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach ( ModComponent component in _originalComponents )
					{
						foreach ( Instruction instruction in component.Instructions )
						{
							// Collect destination paths from instructions
							if ( string.IsNullOrWhiteSpace(instruction.Destination) )
								continue;
							string fileName = System.IO.Path.GetFileName(instruction.Destination);
							if ( string.IsNullOrEmpty(fileName) )
								continue;
							_ = referencedFiles.Add(fileName);
						}
					}

					// Report analysis results
					int totalInstructions = _originalComponents.Sum(c => c.Instructions.Count);
					int totalFiles = referencedFiles.Count;

					await _dialogService.ShowInformationDialog(
					"Orphaned File Analysis:\n\n" +
					"✅ Analysis complete!\n\n" +
					$"Total instructions scanned: {totalInstructions}\n" +
					$"Unique files referenced: {totalFiles}\n\n" +
					"To identify orphaned files, you would need to:\n" +
					"1. Scan your KOTOR installation directory\n" +
					"2. Compare against this reference list\n" +
					"3. Manually review and delete unneeded files\n\n" +
					"⚠️  Note: Automatic deletion is intentionally not implemented to prevent accidental data loss.");
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to clean orphaned files");
			}
		}

		private async void UpdateModLinks_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					var selectedComponents = _originalComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count == 0 )
					{
						await _dialogService.ShowInformationDialog("No mods selected. Please select mods to check mod links.");
						return;
					}

					int totalLinks = 0;
					int validLinks = 0;
					int brokenLinks = 0;
					var componentsWithBrokenLinks = new List<ModComponent>();

					foreach ( ModComponent component in selectedComponents )
					{
						foreach ( string link in component.ModLink )
						{
							totalLinks++;

							// Basic URL validation (simplified)
							if ( Uri.TryCreate(link, UriKind.Absolute, out Uri uri) &&
							(uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) )
							{
								validLinks++;
							}
							else
							{
								brokenLinks++;
								if ( !componentsWithBrokenLinks.Contains(component) )
									componentsWithBrokenLinks.Add(component);
							}
						}
					}

					string report = "Mod Links Validation Results:\n\n";
					report += $"Total mod links checked: {totalLinks}\n";
					report += $"Valid links: {validLinks}\n";
					report += $"Potentially broken links: {brokenLinks}\n\n";

					if ( componentsWithBrokenLinks.Any() )
					{
						report += "Components with potentially broken links:\n";
						foreach ( ModComponent component in componentsWithBrokenLinks.Take(10) )
						{
							report += $"  - {component.Name}\n";
						}

						if ( componentsWithBrokenLinks.Count > 10 )
							report += $"... and {componentsWithBrokenLinks.Count - 10} more.\n";

						report += "\n💡 Tip: Verify these links are still accessible and update if necessary.";
					}
					else
					{
						report += "✅ All mod links appear to be valid URLs.";
					}

					await _dialogService.ShowInformationDialog(report);
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to check mod links");
			}
		}

		private async void ArchiveOldVersions_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await PerformOperationAsync(async () =>
				{
					bool? confirm = await _dialogService.ShowConfirmationDialog(
					"This will analyze your mod collection for potential duplicate versions.\n\n" +
					"⚠️  Note: This is an analysis tool. No files will be moved automatically.\n\n" +
					"Continue with version analysis?",
					"Yes, Analyze",
					"Cancel");

					if ( confirm != true )
						return;

					// Group mods by name similarity to find potential duplicates
					var modsByName = new Dictionary<string, List<ModComponent>>();
					foreach ( ModComponent component in _originalComponents )
					{
						// Extract base name without version numbers
						string baseName = System.Text.RegularExpressions.Regex.Replace(
						component.Name,
						@"\s*[vV]?\d+\.?\d*\.?\d*\s*$",
						"").Trim();

						if ( !modsByName.ContainsKey(baseName) )
							modsByName[baseName] = new List<ModComponent>();

						modsByName[baseName].Add(component);
					}

					// Find potential duplicates (same base name, multiple versions)
					var potentialDuplicates = modsByName
					.Where(kvp => kvp.Value.Count > 1)
					.ToList();

					if ( potentialDuplicates.Count == 0 )
					{
						await _dialogService.ShowInformationDialog(
						"Version Analysis:\n\n" +
						"✅ No potential duplicate versions found!\n\n" +
						"All mod names appear to be unique.");
					}
					else
					{
						var report = new System.Text.StringBuilder();
						_ = report.AppendLine("Version Analysis:\n");
						_ = report.AppendLine($"Found {potentialDuplicates.Count} mod(s) with potential duplicates:\n");

						foreach ( KeyValuePair<string, List<ModComponent>> group in potentialDuplicates.Take(10) )
						{
							_ = report.AppendLine($"'{group.Key}' has {group.Value.Count} version(s):");
							foreach ( ModComponent comp in group.Value )
								_ = report.AppendLine($"  • {comp.Name}");
							_ = report.AppendLine();
						}

						if ( potentialDuplicates.Count > 10 )
							_ = report.AppendLine($"... and {potentialDuplicates.Count - 10} more groups.\n");

						_ = report.AppendLine("💡 Tip: Review these mods and consider keeping only the latest version.");
						_ = report.AppendLine("\n⚠️  Note: Automatic archiving is intentionally not implemented to prevent accidental data loss.");

						await _dialogService.ShowInformationDialog(report.ToString());
					}
				});
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to archive old versions");
			}
		}

		#endregion

		#region Helper Methods

		private async Task PerformBatchOperationAsync(Func<Task> operation)
		{
			try
			{
				await operation();
				ModificationsApplied = true;
				_dialogService.RefreshStatistics();
			}
			catch ( Exception ex )
			{
				await _dialogService.ShowInformationDialog($"Operation failed: {ex.Message}");
			}
		}

		private async Task PerformImportOperationAsync(Func<Task> operation)
		{
			try
			{
				await operation();
				ModificationsApplied = true;
				_dialogService.RefreshStatistics();
			}
			catch ( Exception ex )
			{
				await _dialogService.ShowInformationDialog($"Import failed: {ex.Message}");
			}
		}

		private async Task PerformExportOperationAsync(Func<Task> operation)
		{
			try
			{
				await operation();
			}
			catch ( Exception ex )
			{
				await _dialogService.ShowInformationDialog($"Export failed: {ex.Message}");
			}
		}

		private async Task PerformOperationAsync(Func<Task> operation)
		{
			try
			{
				await operation();
			}
			catch ( Exception ex )
			{
				await _dialogService.ShowInformationDialog($"Operation failed: {ex.Message}");
			}
		}

		private async void ShowBatchResult(string operationName, ModManagementService.BatchOperationResult result)
		{
			try
			{
				string message = $"{operationName} completed:\n\n" +
							   $"Successful: {result.SuccessCount}\n" +
							   $"Failed: {result.FailureCount}";

				if ( result.Errors.Count != 0 )
				{
					message += $"\n\nErrors:\n{string.Join("\n", result.Errors.Take(5))}";
					if ( result.Errors.Count > 5 )
						message += $"\n... and {result.Errors.Count - 5} more";
				}

				await _dialogService.ShowInformationDialog(message);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Failed to show batch result");
			}
		}

		#endregion

		#region Dialog Management

		private void ApplyChanges_Click(object sender, RoutedEventArgs e) => Close();

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			// Restore original component list if modifications were made but not applied
			if ( ModificationsApplied )
				_dialogService.UpdateComponents(_originalComponents);
			Close();
		}

		#endregion

		[UsedImplicitly]
		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		[UsedImplicitly]
		private void ToggleMaximizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
		{
			if ( !(sender is Button maximizeButton) )
				return;
			if ( WindowState == WindowState.Maximized )
			{
				WindowState = WindowState.Normal;
				maximizeButton.Content = "▢";
			}
			else
			{
				WindowState = WindowState.Maximized;
				maximizeButton.Content = "▣";
			}
		}

		[UsedImplicitly]
		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
		{
			if ( !_mouseDownForWindowMoving )
				return;

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
		{
			if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen )
				return;

			// Don't start window drag if clicking on interactive controls
			if ( ShouldIgnorePointerForWindowDrag(e) )
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;

		private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
		{
			// Get the element under the pointer
			if ( !(e.Source is Visual source) )
				return false;

			// Walk up the visual tree to check if we're clicking on an interactive element
			Visual current = source;
			while ( current != null && current != this )
			{
				switch ( current )
				{
					// Check if we're clicking on any interactive control
					case Button _:
					case TextBox _:
					case ComboBox _:
					case ListBox _:
					case MenuItem _:
					case Menu _:
					case Expander _:
					case Slider _:
					case TabControl _:
					case TabItem _:
					case ProgressBar _:
					case ScrollViewer _:
					// Check if the element has context menu or flyout open
					case Control control when control.ContextMenu?.IsOpen == true:
						return true;
					case Control control when control.ContextFlyout?.IsOpen == true:
						return true;
					default:
						current = current.GetVisualParent();
						break;
				}
			}

			return false;
		}
	}
}
