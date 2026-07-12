using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public sealed class Metadata : DataSource
{
    public static readonly HashSet<string> Variables = new HashSet<string>
    {
        "DOMAIN",
        "DATASET",
        "VARIABLE",
        "TYPE",
        "LENGTH",
        "LABEL",
        "ORDER",
        "FORMAT"
    };

    private readonly InternalEntityDetails _entity;
    private readonly InternalEntityDetails _parent;
    private int _recordCount;
    private readonly List<DataRecord> _records = new();
    private readonly Dictionary<string, int> _locations = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataEntryFactory _factory;

    public Metadata(InternalEntityDetails entity, DataEntryFactory factory)
        : this(null, entity, factory)
    {
    }

    private Metadata(InternalEntityDetails? parent, InternalEntityDetails entity, DataEntryFactory factory)
    {
        Reset();
        _parent = parent ?? entity;
        _entity = new InternalEntityDetails(SourceDetails.Reference.Metadata, entity, _parent);
        _factory = factory;
    }

    public void Dispose()
    {
    }

    public InternalEntityDetails GetDetails() => _entity;

    public string GetLocation() => _entity.GetString(SourceDetails.Property.Location);

    public DataSource GetMetadata() => throw new NotSupportedException("cannot get metadata from metadata");

    public string GetName() => _entity.GetString(SourceDetails.Property.Name);

    public int GetRecordCount() => _recordCount;

    public List<DataRecord> GetRecords() => GetRecords(_records.Count);

    public List<DataRecord> GetRecords(int recordCount)
    {
        var records = new List<DataRecord>();
        var current = _recordCount;
        var bound = Math.Min(current + recordCount, _records.Count);

        for (; current < bound; ++current)
        {
            records.Add(_records[current]);
        }

        _recordCount = current;
        return records;
    }

    public HashSet<string> GetVariables() => Variables;

    public object? GetVariableProperty(string variable, DataSource.VariableProperty property)
    {
        throw new NotSupportedException("Can't get variable properties of a metadata source");
    }

    public bool HasRecords() => _recordCount < _records.Count;

    public bool IsComposite() => false;

    public bool IsMetadata() => true;

    public DataSource Replicate()
    {
        var duplicate = new Metadata(_parent, _entity, _factory);
        duplicate._records.AddRange(_records);
        return duplicate;
    }

    public bool Test() => true;

    public DataRecord? GetVariable(string name)
    {
        return _locations.TryGetValue(name.ToUpperInvariant(), out var index) ? _records[index] : null;
    }

    public IReadOnlyCollection<string> GetVariableNames() => _locations.Keys.ToList();

    public void Add(string variable) => Add(variable, null, null, null, null);

    public void Add(string variable, string? type, int? length, string? label, string? format)
    {
        Add(variable, type, length, label, format, format);
    }

    public void Add(string variable, string? type, int? length, string? label, string? format, string? fullFormat)
    {
        var order = _records.Count + 1;
        var domain = _entity.GetString(SourceDetails.Property.Name).ToUpperInvariant();
        var dataset = _entity.HasProperty(SourceDetails.Property.Subname)
            ? _entity.GetString(SourceDetails.Property.Subname)
            : null;

        variable = variable.ToUpperInvariant();

        var values = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOMAIN"] = _factory.Create(domain),
            ["DATASET"] = _factory.Create(dataset),
            ["VARIABLE"] = _factory.Create(variable),
            ["TYPE"] = _factory.Create(type),
            ["LENGTH"] = _factory.Create(length),
            ["LABEL"] = _factory.Create(label),
            ["ORDER"] = _factory.Create((double)order),
            ["FORMAT"] = _factory.Create(format)
        };

        var record = new DataRecordImpl(new InternalDataDetails(order, variable, DataDetails.Info.Variable), _entity, values);
        _records.Add(record);
        _locations[variable] = _records.Count - 1;
        _entity.SetProperty(SourceDetails.Property.Variables, _records.Count);
        _parent.AddVariable(new InternalVariableDetails(variable, order, type, length, label, format, fullFormat));
    }

    public void Reset()
    {
        _recordCount = 0;
    }
}
