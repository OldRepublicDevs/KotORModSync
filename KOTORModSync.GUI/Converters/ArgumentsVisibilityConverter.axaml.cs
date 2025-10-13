



using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class ArgumentsVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is Instruction.ActionType action))
				return false;

			
			return action == Instruction.ActionType.DelDuplicate ||
				   action == Instruction.ActionType.Execute ||
				   action == Instruction.ActionType.Patcher;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotImplementedException();
	}
}
