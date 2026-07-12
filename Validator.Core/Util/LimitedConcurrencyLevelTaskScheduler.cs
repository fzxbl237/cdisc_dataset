using System.Collections.Concurrent;

namespace P21.Validator.Core.Util;

public sealed class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
{
    private readonly BlockingCollection<Task> _tasks = new();
    private readonly Thread[] _threads;

    public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
    {
        if (maxDegreeOfParallelism < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        }

        MaximumConcurrencyLevel = maxDegreeOfParallelism;
        _threads = new Thread[maxDegreeOfParallelism];

        for (var i = 0; i < _threads.Length; i++)
        {
            _threads[i] = new Thread(Execute)
            {
                IsBackground = true,
                Name = $"LimitedConcurrencyLevelTaskScheduler-{i}"
            };
            _threads[i].Start();
        }
    }

    public override int MaximumConcurrencyLevel { get; }

    protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

    protected override void QueueTask(Task task)
    {
        _tasks.Add(task);
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return false;
    }

    private void Execute()
    {
        foreach (var task in _tasks.GetConsumingEnumerable())
        {
            TryExecuteTask(task);
        }
    }
}
