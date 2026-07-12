namespace P21.Validator.Data;

public class DataGrouping
{
    private readonly DataEntry[] _group;
    private readonly int _hashCode;

    public DataGrouping(IEnumerable<DataEntry> group)
    {
        _group = group.ToArray();
        var hash = new HashCode();
        foreach (var entry in _group)
        {
            hash.Add(entry);
        }

        _hashCode = hash.ToHashCode();
    }

    protected DataGrouping(DataGrouping grouping)
        : this(grouping._group)
    {
    }

    public virtual bool Accepts(DataEntry entry) => false;

    public IReadOnlyList<DataEntry> Entries => _group;

    public DataEntry Get(int index) => _group[index];

    public int Size() => _group?.Length ?? 0;

    public bool IsEmpty() => _group == null || _group.Length == 0;

    public override int GetHashCode() => _hashCode;

    public override bool Equals(object? obj)
    {
        if (obj is not DataGrouping other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _group.SequenceEqual(other._group);
    }
}
