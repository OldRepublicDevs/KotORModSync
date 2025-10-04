// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Comprehensive service for managing mod operations including CRUD, validation,
	/// dependency management, and advanced mod functionality.
	/// </summary>
	public class ModManagementService
	{
		public event EventHandler<ModOperationEventArgs> ModOperationCompleted;
		public event EventHandler<ModValidationEventArgs> ModValidationCompleted;

		private readonly MainConfig _mainConfig;

		public ModManagementService(MainConfig mainConfig) => _mainConfig = mainConfig
															  				?? throw new ArgumentNullException(nameof(mainConfig));

		#region CRUD Operations

		/// <summary>
		/// Creates a new mod component with default values.
		/// </summary>
		/// <param name="name">Name of the new mod.</param>
		/// <param name="author">Author of the new mod.</param>
		/// <param name="category">Category of the new mod.</param>
		/// <returns>The newly created component.</returns>
		public Component CreateMod(string name = null, string author = null, string category = null)
		{
			var newComponent = new Component
			{
				Guid = Guid.NewGuid(),
				Name = name ?? $"New Mod {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
				Author = author ?? "Unknown Author",
				Category = category ?? "Uncategorized",
				Tier = "Optional",
				Description = "A new mod component.",
				IsSelected = false,
				IsDownloaded = false,
				ModLink = new List<string>(),
				Dependencies = new List<Guid>(),
				Restrictions = new List<Guid>(),
				InstallAfter = new List<Guid>(),
				InstallBefore = new List<Guid>(),
				Options = new ObservableCollection<Option>(),
				Instructions = new ObservableCollection<Instruction>()
			};

			_mainConfig.allComponents.Add(newComponent);

			// Raise event for UI updates
			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.Create,
				Component = newComponent,
				Success = true
			});

			Logger.LogVerbose($"Created new mod: {newComponent.Name}");
			return newComponent;
		}

		/// <summary>
		/// Duplicates an existing mod component.
		/// </summary>
		/// <param name="sourceComponent">Component to duplicate.</param>
		/// <param name="newName">Optional new name for the duplicated component.</param>
		/// <returns>The duplicated component.</returns>
		public Component DuplicateMod(Component sourceComponent, string newName = null)
		{
			if ( sourceComponent == null ) throw new ArgumentNullException(nameof(sourceComponent));

			var duplicatedComponent = new Component
			{
				Guid = Guid.NewGuid(),
				Name = newName ?? $"{sourceComponent.Name} (Copy)",
				Author = sourceComponent.Author,
				Category = sourceComponent.Category,
				Tier = sourceComponent.Tier,
				Description = sourceComponent.Description,
				Directions = sourceComponent.Directions,
				InstallationMethod = sourceComponent.InstallationMethod,
				ModLink = new List<string>(sourceComponent.ModLink),
				Dependencies = new List<Guid>(sourceComponent.Dependencies),
				Restrictions = new List<Guid>(sourceComponent.Restrictions),
				InstallAfter = new List<Guid>(sourceComponent.InstallAfter),
				InstallBefore = new List<Guid>(sourceComponent.InstallBefore),
				Options = new ObservableCollection<Option>(sourceComponent.Options.Select(CloneOption).ToList()),
				Instructions = new ObservableCollection<Instruction>(sourceComponent.Instructions.Select(CloneInstruction).ToList()),
				IsSelected = false,
				IsDownloaded = false
			};

			_mainConfig.allComponents.Add(duplicatedComponent);

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.Duplicate,
				Component = duplicatedComponent,
				SourceComponent = sourceComponent,
				Success = true
			});

			Logger.LogVerbose($"Duplicated mod: {sourceComponent.Name} -> {duplicatedComponent.Name}");
			return duplicatedComponent;
		}

		/// <summary>
		/// Updates an existing mod component.
		/// </summary>
		/// <param name="component">Component to update.</param>
		/// <param name="updates">Action to perform updates on the component.</param>
		/// <returns>True if update was successful.</returns>
		public bool UpdateMod(Component component, Action<Component> updates)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));
			if ( updates == null ) throw new ArgumentNullException(nameof(updates));

			try
			{
				Component originalComponent = CloneComponent(component);
				updates(component);

				// Validate the component after updates
				ModValidationResult validationResult = ValidateMod(component);
				if ( !validationResult.IsValid )
				{
					Logger.LogWarning($"Component update validation failed: {string.Join(", ", validationResult.Errors)}");
					// Optionally revert changes here
					return false;
				}

				ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
				{
					Operation = ModOperation.Update,
					Component = component,
					OriginalComponent = originalComponent,
					Success = true
				});

				Logger.LogVerbose($"Updated mod: {component.Name}");
				return true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return false;
			}
		}

		/// <summary>
		/// Deletes a mod component.
		/// </summary>
		/// <param name="component">Component to delete.</param>
		/// <param name="force">Force deletion even if component has dependencies.</param>
		/// <returns>True if deletion was successful.</returns>
		public bool DeleteMod(Component component, bool force = false)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));

			// Check for dependencies
			var dependentComponents = _mainConfig.allComponents
				.Where(c => c.Dependencies.Contains(component.Guid) || c.Restrictions.Contains(component.Guid))
				.ToList();

			if ( !force && dependentComponents.Any() )
			{
				Logger.LogWarning($"Cannot delete mod '{component.Name}' - it has {dependentComponents.Count} dependent components");
				return false;
			}

			// Remove from collection
			bool removed = _mainConfig.allComponents.Remove(component);

			if ( !removed )
			    return false;
			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.Delete,
				Component = component,
				Success = true
			});

			Logger.LogVerbose($"Deleted mod: {component.Name}");

			return true;
		}

		#endregion

		#region Reordering Operations

		/// <summary>
		/// Moves a component to a specific position in the list.
		/// </summary>
		/// <param name="component">Component to move.</param>
		/// <param name="targetIndex">Target index position.</param>
		/// <returns>True if move was successful.</returns>
		public bool MoveModToPosition(Component component, int targetIndex)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));

			int currentIndex = _mainConfig.allComponents.IndexOf(component);
			if ( currentIndex == -1 || targetIndex < 0 || targetIndex >= _mainConfig.allComponents.Count )
				return false;

			if ( currentIndex == targetIndex )
				return true;

			_mainConfig.allComponents.RemoveAt(currentIndex);
			_mainConfig.allComponents.Insert(targetIndex, component);

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.Move,
				Component = component,
				FromIndex = currentIndex,
				ToIndex = targetIndex,
				Success = true
			});

			Logger.LogVerbose($"Moved mod '{component.Name}' from position {currentIndex + 1} to {targetIndex + 1}");
			return true;
		}

		/// <summary>
		/// Moves a component up or down in the list.
		/// </summary>
		/// <param name="component">Component to move.</param>
		/// <param name="relativeIndex">Relative position change (positive = down, negative = up).</param>
		/// <returns>True if move was successful.</returns>
		public bool MoveModRelative(Component component, int relativeIndex)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));

			int currentIndex = _mainConfig.allComponents.IndexOf(component);
			if ( currentIndex == -1 ) return false;

			int targetIndex = currentIndex + relativeIndex;
			return MoveModToPosition(component, targetIndex);
		}

		#endregion

		#region Validation and Error Checking

		/// <summary>
		/// Validates a single mod component.
		/// </summary>
		/// <param name="component">Component to validate.</param>
		/// <returns>Validation result with errors and warnings.</returns>
		public ModValidationResult ValidateMod(Component component)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));

			var result = new ModValidationResult();

			// Basic validation
			if ( string.IsNullOrWhiteSpace(component.Name) )
				result.Errors.Add("Component name is required");

			if ( string.IsNullOrWhiteSpace(component.Author) )
				result.Warnings.Add("Component author is not specified");

			// Dependency validation
			foreach ( Guid dependency in component.Dependencies.Where(dependency => _mainConfig.allComponents.All(c => c.Guid != dependency)))
			{
				result.Errors.Add($"Dependency {dependency} not found in component list");
			}

			// Restriction validation
			foreach ( Guid restriction in component.Restrictions.Where(restriction => _mainConfig.allComponents.All(c => c.Guid != restriction)))
			{
				result.Errors.Add($"Restriction {restriction} not found in component list");
			}

			// File validation (if selected and mod directory is set)
			if ( component.IsSelected && _mainConfig.sourcePath != null )
			{
				foreach ( Instruction instruction in component.Instructions )
				{
					foreach ( string source in instruction.Source )
					{
						string fileName = Path.GetFileName(source);
						string fullPath = Path.Combine(_mainConfig.sourcePath.FullName, fileName);
						if ( File.Exists(fullPath) || Directory.Exists(fullPath) )
						    continue;
						result.Errors.Add($"Required file not found: {fileName}");
						component.IsDownloaded = false;
					}
				}
			}

			// Option validation
			foreach ( Option option in component.Options )
			{
				if ( string.IsNullOrWhiteSpace(option.Name) )
					result.Errors.Add($"Option in '{component.Name}' has no name");
			}

			// Instruction validation
			foreach ( Instruction instruction in component.Instructions )
			{
				if ( instruction.Action == Instruction.ActionType.Unset )
					result.Errors.Add($"Instruction in '{component.Name}' has no action");
			}

			ModValidationCompleted?.Invoke(this, new ModValidationEventArgs
			{
				Component = component,
				ValidationResult = result
			});

			return result;
		}

		/// <summary>
		/// Validates all components in the configuration.
		/// </summary>
		/// <returns>Collection of validation results for all components.</returns>
		public Dictionary<Component, ModValidationResult> ValidateAllMods()
		{
			var results = new Dictionary<Component, ModValidationResult>();

			foreach ( Component component in _mainConfig.allComponents )
			{
				results[component] = ValidateMod(component);
			}

			return results;
		}

		#endregion

		#region Dependency and Restriction Management

		/// <summary>
		/// Adds a dependency to a component.
		/// </summary>
		/// <param name="component">Component to add dependency to.</param>
		/// <param name="dependencyComponent">Component to depend on.</param>
		/// <returns>True if dependency was added successfully.</returns>
		public bool AddDependency(Component component, Component dependencyComponent)
		{
			if ( component == null || dependencyComponent == null ) return false;

			if ( component.Dependencies.Contains(dependencyComponent.Guid) )
				return false; // Already a dependency

			component.Dependencies.Add(dependencyComponent.Guid);

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.AddDependency,
				Component = component,
				RelatedComponent = dependencyComponent,
				Success = true
			});

			Logger.LogVerbose($"Added dependency: {component.Name} -> {dependencyComponent.Name}");
			return true;
		}
		public bool RemoveDependency(Component component, Component dependencyComponent)
		{
			if ( component == null || dependencyComponent == null ) return false;

			bool removed = component.Dependencies.Remove(dependencyComponent.Guid);

			if ( removed )
			{
				ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
				{
					Operation = ModOperation.RemoveDependency,
					Component = component,
					RelatedComponent = dependencyComponent,
					Success = true
				});

				Logger.LogVerbose($"Removed dependency: {component.Name} -> {dependencyComponent.Name}");
			}

			return removed;
		}

		/// <summary>
		/// Adds a restriction to a component.
		/// </summary>
		/// <param name="component">Component to add restriction to.</param>
		/// <param name="restrictionComponent">Component to restrict against.</param>
		/// <returns>True if restriction was added successfully.</returns>
		public bool AddRestriction(Component component, Component restrictionComponent)
		{
			if ( component == null || restrictionComponent == null ) return false;

			if ( component.Restrictions.Contains(restrictionComponent.Guid) )
				return false; // Already a restriction

			component.Restrictions.Add(restrictionComponent.Guid);

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.AddRestriction,
				Component = component,
				RelatedComponent = restrictionComponent,
				Success = true
			});

			Logger.LogVerbose($"Added restriction: {component.Name} conflicts with {restrictionComponent.Name}");
			return true;
		}

		/// <summary>
		/// Removes a restriction from a component.
		/// </summary>
		/// <param name="component">Component to remove restriction from.</param>
		/// <param name="restrictionComponent">Component to remove restriction against.</param>
		/// <returns>True if restriction was removed successfully.</returns>
		public bool RemoveRestriction(Component component, Component restrictionComponent)
		{
			if ( component == null || restrictionComponent == null ) return false;

			bool removed = component.Restrictions.Remove(restrictionComponent.Guid);

			if ( removed )
			{
				ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
				{
					Operation = ModOperation.RemoveRestriction,
					Component = component,
					RelatedComponent = restrictionComponent,
					Success = true
				});

				Logger.LogVerbose($"Removed restriction: {component.Name} no longer conflicts with {restrictionComponent.Name}");
			}

			return removed;
		}

		#endregion

		#region Search and Filtering

		/// <summary>
		/// Searches components by name, author, or category.
		/// </summary>
		/// <param name="searchText">Text to search for.</param>
		/// <param name="searchOptions">Search options.</param>
		/// <returns>Filtered list of components.</returns>
		public List<Component> SearchMods(string searchText, ModSearchOptions searchOptions = null)
		{
			if ( string.IsNullOrWhiteSpace(searchText) )
				return _mainConfig.allComponents.ToList();

			if ( searchOptions == null )
				searchOptions = new ModSearchOptions();

			string lowerSearch = searchText.ToLowerInvariant();

			return _mainConfig.allComponents.Where(component =>
			{
				if ( searchOptions.SearchInName && component.Name.IndexOf(lowerSearch, StringComparison.OrdinalIgnoreCase) >= 0 )
					return true;

				if ( searchOptions.SearchInAuthor && component.Author.IndexOf(lowerSearch, StringComparison.OrdinalIgnoreCase) >= 0 )
					return true;

				if ( searchOptions.SearchInCategory && component.Category.IndexOf(lowerSearch, StringComparison.OrdinalIgnoreCase) >= 0 )
					return true;

				if ( searchOptions.SearchInDescription && component.Description.IndexOf(lowerSearch, StringComparison.OrdinalIgnoreCase) >= 0 )
					return true;

				return false;
			}).ToList();
		}

		/// <summary>
		/// Sorts components by specified criteria.
		/// </summary>
		/// <param name="sortBy">Sort criteria.</param>
		/// <param name="sortOrder">Sort order.</param>
		public void SortMods(ModSortCriteria sortBy = ModSortCriteria.Name, SortOrder sortOrder = SortOrder.Ascending)
		{
			Comparison<Component> comparison;

			switch ( sortBy )
			{
				case ModSortCriteria.Name:
					comparison = (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
					break;
				case ModSortCriteria.Author:
					comparison = (a, b) => string.Compare(a.Author, b.Author, StringComparison.OrdinalIgnoreCase);
					break;
				case ModSortCriteria.Category:
					comparison = (a, b) => string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
					break;
				case ModSortCriteria.Tier:
					comparison = (a, b) =>
					{
						var tierOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
						{
							{ "Recommended", 1 },
							{ "Suggested", 2 },
							{ "Optional", 3 },
							{ "", 4 }
						};

						int aOrder = tierOrder.TryGetValue(a.Tier, out int aVal) ? aVal : 4;
						int bOrder = tierOrder.TryGetValue(b.Tier, out int bVal) ? bVal : 4;

						int result = aOrder.CompareTo(bOrder);
						if ( result == 0 )
							result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
						return result;
					};
					break;
				case ModSortCriteria.InstallationOrder:
					comparison = (a, b) => _mainConfig.allComponents.IndexOf(a).CompareTo(_mainConfig.allComponents.IndexOf(b));
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(sortBy), sortBy, null);
			}

			if ( sortOrder == SortOrder.Descending )
			{
				Comparison<Component> originalComparison = comparison;
				comparison = (a, b) => -originalComparison(a, b);
			}

			// Create a copy, sort it, and replace the original
			var sortedComponents = _mainConfig.allComponents.ToList();
			sortedComponents.Sort(comparison);
			_mainConfig.allComponents.Clear();
			_mainConfig.allComponents.AddRange(sortedComponents);

			Logger.LogVerbose($"Sorted {sortedComponents.Count} components by {sortBy} ({sortOrder})");
		}

		#endregion

		#region Batch Operations

		/// <summary>
		/// Performs batch operations on multiple components.
		/// </summary>
		/// <param name="components">Components to operate on.</param>
		/// <param name="operation">Operation to perform.</param>
		/// <param name="parameters">Operation parameters.</param>
		/// <returns>Results of the batch operation.</returns>
		public async Task<BatchOperationResult> PerformBatchOperation(IEnumerable<Component> components, BatchModOperation operation, Dictionary<string, object> parameters = null)
		{
			IEnumerable<Component> enumerable = components as Component[] ?? components.ToArray();
			var result = new BatchOperationResult
			{
				Operation = operation,
				TotalComponents = enumerable.Count(),
				SuccessCount = 0,
				FailureCount = 0,
				Errors = new List<string>()
			};

			foreach ( Component component in enumerable )
			{
				try
				{
					bool success;
					switch ( operation )
					{
						case BatchModOperation.Validate:
							success = ValidateMod(component).IsValid;
							break;
						case BatchModOperation.SetDownloaded:
							{
								bool downloaded = true;
								if ( parameters != null && parameters.TryGetValue("downloaded", out object val) )
								{
									if ( val is bool v )
										downloaded = v;
								}
								success = SetModDownloaded(component, downloaded);
								break;
							}
						case BatchModOperation.SetSelected:
							{
								bool selected = true;
								if ( parameters != null && parameters.TryGetValue("selected", out object val) )
								{
									if ( val is bool v )
										selected = v;
								}
								success = SetModSelected(component, selected);
								break;
							}
						case BatchModOperation.UpdateMetadata:
							success = UpdateModMetadata(component, parameters);
							break;
						case BatchModOperation.UpdateCategory:
							{
								string category = string.Empty;
								if ( parameters != null && parameters.TryGetValue("category", out object val) )
								{
									if ( val is string v )
										category = v;
								}
								component.Category = category;
								success = true;
								break;
							}
						default:
							success = false;
							break;
					}

					if ( success )
						result.SuccessCount++;
					else
						result.FailureCount++;
				}
				catch ( Exception ex )
				{
					result.FailureCount++;
					result.Errors.Add($"Failed to process {component.Name}: {ex.Message}");
					await Logger.LogExceptionAsync(ex);
				}
			}

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.Batch,
				BatchResult = result,
				Success = result.SuccessCount > 0
			});

			return result;
		}

		#endregion

		#region Import/Export

		/// <summary>
		/// Exports components to a file.
		/// </summary>
		/// <param name="components">Components to export.</param>
		/// <param name="filePath">Path to export to.</param>
		/// <param name="format">Export format.</param>
		/// <returns>True if export was successful.</returns>
		public async Task<bool> ExportMods(IEnumerable<Component> components, string filePath, ExportFormat format = ExportFormat.Toml)
		{
			try
			{
				IEnumerable<Component> enumerable = components as Component[] ?? components.ToArray();
				switch ( format )
				{
					case ExportFormat.Toml:
						using ( var writer = new StreamWriter(filePath) )
						{
							foreach ( Component component in enumerable )
							{
								string tomlContent = component.SerializeComponent();
								await writer.WriteLineAsync(tomlContent);
							}
						}
						break;

					case ExportFormat.Json:
						// JSON export implementation
						break;

					case ExportFormat.Xml:
						// XML export implementation
						break;
				}

				await Logger.LogVerboseAsync($"Exported {enumerable.Count()} components to {filePath}");
				return true;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}

		/// <summary>
		/// Imports components from a file.
		/// </summary>
		/// <param name="filePath">Path to import from.</param>
		/// <param name="mergeStrategy">Strategy for merging with existing components.</param>
		/// <returns>Imported components.</returns>
		public async Task<List<Component>> ImportMods(string filePath, ImportMergeStrategy mergeStrategy = ImportMergeStrategy.ByGuid)
		{
			try
			{
				List<Component> importedComponents = Component.ReadComponentsFromFile(filePath);

				if ( importedComponents.Count == 0 )
					return importedComponents;

				// Apply merge strategy
				switch ( mergeStrategy )
				{
					case ImportMergeStrategy.Replace:
						_mainConfig.allComponents.Clear();
						_mainConfig.allComponents.AddRange(importedComponents);
						break;

					case ImportMergeStrategy.Merge:
						// Merge with existing components (implementation depends on strategy)
						break;

					case ImportMergeStrategy.ByGuid:
						// Merge by GUID matching
						MergeByGuid(importedComponents);
						break;

					case ImportMergeStrategy.ByNameAndAuthor:
						// Merge by name and author fuzzy matching
						MergeByNameAndAuthor(importedComponents);
						break;
				}

				await Logger.LogVerboseAsync($"Imported {importedComponents.Count} components from {filePath}");
				return importedComponents;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return new List<Component>();
			}
		}

		#endregion

		#region Statistics and Analytics

		/// <summary>
		/// Gets comprehensive statistics about the mod collection.
		/// </summary>
		/// <returns>Mod collection statistics.</returns>
		public ModStatistics GetModStatistics()
		{
			var stats = new ModStatistics
			{
				TotalMods = _mainConfig.allComponents.Count,
				SelectedMods = _mainConfig.allComponents.Count(c => c.IsSelected),
				DownloadedMods = _mainConfig.allComponents.Count(c => c.IsDownloaded),
				Categories = _mainConfig.allComponents
					.Where(c => !string.IsNullOrEmpty(c.Category))
					.GroupBy(c => c.Category)
					.ToDictionary(g => g.Key, g => g.Count()),
				Tiers = _mainConfig.allComponents
					.Where(c => !string.IsNullOrEmpty(c.Tier))
					.GroupBy(c => c.Tier)
					.ToDictionary(g => g.Key, g => g.Count()),
				Authors = _mainConfig.allComponents
					.Where(c => !string.IsNullOrEmpty(c.Author))
					.GroupBy(c => c.Author)
					.ToDictionary(g => g.Key, g => g.Count()),
				AverageInstructionsPerMod = _mainConfig.allComponents.Any()
					? _mainConfig.allComponents.Average(c => c.Instructions.Count)
					: 0,
				AverageOptionsPerMod = _mainConfig.allComponents.Any()
					? _mainConfig.allComponents.Average(c => c.Options.Count)
					: 0
			};

			return stats;
		}

		#endregion

		#region Helper Methods

		private void MergeByGuid(List<Component> importedComponents)
		{
			foreach ( Component imported in importedComponents )
			{
				Component existing = _mainConfig.allComponents.FirstOrDefault(c => c.Guid == imported.Guid);
				if ( existing != null )
				{
					// Update existing component with imported data
					existing.Name = imported.Name;
					existing.Author = imported.Author;
					existing.Category = imported.Category;
					existing.Tier = imported.Tier;
					existing.Description = imported.Description;
					existing.Directions = imported.Directions;
					existing.InstallationMethod = imported.InstallationMethod;
					existing.ModLink = imported.ModLink;
					existing.Dependencies = imported.Dependencies;
					existing.Restrictions = imported.Restrictions;
					existing.InstallAfter = imported.InstallAfter;
					existing.InstallBefore = imported.InstallBefore;
					existing.Options = imported.Options;
					existing.Instructions = imported.Instructions;
				}
				else
				{
					_mainConfig.allComponents.Add(imported);
				}
			}
		}

		private void MergeByNameAndAuthor(List<Component> importedComponents)
		{
			// Build a list of matches using fuzzy matching (similar to ComponentMergeConflictViewModel)
			var matchedPairs = new List<(Component existing, Component incoming)>();
			var matchedExisting = new HashSet<Component>();
			var matchedIncoming = new HashSet<Component>();

			// Find fuzzy matches by name and author
			foreach ( Component imported in importedComponents )
			{
				// Try to find a fuzzy match in existing components
				Component bestMatch = null;
				double bestScore = 0.0;

				foreach ( Component existing in _mainConfig.allComponents )
				{
					// Skip if already matched
					if ( matchedExisting.Contains(existing) )
						continue;

					// Use FuzzyMatcher to check if components match
					// Note: FuzzyMatcher is in KOTORModSync.GUI namespace, so this would need to be refactored
					// For now, we'll do a simple name/author comparison
					// TODO: Extract FuzzyMatcher logic to Core or reference it properly

					// Simple fuzzy matching logic (placeholder until FuzzyMatcher is accessible)
					string existingNameNorm = existing.Name.ToLowerInvariant().Trim();
					string importedNameNorm = imported.Name.ToLowerInvariant().Trim();
					string existingAuthorNorm = existing.Author.ToLowerInvariant().Trim();
					string importedAuthorNorm = imported.Author.ToLowerInvariant().Trim();

					// Check if names and authors match (case-insensitive)
					bool namesMatch = existingNameNorm == importedNameNorm;
					bool authorsMatch = existingAuthorNorm == importedAuthorNorm ||
									   string.IsNullOrWhiteSpace(existingAuthorNorm) ||
									   string.IsNullOrWhiteSpace(importedAuthorNorm);

					if ( namesMatch && authorsMatch )
					{
						bestMatch = existing;
						bestScore = 1.0;
						break;
					}

					// Simple containment check for partial matches
					if ( authorsMatch && (existingNameNorm.Contains(importedNameNorm) || importedNameNorm.Contains(existingNameNorm)) )
					{
						int minLen = Math.Min(existingNameNorm.Length, importedNameNorm.Length);
						int maxLen = Math.Max(existingNameNorm.Length, importedNameNorm.Length);
						double score = (double)minLen / maxLen;

						if ( score > bestScore && score >= 0.7 ) // 70% similarity threshold
						{
							bestMatch = existing;
							bestScore = score;
						}
					}
				}

				// If we found a good match, record it
				if ( bestMatch != null && bestScore >= 0.7 )
				{
					matchedPairs.Add((bestMatch, imported));
					matchedExisting.Add(bestMatch);
					matchedIncoming.Add(imported);
				}
			}

			// Merge matched pairs (update existing with imported data)
			foreach ( (Component existing, Component imported) in matchedPairs )
			{
				// Update existing component with imported data (similar to MergeByGuid)
				existing.Name = imported.Name;
				existing.Author = imported.Author;
				existing.Category = imported.Category;
				existing.Tier = imported.Tier;
				existing.Description = imported.Description;
				existing.Directions = imported.Directions;
				existing.InstallationMethod = imported.InstallationMethod;
				existing.ModLink = imported.ModLink;
				existing.Dependencies = imported.Dependencies;
				existing.Restrictions = imported.Restrictions;
				existing.InstallAfter = imported.InstallAfter;
				existing.InstallBefore = imported.InstallBefore;
				existing.Options = imported.Options;
				existing.Instructions = imported.Instructions;

				Logger.LogVerbose($"Merged component by name/author: {existing.Name} (GUID: {existing.Guid})");
			}

			// Add unmatched imported components as new components
			foreach ( Component imported in importedComponents )
			{
				if ( !matchedIncoming.Contains(imported) )
				{
					_mainConfig.allComponents.Add(imported);
					Logger.LogVerbose($"Added new component from import: {imported.Name} (GUID: {imported.Guid})");
				}
			}
		}

		private static bool SetModDownloaded(Component component, bool downloaded)
		{
			component.IsDownloaded = downloaded;
			return true;
		}

		private static bool SetModSelected(Component component, bool selected)
		{
			component.IsSelected = selected;
			return true;
		}

		private static bool UpdateModMetadata(Component component, Dictionary<string, object> parameters)
		{
			if ( parameters.TryGetValue("Name", out object name) )
				component.Name = name.ToString();

			if ( parameters.TryGetValue("Author", out object author) )
				component.Author = author.ToString();

			if ( parameters.TryGetValue("Category", out object category) )
				component.Category = category.ToString();

			if ( parameters.TryGetValue("Tier", out object tier) )
				component.Tier = tier.ToString();

			if ( parameters.TryGetValue("Description", out object description) )
				component.Description = description.ToString();

			return true;
		}

		private Component CloneComponent(Component source) => new Component
		{
			Guid = source.Guid,
			Name = source.Name,
			Author = source.Author,
			Category = source.Category,
			Tier = source.Tier,
			Description = source.Description,
			Directions = source.Directions,
			InstallationMethod = source.InstallationMethod,
			ModLink = new List<string>(source.ModLink),
			Dependencies = new List<Guid>(source.Dependencies),
			Restrictions = new List<Guid>(source.Restrictions),
			InstallAfter = new List<Guid>(source.InstallAfter),
			InstallBefore = new List<Guid>(source.InstallBefore),
			Options = new ObservableCollection<Option>(source.Options.Select(CloneOption).ToList()),
			Instructions = new ObservableCollection<Instruction>(source.Instructions.Select(CloneInstruction).ToList()),
			IsSelected = source.IsSelected,
			IsDownloaded = source.IsDownloaded
		};

		private Option CloneOption(Option source) => new Option
		{
			Guid = source.Guid,
			Name = source.Name,
			Description = source.Description,
			Directions = source.Directions,
			Dependencies = new List<Guid>(source.Dependencies),
			Restrictions = new List<Guid>(source.Restrictions),
			InstallAfter = new List<Guid>(source.InstallAfter),
			InstallBefore = new List<Guid>(source.InstallBefore),
			Instructions = new ObservableCollection<Instruction>(source.Instructions.Select(CloneInstruction).ToList()),
			IsSelected = source.IsSelected
		};

		private static Instruction CloneInstruction(Instruction source) => new Instruction
		{
			Action = source.Action,
			Source = new List<string>(source.Source),
			Destination = source.Destination,
			Arguments = source.Arguments,
			Overwrite = source.Overwrite,
			Dependencies = new List<Guid>(source.Dependencies),
			Restrictions = new List<Guid>(source.Restrictions)
		};

		#endregion

		// Nested types for UI consumption
		#region Event Args and Enums

		public class ModOperationEventArgs : EventArgs
		{
			public ModOperation Operation { get; set; }
			public Component Component { get; set; }
			public Component SourceComponent { get; set; }
			public Component RelatedComponent { get; set; }
			public Component OriginalComponent { get; set; }
			public int? FromIndex { get; set; }
			public int? ToIndex { get; set; }
			public BatchOperationResult BatchResult { get; set; }
			public bool Success { get; set; }
		}

		public class ModValidationEventArgs : EventArgs
		{
			public Component Component { get; set; }
			public ModValidationResult ValidationResult { get; set; }
		}

		public enum ModOperation
		{
			Create,
			Read,
			Update,
			Delete,
			Move,
			Duplicate,
			AddDependency,
			RemoveDependency,
			AddRestriction,
			RemoveRestriction,
			Batch
		}

		public enum BatchModOperation
		{
			Validate,
			SetDownloaded,
			SetSelected,
			UpdateMetadata,
			UpdateCategory
		}

		public enum ModSortCriteria
		{
			Name,
			Author,
			Category,
			Tier,
			InstallationOrder
		}

		public enum SortOrder
		{
			Ascending,
			Descending
		}

		public enum ExportFormat
		{
			Toml,
			Json,
			Xml
		}

		// Renamed to avoid conflict with ComponentMergeService.MergeStrategy
		public enum ImportMergeStrategy
		{
			Replace,
			Merge,
			ByGuid,
			ByNameAndAuthor
		}

		public class ModSearchOptions
		{
			public bool SearchInName { get; set; } = true;
			public bool SearchInAuthor { get; set; } = true;
			public bool SearchInCategory { get; set; } = true;
			public bool SearchInDescription { get; set; } = false;
		}

		public class ModValidationResult
		{
			public bool IsValid => Errors.Count == 0;
			public List<string> Errors { get; set; } = new List<string>();
			public List<string> Warnings { get; set; } = new List<string>();
		}

		public class ModStatistics
		{
			public int TotalMods { get; set; }
			public int SelectedMods { get; set; }
			public int DownloadedMods { get; set; }
			public Dictionary<string, int> Categories { get; set; } = new Dictionary<string, int>();
			public Dictionary<string, int> Tiers { get; set; } = new Dictionary<string, int>();
			public Dictionary<string, int> Authors { get; set; } = new Dictionary<string, int>();
			public double AverageInstructionsPerMod { get; set; }
			public double AverageOptionsPerMod { get; set; }
		}

		public class BatchOperationResult
		{
			public BatchModOperation Operation { get; set; }
			public int TotalComponents { get; set; }
			public int SuccessCount { get; set; }
			public int FailureCount { get; set; }
			public List<string> Errors { get; set; } = new List<string>();
		}

		#endregion
	}
}
