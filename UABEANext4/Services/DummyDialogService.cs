using System.Threading.Tasks;
using UABEANext4.Interfaces;

namespace UABEANext4.Services;
internal class DummyDialogService : IDialogService
{
    public void Show(IDialogAware viewModel)
    {
    }

    public void Show<TResult>(IDialogAware<TResult> viewModel)
    {
    }

    public Task ShowDialog(IDialogAware viewModel)
    {
        return Task.CompletedTask;
    }

    public Task<TResult?> ShowDialog<TResult>(IDialogAware<TResult> viewModel)
    {
        return Task.FromResult(default(TResult));
    }
}
