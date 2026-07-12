using System.Text;
using P21.Validator.Api.Models;
using P21.Validator.Api.Options;

namespace P21.Validator.Data;

public abstract class AbstractDataSource : DataSource
{
    protected const int DefaultRecordCount = 10;

    private bool _isFinished;
    private int _recordCount;
    private HashSet<string>? _variables;

    protected InternalEntityDetails Entity;
    protected readonly SourceOptions Options;
    protected readonly Metadata Metadata;
    protected readonly DataEntryFactory Factory;
    protected readonly Encoding? Charset;

    protected AbstractDataSource(SourceOptions options, DataEntryFactory factory)
        : this(options, factory, null)
    {
    }

    protected AbstractDataSource(SourceOptions options, DataEntryFactory factory, Encoding? defaultEncoding)
    {
        Options = options;
        Factory = factory;
        Entity = new InternalEntityDetails(SourceDetails.Reference.Data, options.Name ?? string.Empty, options.Subname, options.Source ?? string.Empty);
        Entity.SetProperty(SourceDetails.Property.Records, 0);
        Metadata = new Metadata(Entity, Factory);
        Charset = options.Charset ?? defaultEncoding;
    }

    public virtual InternalEntityDetails GetDetails() => Entity;

    public virtual string GetLocation() => Entity.GetString(SourceDetails.Property.Location);

    public virtual DataSource GetMetadata() => Metadata;

    public virtual string GetName() => Entity.GetString(SourceDetails.Property.Name);

    public virtual int GetRecordCount() => _recordCount;

    public virtual List<DataRecord> GetRecords() => GetRecords(DefaultRecordCount);

    public virtual List<DataRecord> GetRecords(int recordCount)
    {
        if (_variables == null)
        {
            GetVariables();
        }

        try
        {
            return ParseRecords(recordCount);
        }
        catch (InvalidDataException)
        {
            Entity.SetProperty(SourceDetails.Property.Corrupted, true);
            throw;
        }
    }

    public virtual HashSet<string> GetVariables()
    {
        if (_variables == null)
        {
            var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var threw = false;

            try
            {
                ParseVariables();

                if (Metadata.HasRecords())
                {
                    foreach (var record in Metadata.GetRecords())
                    {
                        var variable = record.GetValue("VARIABLE").ToString();
                        if (variables.Contains(variable))
                        {
                            throw new InvalidDataException(InvalidDataException.Codes.DuplicateVariable, $"The variable {variable} must only appear once");
                        }

                        variables.Add(variable);
                    }

                    if (Metadata is Metadata metadata)
                    {
                        metadata.Reset();
                    }
                }
            }
            catch (InvalidDataException)
            {
                threw = true;
                throw;
            }
            finally
            {
                _variables = variables;
                if (threw || _variables.Count == 0)
                {
                    Entity.SetProperty(SourceDetails.Property.Corrupted, true);
                }
            }
        }

        if (_variables.Count == 0)
        {
            throw new InvalidDataException(InvalidDataException.Codes.NoVariables, "This source does not appear to contain variable definitions.");
        }

        return _variables;
    }

    public virtual object? GetVariableProperty(string variable, DataSource.VariableProperty property)
    {
        if (_variables == null)
        {
            GetVariables();
        }

        var record = Metadata is Metadata metadata
            ? metadata.GetVariable(variable)
            : null;
        if (record == null)
        {
            throw new ArgumentException($"The variable {variable} is not defined for this source");
        }

        var value = record.GetValue(property.ToString().ToUpperInvariant()).GetValue();
        return value is decimal decimalValue ? (double)decimalValue : value;
    }

    public virtual bool HasRecords() => !_isFinished && !Entity.GetBoolean(SourceDetails.Property.Corrupted, false);

    public virtual bool IsComposite() => false;

    public virtual bool IsMetadata() => false;

    public abstract DataSource Replicate();

    public abstract bool Test();

    protected abstract List<DataRecord> ParseRecords(int recordCount);

    protected abstract void ParseVariables();

    protected void MarkComplete()
    {
        _isFinished = true;
        Dispose();
    }

    protected DataRecord NewRecord(IReadOnlyDictionary<string, DataEntry> values)
    {
        return new DataRecordImpl(new InternalDataDetails(GetRecordCount(), DataDetails.Info.Data), Entity, values);
    }

    protected void Next()
    {
        _recordCount++;
        Entity.SetProperty(SourceDetails.Property.Records, _recordCount);
    }

    protected void Rewind(int offset)
    {
        _recordCount -= offset;
        Entity.SetProperty(SourceDetails.Property.Records, _recordCount);
    }

    public abstract void Dispose();
}
