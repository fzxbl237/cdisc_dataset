using P21.Validator.Api.Models;
using P21.Validator.Core.Settings;

namespace P21.Validator.Core.Report;

public sealed class RuleMetadata : RuleInstance
{
    public RuleMetadata(RuleDefinition definition, string domain)
        : this(domain,
            definition.GetId(),
            definition.GetProperty("PublisherID"),
            definition.GetContext(),
            definition.GetProperty("Category"),
            definition.GetProperty("Message"),
            definition.GetProperty("Description"),
            definition.GetType())
    {
    }

    public RuleMetadata(string id, string publisherId, string category, string message, string description, Diagnostic.Type type)
        : this(null, id, publisherId, null, category, message, description, type)
    {
    }

    public RuleMetadata(string? domain, string id, string publisherId, string? context, string category, string message,
        string description, Diagnostic.Type type)
    {
        Domain = domain;
        Id = id;
        PublisherId = publisherId;
        Context = context;
        Category = category;
        Message = message;
        Description = description;
        Type = type;
    }

    public string? Domain { get; }
    public string Id { get; }
    public string PublisherId { get; }
    public string? Context { get; }
    public string Category { get; }
    public string Message { get; }
    public string Description { get; }
    public Diagnostic.Type Type { get; }

    public string? GetDomain() => Domain;
    public string GetId() => Id;
    public string GetPublisherId() => PublisherId;
    public string? GetContext() => Context;
    public string GetCategory() => Category;
    public string GetMessage() => Message;
    public string GetDescription() => Description;
    public Diagnostic.Type GetType() => Type;

    public override string ToString()
    {
        return Id + (Context != null ? $"[{Context}]" : string.Empty);
    }
}
