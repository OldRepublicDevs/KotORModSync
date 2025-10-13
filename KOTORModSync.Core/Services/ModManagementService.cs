// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services
{

	public class ModManagementService
	{
		public event EventHandler<ModOperationEventArgs> ModOperationCompleted;
		public event EventHandler<ModValidationEventArgs> ModValidationCompleted;

		private readonly MainConfig _mainConfig;

		public ModManagementService(MainConfig mainConfig) => _mainConfig = mainConfig
															  				?? throw new ArgumentNullException(nameof(mainConfig));

		#region CRUD Operations



		public ModComponent CreateMod(string name = null, string author = null, string category = null)
		{
			var newComponent = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = name ?? $"New Mod {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
				Author = author ?? "Unknown Author",
				Category = new List<string> { category ?? "Uncategorized" },
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

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.Create,
				ModComponent = newComponent,
				Success = true
			});

			Logger.LogVerbose($"Created new mod: {newComponent.Name}");
			return newComponent;
		}


		public ModComponent DuplicateMod(ModComponent sourceComponent, string newName = null)
		{
			if ( sourceComponent == null ) throw new ArgumentNullException(nameof(sourceComponent));

			var duplicatedComponent = new ModComponent
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
				ModComponent = duplicatedComponent,
				SourceComponent = sourceComponent,
				Success = true
			});

			Logger.LogVerbose($"Duplicated mod: {sourceComponent.Name} -> {duplicatedComponent.Name}");
			return duplicatedComponent;
		}


		public bool UpdateMod(ModComponent component, Action<ModComponent> updates)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));
			if ( updates == null ) throw new ArgumentNullException(nameof(updates));

			try
			{
				ModComponent originalComponent = CloneComponent(component);
				updates(component);

				ModValidationResult validationResult = ValidateMod(component);
				if ( !validationResult.IsValid )
				{
					Logger.LogWarning($"ModComponent update validation failed: {string.Join(", ", validationResult.Errors)}");

					return false;
				}

				ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
				{
					Operation = ModOperation.Update,
					ModComponent = component,
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


		public bool DeleteMod(ModComponent component, bool force = false)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));

			var dependentComponents = _mainConfig.allComponents
				.Where(c => c.Dependencies.Contains(component.Guid) || c.Restrictions.Contains(component.Guid))
				.ToList();

			if ( !force && dependentComponents.Any() )
			{
				Logger.LogWarning($"Cannot delete mod '{component.Name}' - it has {dependentComponents.Count} dependent components");
				return false;
			}

			bool removed = _mainConfig.allComponents.Remove(component);

			if ( !removed )
				return false;
			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.Delete,
				ModComponent = component,
				Success = true
			});

			Logger.LogVerbose($"Deleted mod: {component.Name}");

			return true;
		}

		#endregion

		#region Reordering Operations


		public bool MoveModToPosition(ModComponent component, int targetIndex)
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
				ModComponent = component,
				FromIndex = currentIndex,
				ToIndex = targetIndex,
				Success = true
			});

			Logger.LogVerbose($"Moved mod '{component.Name}' from position {currentIndex + 1} to {targetIndex + 1}");
			return true;
		}


		public bool MoveModRelative(ModComponent component, int relativeIndex)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));

			int currentIndex = _mainConfig.allComponents.IndexOf(component);
			if ( currentIndex == -1 ) return false;

			int targetIndex = currentIndex + relativeIndex;
			return MoveModToPosition(component, targetIndex);
		}

		#endregion

		#region Validation and Error Checking



		public ModValidationResult ValidateMod(ModComponent component)
		{
			if ( component == null ) throw new ArgumentNullException(nameof(component));

			var result = new ModValidationResult();

			if ( string.IsNullOrWhiteSpace(component.Name) )
				result.Errors.Add("ModComponent name is required");

			if ( string.IsNullOrWhiteSpace(component.Author) )
				result.Warnings.Add("ModComponent author is not specified");

			foreach ( Guid dependency in component.Dependencies.Where(dependency => _mainConfig.allComponents.All(c => c.Guid != dependency)) )
			{
				result.Errors.Add($"Dependency {dependency} not found in component list");
			}

			foreach ( Guid restriction in component.Restrictions.Where(restriction => _mainConfig.allComponents.All(c => c.Guid != restriction)) )
			{
				result.Errors.Add($"Restriction {restriction} not found in component list");
			}

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

			foreach ( Option option in component.Options )
			{
				if ( string.IsNullOrWhiteSpace(option.Name) )
					result.Errors.Add($"Option in '{component.Name}' has no name");
			}

			foreach ( Instruction instruction in component.Instructions )
			{
				if ( instruction.Action == Instruction.ActionType.Unset )
					result.Errors.Add($"Instruction in '{component.Name}' has no action");
			}

			ModValidationCompleted?.Invoke(this, new ModValidationEventArgs
			{
				ModComponent = component,
				ValidationResult = result
			});

			return result;
		}


		public Dictionary<ModComponent, ModValidationResult> ValidateAllMods()
		{
			var results = new Dictionary<ModComponent, ModValidationResult>();

			foreach ( ModComponent component in _mainConfig.allComponents )
			{
				results[component] = ValidateMod(component);
			}

			return results;
		}

		#endregion

		#region Dependency and Restriction Management


		public bool AddDependency(ModComponent component, ModComponent dependencyComponent)
		{
			if ( component == null || dependencyComponent == null ) return false;

			if ( component.Dependencies.Contains(dependencyComponent.Guid) )
				return false;

			component.Dependencies.Add(dependencyComponent.Guid);

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.AddDependency,
				ModComponent = component,
				RelatedComponent = dependencyComponent,
				Success = true
			});

			Logger.LogVerbose($"Added dependency: {component.Name} -> {dependencyComponent.Name}");
			return true;
		}
		public bool RemoveDependency(ModComponent component, ModComponent dependencyComponent)
		{
			if ( component == null || dependencyComponent == null ) return false;

			bool removed = component.Dependencies.Remove(dependencyComponent.Guid);

			if ( removed )
			{
				ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
				{
					Operation = ModOperation.RemoveDependency,
					ModComponent = component,
					RelatedComponent = dependencyComponent,
					Success = true
				});

				Logger.LogVerbose($"Removed dependency: {component.Name} -> {dependencyComponent.Name}");
			}

			return removed;
		}


		public bool AddRestriction(ModComponent component, ModComponent restrictionComponent)
		{
			if ( component == null || restrictionComponent == null ) return false;

			if ( component.Restrictions.Contains(restrictionComponent.Guid) )
				return false;

			component.Restrictions.Add(restrictionComponent.Guid);

			ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
			{
				Operation = ModOperation.AddRestriction,
				ModComponent = component,
				RelatedComponent = restrictionComponent,
				Success = true
			});

			Logger.LogVerbose($"Added restriction: {component.Name} conflicts with {restrictionComponent.Name}");
			return true;
		}


		public bool RemoveRestriction(ModComponent component, ModComponent restrictionComponent)
		{
			if ( component == null || restrictionComponent == null ) return false;

			bool removed = component.Restrictions.Remove(restrictionComponent.Guid);

			if ( removed )
			{
				ModOperationCompleted?.Invoke(this, new ModOperationEventArgs
				{
					Operation = ModOperation.RemoveRestriction,
					ModComponent = component,
					RelatedComponent = restrictionComponent,
					Success = true
				});

				Logger.LogVerbose($"Removed restriction: {component.Name} no longer conflicts with {restrictionComponent.Name}");
			}

			return removed;
		}

		#endregion

		#region Search and Filtering


		public List<ModComponent> SearchMods(string searchText, ModSearchOptions searchOptions = null)
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

				if ( searchOptions.SearchInCategory && component.Category != null &&
					 component.Category.Any(cat => cat.IndexOf(lowerSearch, StringComparison.OrdinalIgnoreCase) >= 0) )
					return true;

				if ( searchOptions.SearchInDescription && component.Description.IndexOf(lowerSearch, StringComparison.OrdinalIgnoreCase) >= 0 )
					return true;

				return false;
			}).ToList();
		}



		public void SortMods(ModSortCriteria sortBy = ModSortCriteria.Name, SortOrder sortOrder = SortOrder.Ascending)
		{
			Comparison<ModComponent> comparison;

			switch ( sortBy )
			{
				case ModSortCriteria.Name:
					comparison = (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
					break;
				case ModSortCriteria.Author:
					comparison = (a, b) => string.Compare(a.Author, b.Author, StringComparison.OrdinalIgnoreCase);
					break;
				case ModSortCriteria.Category:
					comparison = (a, b) =>
					{
						string aCategory = a.Category.Count > 0 ? string.Join(", ", a.Category) : string.Empty;
						string bCategory = b.Category.Count > 0 ? string.Join(", ", b.Category) : string.Empty;
						return string.Compare(aCategory, bCategory, StringComparison.OrdinalIgnoreCase);
					};
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
				Comparison<ModComponent> originalComparison = comparison;
				comparison = (a, b) => -originalComparison(a, b);
			}

			var sortedComponents = _mainConfig.allComponents.ToList();
			sortedComponents.Sort(comparison);
			_mainConfig.allComponents.Clear();
			_mainConfig.allComponents.AddRange(sortedComponents);

			Logger.LogVerbose($"Sorted {sortedComponents.Count} components by {sortBy} ({sortOrder})");
		}

		#endregion

		#region Batch Operations



		public async Task<BatchOperationResult> PerformBatchOperation(IEnumerable<ModComponent> components, BatchModOperation operation, Dictionary<string, object> parameters = null)
		{
			IEnumerable<ModComponent> enumerable = components as ModComponent[] ?? components.ToArray();
			var result = new BatchOperationResult
			{
				Operation = operation,
				TotalComponents = enumerable.Count(),
				SuccessCount = 0,
				FailureCount = 0,
				Errors = new List<string>()
			};

			foreach ( ModComponent component in enumerable )
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

								component.Category = string.IsNullOrEmpty(category)
									? new List<string>()
									: new List<string> { category };
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



		public static async Task<bool> ExportMods(IEnumerable<ModComponent> components, string filePath, ExportFormat format = ExportFormat.Toml)
		{
			try
			{
				IEnumerable<ModComponent> enumerable = components as ModComponent[] ?? components.ToArray();
				switch ( format )
				{
					case ExportFormat.Toml:
						using ( var writer = new StreamWriter(filePath) )
						{
							foreach ( ModComponent component in enumerable )
							{
								string tomlContent = component.SerializeComponent();
								await writer.WriteLineAsync(tomlContent);
							}
						}
						break;

					case ExportFormat.Json:

						break;

					case ExportFormat.Xml:

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


		public async Task<List<ModComponent>> ImportMods(string filePath, ImportMergeStrategy mergeStrategy = ImportMergeStrategy.ByGuid)
		{
			try
			{
				List<ModComponent> importedComponents = ModComponent.ReadComponentsFromFile(filePath);

				if ( importedComponents.Count == 0 )
					return importedComponents;

				switch ( mergeStrategy )
				{
					case ImportMergeStrategy.Replace:
						_mainConfig.allComponents.Clear();
						_mainConfig.allComponents.AddRange(importedComponents);
						break;

					case ImportMergeStrategy.Merge:

						break;

					case ImportMergeStrategy.ByGuid:

						MergeByGuid(importedComponents);
						break;

					case ImportMergeStrategy.ByNameAndAuthor:

						MergeByNameAndAuthor(importedComponents);
						break;
				}

				await Logger.LogVerboseAsync($"Imported {importedComponents.Count} components from {filePath}");
				return importedComponents;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return new List<ModComponent>();
			}
		}

		#endregion

		#region Statistics and Analytics


		public ModStatistics GetModStatistics()
		{
			var stats = new ModStatistics
			{
				TotalMods = _mainConfig.allComponents.Count,
				SelectedMods = _mainConfig.allComponents.Count(c => c.IsSelected),
				DownloadedMods = _mainConfig.allComponents.Count(c => c.IsDownloaded),
				Categories = _mainConfig.allComponents
					.Where(c => c.Category.Count > 0)
					.SelectMany(c => c.Category)
					.GroupBy(cat => cat)
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

		private void MergeByGuid(List<ModComponent> importedComponents)
		{
			foreach ( ModComponent imported in importedComponents )
			{
				ModComponent existing = _mainConfig.allComponents.FirstOrDefault(c => c.Guid == imported.Guid);
				if ( existing != null )
				{

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

		private void MergeByNameAndAuthor(List<ModComponent> importedComponents)
		{

			var matchedPairs = new List<(ModComponent existing, ModComponent incoming)>();
			var matchedExisting = new HashSet<ModComponent>();
			var matchedIncoming = new HashSet<ModComponent>();

			foreach ( ModComponent imported in importedComponents )
			{

				ModComponent bestMatch = null;
				double bestScore = 0.0;

				foreach ( ModComponent existing in _mainConfig.allComponents )
				{

					if ( matchedExisting.Contains(existing) )
						continue;


					string existingNameNorm = existing.Name.ToLowerInvariant().Trim();
					string importedNameNorm = imported.Name.ToLowerInvariant().Trim();
					string existingAuthorNorm = existing.Author.ToLowerInvariant().Trim();
					string importedAuthorNorm = imported.Author.ToLowerInvariant().Trim();

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

					if ( authorsMatch && (existingNameNorm.Contains(importedNameNorm) || importedNameNorm.Contains(existingNameNorm)) )
					{
						int minLen = Math.Min(existingNameNorm.Length, importedNameNorm.Length);
						int maxLen = Math.Max(existingNameNorm.Length, importedNameNorm.Length);
						double score = (double)minLen / maxLen;

						if ( score > bestScore && score >= 0.7 )
						{
							bestMatch = existing;
							bestScore = score;
						}
					}
				}

				if ( bestMatch != null && bestScore >= 0.7 )
				{
					matchedPairs.Add((bestMatch, imported));
					matchedExisting.Add(bestMatch);
					matchedIncoming.Add(imported);
				}
			}

			foreach ( (ModComponent existing, ModComponent imported) in matchedPairs )
			{

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

			foreach ( ModComponent imported in importedComponents )
			{
				if ( !matchedIncoming.Contains(imported) )
				{
					_mainConfig.allComponents.Add(imported);
					Logger.LogVerbose($"Added new component from import: {imported.Name} (GUID: {imported.Guid})");
				}
			}
		}

		private static bool SetModDownloaded(ModComponent component, bool downloaded)
		{
			component.IsDownloaded = downloaded;
			return true;
		}

		private static bool SetModSelected(ModComponent component, bool selected)
		{
			component.IsSelected = selected;
			return true;
		}

		private static bool UpdateModMetadata(ModComponent component, Dictionary<string, object> parameters)
		{
			if ( parameters.TryGetValue("Name", out object name) )
				component.Name = name.ToString();

			if ( parameters.TryGetValue("Author", out object author) )
				component.Author = author.ToString();

			if ( parameters.TryGetValue("Category", out object category) )
			{
				if ( category is List<string> categoryList )
					component.Category = new List<string>(categoryList);
				else if ( category is IEnumerable<string> categoryEnum )
					component.Category = categoryEnum.ToList();
				else if ( category != null )
					component.Category = new List<string> { category.ToString() };
			}

			if ( parameters.TryGetValue("Tier", out object tier) )
				component.Tier = tier.ToString();

			if ( parameters.TryGetValue("Description", out object description) )
				component.Description = description.ToString();

			return true;
		}

		private ModComponent CloneComponent(ModComponent source) => new ModComponent
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

		#region Event Args and Enums

		public class ModOperationEventArgs : EventArgs
		{
			public ModOperation Operation { get; set; }
			public ModComponent ModComponent { get; set; }
			public ModComponent SourceComponent { get; set; }
			public ModComponent RelatedComponent { get; set; }
			public ModComponent OriginalComponent { get; set; }
			public int? FromIndex { get; set; }
			public int? ToIndex { get; set; }
			public BatchOperationResult BatchResult { get; set; }
			public bool Success { get; set; }
		}

		public class ModValidationEventArgs : EventArgs
		{
			public ModComponent ModComponent { get; set; }
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
