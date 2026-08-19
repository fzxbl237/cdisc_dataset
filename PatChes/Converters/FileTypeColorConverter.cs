using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PatChes.Models.Enums;

namespace PatChes.Converters;

public sealed class FileTypeColorConverter : IValueConverter
{
    public static FileTypeColorConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ProjectFileType.Protocol => "success",
            ProjectFileType.Acrf => "info",
            ProjectFileType.Sdtm => "warning",
            ProjectFileType.Adam => "error",
            _ => "default"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
