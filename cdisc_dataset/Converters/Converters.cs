using Avalonia.Data.Converters;

namespace cdisc_dataset.Converters;

public static class Converters
{
    public static FuncValueConverter<double, double> DatagridHeightConverter { get; } =
        new FuncValueConverter<double, double>(num => num - 100);

    public static FuncValueConverter<object?, bool> IsNull { get; } =
        new FuncValueConverter<object?, bool>(value => value is null);

    public static FuncValueConverter<object?, bool> IsNotNull { get; } =
        new FuncValueConverter<object?, bool>(value => value is not null);
}