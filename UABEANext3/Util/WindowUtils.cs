using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using System;
using Avalonia;

namespace UABEANext3.Util
{
    public class WindowUtils
    {
        public static Window GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
            {
                return window;
            }

            throw new Exception("Window not found!");
        }
    }
}
