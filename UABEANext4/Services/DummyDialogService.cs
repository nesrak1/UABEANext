using System.Threading.Tasks;
using UABEANext4.Interfaces;

namespace UABEANext4.Services;
internal class DummyDialogService : IDialogService
{
    public Task<TResult?> ShowDialog<TResult>(IDialogAware<TResult> viewModel)
    {
        return Task.FromResult(default(TResult));
    }
}
