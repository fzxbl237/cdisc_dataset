namespace P21.Validator.Api.Events;

public interface ValidationEvent
{
    public enum Type
    {
        Configuring,
        Processing,
        Subprocessing
    }

    public enum State
    {
        Start,
        InProgress,
        Stop
    }

    long GetCurrent();
    long GetMaximum();
    string GetName();
    State GetState();
    ValidationEvent? GetSubevent();
    long GetTimestamp();
    Type GetType();
}
