namespace Net.Pinnacle21.Define.Parser;

public interface IComparison
{
    string Identifier { get; }
    Comparator Comparator { get; }
    IReadOnlyCollection<string> Values { get; }
}

public interface IAndConjunction
{
    IReadOnlyCollection<IComparison> Comparisons { get; }
}

public interface IOrConjunction
{
    IReadOnlyCollection<IAndConjunction> Conjunctions { get; }
}

public sealed class DefaultAndConjunction : IAndConjunction
{
    public DefaultAndConjunction(IEnumerable<IComparison> comparisons)
    {
        Comparisons = comparisons.ToList().AsReadOnly();
    }

    public IReadOnlyCollection<IComparison> Comparisons { get; }
}

public sealed class DefaultOrConjunction : IOrConjunction
{
    public DefaultOrConjunction(IEnumerable<IAndConjunction> conjunctions)
    {
        Conjunctions = conjunctions.ToList().AsReadOnly();
    }

    public IReadOnlyCollection<IAndConjunction> Conjunctions { get; }
}
