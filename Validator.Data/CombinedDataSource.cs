using P21.Validator.Api.Models;

namespace P21.Validator.Data;

public sealed class CombinedDataSource : DataSource
{
    private const string SplitVariable = "VAL:DATASET";

    private readonly HashSet<string> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _identifyAsMetadata;
    private readonly List<DataSource> _sources = new();
    private readonly CombinedDataSource? _metadata;
    private readonly InternalEntityDetails _entity;
    private readonly DataEntryFactory _factory;

    private AugmentMetadata? _augment;
    private bool _isMetadata;
    private int _recordCount;
    private DataSource? _source;
    private int _sourcePosition;

    public CombinedDataSource(DataEntryFactory factory, params DataSource[] sources)
        : this(
            sources.Length > 0 ? sources[0].GetDetails().GetString(SourceDetails.Property.Name) : null,
            CombineLocations(sources),
            factory)
    {
        foreach (var source in sources)
        {
            Add(source);
        }
    }

    public CombinedDataSource(string? name, string location, DataEntryFactory factory)
    {
        if (name == null)
        {
            throw new ArgumentException("name cannot be null", nameof(name));
        }

        _entity = new InternalEntityDetails(SourceDetails.Reference.Data, name, null, location);
        _entity.SetProperty(SourceDetails.Property.Subname, name);
        _entity.SetProperty(SourceDetails.Property.Combined, true);
        _metadata = new CombinedDataSource(_entity, factory);
        _identifyAsMetadata = false;
        _factory = factory;
    }

    private CombinedDataSource(InternalEntityDetails entity, DataEntryFactory factory)
    {
        _entity = new InternalEntityDetails(SourceDetails.Reference.Metadata, entity);
        _metadata = null;
        _isMetadata = true;
        _identifyAsMetadata = true;
        _factory = factory;
    }

    public IReadOnlyList<DataSource> GetSources() => _sources;

    public void Add(DataSource source)
    {
        try
        {
            if (_sources.Count == 0)
            {
                _isMetadata = source.IsMetadata();
                _source = source;
                ConfirmAndUpdateMetadata(source);
            }
            else if (!ConfirmAndUpdateMetadata(source))
            {
                throw new ArgumentException("sources had incompatible metadata");
            }
        }
        catch (InvalidDataException)
        {
            throw new ArgumentException("invalid source");
        }

        _sources.Add(source);
        _entity.AddSubentity(source.GetDetails());
        _entity.SetProperty(SourceDetails.Property.Location, CombineLocations(_sources.ToArray()));
    }

    public void Dispose()
    {
        foreach (var source in _sources)
        {
            source.Dispose();
        }
    }

    public InternalEntityDetails GetDetails() => _entity;

    public string GetLocation() => _entity.GetString(SourceDetails.Property.Location);

    public DataSource GetMetadata()
    {
        if (_metadata == null)
        {
            throw new InvalidOperationException("metadata is not available for metadata sources");
        }

        return _metadata;
    }

    public string GetName() => _entity.GetString(SourceDetails.Property.Name);

    public int GetRecordCount() => _recordCount;

    public List<DataRecord> GetRecords() => GetRecords(10);

    public List<DataRecord> GetRecords(int recordCount)
    {
        var records = new List<DataRecord>();

        if (_augment == null)
        {
            DetermineAugment();
        }

        while (HasRecords() && records.Count < recordCount)
        {
            if (_source != null && _source.HasRecords())
            {
                records.AddRange(Augment(_source.GetRecords(recordCount)));
            }

            if (records.Count < recordCount || _source == null || !_source.HasRecords())
            {
                _sourcePosition++;
                if (_sourcePosition < _sources.Count)
                {
                    _source = _sources[_sourcePosition];
                    DetermineAugment();
                }
            }
        }

        _entity.SetProperty(SourceDetails.Property.Records, _recordCount);
        return records;
    }

    public HashSet<string> GetVariables()
    {
        return new HashSet<string>(_isMetadata ? Metadata.Variables : _variables, StringComparer.OrdinalIgnoreCase);
    }

    public object? GetVariableProperty(string variable, DataSource.VariableProperty property)
    {
        throw new NotSupportedException("cannot call getVariableProperty on a combined data source");
    }

    public bool HasRecords() => _source != null && _source.HasRecords();

    public bool IsComposite() => true;

    public bool IsMetadata() => _identifyAsMetadata && _isMetadata;

    public DataSource Replicate()
    {
        CombinedDataSource duplicate = !_identifyAsMetadata
            ? new CombinedDataSource(_entity.GetString(SourceDetails.Property.Name), _entity.GetString(SourceDetails.Property.Location), _factory)
            : new CombinedDataSource(_entity, _factory);

        foreach (var source in _sources)
        {
            duplicate.Add(source.Replicate());
        }

        return duplicate;
    }

    public bool Test() => true;

    private List<DataRecord> Augment(List<DataRecord> records)
    {
        var originalCount = _recordCount;
        _recordCount += records.Count;

        if (_isMetadata)
        {
            return records;
        }

        var values = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [SplitVariable] = _augment!.SplitVariable
        };

        foreach (var variable in _augment!.Variables)
        {
            values[variable] = DataEntry.NullEntry;
        }

        return records.Select((record, index) => new SupplementalRenumberedDataRecord(record, values, index + 1 + originalCount)).Cast<DataRecord>().ToList();
    }

    private void DetermineAugment()
    {
        if (_isMetadata)
        {
            return;
        }

        try
        {
            var splitVariable = _factory.Create(_source!.GetDetails().GetString(SourceDetails.Property.Subname));
            var defined = _source.GetVariables();
            var combined = new HashSet<string>(_variables, StringComparer.OrdinalIgnoreCase);
            combined.ExceptWith(defined);
            _augment = new AugmentMetadata(combined, splitVariable);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException("Failed to determine augment metadata", ex);
        }
    }

    private bool ConfirmAndUpdateMetadata(DataSource source)
    {
        if (source.IsMetadata() != _isMetadata)
        {
            throw new ArgumentException("The two sources are not the same reference type");
        }

        if (_isMetadata)
        {
            return source.GetVariables().SequenceEqual(Metadata.Variables);
        }

        foreach (var variable in source.GetVariables())
        {
            _variables.Add(variable);
        }

        _metadata!.Add(source.GetMetadata().Replicate());
        return true;
    }

    private static string CombineLocations(DataSource[] sources)
    {
        if (sources.Length == 0)
        {
            return "unknown";
        }

        return string.Join(Path.PathSeparator, sources.Select(source => source.GetLocation()));
    }

    private sealed class SupplementalRenumberedDataRecord : SupplementalDataRecord
    {
        private readonly int _id;

        public SupplementalRenumberedDataRecord(DataRecord record, IReadOnlyDictionary<string, DataEntry> values, int id)
            : base(record, values, false)
        {
            _id = id;
        }

        public override int GetId() => _id;
    }

    private sealed class AugmentMetadata
    {
        public AugmentMetadata(HashSet<string> variables, DataEntry splitVariable)
        {
            Variables = variables;
            SplitVariable = splitVariable;
        }

        public HashSet<string> Variables { get; }
        public DataEntry SplitVariable { get; }
    }
}
