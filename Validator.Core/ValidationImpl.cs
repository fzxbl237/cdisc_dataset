using System.Collections.Concurrent;
using P21.Validator.Api.Events;
using P21.Validator.Api.Events.Util;
using P21.Validator.Api.Models;
using P21.Validator.Api.Options;
using P21.Validator.Api.Validation;
using P21.Validator.Core.Engine;
using P21.Validator.Core.Report;
using P21.Validator.Core.Settings;
using P21.Validator.Core.Util;
using P21.Validator.Data;

namespace P21.Validator.Core;

public sealed class ValidationImpl : BaseValidation
{
    private static readonly Dispatcher Dispatcher = new();

    private readonly List<SourceOptions> _sources;
    private readonly ConfigOptions _config;
    private readonly ValidationOptions _options;
    private readonly TaskScheduler? _sharedScheduler;
    private readonly HashSet<DiagnosticListener> _diagnosticListeners = new();
    private readonly HashSet<RuleListener> _ruleListeners = new();
    private readonly HashSet<Action<ValidationEvent>> _eventListeners = new();

    public ValidationImpl(List<SourceOptions> sources, ConfigOptions config, ValidationOptions? options, TaskScheduler? sharedScheduler)
        : base(P21.Validator.Api.Models.CancellationToken.None)
    {
        _sources = sources;
        _config = config;
        _options = options ?? ValidationOptions.CreateBuilder().Build();
        _sharedScheduler = sharedScheduler;
    }

    public async Task<ValidationResult> ExecuteAsync(P21.Validator.Api.Models.CancellationToken? cancellationToken)
    {
        var scheduler = CreateScheduler();
        var factory = new DataEntryFactory(_options);
        var metrics = new WritableRuleMetricsImpl();
        var sourceProvider = new SourceProvider(factory);
        var lookupProvider = new ConcurrentLookupProvider(factory, sourceProvider, _eventListeners);

        var session = ValidationSession.Create(_options, cancellationToken, factory, new LookupProviderFactory(lookupProvider));
        var diagnostics = new List<Diagnostic>();
        var completedSuccessfully = true;

        var global = new InternalEntityDetails(SourceDetails.Reference.Data, new Dictionary<SourceDetails.Property, object>
        {
            [SourceDetails.Property.Name] = "GLOBAL",
            [SourceDetails.Property.Location] = string.Empty,
            [SourceDetails.Property.Label] = "Global Metadata"
        }, null);

        var defineCheck = new DefineCheck(metrics, global, _diagnosticListeners, _ruleListeners);

        var testSources = Task.Run(() => sourceProvider.Add(_sources));
        var parseConfiguration = Task.Run(() => CreateManager(session, metrics, defineCheck));

        var sourceDetails = new HashSet<SourceDetails> { global };

        var manager = await parseConfiguration.ConfigureAwait(false);
        await testSources.ConfigureAwait(false);

        var engine = new BlockValidator(session, _diagnosticListeners, _eventListeners, scheduler, manager.Prepare("GLOBAL", null, true)!);
        _diagnosticListeners.Add(new DiagnosticCollector(diagnostics));
        var sourceNames = sourceProvider.GetSourceNames();

        foreach (var sourceName in sourceNames)
        {
            var source = sourceProvider.GetSource(sourceName);
            sourceDetails.Add(source.GetDetails());
        }

        foreach (var sourceName in sourceNames)
        {
            if (session.ShouldCancel())
            {
                completedSuccessfully = false;
                break;
            }

            var source = sourceProvider.GetSource(sourceName);
            HashSet<string> variables;

            try
            {
                variables = source.GetVariables();
            }
            catch (P21.Validator.Data.InvalidDataException)
            {
                variables = new HashSet<string>();
            }

            var configuration = manager.Prepare(sourceName, variables, true);
            engine.Prepare(new EngineEntity(source, configuration));
        }

        Dispatcher.DispatchTo(_ruleListeners, listener => listener.Complete);
        defineCheck.Validate(_config);

        completedSuccessfully = completedSuccessfully && engine.Validate();

        return new ValidationResult(diagnostics, sourceDetails.ToList().AsReadOnly(), metrics, completedSuccessfully);
    }

