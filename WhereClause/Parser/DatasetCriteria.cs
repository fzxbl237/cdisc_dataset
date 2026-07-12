namespace Net.Pinnacle21.Define.Parser;

public interface IDatasetCriteria
{
    string Dataset { get; }
    IReadOnlyCollection<IComparison> Comparisons { get; }
}

public sealed class DatasetCriteria : IDatasetCriteria
{
    public DatasetCriteria(string dataset, IEnumerable<IComparison> comparisons)
    {
        Dataset = dataset;
        Comparisons = comparisons.ToList().AsReadOnly();
    }

    public string Dataset { get; }
    public IReadOnlyCollection<IComparison> Comparisons { get; }
}
