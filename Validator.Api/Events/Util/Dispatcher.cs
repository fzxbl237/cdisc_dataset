namespace P21.Validator.Api.Events.Util;

public sealed class Dispatcher
{
    public void DispatchTo<T>(IEnumerable<T> listeners, Func<T, Action> mapping)
    {
        foreach (var listener in listeners)
        {
            var action = mapping(listener);
            action?.Invoke();
        }
    }

    public void DispatchTo<T, TArg>(IEnumerable<T> listeners, Func<T, Action<TArg>> mapping, TArg arg)
    {
        foreach (var listener in listeners)
        {
            var action = mapping(listener);
            action?.Invoke(arg);
        }
    }
}
