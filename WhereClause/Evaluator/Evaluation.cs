namespace Net.Pinnacle21.Define.Evaluator;

using Net.Pinnacle21.Define.Parser;

public sealed class WhereClauseEvaluatorOptions
{
    public bool NullChecksAllowed { get; init; }
    public bool ComplexNamesAllowed { get; init; }
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

public sealed class WhereClauseEvaluator
{
    public EvaluatedWhereClause Parse(string whereClauseText) => Parse(whereClauseText, null);

    public EvaluatedWhereClause Parse(string whereClauseText, WhereClauseEvaluatorOptions? options)
    {
        options ??= new WhereClauseEvaluatorOptions();
        try
        {
            var checks = ParseImpl(whereClauseText, options);
            return new EvaluatedWhereClause(checks, checks.Count > 0);
        }
        catch (SyntaxException)
        {
            return new EvaluatedWhereClause(Array.Empty<EvaluatedRangeCheck>(), false);
        }
    }

    private static List<EvaluatedRangeCheck> ParseImpl(string whereClause, WhereClauseEvaluatorOptions options)
    {
        var parser = new WhereClauseParser(
            WhereClauseParserOptions.CreateBuilder()
                .SetBracketsAllowed(false)
                .SetWhereAllowed(false)
                .SetOrAllowed(false)
                .SetOperatorSymbolsAllowed(false)
                .SetConjunctionSymbolsAllowed(false)
                .SetNullChecksAllowed(options.NullChecksAllowed)
                .SetComplexNamesAllowed(options.ComplexNamesAllowed)
                .Build());

        var result = parser.Parse(whereClause);
        return result.Conjunctions.SelectMany(c => c.Comparisons).Select(GetEvaluatedRangeCheck).ToList();
    }

    public static EvaluatedWhereClause GetEvaluatedWhereClause(IEnumerable<IComparison> comparisons)
    {
        var checks = comparisons.Select(GetEvaluatedRangeCheck).ToList();
        return new EvaluatedWhereClause(checks, true);
    }

    public static EvaluatedRangeCheck GetEvaluatedRangeCheck(IComparison comparison)
        => new(comparison.Identifier, comparison.Comparator.GetLiteral(), comparison.Values.Select(v => v.Trim()).ToList());
}

public sealed class SelectionCriteriaEvaluator
{
    public Net.Pinnacle21.Define.Models.Arm.EvaluatedSelectionCriteria Parse(string selectionCriteriaText)
    {
        try
        {
            var parser = new AnalysisCriteriaParser(AnalysisCriteriaParserOptions.CreateBuilder().SetAllowOperatorSymbols(false).Build());
            var criteria = parser.Parse(selectionCriteriaText);
            var evaluated = criteria.Select(c => new Net.Pinnacle21.Define.Models.Arm.EvaluatedSelectionCriterion(c.Dataset, new Net.Pinnacle21.Define.Models.EvaluatedWhereClause(WhereClauseEvaluator.GetEvaluatedWhereClause(c.Comparisons).RangeChecks.Select(r => new Net.Pinnacle21.Define.Models.EvaluatedRangeCheck(r.Identifier, r.Comparator, r.Values)).ToList(), WhereClauseEvaluator.GetEvaluatedWhereClause(c.Comparisons).IsValid))).ToList();
            return evaluated.Count == 0 ? new Net.Pinnacle21.Define.Models.Arm.EvaluatedSelectionCriteria(Array.Empty<Net.Pinnacle21.Define.Models.Arm.EvaluatedSelectionCriterion>(), false) : new Net.Pinnacle21.Define.Models.Arm.EvaluatedSelectionCriteria(evaluated, true);
        }
        catch (SyntaxException)
        {
            return new Net.Pinnacle21.Define.Models.Arm.EvaluatedSelectionCriteria(Array.Empty<Net.Pinnacle21.Define.Models.Arm.EvaluatedSelectionCriterion>(), false);
        }
    }
}
