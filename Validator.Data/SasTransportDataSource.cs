using System.Buffers.Binary;
using System.Text;
using P21.Validator.Api.Models;
using P21.Validator.Api.Options;

namespace P21.Validator.Data;

public sealed class SasTransportDataSource : AbstractDataSource
{
    private const int DefaultBufferSize = 80;
    private const int ExtendedBufferSize = 140;
    private const int MappableRegionSize = 1024 * 1024 * 100;
    private const int ExpectedNumericWidth = 8;
    private const int NameStrNumPosition = 54;
    private const int DscrptrLabelPosition = 32;

    private static readonly byte[] LibraryHeaderBytes =
    {
        72, 69, 65, 68, 69, 82, 32, 82, 69, 67, 79, 82, 68, 42, 42, 42, 42, 42, 42,
        42, 76, 73, 66, 82, 65, 82, 89, 32, 72, 69, 65, 68, 69, 82, 32, 82, 69, 67,
        79, 82, 68, 33, 33, 33, 33, 33, 33, 33, 48, 48, 48, 48, 48, 48, 48, 48, 48,
        48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48,
        48, 48
    };

    private static readonly byte[] DscrptrHeaderBytes =
    {
        72, 69, 65, 68, 69, 82, 32, 82, 69, 67, 79, 82, 68, 42, 42, 42, 42, 42, 42,
        42, 68, 83, 67, 82, 80, 84, 82, 32, 72, 69, 65, 68, 69, 82, 32, 82, 69, 67,
        79, 82, 68, 33, 33, 33, 33, 33, 33, 33, 48, 48, 48, 48, 48, 48, 48, 48, 48,
        48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48,
        48, 48, 32, 32
    };

    private static readonly byte[] NameStrHeaderBytes =
    {
        72, 69, 65, 68, 69, 82, 32, 82, 69, 67, 79, 82, 68, 42, 42, 42, 42, 42, 42,
        42, 78, 65, 77, 69, 83, 84, 82, 32, 72, 69, 65, 68, 69, 82, 32, 82, 69, 67,
        79, 82, 68, 33, 33, 33, 33, 33, 33, 33, 48, 48, 48, 48, 48
    };

    private static readonly byte[] ObsHeaderBytes =
    {
        72, 69, 65, 68, 69, 82, 32, 82, 69, 67, 79, 82, 68, 42, 42, 42, 42, 42, 42,
        42, 79, 66, 83, 32, 32, 32, 32, 32, 72, 69, 65, 68, 69, 82, 32, 82, 69, 67,
        79, 82, 68, 33, 33, 33, 33, 33, 33, 33, 48, 48, 48, 48, 48, 48, 48, 48, 48,
        48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48, 48,
        48, 48
    };

    private readonly MemoryStream _stream;
    private readonly List<Variable> _variables = new();
    private readonly long _bytesTotal;
    private Memory<byte>? _buffer;
    private int _recordSize;
    private long _bytesRead;
    private int _previouslyNull;

    public SasTransportDataSource(SourceOptions options, DataEntryFactory factory)
        : base(options, factory, Encoding.GetEncoding("ISO-8859-1"))
    {
        var optionsMemoryStream = options.MemoryStream;
        if (optionsMemoryStream != null)
        {
            _bytesTotal = optionsMemoryStream.Length;
            _stream = optionsMemoryStream;
        }
        // var source = new FileInfo(options.Source ?? string.Empty);
        // if (!source.Exists)
        // {
        //     throw new InvalidDataException(InvalidDataException.Codes.MissingSource, "The source file is missing or is not a file");
        // }
        //
        // _bytesTotal = source.Length;
        Entity.SetProperty(SourceDetails.Property.FileSize, _bytesTotal);
        //_stream = source.OpenRead();
    }

    public override void Dispose()
    {
        _stream.Dispose();
    }

    public override DataSource Replicate()
    {
        return new SasTransportDataSource(Options, Factory);
    }

    public override bool Test()
    {
        try
        {
            var test = GetBuffer(DefaultBufferSize);
            return test != null && BufferStartsWith(test.Value.Span, LibraryHeaderBytes);
        }
        catch
        {
            return false;
        }
    }

