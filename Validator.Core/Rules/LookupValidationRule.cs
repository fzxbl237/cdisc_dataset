using System.Text.RegularExpressions;
using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Rules.Expressions;
using P21.Validator.Core.Settings;
using P21.Validator.Core.Util;
using P21.Validator.Data;

namespace P21.Validator.Core.Rules;

public sealed class LookupValidationRule : AbstractScriptableValidationRule
{
    private static readonly string[] RequiredVariables = { "From" };

    private readonly bool _ignoreWhereFailure;
    private readonly KeyMap<Lookup> _lookups = new();
    private readonly LookupProvider _provider;
    private readonly PreparedQuery _query;

    public LookupValidationRule(RuleDefinition definition, ValidationSession token, WritableRuleMetrics.Scope metrics)
        : base(definition, token, ValidationRule.Target.Record, RequiredVariables, metrics)
    {
        if (!definition.HasProperty("Variable") && !definition.HasProperty("Search"))
        {
            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                "Lookup rules must have one of (Variable, Search)");
        }

        if (definition.HasProperty("Variable") && definition.HasProperty("Search"))
        {
            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                "Lookup rules can only have one of (Variable, Search)");
        }

        if (definition.HasProperty("When"))
        {
            PrepareExpression(definition.GetProperty("When"));
        }

        _ignoreWhereFailure = definition.HasProperty("WhereFailure") &&
                              definition.GetProperty("WhereFailure").Equals("Ignore", StringComparison.OrdinalIgnoreCase);

        var adapter = definition.HasProperty("Provider") ? definition.GetProperty("Provider") : null;

        try
        {
            _provider = Session.GetLookupProvider(adapter);
        }
        catch (ArgumentException ex)
        {
            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition, ex);
        }

        string search;
        var target = definition.GetProperty("From");
        var where = definition.GetProperty("Where");

        if (definition.HasProperty("Variable"))
        {
            search = definition.GetProperty("Variable")
                .Replace(",", " @and ")
                .Replace("== ", "== ")
                .Replace("==", "== ");
            search = Regex.Replace(search, "(==\\s*)([A-Za-z+][A-Za-z0-9]+)", "$1 [$2]");
        }
        else
        {
            search = definition.GetProperty("Search");
        }

        _query = new PreparedQuery(target, search, where, Session.GetDataEntryFactory(), adapter == null);

        foreach (var variable in _query.GetLocal())
        {
            AddVariable(variable);
        }

        if (_query.IsRequestable())
        {
            _provider.Request(_query.GetTarget(), _query.GetRemote());
        }
    }

    protected override List<Outcome> PerformDatasetValidation(SourceDetails entity)
    {
        foreach (var lookupName in _lookups.KeySet())
        {
            _lookups.Remove(lookupName);
        }

        return base.PerformDatasetValidation(entity);
    }

    protected override byte PerformValidation(DataRecord dataRecord)
    {
        if (!CheckExpression(dataRecord))
        {
            return 0;
        }

        var result = true;
        var remote = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var target = _query.GetTarget(dataRecord);

        if (!_lookups.ContainsKey(target) || _lookups.Get(target) != null)
        {
            var search = _query.GetSearch(dataRecord);
            var where = _query.GetWhere(dataRecord);

            foreach (var mapping in search)
            {
                remote.Add(mapping.GetRemote());
            }

            foreach (var mapping in where)
            {
                remote.Add(mapping.GetRemote());
            }

            Lookup? lookup;
            if (_lookups.ContainsKey(target))
            {
                lookup = _lookups.Get(target);
                if (lookup != null && !lookup.Contains(remote))
                {
                    lookup = _provider.Get(target, remote);
                    if (lookup == null)
                    {
                        throw MissingLookup(target);
                    }

                    _lookups.Put(target, lookup);
                }
            }
            else
            {
                lookup = _provider.Get(target, remote);
                _lookups.Put(target, lookup!);

                if (lookup == null)
                {
                    throw MissingLookup(target);
                }
            }

            result = lookup!.Seek(search, where, _ignoreWhereFailure);
        }

        return (byte)(result ? 2 : 1);
    }

    private CorruptRuleException MissingLookup(string target)
    {
        var cleaned = Regex.Replace(target, "^FILE:([A-Z-]{2,}:)?", string.Empty);
        return new CorruptRuleException(CorruptRuleException.State.Temporary, GetID(),
            string.Format(Text.Get("Messages.MissingLookup"), cleaned),
            string.Format(Text.Get("Descriptions.MissingLookup"), GetID(), cleaned));
    }
}
