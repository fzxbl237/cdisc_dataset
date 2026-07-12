using Avalonia.Data.Converters;

namespace cdisc_dataset.Converters;

public static class Converters
{
    public static FuncValueConverter<double, double> DatagridHeightConverter { get; } =
        new FuncValueConverter<double, double>(num => num - 100);
}