



using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	
	
	
	public class StepNavigationService
	{
		private readonly MainConfig _mainConfig;
		private readonly ValidationService _validationService;

		public StepNavigationService(MainConfig mainConfig, ValidationService validationService)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
		}

		
		
		
		public int GetCurrentIncompleteStep()
		{
			try
			{
				
				bool step1Complete = ValidationService.IsStep1Complete();
				if ( !step1Complete )
					return 1;

				
				bool step2Complete = _mainConfig.allComponents?.Count > 0;
				if ( !step2Complete )
					return 2;

				
				bool step3Complete = _mainConfig.allComponents?.Any(c => c.IsSelected) == true;
				if ( !step3Complete )
					return 3;

				
				bool step4Complete = false;
				if ( step3Complete && _mainConfig.allComponents != null )
				{
					var selectedComponents = _mainConfig.allComponents.Where(c => c.IsSelected).ToList();
					if ( selectedComponents.Count > 0 )
						step4Complete = selectedComponents.All(c => c.IsDownloaded);
				}
				if ( !step4Complete )
					return 4;

				
				return 5;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error determining current step");
				return 1;
			}
		}

		
		
		
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
					
					Rect targetBounds = targetStepBorder.Bounds;
					double targetOffset = targetBounds.Top - scrollViewer.Viewport.Height / 2 + targetBounds.Height / 2;

					
					targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));

					
					scrollViewer.Offset = new Vector(0, targetOffset);

					
					await HighlightStepAsync(targetStepBorder);
				}
				else
				{
					
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

		
		
		
		private static async Task HighlightStepAsync(Border stepBorder)
		{
			try
			{
				
				IBrush originalBorderBrush = stepBorder.BorderBrush;
				Thickness originalBorderThickness = stepBorder.BorderThickness;

				
				stepBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); 
				stepBorder.BorderThickness = new Thickness(3);

				
				await Task.Delay(1000);

				
				stepBorder.BorderBrush = originalBorderBrush;
				stepBorder.BorderThickness = originalBorderThickness;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error highlighting step");
			}
		}

		
		
		
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

