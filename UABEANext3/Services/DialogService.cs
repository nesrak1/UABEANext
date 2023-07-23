using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEANext3.Views;

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
