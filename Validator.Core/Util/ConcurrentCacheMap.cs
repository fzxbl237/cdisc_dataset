using System.Collections.Concurrent;

namespace P21.Validator.Core.Util;

public sealed class ConcurrentCacheMap<TValue> where TValue : class
{
    private readonly ConcurrentDictionary<string, WeakReference<TValue>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly BoundedQueue<TValue> _lastHardReferences;

    public ConcurrentCacheMap(int retainerSize = 1)
    {
        _lastHardReferences = new BoundedQueue<TValue>(retainerSize);
    }

    public void Clear()
    {
        _cache.Clear();
        _lastHardReferences.Clear();
    }

    public bool ContainsKey(string key)
    {
        return _cache.ContainsKey(key);
    }

    public bool ContainsValue(TValue value)
    {
        return _cache.Values.Any(reference => reference.TryGetTarget(out var target) && EqualityComparer<TValue>.Default.Equals(target, value));
    }

    public TValue? Get(string key)
    {
        if (_cache.TryGetValue(key, out var reference) && reference.TryGetTarget(out var value))
        {
            return value;
        }

        return default;
    }

    public ICollection<string> KeySet() => _cache.Keys.ToList();

    public TValue? Put(string key, TValue value)
    {
        _lastHardReferences.Offer(value);
        if (_cache.TryGetValue(key, out var existing) && existing.TryGetTarget(out var existingValue))
        {
            _cache[key] = new WeakReference<TValue>(value);
            return existingValue;
        }

        _cache[key] = new WeakReference<TValue>(value);
        return default;
    }

    public void PutAll(IDictionary<string, TValue> values)
    {
        foreach (var entry in values)
        {
            Put(entry.Key, entry.Value);
        }
    }

    public TValue? PutIfAbsent(string key, TValue value)
    {
        if (!ContainsKey(key))
        {
            return Put(key, value);
        }

        return Get(key);
    }

    public bool Remove(string key, TValue value)
    {
        if (Get(key) is TValue existing && EqualityComparer<TValue>.Default.Equals(existing, value))
        {
            return _cache.TryRemove(key, out _);
        }

        return false;
    }

    public TValue? Replace(string key, TValue replacement)
    {
        if (ContainsKey(key))
        {
            return Put(key, replacement);
        }

        return default;
    }

    public bool Replace(string key, TValue value, TValue replacement)
    {
        if (Get(key) is TValue existing && EqualityComparer<TValue>.Default.Equals(existing, value))
        {
            Put(key, replacement);
            return true;
        }

        return false;
    }

    public TValue? Remove(string key)
    {
        if (_cache.TryRemove(key, out var reference) && reference.TryGetTarget(out var value))
        {
            return value;
        }

        return default;
    }

    public int Size() => _cache.Count;

    public ICollection<TValue> Values()
    {
        var values = new List<TValue>();
        foreach (var reference in _cache.Values)
        {
            if (reference.TryGetTarget(out var value))
            {
                values.Add(value);
            }
        }

        return values;
    }
}
