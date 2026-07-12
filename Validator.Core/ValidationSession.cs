using P21.Validator.Api.Options;
using P21.Validator.Data;
using CancellationToken = P21.Validator.Api.Models.CancellationToken;

namespace P21.Validator.Core;

public sealed class ValidationSession
{
    private readonly Guid _id;
    private readonly ValidationOptions _options;
    private readonly CancellationToken? _cancellationToken;
    private readonly LookupProviderFactory _lookupProviderFactory;
    private readonly DataEntryFactory _entryFactory;

    private ValidationSession(Guid id, ValidationOptions options, CancellationToken? cancellationToken, DataEntryFactory entryFactory, LookupProviderFactory lookupProviderFactory)
    {
        _id = id;
        _options = options;
        _cancellationToken = cancellationToken;
        _entryFactory = entryFactory;
        _lookupProviderFactory = lookupProviderFactory;
    }

    public static ValidationSession Create(ValidationOptions options, CancellationToken? cancellationToken, DataEntryFactory entryFactory, LookupProviderFactory lookupProviderFactory)
    {
        return new ValidationSession(Guid.NewGuid(), options, cancellationToken, entryFactory, lookupProviderFactory);
    }

    public override bool Equals(object? obj)
    {
        return obj is ValidationSession session && session._id == _id;
    }

    public override int GetHashCode() => _id.GetHashCode();

    public string GetId() => _id.ToString();

    public bool ShouldCancel() => _cancellationToken != null && _cancellationToken.IsCancellationRequested();

    public ValidationOptions GetOptions() => _options;

    public LookupProvider GetLookupProvider(string? adapter) => _lookupProviderFactory.GetProvider(adapter);

    public DataEntryFactory GetDataEntryFactory() => _entryFactory;
}
