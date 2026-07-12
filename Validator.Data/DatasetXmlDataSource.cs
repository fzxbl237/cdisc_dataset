using System.Xml;
using P21.Validator.Api.Options;

namespace P21.Validator.Data;

public sealed class DatasetXmlDataSource : AbstractDataSource
{
    private readonly Dictionary<string, string?> _oids = new(StringComparer.OrdinalIgnoreCase);
    private XmlReader? _reader;
    private readonly FileInfo _define;
    private readonly FileInfo _source;
    private string? _metadataOid;
    private string? _itemGroupOid;
    private bool _isTested;

    public DatasetXmlDataSource(SourceOptions options, DataEntryFactory factory)
        : base(options, factory)
    {
        var source = new FileInfo(options.Source ?? string.Empty);
        var define = options.HasProperty("Define")
            ? new FileInfo(options.GetProperty("Define"))
            : new FileInfo(Path.Combine(source.DirectoryName ?? string.Empty, "define.xml"));

        if (!source.Exists || !define.Exists)
        {
            throw new InvalidDataException(InvalidDataException.Codes.MissingSource, "The source file is missing or is not a file");
        }

        _source = source;
        _define = define;
    }

    public override DataSource Replicate() => new DatasetXmlDataSource(Options, Factory);

    public override void Dispose()
    {
        _reader?.Close();
    }

    public override bool Test()
    {
        var result = true;
        try
        {
            using var stream = _source.OpenRead();
            _reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true });

            var depth = 0;
            while (_reader.Read() && result)
            {
                if (_reader.NodeType == XmlNodeType.Element)
                {
                    var tag = _reader.LocalName;
                    if (depth == 0 && tag != "ODM")
                    {
                        result = false;
                    }
                    else if (depth == 1)
                    {
                        if (tag is "ReferenceData" or "ClinicalData")
                        {
                            _metadataOid = _reader.GetAttribute("MetaDataVersionOID");
                            if (_metadataOid == null)
                            {
                                result = false;
                            }
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else if (depth == 2)
                    {
                        if (tag == "ItemGroupData")
                        {
                            _itemGroupOid = _reader.GetAttribute("ItemGroupOID");
                            if (_itemGroupOid == null)
                            {
                                result = false;
                            }

                            break;
                        }
                        else
                        {
                            result = false;
                        }
                    }

                    depth++;
                }
                else if (_reader.NodeType == XmlNodeType.EndElement)
                {
                    depth--;
                }
            }
        }
        catch
        {
            result = false;
        }

        _isTested = true;
        return result;
    }

    protected override void ParseVariables()
    {
        if (!_isTested)
        {
            Test();
        }

        try
        {
            using var stream = _define.OpenRead();
            using var define = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true });

            var depth = 0;
            var inItemGroupDef = false;
            var confirmedMetadataOid = false;

            while (define.Read())
            {
                if (define.NodeType == XmlNodeType.Element)
                {
                    var tag = define.LocalName;
                    if (depth == 0 && tag != "ODM")
                    {
                        throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                    }

                    if (depth == 1 && tag != "Study")
                    {
                        throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                    }

                    if (depth == 2)
                    {
                        if (tag == "MetaDataVersion")
                        {
                            var oid = define.GetAttribute("OID");
                            if (!string.Equals(_metadataOid, oid, StringComparison.Ordinal))
                            {
                                throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                            }

                            confirmedMetadataOid = true;
                        }
                        else if (tag != "GlobalVariables")
                        {
                            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                        }
                    }
                    else if (depth > 2 && confirmedMetadataOid)
                    {
                        if (depth == 3)
                        {
                            if (tag == "ItemGroupDef")
                            {
                                var oid = define.GetAttribute("OID");
                                inItemGroupDef = string.Equals(_itemGroupOid, oid, StringComparison.Ordinal);
                            }
                            else if (tag == "ItemDef")
                            {
                                var oid = define.GetAttribute("OID");
                                if (oid == null)
                                {
                                    throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                                }

                                if (_oids.ContainsKey(oid))
                                {
                                    var name = define.GetAttribute("Name");
                                    if (name == null)
                                    {
                                        throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                                    }

                                    _oids[oid] = name;
                                }
                            }
                        }
                        else if (depth == 4)
                        {
                            if (inItemGroupDef && tag == "ItemRef")
                            {
                                var oid = define.GetAttribute("ItemOID");
                                if (oid == null)
                                {
                                    throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                                }

                                _oids[oid] = null;
                            }
                        }
                    }

                    depth++;
                }
                else if (define.NodeType == XmlNodeType.EndElement)
                {
                    depth--;
                    if (depth == 3 && inItemGroupDef)
                    {
                        inItemGroupDef = false;
                    }
                }
            }

            if (!confirmedMetadataOid)
            {
                throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
            }
        }
        catch
        {
            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
        }

        foreach (var mapping in _oids)
        {
            if (mapping.Value == null)
            {
                throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource, $"No name found for ItemOID {mapping.Key}");
            }

            Metadata.Add(mapping.Value);
        }
    }

    protected override List<DataRecord> ParseRecords(int recordCount)
    {
        var records = new List<DataRecord>(recordCount);
        try
        {
            if (_reader == null)
            {
                throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
            }

            var depth = 0;
            var variables = GetVariables();
            Dictionary<string, DataEntry>? values = null;

            do
            {
                if (_reader.NodeType == XmlNodeType.Element)
                {
                    var tag = _reader.LocalName;
                    if (depth == 0)
                    {
                        if (tag != "ItemGroupData")
                        {
                            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                        }

                        if (values != null)
                        {
                            var unpopulated = new HashSet<string>(variables, StringComparer.OrdinalIgnoreCase);
                            foreach (var key in values.Keys)
                            {
                                unpopulated.Remove(key);
                            }

                            foreach (var variable in unpopulated)
                            {
                                values[variable] = DataEntry.NullEntry;
                            }

                            records.Add(NewRecord(values));
                        }

                        Next();
                        values = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase);
                    }
                    else if (depth == 1 && tag == "ItemData")
                    {
                        var oid = _reader.GetAttribute("ItemOID");
                        if (oid == null || !_oids.ContainsKey(oid))
                        {
                            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                        }

                        var value = _reader.GetAttribute("Value");
                        values![ _oids[oid]! ] = Factory.Create(value);
                    }

                    depth++;
                }
                else if (_reader.NodeType == XmlNodeType.EndElement)
                {
                    depth--;
                    if (depth == 0)
                    {
                        recordCount--;
                    }
                }
            } while (_reader.Read() && depth >= 0 && recordCount > 0);

            if (values != null)
            {
                var unpopulated = new HashSet<string>(variables, StringComparer.OrdinalIgnoreCase);
                foreach (var key in values.Keys)
                {
                    unpopulated.Remove(key);
                }

                foreach (var variable in unpopulated)
                {
                    values[variable] = DataEntry.NullEntry;
                }

                records.Add(NewRecord(values));
            }

            if (depth < 0 || _reader.NodeType == XmlNodeType.EndElement)
            {
                MarkComplete();
            }
            else if (!_reader.EOF)
            {
                throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
            }
        }
        catch
        {
            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
        }

        return records;
    }
}
