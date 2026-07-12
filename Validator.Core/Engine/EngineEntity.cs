using P21.Validator.Api.Models;
using P21.Validator.Core.Rules;
using P21.Validator.Core.Settings;
using P21.Validator.Data;

namespace P21.Validator.Core.Engine;

public sealed class EngineEntity
{
    private readonly Configuration? _configuration;
    private readonly DataSource _source;
    private readonly SourceDetails.Reference? _reference;

    public EngineEntity(DataSource source, Configuration? configuration)
    {
        _source = source;
        _configuration = configuration;

        if (_configuration != null)
        {
            var details = _source.GetDetails();
            details.SetProperty(SourceDetails.Property.Keys, configuration.GetProperty("DomainKeys"));
            details.SetProperty(SourceDetails.Property.Class, configuration.GetProperty("Class"));
            details.SetProperty(SourceDetails.Property.Configuration, configuration.GetProperty("Configuration"));

            if (!details.HasProperty(SourceDetails.Property.Label))
            {
                details.SetProperty(SourceDetails.Property.Label, configuration.GetProperty("Label"));
            }

            _reference = details.GetReference();
        }
        else
        {
            _reference = null;
        }
    }

    public Configuration? GetConfiguration() => _configuration;

    public InternalEntityDetails GetDetails() => _source.GetDetails();

    public IReadOnlyCollection<ValidationRule> GetFilters()
    {
        if (_configuration == null || _reference == SourceDetails.Reference.Metadata)
        {
            return Array.Empty<ValidationRule>();
        }

        return _configuration.GetFilters();
    }

    public IReadOnlyCollection<ValidationRule> GetRules()
    {
        if (_configuration == null || _reference == null)
        {
            return Array.Empty<ValidationRule>();
        }

        return _configuration.GetRules(_reference.Value);
    }

    public DataSource GetSource() => _source;
}
