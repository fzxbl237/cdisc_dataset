using P21.Validator.Api.Events;

namespace P21.Validator.Core.Util;

public static class ValidationEvents
{
    private const int NullInt = -1;

    public static ValidationEvent ConfigurationStartEvent(string configurationPath)
    {
        return new ValidationEventImpl(ValidationEvent.Type.Configuring, ValidationEvent.State.Start,
            configurationPath, NullInt, NullInt, null);
    }

    public static ValidationEvent ConfigurationStopEvent(string configurationPath)
    {
        return new ValidationEventImpl(ValidationEvent.Type.Configuring, ValidationEvent.State.Stop,
            configurationPath, NullInt, NullInt, null);
    }

    public static ValidationEvent ProcessingStartEvent(string name, long currentTask, long totalTasks)
    {
        return new ValidationEventImpl(ValidationEvent.Type.Processing, ValidationEvent.State.Start,
            name, currentTask, totalTasks, null);
    }

    public static ValidationEvent ProcessingIncrementEvent(string name, long currentTask, long totalTasks, long recordCount)
    {
        var subevent = new ValidationEventImpl(ValidationEvent.Type.Processing, ValidationEvent.State.InProgress,
            name, recordCount, NullInt, null);

        return new ValidationEventImpl(ValidationEvent.Type.Processing, ValidationEvent.State.InProgress, name,
            currentTask, totalTasks, subevent);
    }

    public static ValidationEvent ProcessingStopEvent(string name, long currentTask, long totalTasks)
    {
        return new ValidationEventImpl(ValidationEvent.Type.Processing, ValidationEvent.State.Stop,
            name, currentTask, totalTasks, null);
    }

    public static ValidationEvent SubprocessingStartEvent(string name)
    {
        return new ValidationEventImpl(ValidationEvent.Type.Subprocessing, ValidationEvent.State.Start, name,
            NullInt, NullInt, null);
    }

    public static ValidationEvent SubprocessingIncrementEvent(string name, long recordCount)
    {
        return new ValidationEventImpl(ValidationEvent.Type.Subprocessing, ValidationEvent.State.InProgress, name,
            recordCount, NullInt, null);
    }

    public static ValidationEvent SubprocessingStopEvent(string name)
    {
        return new ValidationEventImpl(ValidationEvent.Type.Subprocessing, ValidationEvent.State.Stop, name,
            NullInt, NullInt, null);
    }

    private sealed class ValidationEventImpl : ValidationEvent
    {
        private readonly long _timestamp;
        private readonly long _current;
        private readonly long _maximum;
        private readonly string _name;
        private readonly ValidationEvent.Type _type;
        private readonly ValidationEvent.State _state;
        private readonly ValidationEvent? _subevent;

        public ValidationEventImpl(ValidationEvent.Type type, ValidationEvent.State state, string name, long current, long maximum,
            ValidationEvent? subevent)
        {
            _timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _type = type;
            _state = state;
            _name = name;
            _current = current;
            _maximum = maximum;
            _subevent = subevent;
        }

        public long GetTimestamp() => _timestamp;
        public long GetCurrent() => _current;
        public long GetMaximum() => _maximum;
        public string GetName() => _name;
        public ValidationEvent.Type GetType() => _type;
        public ValidationEvent.State GetState() => _state;
        public ValidationEvent? GetSubevent() => _subevent;
    }
}
