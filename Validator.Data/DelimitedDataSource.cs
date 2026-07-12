using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using P21.Validator.Api.Models;
using P21.Validator.Api.Options;

namespace P21.Validator.Data;

public sealed class DelimitedDataSource : AbstractDataSource
{
    private const char DefaultDelimiter = '|';
    private const char DefaultQualifier = '"';

    private readonly char _delimiter;
    private readonly char _qualifier;
    private TextFieldParser? _reader;
    private readonly FileInfo _source;
    private string[]? _buffer;
    private int _variableCount = -1;
    private readonly bool _hasHeader;

    public DelimitedDataSource(SourceOptions options, DataEntryFactory factory)
        : base(options, factory, Encoding.UTF8)
    {
        var source = new FileInfo(options.Source ?? string.Empty);
        if (!source.Exists)
        {
            throw new InvalidDataException(InvalidDataException.Codes.MissingSource, "The source file is missing or is not a file");
        }

        _source = source;
        Entity.SetProperty(SourceDetails.Property.FileSize, source.Length);

        var delimiter = options.GetProperty("Delimiter");
        var qualifier = options.GetProperty("Qualifier");

        _delimiter = !string.IsNullOrEmpty(delimiter) ? delimiter[0] : DefaultDelimiter;
        _qualifier = !string.IsNullOrEmpty(qualifier) ? qualifier[0] : DefaultQualifier;
        _hasHeader = !options.HasProperty("Header", "false");

        SetReader();
    }

    public override DataSource Replicate()
    {
        return new DelimitedDataSource(Options, Factory);
    }

    public override void Dispose()
    {
        _reader?.Close();
    }

    public override bool Test()
    {
        var result = true;
        try
        {
            var line = _reader?.ReadFields();
            if (line != null)
            {
                var count = line.Length;
                line = _reader?.ReadFields();
                if (line != null && count != line.Length)
                {
                    result = false;
                }
                else
                {
                    _variableCount = count;
                }
            }
            else
            {
                result = false;
            }
        }
        catch
        {
            result = false;
        }

        if (result)
        {
            try
            {
                SetReader();
            }
            catch (InvalidDataException)
            {
                result = false;
            }
        }

        return result;
    }

    protected override void ParseVariables()
    {
        try
        {
            var count = 0;
            if (_hasHeader || _variableCount < 0)
            {
                var variables = _reader?.ReadFields();
                if (variables == null || variables.Length == 0)
                {
                    throw new InvalidDataException(InvalidDataException.Codes.NoVariables);
                }

                if (_hasHeader)
                {
                    foreach (var variable in variables)
                    {
                        Metadata.Add(System.Text.RegularExpressions.Regex.Replace(variable, "[^A-Za-z0-9]", string.Empty));
                    }
                }
                else
                {
                    count = variables.Length;
                    _buffer = variables;
                }
            }
            else
            {
                count = _variableCount;
            }

            if (!_hasHeader)
            {
                for (var i = 0; i < count; ++i)
                {
                    Metadata.Add($"V{i + 1}");
                }
            }
        }
        catch
        {
            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
        }
    }

    protected override List<DataRecord> ParseRecords(int recordCount)
    {
        var records = new List<DataRecord>(recordCount);

        try
        {
            string[]? line = null;
            var wasBuffered = false;
            var variables = GetVariables();

            if (_buffer != null)
            {
                line = _buffer;
                wasBuffered = true;
            }

            while (HasRecords() && recordCount > 0)
            {
                if (!wasBuffered || (wasBuffered = false))
                {
                    line = _reader?.ReadFields();
                }

                if (line != null)
                {
                    Next();
                    var values = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase);
                    var index = 0;
                    foreach (var currentVariable in variables)
                    {
                        values[currentVariable] = Factory.Create(line[index]);
                        ++index;
                    }

                    records.Add(NewRecord(values));
                }
                else
                {
                    MarkComplete();
                }

                --recordCount;
            }
        }
        catch
        {
            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
        }

        return records;
    }

    private void SetReader()
    {
        if (!File.Exists(_source.FullName))
        {
            throw new InvalidDataException(InvalidDataException.Codes.MissingSource, "The source file is missing or is not a file");
        }

        _reader?.Close();
        _reader = new TextFieldParser(_source.FullName, Charset ?? Encoding.UTF8)
        {
            HasFieldsEnclosedInQuotes = true,
            TextFieldType = FieldType.Delimited
        };
        _reader.SetDelimiters(_delimiter.ToString(CultureInfo.InvariantCulture));
    }
}
