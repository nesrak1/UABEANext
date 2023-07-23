using Avalonia.Controls;
using System.Threading.Tasks;

namespace UABEANext3.Services
{
    public class DialogService : IDialogService
    {
        private readonly Window _mainWindow;

        public DialogService(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public async Task<TResult> ShowDialog<TResult>(Window newWindow)
        {
            var dialogResult = await newWindow.ShowDialog<TResult>(_mainWindow);
            return dialogResult;
        }
    }
}
