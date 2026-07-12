using System.Collections.Concurrent;

namespace P21.Validator.Data;

public sealed class LookupProviderFactory
{
    private readonly ConcurrentDictionary<string, LookupProvider> _externalProviders = new(StringComparer.OrdinalIgnoreCase);
    private readonly LookupProvider _defaultProvider;

    public LookupProviderFactory(LookupProvider defaultProvider)
    {
        _defaultProvider = defaultProvider;
    }

    public LookupProvider GetProvider(string? adapter)
    {
        if (adapter == null)
        {
            return _defaultProvider;
        }

        var provider = _externalProviders.GetOrAdd(adapter, key =>
        {
            try
            {
                var impl = Type.GetType(key, false);
                if (impl != null)
                {
                    return (LookupProvider)Activator.CreateInstance(impl)!;
                }
            }
            catch
            {
            }

            return null!;
        });

        if (provider == null)
        {
            throw new ArgumentException($"The identifier {adapter} does not match an available LookupProvider implementation");
        }

        return provider;
    }
}
