using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEANext3.Services
{
    public interface IDialogService
    {
        Task<TResult> ShowDialog<TResult>(Window newWindow);
    }
}
