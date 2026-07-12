using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using P21.Validator.Api.Events;
using P21.Validator.Api.Events.Util;
using P21.Validator.Api.Models;
using P21.Validator.Core.Util;

namespace P21.Validator.Data;

public sealed class ConcurrentLookupProvider : LookupProvider
{
    private static readonly Dispatcher Dispatcher = new();

    private readonly KeyMap<Dictionary<string, int>> _requests = new();
    private readonly ConcurrentDictionary<string, DataCache> _caches = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Action<ValidationEvent>> _eventListeners;
    private readonly SourceProvider _provider;
    private readonly DataEntryFactory _factory;

    public ConcurrentLookupProvider(DataEntryFactory factory, SourceProvider provider, HashSet<Action<ValidationEvent>> eventListeners)
    {
        _factory = factory;
        _provider = provider;
        _eventListeners = eventListeners;
    }

    public Lookup? Get(string sourceName, HashSet<string> variables)
    {
        while (true)
        {
            if (_caches.TryGetValue(sourceName, out var cache))
            {
                if (cache.Contains(variables))
                {
                    lock (cache)
                    {
                        if (!cache.IsComplete())
                        {
                            Monitor.Wait(cache);
                        }

                        return cache.IsCorrupt() ? null : cache;
                    }
                }

                if (!VerifyExists(sourceName, variables))
                {
                    return null;
                }
            }

            var isDatasetName = Regex.IsMatch(sourceName, "^[A-Za-z][A-Za-z0-9]*$");

            if (cache == null)
            {
                if (isDatasetName && !VerifyExists(sourceName, variables))
                {
                    return null;
                }

                if (_provider.ContainsSource(sourceName) && !_provider.ContainsValidSource(sourceName))
                {
                    return null;
                }

                cache = new DataCache(_factory, variables);

                if (_caches.TryAdd(sourceName, cache))
                {
                    lock (cache)
                    {
                        if (!_provider.ContainsSource(sourceName))
                        {
                            var options = _provider.ParseSource(sourceName);
                            if (options != null)
                            {
                                _provider.Add(options);
                            }
                        }

                        if (!_provider.ContainsValidSource(sourceName))
                        {
                            cache.SetCorrupt();
                            _caches.TryRemove(sourceName, out _);
                            cache.SetComplete();
                            Monitor.PulseAll(cache);
                            return null;
                        }

                        try
                        {
                            using var source = _provider.GetSource(sourceName, true);
                            if (!source.GetVariables().IsSupersetOf(variables))
                            {
                                cache.SetCorrupt();
                                _caches.TryRemove(sourceName, out _);
                                return null;
                            }

                            while (source.HasRecords())
                            {
                                cache.Store(source.GetRecords());
                                Dispatcher.DispatchTo(_eventListeners, listener => () => listener(ValidationEvents.SubprocessingIncrementEvent(source.GetName(), source.GetRecordCount())));
                            }

                            return cache;
                        }
                        catch (InvalidDataException)
                        {
                            cache.SetCorrupt();
                            _caches.TryRemove(sourceName, out _);
                            return null;
                        }
                        finally
                        {
                            cache.SetComplete();
                            Monitor.PulseAll(cache);
                        }
                    }
                }

                continue;
            }

            if (!VerifyExists(sourceName, variables))
            {
                return null;
            }

            lock (cache)
            {
                if (_caches.TryGetValue(sourceName, out var current) && current != cache && current.Contains(variables))
                {
                    return current;
                }

                var updated = new DataCache(_factory, variables, cache);
                try
                {
                    using var source = _provider.GetSource(sourceName, true);
                    source.GetVariables();

                    while (source.HasRecords())
                    {
                        updated.Store(source.GetRecords());
                        Dispatcher.DispatchTo(_eventListeners, listener => () => listener(ValidationEvents.SubprocessingIncrementEvent(source.GetName(), source.GetRecordCount())));
                    }

                    _caches[sourceName] = updated;
                    return updated;
                }
                catch (InvalidDataException)
                {
                    return null;
                }
                finally
                {
                    updated.SetComplete();
                }
            }
        }
    }

    public void Request(string sourceName, HashSet<string> variables)
    {
        Dictionary<string, int> requestedVariables;
        if (_caches.ContainsKey(sourceName))
        {
            requestedVariables = _requests.Get(sourceName) ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            requestedVariables = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var variable in variables)
        {
            requestedVariables[variable] = requestedVariables.TryGetValue(variable, out var count) ? count + 1 : 1;
        }

        _requests.Put(sourceName, requestedVariables);
    }

    public bool VerifyExists(string sourceName) => _provider.ContainsSource(sourceName);

    public bool VerifyExists(string sourceName, HashSet<string> variables)
    {
        if (!VerifyExists(sourceName))
        {
            return false;
        }

        var source = _provider.GetSource(sourceName);
        try
        {
            return source.GetVariables().IsSupersetOf(variables);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
