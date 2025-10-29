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

	public class UIStateService
	{
		private readonly MainConfig _mainConfig;
		private readonly ValidationService _validationService;

		public UIStateService( MainConfig mainConfig, ValidationService validationService )
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException( nameof( mainConfig ) );
			_validationService = validationService ?? throw new ArgumentNullException( nameof( validationService ) );
		}

		public void UpdateStepProgress(
			Border step1Border, Border step1Indicator, TextBlock step1Text,
			Border step2Border, Border step2Indicator, TextBlock step2Text,
			Border step3Border, Border step3Indicator, TextBlock step3Text,
			Border step4Border, Border step4Indicator, TextBlock step4Text,
			Border step5Border, Border step5Indicator, TextBlock step5Text,
			ProgressBar progressBar, TextBlock progressText,
			CheckBox step5Check,
			bool editorMode,
			Func<ModComponent, bool> isComponentValidFunc )
		{
			try
			{
				bool canUpdateProgress = progressBar != null && progressText != null;

				bool step1Complete = ValidationService.IsStep1Complete();
				UpdateStepCompletion( step1Border, step1Indicator, step1Text, step1Complete );

				bool step2Complete = step1Complete && _mainConfig.allComponents?.Count > 0;
				UpdateStepCompletion( step2Border, step2Indicator, step2Text, step2Complete );

				bool step3Complete = _mainConfig.allComponents?.Any( c => c.IsSelected ) == true;
				UpdateStepCompletion( step3Border, step3Indicator, step3Text, step3Complete );

				bool step4Complete = false;
				if (step3Complete && _mainConfig.allComponents != null)
				{
					var selectedComponents = _mainConfig.allComponents.Where( c => c.IsSelected ).ToList();
					if (selectedComponents.Count > 0)
					{
						step4Complete = selectedComponents.All( c => c.IsDownloaded );
					}
				}
				UpdateStepCompletion( step4Border, step4Indicator, step4Text, step4Complete );

				bool step5Complete = false;
				if (step4Complete && _mainConfig.allComponents != null)
				{
					var selectedComponents = _mainConfig.allComponents.Where( c => c.IsSelected ).ToList();
					if (selectedComponents.Count > 0)
					{

						bool realTimeValidationPassed = selectedComponents.All( isComponentValidFunc );

						bool buttonValidationPassed = step5Check?.IsChecked == true;

						step5Complete = realTimeValidationPassed && buttonValidationPassed;
					}
				}
				UpdateStepCompletion( step5Border, step5Indicator, step5Text, step5Complete );

				int completedSteps = (step1Complete ? 1 : 0) + (step2Complete ? 1 : 0) +
									(step3Complete ? 1 : 0) + (step4Complete ? 1 : 0) +
									(step5Complete ? 1 : 0);

				if (!canUpdateProgress)
					return;

				progressBar.Value = completedSteps;

				string[] messages = {
					"Complete the steps above to get started",
					"Great start! Continue with the next steps",
					"Almost there! Just a few more steps",
					"Excellent progress! You're almost ready",
					"🎉 All preparation steps completed! You're ready to install mods",
				};
				progressText.Text = messages[Math.Min( completedSteps, messages.Length - 1 )];
			}
			catch (Exception exception)
			{
				Logger.LogException( exception, "Error updating step progress" );
			}
		}

	private static void UpdateStepCompletion( Border stepBorder, Border indicator, TextBlock text, bool isComplete )
	{
		if (stepBorder == null || indicator == null || text == null)
			return;

		if (isComplete)
		{
			// Get theme-aware colors
			string currentTheme = ThemeManager.GetCurrentStylePath();
			bool isKotorTheme = currentTheme.Contains( "KotorStyle" ) || currentTheme.Contains( "Kotor2Style" );

			if (isKotorTheme)
			{
				// KOTOR themes: Use dark green
				stepBorder.Background = new SolidColorBrush( Color.FromRgb( 0x2E, 0x7D, 0x32 ) );
				stepBorder.BorderBrush = new SolidColorBrush( Color.FromRgb( 0x4C, 0xAF, 0x50 ) );
				stepBorder.BorderThickness = new Thickness( 3 );

				indicator.Background = new SolidColorBrush( Color.FromRgb( 0x4C, 0xAF, 0x50 ) );
				text.Foreground = Brushes.White;
			}
			else
			{
				// Fluent theme: Use blue for better visibility
				stepBorder.Background = new SolidColorBrush( Color.FromRgb( 0x19, 0x76, 0xD2 ) ); // Material Design Blue 700
				stepBorder.BorderBrush = new SolidColorBrush( Color.FromRgb( 0x21, 0x96, 0xF3 ) ); // Material Design Blue 500
				stepBorder.BorderThickness = new Thickness( 3 );

				indicator.Background = new SolidColorBrush( Color.FromRgb( 0x21, 0x96, 0xF3 ) );
				text.Foreground = Brushes.White;
			}

			text.Text = "🎉 COMPLETE! 🎉";
		}
		else
		{

			stepBorder.Background = Brushes.Transparent;
			stepBorder.ClearValue( Border.BorderBrushProperty );
			stepBorder.BorderThickness = new Thickness( uniformLength: 2 );

			indicator.Background = Brushes.Transparent;
			text.ClearValue( TextBlock.ForegroundProperty );
			text.Text = "";
		}
	}
	}
}