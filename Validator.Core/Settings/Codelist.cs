using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public sealed class Codelist : IEnumerable<Codelist.Code>
{
    private readonly Dictionary<string, Code> _codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _dictionary;
    private readonly string _name;
    private readonly string _oid;
    private readonly string _type;
    private readonly string? _version;
    private readonly bool _isExtensible;

    public Codelist(string name, string type, string oid, bool isExtensible)
        : this(name, type, oid, isExtensible, null, null)
    {
    }

    public Codelist(string name, string type, string oid, string dictionary, string version)
        : this(name, type, oid, false, dictionary, version)
    {
    }

    private Codelist(string name, string type, string oid, bool isExtensible, string? dictionary, string? version)
    {
        _name = name;
        _type = type;
        _oid = oid;
        _dictionary = dictionary;
        _version = version;
        _isExtensible = isExtensible;
    }

    public bool HasCodes() => _codes.Count > 0;

    public bool IsExtensible() => _isExtensible;

    public bool IsExternal() => _dictionary != null;

    public string GetDataType() => _type;

    public string? GetDictionary() => _dictionary;

    public string GetName() => _name;

    public string GetOid() => _oid;

    public string? GetVersion() => _version;

    public bool HasCode(string codedValue) => _codes.ContainsKey(codedValue);

    public Code? GetCode(string codedValue)
    {
        if (IsExternal())
        {
            throw new NotSupportedException("Can't get codes from an external codelist");
        }

        return _codes.TryGetValue(codedValue, out var code) ? code : null;
    }

    public void AddCode(Code code)
    {
        if (IsExternal())
        {
            throw new NotSupportedException("Can't add to an external codelist");
        }

        _codes[code.GetValue()] = code;
    }

    public IEnumerator<Code> GetEnumerator() => _codes.Values.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public sealed class Code
    {
        private readonly string _value;
        private readonly KeyMap<string> _decodes = new();

        public Code(string codedValue)
        {
            _value = codedValue;
        }

        public IReadOnlyCollection<string> GetDecodeLangs() => _decodes.KeySet();

        public string? GetDecode(string lang) => _decodes.Get(lang);

        public bool HasDecode() => _decodes.Size() > 0;

        public string GetDecode()
        {
            if (_decodes.Size() != 1)
            {
                throw new ArgumentException($"Number of decodes is {_decodes.Size()}, not 1");
            }

            return _decodes.Values().First();
        }

        public string GetValue() => _value;

        public void SetDecode(string decode, string lang)
        {
            _decodes.Put(lang, decode);
        }
    }
}