    protected override List<DataRecord> ParseRecords(int batchSize)
    {
        var records = new List<DataRecord>(batchSize);
        var pending = batchSize;

        while (HasRecords() && pending > 0)
        {
            var observation = GetBuffer(_recordSize);
            if (observation == null)
            {
                break;
            }

            var previous = _bytesRead - _recordSize;
            var remaining = _bytesTotal - DefaultBufferSize;

            if (_recordSize <= DefaultBufferSize && previous > remaining)
            {
                var looksNull = true;
                for (var i = 0; i < _recordSize && looksNull; ++i)
                {
                    looksNull = observation.Value.Span[i] == 32;
                }

                if (looksNull)
                {
                    _previouslyNull++;
                }
                else
                {
                    _previouslyNull = 0;
                }
            }

            Next();
            var values = new Dictionary<string, DataEntry>(StringComparer.OrdinalIgnoreCase);
            var offset = 0;

            foreach (var variable in _variables)
            {
                if (offset + variable.Length > observation.Value.Length)
                {
                    //throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                    continue;
                }

                var slice = observation.Value.Slice(offset, variable.Length).ToArray();
                offset += variable.Length;

                DataEntry entry;
                if (variable.Type == Variable.PhysicalFormat.Character)
                {
                    entry = Factory.Create(ToString(slice));
                }
                else
                {
                    entry = Factory.Create(Convert(slice, Factory.DecimalCount));
                }

                values[variable.Name] = entry;
            }

            if(observation.Value.Length>0)
                records.Add(NewRecord(values));
            pending--;
        }

        if (_previouslyNull > 0)
        {
            Rewind(_previouslyNull);
            for (var i = 0; i < _previouslyNull && records.Count > 0; ++i)
            {
                records.RemoveAt(records.Count - 1);
            }

            _previouslyNull = 0;
        }

        return records;
    }

