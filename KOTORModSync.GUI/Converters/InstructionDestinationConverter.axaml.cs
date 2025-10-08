// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
	/// <summary>
	/// Converts instruction destinations to show explicit target paths for operations
	/// </summary>
	public partial class InstructionDestinationConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( !(value is Instruction instruction) )
				return string.Empty;

			// For actions that don't have explicit destinations, show implied destinations
			switch ( instruction.Action )
			{
				case Instruction.ActionType.Extract:
					// Extract typically extracts to the same directory as the archive
					return "→ (extracted to same directory)";

				case Instruction.ActionType.Move:
				case Instruction.ActionType.Copy:
					// These have explicit destinations
					if ( !string.IsNullOrEmpty(instruction.Destination) )
					{
						string resolvedDestination = ResolvePath(instruction.Destination);
						return $"→ {resolvedDestination}";
					}
					return "→ (no destination specified)";

				case Instruction.ActionType.Rename:
					// Rename shows the new filename
					if ( !string.IsNullOrEmpty(instruction.Destination) )
					{
						return $"→ rename to: {instruction.Destination}";
					}
					return "→ (no new name specified)";

				case Instruction.ActionType.Delete:
					// Delete doesn't have a destination
					return "→ (delete operation)";

				case Instruction.ActionType.Patcher:
					// Patcher always targets the KOTOR directory
					if ( MainConfig.DestinationPath != null )
					{
						return $"→ {MainConfig.DestinationPath.FullName}";
					}
					return "→ <<kotorDirectory>>";

				case Instruction.ActionType.Execute:
					// Execute runs a program
					if ( !string.IsNullOrEmpty(instruction.Arguments) )
					{
						return $"→ execute with args: {instruction.Arguments}";
					}
					return "→ (execute program)";

				case Instruction.ActionType.DelDuplicate:
					// DelDuplicate removes duplicate files
					if ( !string.IsNullOrEmpty(instruction.Arguments) )
					{
						return $"→ remove duplicate .{instruction.Arguments} files";
					}
					return "→ (remove duplicates)";

				case Instruction.ActionType.Choose:
					// Choose shows available options
					return "→ (choose from options)";

				case Instruction.ActionType.Run:
					// Run is similar to Execute
					return "→ (run program)";

				default:
					return "→ (unknown operation)";
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		[NotNull]
		private static string ResolvePath([CanBeNull] string path)
		{
			if ( string.IsNullOrEmpty(path) )
				return string.Empty;

			// Check if paths are defined before resolving
			if ( MainConfig.SourcePath == null && MainConfig.DestinationPath == null )
			{
				return path; // Return original path if directories aren't configured
			}

			try
			{
				return Utility.ReplaceCustomVariables(path);
			}
			catch ( Exception )
			{
				// If resolution fails, return the original path
				return path;
			}
		}
	}
}
