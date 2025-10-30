// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Dialogs;

using ReactiveUI;

using static KOTORModSync.Core.Services.ModManagementService;

namespace KOTORModSync.Services
{

	public class MenuBuilderService
	{
		private readonly ModManagementService _modManagementService;
		private readonly Window _parentWindow;

		public MenuBuilderService(ModManagementService modManagementService, Window parentWindow)
		{
			_modManagementService = modManagementService ?? throw new ArgumentNullException(nameof(modManagementService));
			_parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
		}

		public ContextMenu BuildContextMenuForComponent(
			ModComponent component,
			bool editorMode,
			Action<ModComponent> setCurrentComponent,
			Action<TabControl, TabItem> setTab,
			TabControl tabControl,
			TabItem guiEditTab,
			TabItem rawEditTab,
			Action<ModComponent> onComponentSelectionChanged,
			Action<object, object> onRemoveComponent,
			Action<object, object> onInstallSingle)
		{
			var contextMenu = new ContextMenu();

			if (component is null)
				return contextMenu;

			contextMenu.Items.Add(new MenuItem
			{
				Header = component.IsSelected ? "☑️ Deselect Mod" : "☐ Select Mod",
				Command = ReactiveCommand.Create(() =>
				{
					component.IsSelected = !component.IsSelected;
					onComponentSelectionChanged?.Invoke(component);
				}),
			});

			if (editorMode)
			{
				AddEditorModeMenuItems(contextMenu, component, setCurrentComponent, setTab, tabControl, guiEditTab, rawEditTab, onRemoveComponent, onInstallSingle);
			}

			return contextMenu;
		}

		public void BuildGlobalActionsFlyout(
			MenuFlyout menu,
			bool editorMode,
			Action onRefresh,
			Func<Task> onValidateAll,
			Func<ModComponent> onCreate,
			Action<ModComponent> setCurrentComponent,
			Action<TabControl, TabItem> setTab,
			TabControl tabControl,
			TabItem guiEditTab,
			Func<Task> onShowModManagement,
			Func<Task> onShowStats,
			Action<object, object> onSave,
			Action<object, object> onClose)
		{
			menu.Items.Clear();

			AddCommonMenuItems(menu.Items, onRefresh);

			menu.Items.Add(new Separator());

			if (editorMode)
			{
				AddEditorModeFlyoutItems(menu, onCreate, setCurrentComponent, setTab, tabControl, guiEditTab, onShowModManagement, onSave, onClose);
			}
		}

		public void BuildGlobalActionsContextMenu(
			ContextMenu menu,
			bool editorMode,
			Action onRefresh,
			Func<Task> onValidateAll,
			Func<ModComponent> onCreate,
			Action<ModComponent> setCurrentComponent,
			Action<TabControl, TabItem> setTab,
			TabControl tabControl,
			TabItem guiEditTab,
			Func<Task> onShowModManagement,
			Func<Task> onShowStats,
			Action<object, object> onSave,
			Action<object, object> onClose)
		{
			menu.Items.Clear();

			AddCommonMenuItems(menu.Items, onRefresh);

			menu.Items.Add(new Separator());

			if (editorMode)
			{
				AddEditorModeContextMenuItems(menu.Items, onCreate, setCurrentComponent, setTab, tabControl, guiEditTab, onShowModManagement, onSave, onClose);
			}
		}

		#region Private Helper Methods

