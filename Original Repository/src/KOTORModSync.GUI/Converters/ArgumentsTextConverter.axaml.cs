// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;

using Avalonia.Data.Converters;

using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public partial class ArgumentsTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Instruction.ActionType action))
            {
                return "Arguments / Options";
            }

            switch (action)
            {
                case Instruction.ActionType.DelDuplicate:
                    return "File Extensions to Delete";
                case Instruction.ActionType.Execute:
                    return "Command Line Arguments";
                case Instruction.ActionType.Patcher:
                    return "Patcher Options";
                default:
                    return "Arguments / Options";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
