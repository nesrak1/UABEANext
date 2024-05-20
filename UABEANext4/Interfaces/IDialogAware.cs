using System;

namespace UABEANext4.Interfaces;

public interface IDialogAware
{
    public string Title { get; }
    public int Width { get; }
    public int Height { get; }
}

public interface IDialogAware<TResult> : IDialogAware
{
    public event Action<TResult?> RequestClose;
}
