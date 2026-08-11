using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace cdisc_dataset.Converters;

public sealed class IssueSeverityTagColorConverter : IValueConverter
{
    public static IssueSeverityTagColorConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Error" => "error",
            "Warning" => "warning",
            _ => "default"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
