using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEANext4.Util;
public static class ApplicationExtensions
{
    /// <summary>
    /// Returns the TopLevel from the main window or view. 
    /// </summary>
    /// <param name="app">The application to get the TopLevel for.</param>
    /// <returns>A TopLevel object.</returns>
    public static TopLevel? GetTopLevel(this Application? app)
    {
        if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
        {
            var visualRoot = mainView.GetVisualRoot();
            if (visualRoot is TopLevel topLevel)
            {
                return topLevel;
            }
        }
        return null;
    }

    public static async Task CopyToClipboard(string text)
    {
        var topLevel = Application.Current.GetTopLevel();
        if (topLevel?.Clipboard != null)
        {
            await topLevel.Clipboard.SetTextAsync(text);
        }
    }

    public static string GetInconPath(string iconName)
    {
        return $"avares://UABEANext4/Assets/Icons/{iconName}";
    }
}
