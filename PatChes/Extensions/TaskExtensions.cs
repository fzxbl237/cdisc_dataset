using System;
using System.Threading.Tasks;

namespace PatChes.Extensions;

public static class TaskExtensions
{
    public static async void AwaitWithOpt(this Task task, Action? onCompleted = null, Action<Exception>? onError = null)
    {
        try
        {
            await task;
            onCompleted?.Invoke();
        }
        catch (Exception e)
        {
            onError?.Invoke(e);
        }
    }
    
    
}