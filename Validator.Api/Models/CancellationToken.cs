namespace P21.Validator.Api.Models;

public sealed class CancellationToken
{
    public static CancellationToken None { get; } = new();

    public bool IsCancelled { get; private set; }

    public bool IsCancellationRequested()
    {
        return IsCancelled;
    }

    public void Cancel()
    {
        IsCancelled = true;
    }
}
