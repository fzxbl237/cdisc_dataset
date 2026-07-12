namespace Net.Pinnacle21.Define.Parser;

public enum Comparator
{
    EqualTo,
    NotEqualTo,
    LessThan,
    LessThanEqualTo,
    GreaterThan,
    GreaterThanEqualTo,
    In,
    NotIn,
    IsNull,
    IsNotNull,
}

public static class ComparatorExtensions
{
    public static string GetLiteral(this Comparator value) => value switch
    {
        Comparator.EqualTo => "EQ",
        Comparator.NotEqualTo => "NE",
        Comparator.LessThan => "LT",
        Comparator.LessThanEqualTo => "LE",
        Comparator.GreaterThan => "GT",
        Comparator.GreaterThanEqualTo => "GE",
        Comparator.In => "IN",
        Comparator.NotIn => "NOTIN",
        Comparator.IsNull => "IS NULL",
        Comparator.IsNotNull => "IS NOT NULL",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static bool IsMultiValueCheck(this Comparator value) => value is Comparator.In or Comparator.NotIn;
    public static bool IsNullCheck(this Comparator value) => value is Comparator.IsNull or Comparator.IsNotNull;
}
