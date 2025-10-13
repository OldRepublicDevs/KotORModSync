// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for managing UI state such as step progress indicators
	/// </summary>
	public class UIStateService
	{
		private readonly MainConfig _mainConfig;
		private readonly ValidationService _validationService;

		public UIStateService(MainConfig mainConfig, ValidationService validationService)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
		}

		/// <summary>
		/// Updates step progress indicators based on current state
		/// </summary>
		public void UpdateStepProgress(
			Border step1Border, Border step1Indicator, TextBlock step1Text,
			Border step2Border, Border step2Indicator, TextBlock step2Text,
			Border step3Border, Border step3Indicator, TextBlock step3Text,
			Border step4Border, Border step4Indicator, TextBlock step4Text,
			Border step5Border, Border step5Indicator, TextBlock step5Text,
			ProgressBar progressBar, TextBlock progressText,
			CheckBox step5Check,
			bool editorMode,
			Func<ModComponent, bool> isComponentValidFunc)
		{
			try
			{
				bool canUpdateProgress = progressBar != null && progressText != null;

				// Check Step 1: Directories are set and valid
				bool step1Complete = ValidationService.IsStep1Complete();
				UpdateStepCompletion(step1Border, step1Indicator, step1Text, step1Complete);

				// Check Step 2: Components are loaded (only counts after Step 1)
				bool step2Complete = step1Complete && _mainConfig.allComponents?.Count > 0;
				UpdateStepCompletion(step2Border, step2Indicator, step2Text, step2Complete);

				// Check Step 3: At least one component is selected
				bool step3Complete = _mainConfig.allComponents?.Any(c => c.IsSelected) == true;
				UpdateStepCompletion(step3Border, step3Indicator, step3Text, step3Complete);

				// Check Step 4: All selected mods are downloaded
				bool step4Complete = false;
				if ( step3Complete && _mainConfig.allComponents != null )
				{
					var selectedComponents = _mainConfig.allComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count > 0 )
					{
						step4Complete = selectedComponents.All(c => c.IsDownloaded);
					}
				}
				UpdateStepCompletion(step4Border, step4Indicator, step4Text, step4Complete);

				// Check Step 5: Configuration validation
				bool step5Complete = false;
				if ( step4Complete && _mainConfig.allComponents != null )
				{
					var selectedComponents = _mainConfig.allComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count > 0 )
					{
						// Check if all selected components pass validation
						bool realTimeValidationPassed = selectedComponents.All(isComponentValidFunc);

						// Also check if the Validate button was clicked
						bool buttonValidationPassed = step5Check?.IsChecked == true;

						// Step 5 is complete if both pass
						step5Complete = realTimeValidationPassed && buttonValidationPassed;
					}
				}
				UpdateStepCompletion(step5Border, step5Indicator, step5Text, step5Complete);

				// Update progress bar (0-5 scale)
				int completedSteps = (step1Complete ? 1 : 0) + (step2Complete ? 1 : 0) +
									(step3Complete ? 1 : 0) + (step4Complete ? 1 : 0) +
									(step5Complete ? 1 : 0);

				if ( !canUpdateProgress )
					return;

				progressBar.Value = completedSteps;

				// Update progress text
				string[] messages = {
					"Complete the steps above to get started",
					"Great start! Continue with the next steps",
					"Almost there! Just a few more steps",
					"Excellent progress! You're almost ready",
					"🎉 All preparation steps completed! You're ready to install mods",
				};
				progressText.Text = messages[Math.Min(completedSteps, messages.Length - 1)];
			}
			catch ( Exception exception )
			{
				Logger.LogException(exception, "Error updating step progress");
			}
		}

		/// <summary>
		/// Updates a single step's completion indicator
		/// </summary>
		private static void UpdateStepCompletion(Border stepBorder, Border indicator, TextBlock text, bool isComplete)
		{
			if ( stepBorder == null || indicator == null || text == null )
				return;

			if ( isComplete )
			{
				// COMPLETION EFFECT
				stepBorder.Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)); // Green
				stepBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Lighter green
				stepBorder.BorderThickness = new Thickness(3);

				indicator.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
				text.Foreground = Brushes.White;
				text.Text = "🎉 COMPLETE! 🎉";
			}
			else
			{
				// Reset to normal state
				stepBorder.Background = Brushes.Transparent;
				stepBorder.ClearValue(Border.BorderBrushProperty);
				stepBorder.BorderThickness = new Thickness(uniformLength: 2);

				indicator.Background = Brushes.Transparent;
				text.ClearValue(TextBlock.ForegroundProperty);
				text.Text = "";
			}
		}
	}
}

