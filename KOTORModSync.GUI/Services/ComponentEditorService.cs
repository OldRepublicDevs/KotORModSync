// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia.Controls;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Dialogs;

namespace KOTORModSync.Services
{

    public class ComponentEditorService
    {
        private readonly MainConfig _mainConfig;
        private readonly Window _parentWindow;

        public ComponentEditorService(MainConfig mainConfig, Window parentWindow)
        {
            _mainConfig = mainConfig
                          ?? throw new ArgumentNullException(nameof(mainConfig));
            _parentWindow = parentWindow
                            ?? throw new ArgumentNullException(nameof(parentWindow));
        }

        public static bool HasUnsavedChanges(
            ModComponent currentComponent,
            string rawEditText,
            string currentFormat = "toml"
        )
        {
            if (currentComponent is null || string.IsNullOrWhiteSpace(rawEditText))
            {
                return false;
            }

            // Serialize the component in the same format as the textbox
            var components = new List<ModComponent> { currentComponent };
            string serializedInCurrentFormat = ModComponentSerializationService.SerializeModComponentAsString(components, currentFormat);

            // Compare the raw text with the serialized version in the same format
            bool hasChanges = !string.Equals(rawEditText, serializedInCurrentFormat, StringComparison.Ordinal);

            if (hasChanges)
            {
                Logger.LogVerbose($"[HasUnsavedChanges] Changes detected for component '{currentComponent.Name}' in format '{currentFormat}'");
                Logger.LogVerbose($"[HasUnsavedChanges] Raw text length: {rawEditText.Length}, Serialized length: {serializedInCurrentFormat.Length}");
            }
            else
            {
                Logger.LogVerbose($"[HasUnsavedChanges] No changes detected for component '{currentComponent.Name}' in format '{currentFormat}'");
            }

            return hasChanges;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public async Task<bool> SaveChangesAsync(ModComponent currentComponent, string rawEditText, string currentFormat = "toml", bool noPrompt = false)
        {
            try
            {
                if (!HasUnsavedChanges(currentComponent, rawEditText, currentFormat))
                {
                    await Logger.LogVerboseAsync("No changes detected, nothing to save.").ConfigureAwait(false);
                    return true;
                }

                if (!noPrompt)
                {
                    bool? result = await ConfirmationDialog.ShowConfirmationDialogAsync(
                        _parentWindow,
                        confirmText: "Are you sure you want to save?",
                        yesButtonText: "Save",
                        noButtonText: "Discard"
                    ).ConfigureAwait(true);

                    switch (result)
                    {
                        case true:
                            break;
                        case false:
                            await Logger.LogVerboseAsync("User chose to discard changes in ComponentEditorService.").ConfigureAwait(false);
                            return true;
                        case null:

                            return false;
                    }
                }

                if (currentComponent is null)
                {
                    string output = "CurrentComponent is null which shouldn't ever happen in this context." +
                                   Environment.NewLine +
                                   "Please report this issue to a developer, this should never happen.";
                    await Logger.LogErrorAsync(output).ConfigureAwait(false);
                    await InformationDialog.ShowInformationDialogAsync(
                        _parentWindow,
                        output).ConfigureAwait(true);
                    return false;
                }

                if (string.IsNullOrEmpty(rawEditText))
                {
                    return true;
                }

                ModComponent newComponent = null;
                try
                {
                    await Logger.LogVerboseAsync("Attempting YAML deserialization...").ConfigureAwait(false);
                    newComponent = ModComponentSerializationService.DeserializeYamlComponent(rawEditText);
                }
                catch (Exception ex)
                {
                    await Logger.LogVerboseAsync($"YAML deserialization failed: {ex.Message}").ConfigureAwait(false);
                }

                if (newComponent is null)
                {
                    try
                    {
                        await Logger.LogVerboseAsync("Attempting TOML deserialization...").ConfigureAwait(false);
                        newComponent = ModComponent.DeserializeTomlComponent(rawEditText);
                    }
                    catch (Exception ex)
                    {
                        await Logger.LogVerboseAsync($"TOML deserialization failed: {ex.Message}").ConfigureAwait(false);
                    }
                }

                if (newComponent is null)
                {
                    bool? confirmResult = await ConfirmationDialog.ShowConfirmationDialogAsync(
                        _parentWindow,
                        confirmText: "Could not deserialize your raw config text into a ModComponent instance in memory." +
                        " There may be syntax errors, check the output window for details." +
                        Environment.NewLine +
                        Environment.NewLine +
                        "Would you like to discard your changes and continue with your last attempted action?",
                        yesButtonText: "Discard",
                        noButtonText: "Continue",
                        yesButtonTooltip: "Discard your changes and continue with your last attempted action.",
                        noButtonTooltip: "Continue with your last attempted action.",
                        closeButtonTooltip: "Cancel"
                    ).ConfigureAwait(true);

                    return confirmResult == true;
                }

                int index = _mainConfig.allComponents.IndexOf(currentComponent);
                if (index == -1)
                {
                    string componentName = string.IsNullOrWhiteSpace(newComponent.Name)
                        ? "."
                        : $" '{newComponent.Name}'.";
                    string output = $"Could not find the index of component{componentName}" +
                                   " Ensure you single-clicked on a component on the left before pressing save." +
                                   " Please back up your work and try again.";


                    await Logger.LogErrorAsync(output).ConfigureAwait(false);
                    await InformationDialog.ShowInformationDialogAsync(
                        _parentWindow,
                        output).ConfigureAwait(true);

                    return false;
                }

                ModComponent existingComponent = _mainConfig.allComponents[index];
                CopyComponentProperties(newComponent, existingComponent);



                await Logger.LogAsync($"Saved '{newComponent.Name}' successfully. Refer to the output window for more information.").ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                string output = "An unexpected exception was thrown. Please refer to the output window for details and report this issue to a developer.";


                await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
                await InformationDialog.ShowInformationDialogAsync(
                    _parentWindow,


                    $"{output} {Environment.NewLine} {Environment.NewLine} Error saving component changes: {ex.Message}").ConfigureAwait(true);
                return false;
            }
        }

        public static async Task<string> LoadIntoRawEditorAsync(ModComponent component)
        {
            if (component is null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            await Logger.LogVerboseAsync($"Loading '{component.Name}' into the raw editor...").ConfigureAwait(false);

            string serialized = component.SerializeComponent();
            return serialized;
        }

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public async Task<bool> RemoveComponentAsync(ModComponent component)
        {
            try
            {
                if (component is null)
                {
                    await Logger.LogAsync("No component provided for removal.").ConfigureAwait(false);
                    return false;
                }

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

                if (dependentComponents.Count != 0)
                {

                    await Logger.LogAsync($"Cannot remove '{component.Name}' - {dependentComponents.Count} components depend on it:").ConfigureAwait(false);
                    foreach (ModComponent dependent in dependentComponents)
                    {
                        var dependencyTypes = new System.Collections.Generic.List<string>();
                        if (dependent.Dependencies.Contains(component.Guid))
                        {
                            dependencyTypes.Add("Dependency");
                        }

                        if (dependent.Restrictions.Contains(component.Guid))
                        {
                            dependencyTypes.Add("Restriction");
                        }

                        if (dependent.InstallBefore.Contains(component.Guid))
                        {
                            dependencyTypes.Add("InstallBefore");
                        }

                        if (dependent.InstallAfter.Contains(component.Guid))
                        {
                            dependencyTypes.Add("InstallAfter");
                        }

                        await Logger.LogAsync($"  - {dependent.Name} ({string.Join(", ", dependencyTypes)})").ConfigureAwait(false);
                    }

                    (bool confirmed, List<ModComponent> componentsToUnlink) = await DependencyUnlinkDialog.ShowUnlinkDialog(
                        _parentWindow,
                        component,
                        dependentComponents
                    ).ConfigureAwait(true);

                    if (!confirmed)
                    {
                        return false;
                    }

                    foreach (ModComponent componentToUnlink in componentsToUnlink)
                    {
                        _ = componentToUnlink.Dependencies.Remove(component.Guid);
                        _ = componentToUnlink.Restrictions.Remove(component.Guid);
                        _ = componentToUnlink.InstallBefore.Remove(component.Guid);
                        _ = componentToUnlink.InstallAfter.Remove(component.Guid);

                        await Logger.LogAsync($"Unlinked dependencies from '{componentToUnlink.Name}'").ConfigureAwait(false);
                    }
                }

                _ = _mainConfig.allComponents.Remove(component);
                await Logger.LogAsync($"Removed component: {component.Name}").ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)


            {
                await Logger.LogExceptionAsync(ex, "Failed to remove component").ConfigureAwait(false);
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
            destination.ResourceRegistry = new Dictionary<string, ResourceMetadata>(source.ResourceRegistry, StringComparer.OrdinalIgnoreCase);
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
