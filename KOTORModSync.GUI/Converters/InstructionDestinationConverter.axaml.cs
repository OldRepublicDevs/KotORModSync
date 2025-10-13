// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{

	public partial class InstructionDestinationConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( !(value is Instruction instruction) )
				return string.Empty;

			switch ( instruction.Action )
			{
				case Instruction.ActionType.Extract:

					return "→ (extracted to same directory)";

				case Instruction.ActionType.Move:
				case Instruction.ActionType.Copy:

					if ( !string.IsNullOrEmpty(instruction.Destination) )
					{
						string resolvedDestination = ResolvePath(instruction.Destination);
						return $"→ {resolvedDestination}";
					}
					return "→ (no destination specified)";

				case Instruction.ActionType.Rename:

					if ( !string.IsNullOrEmpty(instruction.Destination) )
					{
						return $"→ rename to: {instruction.Destination}";
					}
					return "→ (no new name specified)";

				case Instruction.ActionType.Delete:

					return "→ (delete operation)";

				case Instruction.ActionType.Patcher:

					if ( MainConfig.DestinationPath != null )
					{
						return $"→ {MainConfig.DestinationPath.FullName}";
					}
					return "→ <<kotorDirectory>>";

				case Instruction.ActionType.Execute:

					if ( !string.IsNullOrEmpty(instruction.Arguments) )
					{
						return $"→ execute with args: {instruction.Arguments}";
					}
					return "→ (execute program)";

				case Instruction.ActionType.DelDuplicate:

					if ( !string.IsNullOrEmpty(instruction.Arguments) )
					{
						return $"→ remove duplicate .{instruction.Arguments} files";
					}
					return "→ (remove duplicates)";

				case Instruction.ActionType.Choose:

					return "→ (choose from options)";

				case Instruction.ActionType.Run:

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

			if ( MainConfig.SourcePath == null && MainConfig.DestinationPath == null )
			{
				return path;
			}

			try
			{
				return Utility.ReplaceCustomVariables(path);
			}
			catch ( Exception )
			{

				return path;
			}
		}
	}
}