		private void AddEditorModeMenuItems(
			ContextMenu contextMenu,
			ModComponent component,
			Action<ModComponent> setCurrentComponent,
			Action<TabControl, TabItem> setTab,
			TabControl tabControl,
			TabItem guiEditTab,
			TabItem rawEditTab,
			Action<object, object> onRemoveComponent,
			Action<object, object> onInstallSingle)
		{
			contextMenu.Items.Add(new Separator());

			contextMenu.Items.Add(new MenuItem
			{
				Header = "⬆️ Move Up",
				Command = ReactiveCommand.Create(() => _modManagementService.MoveModRelative(component, -1)),
				InputGesture = new KeyGesture(Key.Up, KeyModifiers.Control),
			});

			contextMenu.Items.Add(new MenuItem
			{
				Header = "⬇️ Move Down",
				Command = ReactiveCommand.Create(() => _modManagementService.MoveModRelative(component, 1)),
				InputGesture = new KeyGesture(Key.Down, KeyModifiers.Control),
			});

			contextMenu.Items.Add(new MenuItem
			{
				Header = "📊 Move to Top",
				Command = ReactiveCommand.Create(() => _modManagementService.MoveModToPosition(component, 0)),
			});

			contextMenu.Items.Add(new MenuItem
			{
				Header = "📊 Move to Bottom",
				Command = ReactiveCommand.Create(() => _modManagementService.MoveModToPosition(component, MainConfig.AllComponents.Count - 1)),
			});

			contextMenu.Items.Add(new Separator());

			contextMenu.Items.Add(new MenuItem
			{
				Header = "🗑️ Delete Mod",
				Command = ReactiveCommand.CreateFromTask(async () =>
				{
					setCurrentComponent(component);
					bool? confirm = await ConfirmationDialog.ShowConfirmationDialogAsync(
						_parentWindow,
						confirmText: $"Are you sure you want to delete the mod '{component.Name}'? This action cannot be undone.",
						yesButtonText: "Delete",
						noButtonText: "Cancel"


					).ConfigureAwait(true);

					if (confirm == true)
						onRemoveComponent(null, null);
				}),
			});

			contextMenu.Items.Add(new MenuItem
			{
				Header = "🔄 Duplicate Mod",
				Command = ReactiveCommand.Create(() =>
				{
					ModComponent duplicated = _modManagementService.DuplicateMod(component);
					if (duplicated != null)
					{
						setCurrentComponent(duplicated);
						setTab(tabControl, guiEditTab);
					}
				}),
			});

			contextMenu.Items.Add(new Separator());

			contextMenu.Items.Add(new MenuItem
			{
				Header = "📝 Edit Instructions",
				Command = ReactiveCommand.Create(() =>
				{
					setCurrentComponent(component);
					setTab(tabControl, guiEditTab);
				}),
			});

			contextMenu.Items.Add(new MenuItem
			{
				Header = "📄 Edit Raw TOML",
				Command = ReactiveCommand.Create(() =>
				{
					setCurrentComponent(component);
					setTab(tabControl, rawEditTab);
				}),
			});

			contextMenu.Items.Add(new Separator());

			contextMenu.Items.Add(new MenuItem
			{
				Header = "🧪 Test Install This Mod",
				Command = ReactiveCommand.Create(() =>
				{
					setCurrentComponent(component);
					onInstallSingle(null, null);
				}),
			});

			contextMenu.Items.Add(new MenuItem
			{
				Header = "🔍 Validate Mod Files",
				Command = ReactiveCommand.Create((Func<Task>)(async () =>
				{
					ModValidationResult validation = _modManagementService.ValidateMod(component);
					if (!validation.IsValid)
					{
						await InformationDialog.ShowInformationDialogAsync(_parentWindow,
							$"Validation failed for '{component.Name}':\n\n" +
							string.Join("\n", validation.Errors.Take(5))).ConfigureAwait(true);
					}
					else
					{
						await InformationDialog.ShowInformationDialogAsync(_parentWindow,


							$"✅ '{component.Name}' validation passed!").ConfigureAwait(true);
					}
				})),
			});
		}

		private void AddCommonMenuItems(ItemCollection items, Action onRefresh)
		{
			_ = items.Add(new MenuItem
			{
				Header = "🔄 Refresh List",
				Command = ReactiveCommand.Create(onRefresh),
				InputGesture = new KeyGesture(Key.F5),
			});

			_ = items.Add(new MenuItem
			{
				Header = "🔄 Validate All Mods",
				Command = ReactiveCommand.CreateFromTask((Func<Task>)(async () =>
				{
					Dictionary<ModComponent, ModValidationResult> results = _modManagementService.ValidateAllMods();
					int errorCount = results.Count(r => !r.Value.IsValid);
					int warningCount = results.Sum(r => r.Value.Warnings.Count);

					await InformationDialog.ShowInformationDialogAsync(_parentWindow,
						"Validation complete!\n\n" +
						$"Errors: {errorCount}\n" +
						$"Warnings: {warningCount}\n\n" +


						$"Valid mods: {results.Count(r => r.Value.IsValid)}/{results.Count}").ConfigureAwait(true);
				})),
			});
		}

