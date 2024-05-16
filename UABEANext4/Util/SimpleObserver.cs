using System;

namespace UABEANext4.Util;

public class SimpleObserver<T> : IObserver<T>
{
    private readonly Action<T> _listener;
    public SimpleObserver(Action<T> listener) => _listener = listener;
    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public void OnNext(T value) => _listener(value);
}
