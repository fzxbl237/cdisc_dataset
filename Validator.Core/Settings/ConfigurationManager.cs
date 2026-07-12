using P21.Validator.Api.Events;
using P21.Validator.Api.Events.Util;
using P21.Validator.Api.Models;
using P21.Validator.Core.Report;
using P21.Validator.Core.Rules;
using P21.Validator.Core.Util;

namespace P21.Validator.Core.Settings;

public sealed class ConfigurationManager
{
    private static readonly Dispatcher Dispatcher = new();
    private static readonly KeyMap<Type> RuleTypes = new();

    static ConfigurationManager()
    {
        RuleTypes.Put("Condition", typeof(ConditionalValidationRule));
        RuleTypes.Put("Find", typeof(FindValidationRule));
        RuleTypes.Put("Lookup", typeof(LookupValidationRule));
        RuleTypes.Put("Match", typeof(MatchValidationRule));
        RuleTypes.Put("Metadata", typeof(MetadataValidationRule));
        RuleTypes.Put("Unique", typeof(UniqueValueValidationRule));
        RuleTypes.Put("Regex", typeof(RegularExpressionValidationRule));
        RuleTypes.Put("Required", typeof(ConditionalRequiredValidationRule));
        RuleTypes.Put("Property", typeof(DomainPropertyValidationRule));
        RuleTypes.Put("Varlength", typeof(VariableLengthValidationRule));
        RuleTypes.Put("Varorder", typeof(VariableOrderValidationRule));
    }

    private readonly HashSet<ConfigurationMetadata> _metadata = new();
    private readonly KeyMap<Template> _configurations = new();
    private readonly KeyMap<Template> _prototypes = new();
    private readonly ValidationSession _token;
    private readonly WritableRuleMetrics _metrics;
    private readonly HashSet<RuleListener> _ruleListeners;
    private readonly Dictionary<string, string> _defaults;

    public ConfigurationManager(ValidationSession token, WritableRuleMetrics metrics, HashSet<RuleListener> ruleListeners, Dictionary<string, string> defaults)
    {
        _token = token;
        _metrics = metrics;
        _ruleListeners = ruleListeners;
        _defaults = defaults;
    }

    public void Complete()
    {
        foreach (var configuration in _configurations.Values())
        {
            configuration.Complete();
        }
    }

    public void Define(Template configuration)
    {
        _configurations.Put(configuration.GetTargetName(), configuration);
    }

    public bool Defines(string name) => _configurations.ContainsKey(name);

    public Template? GetConfiguration(string name) => _configurations.Get(name);

    public IReadOnlyCollection<ConfigurationMetadata> GetMetadata() => _metadata;

    public Template? GetPrototype(string name) => _prototypes.Get(name);

    public void Register(ConfigurationMetadata metadata) => _metadata.Add(metadata);

    public void Store(Definition rule)
    {
        Diagnostic.Type? type = null;
        if (rule.HasProperty("Type"))
        {
            type = Diagnostic.FromString(rule.GetProperty("Type"));
        }

        Dispatcher.DispatchTo(_ruleListeners, listener => () => listener.AcceptTemplate(new RuleMetadata(
            rule.GetProperty("ID"),
            rule.GetProperty("PublisherID"),
            rule.GetProperty("Category"),
            rule.GetProperty("Message"),
            rule.GetProperty("Description"),
            type ?? Diagnostic.Type.Warning
        )));
    }

    public Configuration? Prepare(string name, HashSet<string> variables, bool createRules)
    {
        Template? template = null;
        var ruleTemplateName = name;

        if (_configurations.ContainsKey(name))
        {
            template = _configurations.Get(name);
        }

        if ((template == null || !template.IsConfiguration()) && variables != null)
        {
            Template? prototype = null;
            var matches = 0;

            foreach (var candidate in _prototypes.Values())
            {
                var count = candidate.Matches(name, variables);
                if (count != 0 && (matches == 0 || count > matches))
                {
                    matches = count;
                    prototype = candidate;
                }
            }

            if (prototype != null)
            {
                if (template == null)
                {
                    template = prototype;
                }
                else
                {
                    foreach (var target in Enum.GetValues<SourceDetails.Reference>())
                    {
                        foreach (var ruleDefinition in prototype.GetRules(target))
                        {
                            template.DefineRule(target, ruleDefinition);
                        }
                    }

                    prototype.UpdateVariables(template);
                    template.MarkConfigured();
                    template.Complete();
                }

                ruleTemplateName = prototype.GetProperty("Name").ToUpperInvariant();
            }
        }

        Configuration? configuration = null;
        if (template != null && template.IsConfiguration())
        {
            configuration = template.CreateFrom(name, variables);
            if (createRules)
            {
                foreach (var target in Enum.GetValues<SourceDetails.Reference>())
                {
                    var iReadOnlyCollection = template.GetRules(target);
                    foreach (var ruleDefinition in iReadOnlyCollection)
                    {
                        foreach (var rule in PrepareRule(ruleDefinition, configuration, target))
                        {
                            configuration.DefineRule(target, rule);
                        }
                    }
                }
            }
        }

        if (configuration != null)
        {
            configuration.SetProperty("Configuration", ruleTemplateName);
        }

        return configuration;
    }

    public void Prototype(Template prototype)
    {
        _prototypes.Put(prototype.GetTargetName(), prototype);
    }

    public bool Prototypes(string name) => _prototypes.ContainsKey(name);

