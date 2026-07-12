namespace Net.Pinnacle21.Define.Parser;

public sealed class AnalysisCriteriaParserOptions
{
    private AnalysisCriteriaParserOptions(Builder builder)
    {
        AllowOperatorSymbols = builder.AllowOperatorSymbols;
    }

    public bool AllowOperatorSymbols { get; }

    public static Builder CreateBuilder() => new();

    public sealed class Builder
    {
        public bool AllowOperatorSymbols { get; private set; } = true;

        public Builder SetAllowOperatorSymbols(bool allow)
        {
            AllowOperatorSymbols = allow;
            return this;
        }

        public AnalysisCriteriaParserOptions Build() => new(this);
    }
}

public sealed class AnalysisCriteriaParser
{
    private readonly WhereClauseParser _whereParser;

    public AnalysisCriteriaParser() : this(null) { }

    public AnalysisCriteriaParser(AnalysisCriteriaParserOptions? options)
    {
        options ??= AnalysisCriteriaParserOptions.CreateBuilder().Build();
        _whereParser = new WhereClauseParser(WhereClauseParserOptions.CreateBuilder()
            .SetOrAllowed(false)
            .SetWhereAllowed(false)
            .SetBracketsAllowed(false)
            .SetOperatorSymbolsAllowed(options.AllowOperatorSymbols)
            .SetNullChecksAllowed(false)
            .Build());
    }

    public IReadOnlyCollection<IDatasetCriteria> Parse(string expression)
    {
        expression = expression ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expression)) return Array.Empty<IDatasetCriteria>();
        var match = System.Text.RegularExpressions.Regex.Match(expression.Trim(), @"^(?<dataset>[\w.]+)\[(?<inner>.*)\]$");
        if (!match.Success) throw new SyntaxException("Syntax error: Expected dataset name.", new Position(expression, 0));
        var dataset = match.Groups["dataset"].Value;
        var inner = match.Groups["inner"].Value;
        var where = _whereParser.Parse(inner);
        return [new DatasetCriteria(dataset, where.Conjunctions.First().Comparisons)];
    }

    public IReadOnlyCollection<IDatasetCriteria> TryParse(string expression)
    {
        try { return Parse(expression); } catch (SyntaxException) { return Array.Empty<IDatasetCriteria>(); }
    }
}
