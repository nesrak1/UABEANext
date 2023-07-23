using Avalonia;
using Avalonia.ReactiveUI;
using System;

namespace UABEANext3
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI().With(new Win32PlatformOptions()
                {
                    CompositionMode = new[] { Win32CompositionMode.LowLatencyDxgiSwapChain }
                });
    }
}
