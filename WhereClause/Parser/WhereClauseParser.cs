namespace Net.Pinnacle21.Define.Parser;

public sealed class WhereClauseParser
{
    private readonly WhereClauseParserOptions _options;

    public WhereClauseParser() : this(null) { }

    public WhereClauseParser(WhereClauseParserOptions? options)
    {
        _options = options ?? WhereClauseParserOptions.CreateBuilder().Build();
    }

    public IOrConjunction Parse(string? expression)
    {
        var text = (expression ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new SyntaxException("Syntax error: The where clause was empty.", new Position(text, 0));

        if (_options.AllowWhere && text.StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase))
            text = text[5..].TrimStart();

        var comparisons = ParseComparisons(text);
        return new DefaultOrConjunction([new DefaultAndConjunction(comparisons)]);
    }

    public IOrConjunction? TryParse(string? expression)
    {
        try { return Parse(expression); } catch { return null; }
    }

    private IReadOnlyList<IComparison> ParseComparisons(string text)
    {
        var comparisons = new List<IComparison>();
        var remaining = text.Trim();

        while (!string.IsNullOrWhiteSpace(remaining))
        {
            var nextBoundary = FindNextComparisonBoundary(remaining);
            var current = nextBoundary > 0 ? remaining[..nextBoundary].TrimEnd() : remaining.TrimEnd();
            var comparison = ParseComparison(current);
            comparisons.Add(comparison);

            if (nextBoundary <= 0) break;
            remaining = remaining[nextBoundary..].TrimStart();
            if (!remaining.StartsWith("AND", StringComparison.OrdinalIgnoreCase))
                throw new SyntaxException("Syntax error: Expected 'AND'.", new Position(remaining, 0));
            remaining = remaining[3..].TrimStart();
        }

        return comparisons;
    }

    private static int FindNextComparisonBoundary(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"\s+AND\s+(?=[\w.]+\s+(?:EQ|NE|LT|LE|GT|GE|IN|NOT\s*IN|IS\s*NULL|IS\s*NOT\s*NULL|=|!=|<=|<|>=|>))",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Index : -1;
    }

    private IComparison ParseComparison(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text,
            @"^(?<id>.+?)\s+(?<op>EQ|NE|LT|LE|GT|GE|IN|NOT\s*IN|IS\s*NULL|IS\s*NOT\s*NULL|=|!=|<=|<|>=|>)\s*(?<value>.*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            throw new SyntaxException("Syntax error: Failed to parse comparison.", new Position(text, 0));

        var identifier = match.Groups["id"].Value.Trim();
        var comparatorText = match.Groups["op"].Value.Replace(" ", string.Empty).ToUpperInvariant();
        var value = match.Groups["value"].Value.Trim();

        var comparator = comparatorText switch
        {
            "EQ" or "=" => Comparator.EqualTo,
            "NE" or "!=" => Comparator.NotEqualTo,
            "LT" or "<" => Comparator.LessThan,
            "LE" or "<=" => Comparator.LessThanEqualTo,
            "GT" or ">" => Comparator.GreaterThan,
            "GE" or ">=" => Comparator.GreaterThanEqualTo,
            "IN" => Comparator.In,
            "NOTIN" => Comparator.NotIn,
            "ISNULL" => Comparator.IsNull,
            "ISNOTNULL" => Comparator.IsNotNull,
            _ => throw new SyntaxException("Syntax error: Encountered unknown comparison operator.", new Position(text, 0)),
        };

        var values = comparator switch
        {
            Comparator.IsNull or Comparator.IsNotNull => [],
            Comparator.In or Comparator.NotIn => ParseListValues(value),
            _ => [TrimQuotes(value)],
        };

        return new ParsedComparison(identifier, comparator, values);
    }

    private static IReadOnlyList<string> ParseListValues(string value)
    {
        var trimmed = value.Trim();
        if ((trimmed.StartsWith('(') && trimmed.EndsWith(')')) || (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
            trimmed = trimmed[1..^1];
        if (string.IsNullOrWhiteSpace(trimmed)) return [];
        return trimmed.Split(',').Select(v => TrimQuotes(v.Trim())).ToList();
    }

    private static string TrimQuotes(string value)
    {
        if (value.Length >= 2 && ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            var inner = value[1..^1];
            return value[0] == '"' ? inner.Replace("\"\"", "\"") : inner.Replace("''", "'");
        }
        return value;
    }
}