    private ValidationRule InstantiateRule(Configuration configuration, SourceDetails.Reference reference, RuleDefinition rule)
    {
        var ruleClass = RuleTypes.Get(rule.GetRuleType()) as Type;
        if (ruleClass == null)
        {
            throw new InvalidOperationException($"Unknown rule type: {rule.GetRuleType()}");
        }

        var constructor = ruleClass.GetConstructor(new[] { typeof(RuleDefinition), typeof(ValidationSession), typeof(WritableRuleMetrics.Scope) });
        if (constructor == null)
        {
            throw new InvalidOperationException($"Missing constructor for {ruleClass.Name}");
        }

        var scope = new WritableRuleMetrics.Scope(_metrics, configuration.GetTargetName(), reference);
        return (ValidationRule)constructor.Invoke(new object?[] { rule, _token, scope });
    }

    private List<RuleDefinition> PerformMagic(RuleDefinition rule, Configuration configuration)
    {
        var rules = new List<RuleDefinition> { rule };

        foreach (MagicVariableParser.MagicProperty magicProperty in Enum.GetValues<MagicVariableParser.MagicProperty>())
        {
            var templates = rules.ToList();
            rules.Clear();

            foreach (var template in templates)
            {
                var scope = new MagicVariableParser();
                MagicVariable? magicVariable = null;

                foreach (var property in magicProperty.GetProperties())
                {
                    if (template.HasProperty(property))
                    {
                        var magicVariables = scope.Parse(magicProperty, $"{template.GetId()}.{property}", template.GetProperty(property));
                        if (magicVariables.Count > 1 || (magicVariables.Count != 0 && magicVariable != null))
                        {
                            throw new ConfigurationException(ConfigurationException.Type.RuleDefinition,
                                "Only one magic variable per rule definition is allowed");
                        }

                        if (magicVariables.Count > 0)
                        {
                            magicVariable = magicVariables[0];
                        }
                    }
                }

                if (magicVariable != null)
                {
                    var contextVariables = new List<Definition>();
                    if (magicProperty == MagicVariableParser.MagicProperty.Variables)
                    {
                        foreach (var candidateVariable in configuration.GetVariables())
                        {
                            if (magicVariable.Matches(candidateVariable))
                            {
                                contextVariables.Add(candidateVariable);
                            }
                        }
                    }
                    else if (magicProperty == MagicVariableParser.MagicProperty.Domains)
                    {
                        foreach (var configurationName in _configurations.KeySet())
                        {
                            var candidateVariable = _configurations.Get(configurationName);
                            if (candidateVariable == null)
                            {
                                continue;
                            }

                            var proxy = new Definition(Definition.Target.Domain, candidateVariable.GetProperty("Name"));
                            proxy.SetProperty("From", candidateVariable.GetProperty("Name"));
                            proxy.SetProperty("Name", configuration.GetTargetName());
                            var combined = Definition.CreateFrom(proxy, candidateVariable);
                            var castTemplate = new Template(combined.GetTargetName(), null);
                            Definition.CopyTo(combined, castTemplate, false);
                            candidateVariable = castTemplate;

                            if (magicVariable.Matches(candidateVariable))
                            {
                                contextVariables.Add(candidateVariable);
                            }
                        }
                    }

                    if (contextVariables.Count > 0)
                    {
                        rules.AddRange(scope.Prepare(template, magicVariable, contextVariables, _defaults));
                    }
                }
                else
                {
                    rules.Add(template);
                }
            }
        }

        return rules;
    }

    private List<ValidationRule> PrepareRule(RuleDefinition rule, Configuration configuration, SourceDetails.Reference reference)
    {
        var rules = new List<ValidationRule>();
        var domain = configuration.GetTargetName();

        foreach (var property in rule.GetProperties())
        {
            rule = rule.WithProperty(property, rule.GetProperty(property).Replace("%Domain%", domain));
        }

        try
        {
            // var validationOptions = _token.GetOptions();
            // var autoDisplayDomainKeys = bool.Parse(validationOptions.GetProperty("Parser.AutoDisplayDomainKeys"));
            bool autoDisplayDomainKeys = true;
            var ruleDefinitions = PerformMagic(rule, configuration);
            foreach (var contextualRule in PerformMagic(rule, configuration))
            {
                var currentRule = contextualRule;
                if (autoDisplayDomainKeys &&
                    !currentRule.HasProperty("Display") &&
                    !currentRule.GetProperty("Category").Equals("Terminology", StringComparison.OrdinalIgnoreCase))
                {
                    currentRule = currentRule.WithProperty("Display", configuration.GetProperty("DomainKeys"));
                }

                currentRule = currentRule.WithSeverityFor(domain);
                var createdRule = InstantiateRule(configuration, reference, currentRule);
                Dispatcher.DispatchTo(_ruleListeners, listener => () => listener.AcceptInstance(createdRule.GetMetadata()));
                rules.Add(createdRule);
            }
        }
        catch (Exception ex)
        {
            throw new RuntimeException(ex.Message, ex);
        }

        return rules;
    }

    public static bool IsValidRuleType(string type) => RuleTypes.ContainsKey(type);

    public sealed class ConfigurationMetadata
    {
        public ConfigurationMetadata(string standardName, string standardVersion)
        {
            StandardName = standardName;
            StandardVersion = standardVersion;
        }

        public string StandardName { get; }
        public string StandardVersion { get; }
    }
}
