using System.Text.RegularExpressions;
using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public abstract class AbstractValidationRule : ValidationRule
{
    private readonly RuleMetadata _metadata;
    private readonly ValidationRule.Target _target;
    private readonly WritableRuleMetrics.RuleMetric _metrics;
    private readonly bool _isGenerating;
    private readonly bool _isUnqualifiedAllowed;
    private readonly byte _negation;
    private readonly HashSet<string>? _excludes;
    private readonly HashSet<string>? _includes;

    protected readonly ValidationSession Session;
    protected readonly HashSet<string> Variables = new(StringComparer.OrdinalIgnoreCase);

    protected AbstractValidationRule(RuleDefinition definition, ValidationSession session, ValidationRule.Target target, string[] required,
        WritableRuleMetrics.Scope metrics)
    {
        _metadata = new RuleMetadata(definition, metrics.GetDomain());
        _target = target;
        Session = session;
        _isGenerating = !definition.GetProperty("DisableMessaging").Equals("true", StringComparison.OrdinalIgnoreCase);
        _isUnqualifiedAllowed = definition.GetProperty("AllowUnqualifiedVariables").Equals("true", StringComparison.OrdinalIgnoreCase);
        _negation = (byte)(definition.GetProperty("Expect").Equals("false", StringComparison.OrdinalIgnoreCase) ? 1 : 0);

        _metrics = metrics.GetRule(_metadata.Id, _metadata.Context, _metadata.Message, _metadata.Type);

        if (definition.HasProperty("Display"))
        {
            var variables = definition.GetProperty("Display").Split(',');
            var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var variable in variables)
            {
                var value = variable.Trim().ToUpperInvariant();
                if (value.StartsWith("-", StringComparison.Ordinal))
                {
                    excludes.Add(value[1..]);
                }
                else
                {
                    if (value.StartsWith("+", StringComparison.Ordinal))
                    {
                        value = value[1..];
                    }

                    includes.Add(value);
                }
            }

            _includes = includes.Count > 0 ? includes : null;
            _excludes = excludes.Count > 0 ? excludes : null;
        }

        if (required != null)
        {
            var missingProperties = new List<string>();
            foreach (var requiredProperty in required)
            {
                if (!definition.HasProperty(requiredProperty))
                {
                    missingProperties.Add(requiredProperty);
                }
            }

            if (missingProperties.Count > 0)
            {
                throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                    string.Format(Text.Get("Exceptions.MissingAttribute"), definition.GetRuleType(), GetID(), string.Join(", ", missingProperties)));
            }
        }
    }

    public ValidationRule.Target GetTarget() => _target;

    public RuleMetadata GetMetadata() => _metadata;

    public string GetId() => _metadata.Id;

    public virtual bool Validate(DataRecord dataRecord, Action<Diagnostic>? reporter)
    {
        var entity = dataRecord.GetSourceDetails();
        if (_isUnqualifiedAllowed)
        {
            dataRecord = new UnqualifiedReferenceDataRecord(dataRecord);
        }

        if (_target == ValidationRule.Target.Record)
        {
            _metrics.Start();
        }

        foreach (var variable in Variables)
        {
            if (!dataRecord.DefinesVariable(variable))
            {
                throw new CorruptRuleException(CorruptRuleException.State.Unrecoverable, GetId(),
                    string.Format(Text.Get("Messages.MissingVariables"), GetId(), entity.GetString(SourceDetails.Property.Name)),
                    Text.Get("Descriptions.MissingVariables") + ": '" + variable + "'");
            }
        }

        var result = (byte)(PerformValidation(dataRecord) ^ _negation);
        if (result == 1 && _isGenerating && reporter != null)
        {
            reporter(new DiagnosticImpl(_metadata, entity, dataRecord.GetDataDetails(), PullRecordValues(dataRecord, Variables)));
        }

        if (_target == ValidationRule.Target.Record)
        {
            _metrics.Stop(result > 0, result == 1);
        }

        return result != 1;
    }

    public bool ValidateDataset(SourceDetails entity, Action<Diagnostic>? reporter)
    {
        var results = PerformDatasetValidation(entity);
        var failed = false;

        foreach (var resultState in results)
        {
            if (_target == ValidationRule.Target.Dataset)
            {
                _metrics.Start();
            }

            var result = (byte)(resultState.Result ^ _negation);
            if (result == 1)
            {
                failed = true;
                if (_isGenerating && reporter != null)
                {
                    var values = new Dictionary<string, string>(resultState.Display);
                    reporter(new DiagnosticImpl(_metadata, resultState.Entity ?? entity, null!, values));
                }
            }

            if (_target == ValidationRule.Target.Dataset)
            {
                _metrics.Stop(result > 0, result == 1);
            }
        }

        return !failed;
    }

    
    protected virtual byte PerformValidation(DataRecord record) => 0;

    protected virtual List<Outcome> PerformDatasetValidation(SourceDetails entity) => new() { new Outcome((byte)0) };

    protected void AddVariable(string variable) => Variables.Add(variable.ToUpperInvariant());

    protected IReadOnlyCollection<string> GetVariables() => Variables;

    public virtual void Setup(SourceDetails entity)
    {
    }

    public string GetID() => _metadata.Id;

    protected IDictionary<string, string> PullRecordValues(DataRecord record, HashSet<string> variables)
    {
        var selection = variables;
        if (_includes != null)
        {
            selection = new HashSet<string>(variables, StringComparer.OrdinalIgnoreCase);
            foreach (var variable in _includes)
            {
                selection.Add(variable);
            }
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in record.GetVariables())
        {
            if (!selection.Contains(variable))
            {
                continue;
            }

            if (_excludes != null && _excludes.Contains(variable))
            {
                continue;
            }

            if (!record.IsTransient(variable))
            {
                var entry = record.GetValue(variable);
                if (entry.HasValue)
                {
                    values[variable] = entry.ToString();
                }
                else
                {
                    values[variable] = string.Empty;
                }
            }
        }

        return values;
    }

    protected sealed class Outcome
    {
        public Outcome(byte result, SourceDetails? entity = null)
        {
            Result = result;
            Entity = entity;
        }

        public Dictionary<string, string> Display { get; } = new(StringComparer.OrdinalIgnoreCase);
        public byte Result { get; }
        public SourceDetails? Entity { get; }
    }

    private sealed class UnqualifiedReferenceDataRecord : DataRecord
    {
        private readonly DataRecord _record;

        public UnqualifiedReferenceDataRecord(DataRecord record)
        {
            _record = record;
        }

        public bool DefinesVariable(string variable)
        {
            return _record.DefinesVariable(variable) || _record.DefinesVariable(SubjectDataSupplement.SubjectVariablePrefix + variable);
        }

        public bool IsTransient(string variable)
        {
            return _record.DefinesVariable(variable)
                ? _record.IsTransient(variable)
                : _record.IsTransient(SubjectDataSupplement.SubjectVariablePrefix + variable);
        }

        public DataDetails GetDataDetails() => _record.GetDataDetails();

        public SourceDetails GetSourceDetails() => _record.GetSourceDetails();

        public DataEntry GetValue(string variable)
        {
            return _record.DefinesVariable(variable)
                ? _record.GetValue(variable)
                : _record.GetValue(SubjectDataSupplement.SubjectVariablePrefix + variable);
        }

        public IReadOnlyCollection<string> GetVariables() => _record.GetVariables();

        public int GetId() => _record.GetId();
    }
}
