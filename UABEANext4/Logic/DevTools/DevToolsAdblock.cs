using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Collections;
using System.Reflection;

namespace UABEANext4.Logic.DevTools;
public static class DevToolsAdblock
{
#if DEBUG
    public static IDisposable Attach(TopLevel root, KeyGesture gesture)
    {
        return Attach(root, new DevToolsOptions()
        {
            Gesture = gesture,
        });
    }

    public static IDisposable Attach(TopLevel root, DevToolsOptions options)
    {
        void PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (options.Gesture.Matches(e))
            {
                var dtAsm = typeof(DevToolsOptions).Assembly;
                var devTools = dtAsm.GetType("Avalonia.Diagnostics.DevTools");
                if (devTools is null)
                    return;

                var open = devTools.GetMethod(
                    "Open",
                    BindingFlags.Static | BindingFlags.Public,
                    [typeof(TopLevel), typeof(DevToolsOptions)]
                );
                if (open is null)
                    return;

                open.Invoke(null, [root, options]);

                var s_open = devTools.GetField(
                    "s_open",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                if (s_open is null)
                    return;

                if (s_open.GetValue(null) is not IDictionary sOpenVal)
                    return;

                foreach (var value in sOpenVal.Values)
                {
                    if (value is not ContentControl contentCtrl)
                        continue;

                    if (contentCtrl.Content is not UserControl content)
                        continue;

                    var rootGrid = content.FindControl<Grid>("rootGrid");
                    if (rootGrid is null)
                        continue;

                    var hyperlinkButton = rootGrid.FindDescendantOfType<HyperlinkButton>();
                    if (hyperlinkButton is null)
                        continue;

                    rootGrid.Children.Remove(hyperlinkButton);
                }
            }
        }

        return (root ?? throw new ArgumentNullException(nameof(root))).AddDisposableHandler(
            InputElement.KeyDownEvent,
            PreviewKeyDown,
            RoutingStrategies.Tunnel);
    }
#endif
}
