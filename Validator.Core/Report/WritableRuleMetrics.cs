using P21.Validator.Api.Models;

namespace P21.Validator.Core.Report;

public interface WritableRuleMetrics : RuleMetrics
{
    RuleMetric GetRule(string name, SourceDetails.Reference reference, string id, string? context, string message, Diagnostic.Type type);

    public sealed class Scope
    {
        private readonly WritableRuleMetrics _metrics;
        private readonly string _domain;
        private readonly SourceDetails.Reference _reference;

        public Scope(WritableRuleMetrics metrics, string domain, SourceDetails.Reference reference)
        {
            _metrics = metrics;
            _domain = domain;
            _reference = reference;
        }

        public string GetDomain() => _domain;

        public SourceDetails.Reference GetReference() => _reference;

        public RuleMetric GetRule(string id, string? context, string message, Diagnostic.Type type)
        {
            return _metrics.GetRule(_domain, _reference, id, context, message, type);
        }
    }

    public interface RuleMetric
    {
        void Start();
        void Stop(bool executed, bool failed);
        string GetMessage();
        Diagnostic.Type GetType();
        SourceDetails.Reference GetReference();
        int GetInvocations();
        int GetExecutions();
        int GetFailures();
        long GetElapsed();
    }
}