    public override ValidationResult Run()
    {
        return ExecuteAsync(P21.Validator.Api.Models.CancellationToken.None).GetAwaiter().GetResult();
    }

    private ConfigurationManager CreateManager(ValidationSession session, WritableRuleMetrics metrics, RuleListener defineListener)
    {
        FileInfo? define = null;
        var definePath = _config.GetDefine();
        if (!string.IsNullOrEmpty(definePath))
        {
            define = new FileInfo(definePath);
        }

        var configFiles = _config.GetConfigs().Select(path => new FileInfo(path)).ToList();
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in _config.GetProperties())
        {
            properties[property] = _config.GetProperty(property);
        }

        var ruleListeners = new HashSet<RuleListener>(_ruleListeners) { defineListener };
        ConfigurationParser parser = new XmlConfigurationParser(configFiles, define, properties);
        var manager = new ConfigurationManager(session, metrics, ruleListeners, properties);
        parser.Parse(manager);
        return manager;
    }

    private TaskScheduler CreateScheduler()
    {
        if (_sharedScheduler != null)
        {
            return _sharedScheduler;
        }

        var processors = Math.Max(Environment.ProcessorCount - 1, 1);
        var threads = processors;

        if (_options.HasProperty("Engine.ThreadCount"))
        {
            var setting = _options.GetProperty("Engine.ThreadCount");
            if (setting.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                threads = processors;
            }
            else if (int.TryParse(setting, out var parsed))
            {
                threads = parsed;
            }
        }

        threads = Math.Clamp(threads, 1, processors);
        var scheduler = new LimitedConcurrencyLevelTaskScheduler(threads);
        return scheduler;
    }

    private sealed class DefineCheck : RuleListener
    {
        private const string DefinePresenceId = "DD0101";

        private readonly WritableRuleMetrics _metrics;
        private readonly SourceDetails _global;
        private RuleMetadata? _metadata;
        private readonly HashSet<DiagnosticListener> _diagnosticListeners;
        private readonly HashSet<RuleListener> _ruleListeners;

        public DefineCheck(WritableRuleMetrics metrics, SourceDetails global, HashSet<DiagnosticListener> diagnosticListeners, HashSet<RuleListener> ruleListeners)
        {
            _metrics = metrics;
            _global = global;
            _diagnosticListeners = diagnosticListeners;
            _ruleListeners = ruleListeners;
        }

        public void Validate(ConfigOptions options)
        {
            if (_metadata == null)
            {
                return;
            }

            var ruleMetric = _metrics.GetRule(_global.GetString(SourceDetails.Property.Name), SourceDetails.Reference.Data,
                DefinePresenceId, null, _metadata.GetMessage(), _metadata.GetType());

            ruleMetric.Start();

            var isDefineAbsent = string.IsNullOrWhiteSpace(options.GetDefine()) || !File.Exists(options.GetDefine());
            if (isDefineAbsent)
            {
                Dispatcher.DispatchTo(_diagnosticListeners, listener => listener.OnDiagnostic, new DiagnosticImpl(_metadata, _global, null!, new Dictionary<string, string>()));
            }

            ruleMetric.Stop(true, isDefineAbsent);
        }

        public void AcceptTemplate(RuleTemplate template)
        {
            if (template.GetId() == DefinePresenceId)
            {
                _metadata = new RuleMetadata(
                    _global.GetString(SourceDetails.Property.Name),
                    template.GetId(),
                    template.GetPublisherId(),
                    null,
                    template.GetCategory(),
                    template.GetMessage(),
                    template.GetDescription(),
                    template.GetType());

                Dispatcher.DispatchTo(_ruleListeners, listener => () => listener.AcceptInstance(_metadata));
            }
        }
    }

    private sealed class DiagnosticCollector : DiagnosticListener
    {
        private readonly List<Diagnostic> _diagnostics;

        public DiagnosticCollector(List<Diagnostic> diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public void OnDiagnostic(Diagnostic diagnostic)
        {
            _diagnostics.Add(diagnostic);
        }
    }
}
