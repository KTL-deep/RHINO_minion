using Rhino;

namespace RhinoMinion.Plugin.Bridge;

internal static class UiThread
{
    public static Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>();
        cancellationToken.Register(() => completion.TrySetCanceled());
        RhinoApp.InvokeOnUiThread((Action)(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled();
                return;
            }

            try
            {
                completion.TrySetResult(action());
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }));
        return completion.Task;
    }
}
