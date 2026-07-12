namespace Net.Pinnacle21.Define.Parser;

public sealed class ParsedComparison : IComparison
{
    public ParsedComparison(string identifier, Comparator comparator, IEnumerable<string> values)
    {
        Identifier = identifier;
        Comparator = comparator;
        Values = values.ToList().AsReadOnly();
    }

    public string Identifier { get; }
    public Comparator Comparator { get; }
    public IReadOnlyCollection<string> Values { get; }

    public override string ToString()
    {
        var escapedValues = Values.Select(EscapeValue).ToList();
        return Comparator is Comparator.In or Comparator.NotIn
            ? $"{Identifier} {Comparator.GetLiteral()} ({string.Join(", ", escapedValues)})"
            : $"{Identifier} {Comparator.GetLiteral()} {escapedValues[0]}";
    }

    private static string EscapeValue(string value)
        => value.Any(char.IsWhiteSpace) || value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}" + '"' : value;
}
