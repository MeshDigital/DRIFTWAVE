using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SLSKDONET.Models;

namespace SLSKDONET.Views.Avalonia.Converters
{
    public static class EnumConverters
    {
        public static IValueConverter BouncerModeEquals { get; } =
            new FuncValueConverter<BouncerMode, BouncerMode, bool>((value, param) => value == param);

        public static IValueConverter BouncerModeConverter { get; } = new EnumToBooleanConverter();
    }
}
