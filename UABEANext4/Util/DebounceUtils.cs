using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace UABEANext4.Util;

public static class DebounceUtils
{
    public static Action<T> Debounce<T>(Action<T> func, int milliseconds = 300)
    {
        CancellationTokenSource? cancelTokenSource = null;

        return arg =>
        {
            cancelTokenSource?.Cancel();
            cancelTokenSource = new CancellationTokenSource();

            Task.Delay(milliseconds, cancelTokenSource.Token)
                .ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            func(arg);
                        });
                    }
                }, TaskScheduler.Default);
        };
    }
}
