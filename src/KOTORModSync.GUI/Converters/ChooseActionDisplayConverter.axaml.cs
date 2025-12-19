// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia.Data.Converters;

using KOTORModSync.Core;

namespace KOTORModSync.Converters
{

    public partial class ChooseActionDisplayConverter : IValueConverter, IMultiValueConverter
    {
        // IValueConverter implementation (for backwards compatibility)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Instruction instruction)
            {
                return ConvertInstruction(instruction);
            }

            return string.Empty;
        }

        // IMultiValueConverter implementation (triggers on property changes)
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is null || values.Count == 0)
            {
                return string.Empty;
            }

            // First value should be the Instruction
            if (values[0] is Instruction instruction)
            {
                return ConvertInstruction(instruction);
            }

            return string.Empty;
        }

        private static object ConvertInstruction(Instruction instruction)
        {
            if (instruction.Action == Instruction.ActionType.Choose)
            {
                int sourceCount = instruction.Source.Count;
                if (sourceCount == 0)
                {
                    return "Choose (no options)";
                }

                if (sourceCount == 1)
                {
                    return "Choose (1 option)";
                }

                return $"Choose ({sourceCount} options)";
            }

            return instruction.Action.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
