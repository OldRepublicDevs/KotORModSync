// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using KOTORModSync.Core;
using KOTORModSync.Dialogs;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for component editing operations
	/// </summary>
	public class ComponentEditorService
	{
		private readonly MainConfig _mainConfig;
		private readonly Window _parentWindow;

		public ComponentEditorService(MainConfig mainConfig, Window parentWindow)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
		}

		/// <summary>
		/// Checks if there are unsaved changes in the raw editor
		/// </summary>
		public static bool HasUnsavedChanges(ModComponent currentComponent, string rawEditText)
		{
			return currentComponent != null
				   && !string.IsNullOrWhiteSpace(rawEditText)
				   && rawEditText != currentComponent.SerializeComponent();
		}

		/// <summary>
		/// Saves changes from the raw editor to a component
		/// </summary>
		public async Task<bool> SaveChangesAsync(ModComponent currentComponent, string rawEditText, bool noPrompt = false)
		{
			try
			{
				if ( !ComponentEditorService.HasUnsavedChanges(currentComponent, rawEditText) )
				{
					await Logger.LogVerboseAsync("No changes detected, nothing to save.");
					return true;
				}

				if ( !noPrompt )
				{
					var result = await ConfirmationDialog.ShowConfirmationDialogWithDiscard(
						_parentWindow,
						confirmText: "Are you sure you want to save?",
						yesButtonText: "Save",
						noButtonText: "Discard"
					);

					switch ( result )
					{
						case ConfirmationDialog.ConfirmationResult.Save:
							// User wants to save, continue with save logic
							break;
						case ConfirmationDialog.ConfirmationResult.Discard:
							// User wants to discard changes, allow save without prompting
							await Logger.LogVerboseAsync("User chose to discard changes in ComponentEditorService.");
							return true;
						case ConfirmationDialog.ConfirmationResult.Cancel:
							// User wants to cancel, don't save
							return false;
					}
				}

				// Get the selected component
				if ( currentComponent is null )
				{
					string output = "CurrentComponent is null which shouldn't ever happen in this context." +
								   Environment.NewLine +
								   "Please report this issue to a developer, this should never happen.";

					await Logger.LogErrorAsync(output);
					await InformationDialog.ShowInformationDialog(_parentWindow, output);
					return false;
				}

				if ( string.IsNullOrEmpty(rawEditText) )
					return true;

				var newComponent = ModComponent.DeserializeTomlComponent(rawEditText);
				if ( newComponent is null )
				{
					bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
						_parentWindow,
						"Could not deserialize your raw config text into a ModComponent instance in memory." +
						" There may be syntax errors, check the output window for details." +
						Environment.NewLine +
						Environment.NewLine +
						"Would you like to discard your changes and continue with your last attempted action?"
					);

					return confirmResult == true;
				}

				// Find the corresponding component in the collection
				int index = _mainConfig.allComponents.IndexOf(currentComponent);
				if ( index == -1 )
				{
					string componentName = string.IsNullOrWhiteSpace(newComponent.Name)
						? "."
						: $" '{newComponent.Name}'.";
					string output = $"Could not find the index of component{componentName}" +
								   " Ensure you single-clicked on a component on the left before pressing save." +
								   " Please back up your work and try again.";
					await Logger.LogErrorAsync(output);
					await InformationDialog.ShowInformationDialog(_parentWindow, output);

					return false;
				}

				// Update the properties of the existing component
				ModComponent existingComponent = _mainConfig.allComponents[index];
				CopyComponentProperties(newComponent, existingComponent);

				await Logger.LogAsync($"Saved '{newComponent.Name}' successfully. Refer to the output window for more information.");
				return true;
			}
			catch ( Exception ex )
			{
				string output = "An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialog(_parentWindow, output + Environment.NewLine + ex.Message);
				return false;
			}
		}

		/// <summary>
		/// Loads a component into the raw editor
		/// </summary>
		public async Task<(bool Success, string SerializedContent)> LoadIntoRawEditorAsync(ModComponent component, string currentRawEditText)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));

			await Logger.LogVerboseAsync($"Loading '{component.Name}' into the raw editor...");

			if ( ComponentEditorService.HasUnsavedChanges(component, currentRawEditText) )
			{
				bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialog(
					_parentWindow,
					"You're attempting to load the component into the raw editor, but" +
					" there may be unsaved changes still in the editor. Really continue?"
				);

				if ( confirmResult != true )
					return (false, null);
			}

			// Return the serialized component
			string serialized = component.SerializeComponent();
			return (true, serialized);
		}

		/// <summary>
		/// Creates a new component with default values
		/// </summary>
		public ModComponent CreateNewComponent()
		{
			var newComponent = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "new mod_" + System.IO.Path.GetFileNameWithoutExtension(System.IO.Path.GetRandomFileName()),
			};

			_mainConfig.allComponents.Add(newComponent);
			Logger.Log($"Created new component: {newComponent.Name}");

			return newComponent;
		}

		/// <summary>
		/// Removes a component from the configuration
		/// </summary>
		public async Task<bool> RemoveComponentAsync(ModComponent component)
		{
			try
			{
				if ( component is null )
				{
					Logger.Log("No component provided for removal.");
					return false;
				}

				// Check for dependent components
				var dependentComponents = new System.Collections.Generic.List<ModComponent>();
				foreach (var c in _mainConfig.allComponents)
				{
					if (c.Dependencies.Contains(component.Guid) ||
						c.Restrictions.Contains(component.Guid) ||
						c.InstallBefore.Contains(component.Guid) ||
						c.InstallAfter.Contains(component.Guid))
					{
						dependentComponents.Add(c);
					}
				}

				if ( dependentComponents.Count != 0 )
				{
					// Log the dependent components
					Logger.Log($"Cannot remove '{component.Name}' - {dependentComponents.Count} components depend on it:");
					foreach ( ModComponent dependent in dependentComponents )
					{
						var dependencyTypes = new System.Collections.Generic.List<string>();
						if ( dependent.Dependencies.Contains(component.Guid) )
							dependencyTypes.Add("Dependency");
						if ( dependent.Restrictions.Contains(component.Guid) )
							dependencyTypes.Add("Restriction");
						if ( dependent.InstallBefore.Contains(component.Guid) )
							dependencyTypes.Add("InstallBefore");
						if ( dependent.InstallAfter.Contains(component.Guid) )
							dependencyTypes.Add("InstallAfter");

						Logger.Log($"  - {dependent.Name} ({string.Join(", ", dependencyTypes)})");
					}

					// Show dependency unlinking dialog
					(bool confirmed, System.Collections.Generic.List<ModComponent> componentsToUnlink) = await DependencyUnlinkDialog.ShowUnlinkDialog(
						_parentWindow, component, dependentComponents);

					if ( !confirmed )
						return false;

					// Unlink the dependencies
					foreach ( ModComponent componentToUnlink in componentsToUnlink )
					{
						_ = componentToUnlink.Dependencies.Remove(component.Guid);
						_ = componentToUnlink.Restrictions.Remove(component.Guid);
						_ = componentToUnlink.InstallBefore.Remove(component.Guid);
						_ = componentToUnlink.InstallAfter.Remove(component.Guid);

						Logger.Log($"Unlinked dependencies from '{componentToUnlink.Name}'");
					}
				}

				// Remove the component
				_ = _mainConfig.allComponents.Remove(component);
				Logger.Log($"Removed component: {component.Name}");
				return true;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}

		#region Private Helper Methods

		private static void CopyComponentProperties(ModComponent source, ModComponent destination)
		{
			destination.Name = source.Name;
			destination.Author = source.Author;
			destination.Category = new System.Collections.Generic.List<string>(source.Category);
			destination.Tier = source.Tier;
			destination.Description = source.Description;
			destination.Directions = source.Directions;
			destination.InstallationMethod = source.InstallationMethod;
			destination.ModLink = source.ModLink;
			destination.Language = source.Language;
			destination.Dependencies = source.Dependencies;
			destination.Restrictions = source.Restrictions;
			destination.InstallAfter = source.InstallAfter;
			destination.Options = source.Options;
			destination.Instructions = source.Instructions;
		}

		#endregion
	}
}

