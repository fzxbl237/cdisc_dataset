namespace P21.Validator.Core.Util;

public sealed class KeyMap<TValue>
{
    private readonly Dictionary<string, TValue> _table = new(StringComparer.OrdinalIgnoreCase);

    public int Size() => _table.Count;

    public bool IsEmpty() => _table.Count == 0;

    public bool ContainsKey(string key) => _table.ContainsKey(FormatKey(key));

    public bool ContainsValue(TValue value) => _table.ContainsValue(value);

    public TValue? Get(string key)
    {
        _table.TryGetValue(FormatKey(key), out var value);
        return value;
    }

    public TValue? Put(string key, TValue value)
    {
        var formatted = FormatKey(key);
        if (_table.TryGetValue(formatted, out var existing))
        {
            _table[formatted] = value;
            return existing;
        }

        _table[formatted] = value;
        return default;
    }

    public TValue? Remove(string key)
    {
        var formatted = FormatKey(key);
        if (_table.TryGetValue(formatted, out var existing))
        {
            _table.Remove(formatted);
            return existing;
        }

        return default;
    }

    public void PutAll(IDictionary<string, TValue> items)
    {
        foreach (var item in items)
        {
            Put(item.Key, item.Value);
        }
    }

    public void Clear() => _table.Clear();

    public IReadOnlyCollection<string> KeySet() => _table.Keys.ToList();

    public IReadOnlyCollection<TValue> Values() => _table.Values.ToList();

    public IReadOnlyCollection<KeyValuePair<string, TValue>> EntrySet() => _table.ToList();

    private static string FormatKey(string key) => key?.ToUpperInvariant() ?? string.Empty;
}