    protected override void ParseVariables()
    {
        var isValid = false;
        var variables = 0;
        var trailing = 0;

        while (!isValid && HasRecords())
        {
            var buffer = GetBuffer(DefaultBufferSize);
            if (buffer == null)
            {
                break;
            }

            if (buffer.Value.Span.SequenceEqual(DscrptrHeaderBytes))
            {
                trailing = 2;
            }
            else if (trailing > 0)
            {
                if (trailing == 1)
                {
                    var labelBytes = buffer.Value.Slice(DscrptrLabelPosition, 40).ToArray();
                    var label = ToString(labelBytes).Trim();
                    Entity.SetProperty(SourceDetails.Property.Label, label);
                    Entity.SetProperty(SourceDetails.Property.DatasetLabel, label);
                }

                trailing--;
            }
            else
            {
                var comparable = buffer.Value.Slice(0, NameStrNumPosition - 1).ToArray();
                if (BufferStartsWith(comparable, NameStrHeaderBytes))
                {
                    var countBytes = buffer.Value.Slice(NameStrNumPosition, 4).ToArray();
                    variables = int.Parse(ToString(countBytes).Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    isValid = true;
                }
            }
        }

        if (!isValid)
        {
            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
        }

        if (variables == 0)
        {
            throw new InvalidDataException(InvalidDataException.Codes.NoVariables);
        }

        for (var i = 0; i < variables; ++i)
        {
            var buffer = GetBuffer(ExtendedBufferSize) ?? throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
                var span = buffer.Span;

            var type = BinaryPrimitives.ReadInt16BigEndian(span[..2]);
            var length = BinaryPrimitives.ReadInt16BigEndian(span[4..6]);
            var name = ToString(span.Slice(8, 8).ToArray()).Trim().ToUpperInvariant();
            var label = ToString(span.Slice(16, 40).ToArray()).Trim();
            var format = ToString(span.Slice(56, 8).ToArray()).Trim();

            if (label.Length == 0)
            {
                label = "null";
            }

            var formatLength = BinaryPrimitives.ReadInt16BigEndian(span.Slice(64, 2));
            var fullFormat = format.Length > 0 && formatLength != 0 ? format + formatLength : format;

            if (length < 0)
            {
                throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
            }

            var variable = new Variable(name, type, length);
            _variables.Add(variable);
            Metadata.Add(name, variable.TypeName, length, label, format, fullFormat);
            _recordSize += length;
        }

        var remainder = (int)(_bytesRead % DefaultBufferSize);
        if (remainder > 0)
        {
            GetBuffer(DefaultBufferSize - remainder);
        }

        var obsBuffer = GetBuffer(DefaultBufferSize) ?? throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
        if (!BufferStartsWith(obsBuffer.Span, ObsHeaderBytes))
        {
            throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
        }
    }

    private ReadOnlyMemory<byte>? GetBuffer(int size)
    {
        var copy = new byte[size];
        var copied = 0;

        if (_buffer == null)
        {
            if (!Reallocate())
            {
                throw new InvalidDataException(InvalidDataException.Codes.CannotParseSource);
            }
        }

        var bufferSpan = _buffer!.Value.Span;
        var readable = Math.Min(copy.Length, bufferSpan.Length);
        if (readable > 0)
        {
            bufferSpan[..readable].CopyTo(copy);
            _buffer = _buffer.Value.Slice(readable);
        }

        copied += readable;
        _bytesRead += copied;

        if (readable != copy.Length && Reallocate())
        {
            bufferSpan = _buffer!.Value.Span;
            readable = Math.Min(copy.Length - copied, bufferSpan.Length);
            bufferSpan[..readable].CopyTo(copy.AsSpan(copied));
            _buffer = _buffer.Value.Slice(readable);
            _bytesRead += readable;
            copied += readable;
        }

        return copied == size ? copy : null;
    }

    private static bool BufferStartsWith(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> prefix)
    {
        if (buffer.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (buffer[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    private static double? Convert(byte[] raw, int roundingCutoff)
    {
        var capacity = raw.Length;
        var adjust = ExpectedNumericWidth - capacity;
        if (adjust > 0)
        {
            var padded = new byte[ExpectedNumericWidth];
            Array.Copy(raw, padded, raw.Length);
            raw = padded;
        }

        var hasValue = false;
        for (var i = 1; i < raw.Length && !hasValue; ++i)
        {
            hasValue = raw[i] != 0;
        }

        if (!hasValue)
        {
            if (raw[0] != 0x5F && raw[0] != 0x2E && (raw[0] < 0x41 || raw[0] > 0x5A))
            {
                hasValue = raw[0] != 0;
            }
        }

        if (!hasValue)
        {
            return raw[0] == 0 ? 0.0 : null;
        }

        var cell = raw.AsSpan();
        var xport1 = BinaryPrimitives.ReadUInt32BigEndian(cell[..4]);
        var xport2 = BinaryPrimitives.ReadUInt32BigEndian(cell[4..8]);

        var exponent = raw[0];
        var ieee1 = xport1 & 0x00FFFFFF;
        var ieee2 = xport2;
        short shift;
        var nib = xport1;

        if ((nib & 0x00800000) != 0)
        {
            shift = 3;
        }
        else if ((nib & 0x00400000) != 0)
        {
            shift = 2;
        }
        else if ((nib & 0x00200000) != 0)
        {
            shift = 1;
        }
        else
        {
            shift = 0;
        }

        if (shift > 0)
        {
            ieee1 >>= shift;
            ieee2 = (xport2 >> shift) | ((xport1 & 0x00000007) << (29 + (3 - shift)));
        }

        ieee1 &= 0xFFEFFFFF;
        ieee1 |= (uint)((((exponent & 0x7F) - 65) << 2) + shift + 1023) << 20 | (xport1 & 0x80000000);

        var ieeeBytes = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(ieeeBytes.AsSpan(0, 4), ieee1);
        BinaryPrimitives.WriteUInt32BigEndian(ieeeBytes.AsSpan(4, 4), ieee2);
        var value = BitConverter.ToDouble(ieeeBytes.Reverse().ToArray(), 0);

        return Values.Round(value, roundingCutoff);
    }

    private bool Reallocate()
    {
        if (_bytesRead == _bytesTotal)
        {
            MarkComplete();
            return false;
        }

        var remaining = Math.Min(_bytesTotal - _bytesRead, MappableRegionSize);
        var buffer = new byte[remaining];
        var read = _stream.Read(buffer, 0, buffer.Length);
        _buffer = buffer.AsMemory(0, read);
        return read > 0;
    }

    private string ToString(byte[] characters)
    {
        return (Charset ?? Encoding.GetEncoding("ISO-8859-1")).GetString(characters);
    }

    private sealed class Variable
    {
        public enum PhysicalFormat
        {
            Numeric = 1,
            Character = 2
        }

        public Variable(string name, short type, short length)
        {
            Name = name;
            Length = length;
            Type = type == (short)PhysicalFormat.Numeric ? PhysicalFormat.Numeric : PhysicalFormat.Character;
        }

        public string Name { get; }
        public short Length { get; }
        public PhysicalFormat Type { get; }
        public string TypeName => Type == PhysicalFormat.Numeric ? "Num" : "Char";
    }
}
