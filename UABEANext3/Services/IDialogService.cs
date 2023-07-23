using Avalonia.Controls;
using System.Threading.Tasks;

namespace UABEANext3.Services
{
    public interface IDialogService
    {
        Task<TResult> ShowDialog<TResult>(Window newWindow);
    }
}
