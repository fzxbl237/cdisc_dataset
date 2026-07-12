using System.Collections.Concurrent;
using P21.Validator.Api.Events;
using P21.Validator.Api.Events.Util;
using P21.Validator.Api.Models;
using P21.Validator.Core.Rules;
using P21.Validator.Core.Settings;
using P21.Validator.Core.Util;
using P21.Validator.Data;
using Serilog;
using InvalidDataException = P21.Validator.Data.InvalidDataException;

namespace P21.Validator.Core.Engine;

public sealed class BlockValidator : ValidationEngine
{
    private static readonly Dispatcher Dispatcher = new();
    //private static readonly ILogger Logger = Log.ForContext<BlockValidator>();

    private readonly HashSet<EngineEntity> _entities = new();
    private readonly HashSet<DiagnosticListener> _diagnosticListeners;
    private readonly HashSet<Action<ValidationEvent>> _eventListeners;
    private readonly ValidationSession _session;
    private readonly TaskScheduler _scheduler;
    private readonly Configuration _global;
    private readonly List<DataSupplement> _supplements = new();

    public BlockValidator(ValidationSession session, HashSet<DiagnosticListener> diagnosticListeners, HashSet<Action<ValidationEvent>> eventListeners, TaskScheduler scheduler, Configuration global)
    {
        _session = session;
        _diagnosticListeners = diagnosticListeners;
        _eventListeners = eventListeners;
        _scheduler = scheduler;
        _global = global;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() // 设置最低日志级别
            .WriteTo.Console()    // 关键：仅写入控制台
            .CreateLogger();
    }

