using System;

namespace UABEANext4.Interfaces;
public interface IDialogAware<TResult>
{
    public string Title { get; }
    public int Width { get; }
    public int Height { get; }
    public event Action<TResult?> RequestClose;
}
