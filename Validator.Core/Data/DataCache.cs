using P21.Validator.Core.Rules.Expressions;

namespace P21.Validator.Data;

public sealed class DataCache : Lookup
{
    private readonly Dictionary<string, SortedDictionary<DataEntry, SortedSet<int>>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, HashSet<DataEntry>>> _casings = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ignorable = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataEntryFactory _factory;

    private bool _isCorrupt;
    private bool _isComplete;

    public DataCache(DataEntryFactory factory, HashSet<string> variables)
    {
        _factory = factory;
        Init(variables);
    }

    public DataCache(DataEntryFactory factory, HashSet<string> variables, DataCache existingCache)
    {
        _factory = factory;
        variables.RemoveWhere(existingCache._cache.ContainsKey);
        Init(variables);

        foreach (var key in existingCache._cache.Keys)
        {
            _ignorable.Add(key);
            _cache[key] = existingCache._cache[key];
            _casings[key] = existingCache._casings.GetValueOrDefault(key) ?? new Dictionary<string, HashSet<DataEntry>>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Init(HashSet<string> variables)
    {
        foreach (var variable in variables)
        {
            _cache[variable] = new SortedDictionary<DataEntry, SortedSet<int>>();
        }
    }

    public void SetCorrupt() => _isCorrupt = true;

    public void SetComplete() => _isComplete = true;

    public bool IsCorrupt() => _isCorrupt;

    public bool IsComplete() => _isComplete;

    public void Store(List<DataRecord> dataRecords)
    {
        var variables = _cache.Keys.ToList();
        foreach (var dataRecord in dataRecords)
        {
            foreach (var variable in variables)
            {
                if (dataRecord.DefinesVariable(variable) && !_ignorable.Contains(variable))
                {
                    var entry = dataRecord.GetValue(variable);
                    var values = _cache[variable];

                    if (!values.TryGetValue(entry, out var records))
                    {
                        records = new SortedSet<int>();
                        values[entry] = records;
                     

                        if (entry.Type == DataEntry.DataType.Text)
                        {
                            var original = entry.ToString();
                            var keyed = ToCasingKey(original);

                            if (!string.Equals(original, keyed, StringComparison.Ordinal))
                            {
                                if (!_casings.TryGetValue(variable, out var map))
                                {
                                    map = new Dictionary<string, HashSet<DataEntry>>(StringComparer.OrdinalIgnoreCase);
                                    _casings[variable] = map;
                                }

                                if (!map.TryGetValue(keyed, out var set))
                                {
                                    set = new HashSet<DataEntry>();
                                    map[keyed] = set;
                                }

                                set.Add(entry);
                            }
                        }
                    }

                    records.Add(dataRecord.GetId());
                }
            }
        }
    }

    public bool Contains(string variable) => _cache.ContainsKey(variable);

    public bool Contains(HashSet<string> variables) => variables.All(_cache.ContainsKey);

    public void Release(HashSet<string> variables)
    {
    }

    public bool Seek(List<PreparedQuery.Mapping> search, List<PreparedQuery.Mapping> where, bool ignoreWhereFailure)
    {
        var matches = new HashSet<int>();
        var result = true;

        if (where.Count > 0)
        {
            Search(matches, where);
            result = matches.Count > 0;
        }

        if (result)
        {
            Search(matches, search);
            result = matches.Count > 0;
        }
        else
        {
            result = ignoreWhereFailure;
        }

        return result;
    }

    private void Search(HashSet<int> matches, List<PreparedQuery.Mapping> mappings)
    {
        var shortCircuitIndex = 0;
        var current = 0;

        foreach (var mapping in mappings)
        {
            if (mapping.GetOperator() == PreparedQuery.Mapping.Operator.Or)
            {
                shortCircuitIndex = current;
            }

            current++;
        }

        current = 0;
        foreach (var mapping in mappings)
        {
            var comparator = mapping.GetComparator();
            var value = mapping.GetValue();
            var variable = mapping.GetRemote();
            var currentMatches = new HashSet<int>();

            if (variable == null || value == null)
            {
                continue;
            }

            if (!_cache.TryGetValue(variable, out var cache))
            {
                current++;
                continue;
            }

            if (comparator == PreparedQuery.Mapping.Comparator.EQ)
            {
                if (cache.TryGetValue(value, out var entries))
                {
                    currentMatches = new HashSet<int>(entries);
                }
            }
            else if (comparator == PreparedQuery.Mapping.Comparator.EIQ)
            {
                if (value.Type == DataEntry.DataType.Text)
                {
                    var keyed = ToCasingKey(value.ToString());
                    if (_casings.TryGetValue(variable, out var casings) && casings.TryGetValue(keyed, out var conversions))
                    {
                        foreach (var conversion in conversions)
                        {
                            if (cache.TryGetValue(conversion, out var entryMatches))
                            {
                                currentMatches.UnionWith(entryMatches);
                            }
                        }
                    }

                    if (currentMatches.Count == 0 && cache.TryGetValue(_factory.Create(keyed), out var keyedMatches))
                    {
                        currentMatches.UnionWith(keyedMatches);
                    }
                }
                else if (cache.TryGetValue(value, out var entries))
                {
                    currentMatches = new HashSet<int>(entries);
                }
            }
            else if (comparator == PreparedQuery.Mapping.Comparator.NEQ)
            {
                currentMatches = ConvertMapToSet(cache.ToDictionary(), value);
            }
            else if (comparator == PreparedQuery.Mapping.Comparator.LT || comparator == PreparedQuery.Mapping.Comparator.LTE)
            {
                currentMatches = ConvertMapToSet(cache.Where(entry => entry.Key.CompareTo(value) < 0).ToDictionary(e => e.Key, e => e.Value), null);

                if (comparator == PreparedQuery.Mapping.Comparator.LTE && cache.TryGetValue(value, out var matchesForValue))
                {
                    currentMatches.UnionWith(matchesForValue);
                }
            }
            else if (comparator == PreparedQuery.Mapping.Comparator.GT || comparator == PreparedQuery.Mapping.Comparator.GTE)
            {
                currentMatches = ConvertMapToSet(cache.Where(entry => entry.Key.CompareTo(value) > 0).ToDictionary(e => e.Key, e => e.Value),
                    comparator == PreparedQuery.Mapping.Comparator.GT ? value : null);
            }

            if ((mapping.GetOperator() == null && matches.Count == 0) || mapping.GetOperator() == PreparedQuery.Mapping.Operator.Or)
            {
                matches.UnionWith(currentMatches);
            }
            else
            {
                matches.IntersectWith(currentMatches);
            }

            if (matches.Count == 0 && current >= shortCircuitIndex)
            {
                break;
            }

            current++;
        }
    }

    private HashSet<int> ConvertMapToSet(Dictionary<DataEntry, SortedSet<int>> selection, DataEntry? excluding)
    {
        var target = new HashSet<int>();
        foreach (var entry in selection)
        {
            if (excluding == null || !excluding.Equals(entry.Key))
            {
                target.UnionWith(entry.Value);
            }
        }

        return target;
    }

    private string ToCasingKey(string key) => key.ToUpperInvariant();
}