    public void Prepare(EngineEntity entity)
    {
        _entities.Add(entity);

        var details = entity.GetDetails();
        var name = details.GetString(SourceDetails.Property.Name);

        try
        {
            if (name.Equals("DM", StringComparison.OrdinalIgnoreCase))
            {
                _supplements.Add(new SubjectDataSupplement(entity.GetSource()));
            }
            else if (name.Equals("TS", StringComparison.OrdinalIgnoreCase))
            {
                _supplements.Add(new GlobalDataSupplement(entity.GetSource()));
            }
        }
        catch (InvalidDataException ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    public bool Validate()
    {
        var tasks = new List<Task<bool>>();
        var global = new CombinedDataSource("GLOBAL", "System", _session.GetDataEntryFactory());
        var itemCount = 0;

        foreach (var entity in _entities)
        {
            itemCount++;
            var task = Task.Run(() => new BlockTask(entity, itemCount, this).Call());
            tasks.Add(task);
            //tasks.Add(new Task<bool>(() => new BlockTask(entity, itemCount, this).Call()));

            if (!entity.GetDetails().GetBoolean(SourceDetails.Property.Corrupted, false))
            {
                var task1 = Task.Run(() => new BlockTask(new EngineEntity(entity.GetSource().GetMetadata(), entity.GetConfiguration()!), itemCount, this).Call());
                tasks.Insert(0,task1);
                //tasks.Insert(0, new Task<bool>(() => new BlockTask(new EngineEntity(entity.GetSource().GetMetadata(), entity.GetConfiguration()!), itemCount, this).Call()));

                try
                {
                    global.Add(entity.GetSource().GetMetadata().Replicate());
                }
                catch (InvalidDataException)
                {
                }
            }
        }

        if (tasks.Count > 0)
        {
            //tasks.Insert(0, new Task<bool>(() => new BlockTask(new EngineEntity(global, _global), 0, this).Call()));
            tasks.Insert(0, Task.Run(() => new BlockTask(new EngineEntity(global, _global), 0, this).Call()));
            //var scheduled = tasks.Select(task => Task.Factory.StartNew(task.Start, System.Threading.CancellationToken.None, TaskCreationOptions.None, _scheduler)).ToArray();

            Task.WaitAll(tasks);
            foreach (var task in tasks)
            {
                if (!task.IsCompletedSuccessfully)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private sealed class BlockTask
    {
        private readonly int _id;
        private readonly BlockValidator _blockValidator;
        private readonly EngineEntity _entity;
        private static readonly ILogger Logger = Log.ForContext<BlockTask>();

        public BlockTask(EngineEntity entity, int taskNumber, BlockValidator blockValidator)
        {
            _id = taskNumber;
            this._blockValidator = blockValidator;
            _entity = entity;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug() // 设置最低日志级别
                .WriteTo.Console()    // 关键：仅写入控制台
                .CreateLogger();
        }

        public bool Call()
        {

            var details = _entity.GetDetails();
            var source = _entity.GetSource();
            var sourceName = details.GetString(SourceDetails.Property.Name);
            var isGlobal = sourceName.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase);

            Dispatcher.DispatchTo(_blockValidator._eventListeners, listener => () => listener(ValidationEvents.ProcessingStartEvent(sourceName, _id, _blockValidator._entities.Count)));

            var completedSuccessfully = false;


            try
            {
                var filters = _entity.GetFilters();
                var rules = _entity.GetRules();

                foreach (var filter in filters)
                {
                    filter.Setup(details);
                }

                foreach (var rule in rules)
                {
                    rule.Setup(details);
                }
  
                ValidateRecords(source, details, isGlobal, filters, rules);
                ValidateDataset(details, rules);

                details.SetProperty(SourceDetails.Property.Validated, true);
                completedSuccessfully = true;
            }
            catch (OperationCanceledException e)
            {
                Log.Error(e.Message);
                throw new Exception(e.Message, e);
            }
            catch(Exception e)
            {
                Log.Error(e.Message);
            }
            finally
            {
                Dispatcher.DispatchTo(_blockValidator._eventListeners, listener => () => listener(ValidationEvents.ProcessingStopEvent(sourceName, _id, _blockValidator._entities.Count)));
            }

            return completedSuccessfully;
        }

        private void ValidateRecords(DataSource source, InternalEntityDetails details, bool isGlobal, IReadOnlyCollection<ValidationRule> filters, IReadOnlyCollection<ValidationRule> rules)
        {
            var totalValidatedRecords = 0;
            var totalExaminedRecords = 0;
            var isFiltered = false;

            try
            {
                while (source.HasRecords())
                {
                    var diagnostics = new List<Diagnostic>();
                    foreach (var record in source.GetRecords())
                    {
                        EnsureAlive();
                        totalExaminedRecords++;

                        var copy = record;

                        if (!source.IsMetadata() && !isGlobal)
                        {
                            foreach (var supplement in _blockValidator._supplements)
                            {
                                copy = supplement.Augment(record);
                            }
                        }

                        if (filters.Count > 0)
                        {
                            var filtered = false;
                            foreach (var filter in filters)
                            {
                                EnsureAlive();

                                try
                                {
                                    filtered = !filter.Validate(copy, null);
                                }
                                catch (CorruptRuleException ex) when (ex.CurrentState == CorruptRuleException.State.Unrecoverable)
                                {
                                    filtered = true;
                                }

                                if (filtered)
                                {
                                    isFiltered = true;
                                    break;
                                }
                            }

                            if (filtered)
                            {
                                continue;
                            }
                        }

                        foreach (var rule in rules)
                        {
                            EnsureAlive();

                            try
                            {
                                rule.Validate(copy, diagnostics.Add);
                            }
                            catch (CorruptRuleException e)
                            {
                                
                                //TODO EXCEPTION can not be print
                                Logger.Information(e.Message);
                            }
                        }

                        totalValidatedRecords++;
                    }

                    foreach (var item in diagnostics)
                    {
                        Dispatcher.DispatchTo(_blockValidator._diagnosticListeners, listener => listener.OnDiagnostic, item);
                    }
                    //Dispatcher.DispatchTo(_blockValidator._diagnosticListeners, listener =>listener.OnDiagnostic, diagnostics);
                    Dispatcher.DispatchTo(_blockValidator._eventListeners, listener => () => listener(ValidationEvents.ProcessingIncrementEvent(source.GetName(), _id, _blockValidator._entities.Count, totalExaminedRecords)));
                }
            }
            catch (InvalidDataException)
            {
            }
            finally
            {
                details.SetProperty(SourceDetails.Property.Filtered, isFiltered);
            }
        }

        private void ValidateDataset(InternalEntityDetails details, IReadOnlyCollection<ValidationRule> rules)
        {
            var diagnostics = new List<Diagnostic>();
            foreach (var rule in rules)
            {
                EnsureAlive();
                rule.ValidateDataset(details, diagnostics.Add);
            }

            foreach (var diagnostic in diagnostics)
            {
                Dispatcher.DispatchTo(_blockValidator._diagnosticListeners, listener => listener.OnDiagnostic, diagnostic);
            }
        }

        private void EnsureAlive()
        {
            if (_blockValidator._session.ShouldCancel())
            {
                throw new OperationCanceledException();
            }
        }
    }

    private int _entityCount => _entities.Count;
}
