using P21.Validator.Api.Models;
using System.Diagnostics;

namespace P21.Validator.Core.Report;

public sealed class WritableRuleMetricsImpl : WritableRuleMetrics
{
    private readonly Dictionary<string, Domain> _domains = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> GetDomains() => _domains.Keys.ToList();

    public IReadOnlyCollection<string> GetDomainRules(string name)
    {
        var domain = GetDomain(name);
        return domain == null ? Array.Empty<string>() : domain.Rules.Keys.ToList();
    }

    public IReadOnlyCollection<RuleInstance> GetRules()
    {
        return Array.Empty<RuleInstance>();
    }

    public long GetExecutionCount() => _domains.Keys.Sum(GetDomainExecutionCount);

    public long GetFailureCount() => _domains.Keys.Sum(GetDomainFailureCount);

    public long GetInvocationCount() => _domains.Keys.Sum(GetDomainInvocationCount);

    public long GetDomainExecutionCount(string domainName) => GetDomainExecutionCount(domainName, null);

    public long GetDomainExecutionCount(string domainName, Diagnostic.Type? type)
    {
        var domain = GetDomain(domainName);
        return domain == null ? 0 : domain.SumOver(metric => metric.GetExecutions(), type);
    }

    public long GetDomainFailureCount(string domainName) => GetDomainFailureCount(domainName, null);

    public long GetDomainFailureCount(string domainName, Diagnostic.Type? type)
    {
        var domain = GetDomain(domainName);
        return domain == null ? 0 : domain.SumOver(metric => metric.GetFailures(), type);
    }

    public long GetDomainInvocationCount(string domainName) => GetDomainInvocationCount(domainName, null);

    public long GetDomainInvocationCount(string domainName, Diagnostic.Type? type)
    {
        var domain = GetDomain(domainName);
        return domain == null ? 0 : domain.SumOver(metric => metric.GetInvocations(), type);
    }

    public Dictionary<string, IReadOnlyCollection<string>> GetDomainContexts(string domainName)
    {
        var domain = GetDomain(domainName);
        if (domain == null)
        {
            return new Dictionary<string, IReadOnlyCollection<string>>();
        }

        return domain.Rules.ToDictionary(rule => rule.Key, rule => (IReadOnlyCollection<string>)rule.Value.Keys.ToList());
    }

    public int GetRuleExecutionCount(string domainName, string id, string context)
    {
        var domain = GetDomain(domainName);
        return domain?.GetExecutionCount(id, context) ?? 0;
    }

    public int GetRuleFailureCount(string domainName, string id, string context)
    {
        var domain = GetDomain(domainName);
        return domain?.GetFailureCount(id, context) ?? 0;
    }

    public int GetRuleInvocationCount(string domainName, string id, string context)
    {
        var domain = GetDomain(domainName);
        return domain?.GetInvocationCount(id, context) ?? 0;
    }

    public long GetRuleElapsedCount(string domainName, string id, string context)
    {
        var domain = GetDomain(domainName);
        return domain?.GetElapsedTime(id, context) ?? 0;
    }

    public SourceDetails.Reference? GetRuleReference(string domainName, string id, string context)
    {
        var domain = GetDomain(domainName);
        return domain?.GetRule(id, context)?.GetReference();
    }

    public string? GetRuleMessage(string domainName, string id, string context)
    {
        var domain = GetDomain(domainName);
        return domain?.GetRule(id, context)?.GetMessage();
    }

    public Diagnostic.Type? GetRuleType(string domainName, string id, string context)
    {
        var domain = GetDomain(domainName);
        return domain?.GetRule(id, context)?.GetType();
    }

    private Domain? GetDomain(string domainName)
    {
        _domains.TryGetValue(domainName.ToUpperInvariant(), out var domain);
        return domain;
    }

    public WritableRuleMetrics.RuleMetric GetRule(string name, SourceDetails.Reference reference, string id, string? context, string message, Diagnostic.Type type)
    {
        Domain domain;
        lock (_domains)
        {
            domain = GetDomain(name) ?? new Domain();
            _domains[name] = domain;
        }

        return domain.GetRule(id, context ?? string.Empty, true, reference, message, type);
    }

    private sealed class Domain
    {
        public Dictionary<string, Dictionary<string, RuleMetricImpl>> Rules { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int GetExecutionCount(string id, string context)
        {
            var rule = GetRule(id, context);
            return rule?.GetExecutions() ?? 0;
        }

        public int GetFailureCount(string id, string context)
        {
            var rule = GetRule(id, context);
            return rule?.GetFailures() ?? 0;
        }

        public int GetInvocationCount(string id, string context)
        {
            var rule = GetRule(id, context);
            return rule?.GetInvocations() ?? 0;
        }

        public long GetElapsedTime(string id, string context)
        {
            var rule = GetRule(id, context);
            return rule?.GetElapsed() ?? 0;
        }

        public long SumOver(Func<RuleMetricImpl, int> selector, Diagnostic.Type? type)
        {
            return Rules.Values.SelectMany(m => m.Values)
                .Where(rule => type == null || rule.GetType() == type)
                .Select(selector)
                .Aggregate(0L, (acc, value) => acc + value);
        }

        public RuleMetricImpl? GetRule(string id, string context)
        {
            return GetRule(id, context, false, SourceDetails.Reference.Data, string.Empty, Diagnostic.Type.Warning);
        }

        public RuleMetricImpl GetRule(string id, string context, bool create, SourceDetails.Reference reference, string message, Diagnostic.Type type)
        {
            if (!Rules.TryGetValue(id, out var instances))
            {
                if (!create)
                {
                    return null!;
                }

                instances = new Dictionary<string, RuleMetricImpl>(StringComparer.OrdinalIgnoreCase);
                Rules[id] = instances;
            }

            if (!instances.TryGetValue(context, out var rule))
            {
                if (!create)
                {
                    return null!;
                }

                rule = new RuleMetricImpl(reference, message, type);
                instances[context] = rule;
            }

            return rule;
        }
    }

    private sealed class RuleMetricImpl : WritableRuleMetrics.RuleMetric
    {
        private readonly SourceDetails.Reference _reference;
        private readonly string _message;
        private readonly Diagnostic.Type _type;
        private int _invocations;
        private int _executions;
        private int _failures;
        private long _elapsed;
        private long _start;

    

        public RuleMetricImpl(SourceDetails.Reference reference, string message, Diagnostic.Type type)
        {
            _reference = reference;
            _message = message;
            _type = type;
        }

        public void Start() => _start = Stopwatch.GetTimestamp();

        public void Stop(bool executed, bool failed)
        {
            var finish = Stopwatch.GetTimestamp();
            _invocations++;

            if (executed)
            {
                _executions++;
                if (failed)
                {
                    _failures++;
                }
            }

            _elapsed += finish - _start;
        }

        public string GetMessage() => _message;
        public Diagnostic.Type GetType() => _type;
        public SourceDetails.Reference GetReference() => _reference;
        public int GetInvocations() => _invocations;
        public int GetExecutions() => _executions;
        public int GetFailures() => _failures;
        public long GetElapsed() => _elapsed;
    }
}
