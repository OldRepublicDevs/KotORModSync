



using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	
	
	
	public partial class PathStatusConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			
			if ( value == null )
				return "❓ Empty";

			if ( !(value is string path) )
				return "❓ Empty";

			if ( string.IsNullOrEmpty(path) )
				return "❓ Empty";

			
			Instruction instruction = parameter as Instruction;

			
			if ( path.Contains("<<modDirectory>>") || path.Contains("<<kotorDirectory>>") )
			{
				
				if ( instruction != null && instruction.Action == Instruction.ActionType.Patcher
					&& path.Equals("<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase) )
				{
					return "✅ Valid (Patcher destination)";
				}

				
				if ( MainConfig.SourcePath == null && MainConfig.DestinationPath == null )
				{
					return "⚠️ Paths not configured";
				}
				else if ( MainConfig.SourcePath == null && path.Contains("<<modDirectory>>") )
				{
					return "⚠️ Mod directory not configured";
				}
				else if ( MainConfig.DestinationPath == null && path.Contains("<<kotorDirectory>>") )
				{
					return "⚠️ KOTOR directory not configured";
				}
				else
				{
					
					return "✅ Valid (will be resolved)";
				}
			}

			
			return "✅ Resolved";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
