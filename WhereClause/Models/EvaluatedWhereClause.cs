namespace Net.Pinnacle21.Define.Models;

public sealed class EvaluatedWhereClause
{
    public EvaluatedWhereClause(IReadOnlyList<EvaluatedRangeCheck> checks, bool isValid)
    {
        RangeChecks = checks;
        IsValid = isValid;
    }

    public IReadOnlyList<EvaluatedRangeCheck> RangeChecks { get; }
    public bool IsValid { get; }
}

public sealed class EvaluatedRangeCheck
{
    public EvaluatedRangeCheck(string identifier, string comparator, IReadOnlyList<string> values)
    {
        Identifier = identifier;
        Comparator = comparator;
        Values = values;
    }

    public string Identifier { get; }
    public string Comparator { get; }
    public IReadOnlyList<string> Values { get; }
}
