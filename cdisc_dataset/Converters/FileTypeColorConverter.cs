using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using cdisc_dataset.Models.Enums;

namespace cdisc_dataset.Converters;

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
