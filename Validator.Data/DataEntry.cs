namespace P21.Validator.Data;

public abstract class DataEntry : IComparable<DataEntry>
{
    public static readonly DataEntry NullEntry = new DataEntryFactory.NullDataEntry();

    public enum DataType
    {
        Date,
        DateTime,
        Duration,
        Float,
        Integer,
        Text,
        Time,
        Null
    }

    public int CompareToAny(object? other)
    {
        return CompareToAny(other, true);
    }

    public abstract int CompareToAny(object? other, bool caseSensitive);

    public abstract DataType Type { get; }

    public abstract object? GetValue();

    public abstract bool IsNumeric { get; }

    public abstract bool IsDate { get; }

    public abstract bool HasValue { get; }

    public abstract int CompareTo(DataEntry? other);
}
