using P21.Validator.Api.Models;
using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public sealed class RuleDefinition
{
    private readonly string _id;
    private readonly string _ruleType;
    private readonly Diagnostic.Type _type;
    private readonly KeyMap<string> _properties = new();
    private readonly List<ConditionalSeverity> _conditions = new();
    private readonly List<Definition> _contexts = new();
    private readonly string? _context;

    public RuleDefinition(string id, string ruleType, Diagnostic.Type type)
        : this(id, ruleType, type, null)
    {
    }

    private RuleDefinition(string id, string ruleType, Diagnostic.Type type, string? context)
    {
        _id = id;
        _ruleType = ruleType;
        _type = type;
        _context = context;
    }

    private RuleDefinition(RuleDefinition baseDefinition)
        : this(baseDefinition, null as Diagnostic.Type?)
    {
    }

    private RuleDefinition(RuleDefinition baseDefinition, Diagnostic.Type? type)
        : this(baseDefinition._id, baseDefinition._ruleType, type ?? baseDefinition._type, baseDefinition._context)
    {
        _properties.PutAll(baseDefinition._properties.EntrySet().ToDictionary(entry => entry.Key, entry => entry.Value));
        _conditions.AddRange(baseDefinition._conditions);
        _contexts.AddRange(baseDefinition._contexts);
    }

    private RuleDefinition(RuleDefinition baseDefinition, string? context)
        : this(baseDefinition._id, baseDefinition._ruleType, baseDefinition._type, context)
    {
        _properties.PutAll(baseDefinition._properties.EntrySet().ToDictionary(entry => entry.Key, entry => entry.Value));
        _conditions.AddRange(baseDefinition._conditions);
        _contexts.AddRange(baseDefinition._contexts);
    }

    public string GetId() => _id;

    public string GetRuleType() => _ruleType;

    public Diagnostic.Type GetType() => _type;

    public bool HasProperty(string property) => _properties.ContainsKey(property);

    public string? GetContext() => _context;

    public IReadOnlyList<Definition> GetContexts() => _contexts.AsReadOnly();

    public string GetProperty(string property)
    {
        return _properties.Get(property) ?? string.Empty;
    }

    public IReadOnlyCollection<string> GetProperties() => _properties.KeySet();

    public RuleDefinition WithProperty(string property, string? value)
    {
        var definition = this;
        if (!HasProperty(property) || !string.Equals(GetProperty(property), value, StringComparison.Ordinal))
        {
            definition = new RuleDefinition(definition);
            definition._properties.Put(property, value ?? string.Empty);
        }

        return definition;
    }

    public RuleDefinition WithConditionalSeverity(ConditionalSeverity condition)
    {
        var definition = new RuleDefinition(this);
        definition._conditions.Add(condition);
        return definition;
    }

    public RuleDefinition WithContext(Definition context)
    {
        var definition = new RuleDefinition(this);
        definition._contexts.Add(context);
        return definition;
    }

    public RuleDefinition WithContext(string context)
    {
        return new RuleDefinition(this, context);
    }

    public RuleDefinition WithSeverityFor(string domain)
    {
        ConditionalSeverity? substitutedSeverity = null;
        var bestMatch = ConditionalSeverity.Match.None;

        foreach (var condition in _conditions)
        {
            var match = condition.Check(domain, _context);
            if (ConditionalSeverity.IsBetterThan(match, bestMatch))
            {
                bestMatch = match;
                substitutedSeverity = condition;
            }
        }

        return substitutedSeverity != null
            ? new RuleDefinition(this, substitutedSeverity.GetType())
            : this;
    }

    public sealed class ConditionalSeverity
    {
        private readonly string? _domain;
        private readonly string? _context;
        private readonly Diagnostic.Type _type;

        public enum Match
        {
            None,
            Rule,
            Domain,
            Context,
            Exact
        }

        public ConditionalSeverity(string? domain, string? context, Diagnostic.Type type)
        {
            _domain = domain;
            _context = context;
            _type = type;
        }

        public Match Check(string? domain, string? context)
        {
            var hasDomain = _domain != null;
            var hasContext = _context != null;
            var isValidDomain = !hasDomain || string.Equals(_domain, domain, StringComparison.OrdinalIgnoreCase);
            var isValidContext = !hasContext || string.Equals(_context?.Replace("__", domain ?? string.Empty), context, StringComparison.OrdinalIgnoreCase);

            if (hasDomain && isValidDomain && hasContext && isValidContext)
            {
                return Match.Exact;
            }

            if (hasDomain && isValidDomain)
            {
                return Match.Domain;
            }

            if (hasContext && isValidContext)
            {
                return Match.Context;
            }

            return isValidDomain && isValidContext ? Match.Rule : Match.None;
        }

        public Diagnostic.Type GetType() => _type;

        public static bool IsBetterThan(Match match, Match other)
        {
            return match > other;
        }
    }
}
