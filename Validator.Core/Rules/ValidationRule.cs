using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public interface ValidationRule
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RuleAssociation : Attribute
    {
        public RuleAssociation(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    public enum Target
    {
        Record,
        Dataset
    }

    string GetId();
    string GetID() => GetId();
    Target GetTarget();
    RuleMetadata GetMetadata();
    void Setup(SourceDetails entity);

    bool Validate(DataRecord dataRecord, Action<Diagnostic>? consumer);
    bool ValidateDataset(SourceDetails entity, Action<Diagnostic>? consumer);

    bool Validate(DataRecord dataRecord) => Validate(dataRecord, null);
    bool ValidateDataset(SourceDetails entity) => ValidateDataset(entity, null);
}