		private void AddEditorModeFlyoutItems(
			MenuFlyout menu,
			Func<ModComponent> onCreate,
			Action<ModComponent> setCurrentComponent,
			Action<TabControl, TabItem> setTab,
			TabControl tabControl,
			TabItem guiEditTab,
			Func<Task> onShowModManagement,
						Action<object, object> onSave,
			Action<object, object> onClose)
		{

			menu.Items.Add(new MenuItem
			{
				Header = "➕ Add New Mod",
				Command = ReactiveCommand.Create(() =>
				{
					var newMod = onCreate();
					if (newMod is null)
						return;
					setCurrentComponent(newMod);
					setTab(tabControl, guiEditTab);
				}),
			});

			menu.Items.Add(new Separator());

			menu.Items.Add(new MenuItem
			{
				Header = "🔎 Select by Name",
				Command = ReactiveCommand.Create(() => _modManagementService.SortMods()),
			});

			menu.Items.Add(new MenuItem
			{
				Header = "🔎 Select by Category",
				Command = ReactiveCommand.Create(() => _modManagementService.SortMods(ModSortCriteria.Category)),
			});

			menu.Items.Add(new MenuItem
			{
				Header = "🔎 Select by Tier",
				Command = ReactiveCommand.Create(() => _modManagementService.SortMods(ModSortCriteria.Tier)),
			});

			menu.Items.Add(new Separator());

			menu.Items.Add(new MenuItem
			{
				Header = "⚙️ Mod Management Tools",
				Command = ReactiveCommand.CreateFromTask(onShowModManagement),
			});

			menu.Items.Add(new MenuItem
			{
				Header = "📈 Mod Statistics",
				Command = ReactiveCommand.CreateFromTask(async () =>
				{
					ModStatistics stats = _modManagementService.GetModStatistics();
					string statsText = "📊 Mod Statistics\n\n" +
									   $"Total Mods: {stats.TotalMods}\n" +
									   $"Selected: {stats.SelectedMods}\n" +
									   $"Downloaded: {stats.DownloadedMods}\n\n" +
									   $"Categories:\n{string.Join("\n", stats.Categories.Select(c => $"  • {c.Key}: {c.Value}"))}\n\n" +
									   $"Tiers:\n{string.Join("\n", stats.Tiers.Select(t => $"  • {t.Key}: {t.Value}"))}\n\n" +
									   $"Average Instructions/Mod: {stats.AverageInstructionsPerMod:F1}\n" +
									   $"Average Options/Mod: {stats.AverageOptionsPerMod:F1}";



					await InformationDialog.ShowInformationDialogAsync(_parentWindow, statsText).ConfigureAwait(true);
				}),
			});

			menu.Items.Add(new Separator());

			menu.Items.Add(new MenuItem
			{
				Header = "💾 Save Config",
				Command = ReactiveCommand.Create(() => onSave(null, null)),
				InputGesture = new KeyGesture(Key.S, KeyModifiers.Control),
			});

			menu.Items.Add(new MenuItem
			{
				Header = "❌ Close TOML",
				Command = ReactiveCommand.Create(() => onClose(null, null)),
			});
		}

		private void AddEditorModeContextMenuItems(
			ItemCollection items,
			Func<ModComponent> onCreate,
			Action<ModComponent> setCurrentComponent,
			Action<TabControl, TabItem> setTab,
			TabControl tabControl,
			TabItem guiEditTab,
			Func<Task> onShowModManagement,
						Action<object, object> onSave,
			Action<object, object> onClose)
		{

			_ = items.Add(new MenuItem
			{
				Header = "➕ Add New Mod",
				Command = ReactiveCommand.Create(() =>
				{
					ModComponent newMod = onCreate();
					if (newMod is null)
						return;
					setCurrentComponent(newMod);
					setTab(tabControl, guiEditTab);
				}),
			});

			items.Add(new Separator());

			items.Add(new MenuItem
			{
				Header = "⚙️ Mod Management Tools",
				Command = ReactiveCommand.CreateFromTask(onShowModManagement),
			});

			items.Add(new MenuItem
			{
				Header = "📈 Mod Statistics",
				Command = ReactiveCommand.CreateFromTask((Func<Task>)(async () =>
				{
					ModStatistics stats = _modManagementService.GetModStatistics();
					string statsText = "📊 Mod Statistics\n\n" +
									   $"Total Mods: {stats.TotalMods}\n" +
									   $"Selected: {stats.SelectedMods}\n" +
									   $"Downloaded: {stats.DownloadedMods}\n\n" +
									   $"Categories:\n{string.Join("\n", stats.Categories.Select(c => $"  • {c.Key}: {c.Value}"))}\n\n" +
									   $"Tiers:\n{string.Join("\n", stats.Tiers.Select(t => $"  • {t.Key}: {t.Value}"))}\n\n" +
									   $"Average Instructions/Mod: {stats.AverageInstructionsPerMod:F1}\n" +
									   $"Average Options/Mod: {stats.AverageOptionsPerMod:F1}";



					await InformationDialog.ShowInformationDialogAsync(_parentWindow, statsText).ConfigureAwait(true);
				})),
			});

			items.Add(new Separator());

			items.Add(new MenuItem
			{
				Header = "💾 Save Config",
				Command = ReactiveCommand.Create(() => onSave(null, null)),
				InputGesture = new KeyGesture(Key.S, KeyModifiers.Control),
			});

			items.Add(new MenuItem
			{
				Header = "❌ Close TOML",
				Command = ReactiveCommand.Create(() => onClose(null, null)),
			});
		}

		#endregion
	}
}