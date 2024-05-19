using System.Threading.Tasks;
using UABEANext4.Interfaces;

namespace UABEANext4.Services;
public interface IDialogService
{
    Task ShowDialog(IDialogAware viewModel);
    
    Task<TResult?> ShowDialog<TResult>(IDialogAware<TResult> viewModel);
}
