// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for navigating between setup steps in the Getting Started tab
	/// </summary>
	public class StepNavigationService
	{
		private readonly MainConfig _mainConfig;
		private readonly ValidationService _validationService;

		public StepNavigationService(MainConfig mainConfig, ValidationService validationService)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
		}

		/// <summary>
		/// Determines the current incomplete step based on completion status
		/// </summary>
		public int GetCurrentIncompleteStep()
		{
			try
			{
				// Check Step 1: Directories
				bool step1Complete = ValidationService.IsStep1Complete();
				if ( !step1Complete )
					return 1;

				// Check Step 2: Components loaded
				bool step2Complete = _mainConfig.allComponents?.Count > 0;
				if ( !step2Complete )
					return 2;

				// Check Step 3: At least one component selected
				bool step3Complete = _mainConfig.allComponents?.Any(c => c.IsSelected) == true;
				if ( !step3Complete )
					return 3;

				// Check Step 4: Downloads
				bool step4Complete = false;
				if ( step3Complete && _mainConfig.allComponents != null )
				{
					var selectedComponents = _mainConfig.allComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count > 0 )
						step4Complete = selectedComponents.All(c => c.IsDownloaded);
				}
				if ( !step4Complete )
					return 4;

				// All steps complete, return 5 (validation/install)
				return 5;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error determining current step");
				return 1;
			}
		}

		/// <summary>
		/// Jumps to the current incomplete step in the Getting Started tab
		/// </summary>
		public async Task JumpToCurrentStepAsync(
			ScrollViewer scrollViewer,
			Func<string, Border> findBorder)
		{
			try
			{
				if ( scrollViewer == null || findBorder == null )
					return;

				int currentStep = GetCurrentIncompleteStep();
				Border targetStepBorder = findBorder($"Step{currentStep}Border");

				if ( targetStepBorder != null )
				{
					// Calculate the position to scroll to
					Rect targetBounds = targetStepBorder.Bounds;
					double targetOffset = targetBounds.Top - scrollViewer.Viewport.Height / 2 + targetBounds.Height / 2;

					// Ensure we don't scroll past the content bounds
					targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));

					// Scroll to the target position
					scrollViewer.Offset = new Vector(0, targetOffset);

					// Briefly highlight the target step
					await HighlightStepAsync(targetStepBorder);
				}
				else
				{
					// All steps complete - scroll to the progress section
					Border progressSection = FindProgressSection(scrollViewer.Content as Panel);
					if ( progressSection == null )
						return;

					Rect progressBounds = progressSection.Bounds;
					double targetOffset = progressBounds.Top - scrollViewer.Viewport.Height / 2 + progressBounds.Height / 2;
					targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));
					scrollViewer.Offset = new Vector(0, targetOffset);
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error jumping to current step");
			}
		}

		/// <summary>
		/// Highlights a step border briefly
		/// </summary>
		private static async Task HighlightStepAsync(Border stepBorder)
		{
			try
			{
				// Store original border properties
				IBrush originalBorderBrush = stepBorder.BorderBrush;
				Thickness originalBorderThickness = stepBorder.BorderThickness;

				// Create highlight effect
				stepBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); // Gold
				stepBorder.BorderThickness = new Thickness(3);

				// Wait for the highlight effect
				await Task.Delay(1000);

				// Restore original appearance
				stepBorder.BorderBrush = originalBorderBrush;
				stepBorder.BorderThickness = originalBorderThickness;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error highlighting step");
			}
		}

		/// <summary>
		/// Finds the progress section in the panel
		/// </summary>
		private static Border FindProgressSection(Panel panel)
		{
			if ( panel == null ) return null;

			foreach ( Control child in panel.Children )
			{
				switch ( child )
				{
					case Border border when border.Classes.Contains("progress-section"):
						return border;
					case Panel childPanel:
						{
							Border result = FindProgressSection(childPanel);
							if ( result != null ) return result;
							break;
						}
				}
			}
			return null;
		}
	}
}

