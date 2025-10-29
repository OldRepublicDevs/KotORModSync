// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;

using KOTORModSync.Core;

namespace KOTORModSync.Services
{

	public class ValidationDisplayService
	{
		private readonly ValidationService _validationService;
		private readonly Func<List<ModComponent>> _getMainComponents;
		private readonly List<ModComponent> _validationErrors = new List<ModComponent>();
		private int _currentErrorIndex;

		public ValidationDisplayService( ValidationService validationService, Func<List<ModComponent>> getMainComponents )
		{
			_validationService = validationService ?? throw new ArgumentNullException( nameof( validationService ) );
			_getMainComponents = getMainComponents ?? throw new ArgumentNullException( nameof( getMainComponents ) );
		}

		public void ShowValidationResults(
			Border validationResultsArea,
			TextBlock validationSummaryText,
			Border errorNavigationArea,
			Border errorDetailsArea,
			Border validationSuccessArea,
			Func<ModComponent, bool> isComponentValid )
		{
			try
			{
				var mainComponents = _getMainComponents();
				var selectedComponents = mainComponents.Where( c => c.IsSelected ).ToList();
				_validationErrors.Clear();

				foreach (ModComponent component in selectedComponents)
				{
					if (!isComponentValid( component ))
						_validationErrors.Add( component );
				}

				if (validationResultsArea == null)
					return;

				validationResultsArea.IsVisible = true;

				if (_validationErrors.Count == 0)
				{

					if (validationSummaryText != null)
						validationSummaryText.Text = $"✅ All {selectedComponents.Count} mods validated successfully!";
					if (errorNavigationArea != null)
						errorNavigationArea.IsVisible = false;
					if (errorDetailsArea != null)
						errorDetailsArea.IsVisible = false;
					if (validationSuccessArea != null)
						validationSuccessArea.IsVisible = true;
				}
				else
				{

					int validCount = selectedComponents.Count - _validationErrors.Count;
					if (validationSummaryText != null)
						validationSummaryText.Text = $"⚠️ {validCount}/{selectedComponents.Count} mods validated successfully";
					if (errorNavigationArea != null)
						errorNavigationArea.IsVisible = true;
					if (errorDetailsArea != null)
						errorDetailsArea.IsVisible = true;
					if (validationSuccessArea != null)
						validationSuccessArea.IsVisible = false;

					_currentErrorIndex = 0;
					UpdateErrorDisplay( null, null, null, null, null, null, null );
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex );
			}
		}

		public void UpdateErrorDisplay(
			TextBlock errorCounterText,
			TextBlock errorModNameText,
			TextBlock errorTypeText,
			TextBlock errorDescriptionText,
			Button autoFixButton,
			Button prevErrorButton,
			Button nextErrorButton )
		{
			try
			{
				if (_validationErrors.Count == 0 || _currentErrorIndex < 0 || _currentErrorIndex >= _validationErrors.Count)
					return;

				ModComponent currentError = _validationErrors[_currentErrorIndex];

				if (errorCounterText != null)
					errorCounterText.Text = $"Error {_currentErrorIndex + 1} of {_validationErrors.Count}";

				if (errorModNameText != null)
					errorModNameText.Text = currentError.Name;

				if (prevErrorButton != null)
					prevErrorButton.IsEnabled = _currentErrorIndex > 0;

				if (nextErrorButton != null)
					nextErrorButton.IsEnabled = _currentErrorIndex < _validationErrors.Count - 1;

				(string ErrorType, string Description, bool CanAutoFix) errorDetails = _validationService.GetComponentErrorDetails( currentError );

				if (errorTypeText != null)
					errorTypeText.Text = errorDetails.ErrorType;

				if (errorDescriptionText != null)
					errorDescriptionText.Text = errorDetails.Description;

				if (autoFixButton != null)
					autoFixButton.IsVisible = errorDetails.CanAutoFix;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex );
			}
		}

		public void NavigateToPreviousError(
			TextBlock errorCounterText,
			TextBlock errorModNameText,
			TextBlock errorTypeText,
			TextBlock errorDescriptionText,
			Button autoFixButton,
			Button prevErrorButton,
			Button nextErrorButton )
		{
			if (_currentErrorIndex > 0)
			{
				_currentErrorIndex--;
				UpdateErrorDisplay( errorCounterText, errorModNameText, errorTypeText, errorDescriptionText, autoFixButton, prevErrorButton, nextErrorButton );
			}
		}

		public void NavigateToNextError(
			TextBlock errorCounterText,
			TextBlock errorModNameText,
			TextBlock errorTypeText,
			TextBlock errorDescriptionText,
			Button autoFixButton,
			Button prevErrorButton,
			Button nextErrorButton )
		{
			if (_currentErrorIndex < _validationErrors.Count - 1)
			{
				_currentErrorIndex++;
				UpdateErrorDisplay( errorCounterText, errorModNameText, errorTypeText, errorDescriptionText, autoFixButton, prevErrorButton, nextErrorButton );
			}
		}

		public bool AutoFixCurrentError( Action<ModComponent> refreshModListVisuals )
		{
			try
			{
				if (_validationErrors.Count == 0 || _currentErrorIndex < 0 || _currentErrorIndex >= _validationErrors.Count)
					return false;

				ModComponent currentError = _validationErrors[_currentErrorIndex];
				(string ErrorType, string Description, bool CanAutoFix) errorDetails = _validationService.GetComponentErrorDetails( currentError );

				if (!errorDetails.CanAutoFix)
					return false;

				if (errorDetails.ErrorType.Contains( "Missing required dependencies" ))
				{
					AutoFixMissingDependencies( currentError );
				}
				else if (errorDetails.ErrorType.Contains( "Conflicting mods selected" ))
				{
					AutoFixConflictingMods( currentError );
				}

				refreshModListVisuals?.Invoke( currentError );
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex );
				return false;
			}
		}

		public ModComponent GetCurrentError()
		{
			if (_validationErrors.Count == 0 || _currentErrorIndex < 0 || _currentErrorIndex >= _validationErrors.Count)
				return null;

			return _validationErrors[_currentErrorIndex];
		}

		#region Private Helper Methods

		private void AutoFixMissingDependencies( ModComponent component )
		{
			List<ModComponent> mainComponents = _getMainComponents();
			if (component.Dependencies.Count == 0 || mainComponents == null)
				return;

			List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList( component.Dependencies, mainComponents );

			foreach (ModComponent dep in dependencyComponents)
			{
				if (dep != null && !dep.IsSelected)
					dep.IsSelected = true;
			}
		}

		private void AutoFixConflictingMods( ModComponent component )
		{
			List<ModComponent> mainComponents = _getMainComponents();
			if (component.Restrictions.Count == 0 || mainComponents == null)
				return;

			List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList( component.Restrictions, mainComponents );

			foreach (ModComponent restriction in restrictionComponents)
			{
				if (restriction != null && restriction.IsSelected)
					restriction.IsSelected = false;
			}
		}

		#endregion
	}
}