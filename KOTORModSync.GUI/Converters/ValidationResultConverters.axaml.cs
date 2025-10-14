// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using KOTORModSync.Core.Services.Validation;

namespace KOTORModSync.Converters
{
	/// <summary>
	/// Extracts the status message from PathValidationResult
	/// </summary>
	public partial class ValidationStatusMessageConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PathValidationResult result)
				return result.StatusMessage ?? "❓ Empty";
			return "❓ Empty";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Extracts the detailed message from PathValidationResult for tooltip
	/// </summary>
	public partial class ValidationDetailedMessageConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PathValidationResult result && !string.IsNullOrWhiteSpace(result.DetailedMessage))
				return result.DetailedMessage;
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Converts PathValidationResult to visibility for jump buttons (blocking instruction or ModLinks)
	/// </summary>
	public partial class ValidationHasBlockingInstructionConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PathValidationResult result)
				return result.BlockingInstructionIndex.HasValue || result.NeedsModLinkAdded;
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Extracts the text for the jump button (either blocking instruction or ModLinks)
	/// </summary>
	public partial class ValidationBlockingInstructionTextConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is PathValidationResult result)
			{
				if (result.NeedsModLinkAdded)
					return "📥 Jump to ModLinks";
				if (result.BlockingInstructionIndex.HasValue)
					return $"⚠️ Jump to Instruction #{result.BlockingInstructionIndex.Value + 1}";
			}
			return "Jump to Error";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

