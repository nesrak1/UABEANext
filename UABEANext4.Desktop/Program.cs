using Avalonia;
using System;
using System.Diagnostics;
using System.IO;

namespace UABEANext4.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
#if !DEBUG
        var currentDomain = AppDomain.CurrentDomain;
        currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UABEAExceptionHandler);
#endif

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static void UABEAExceptionHandler(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception ex)
        {
            File.WriteAllText("uabeacrash.log", ex.ToString());
            if (OperatingSystem.IsWindows())
            {
                // can't trust the process to be stable enough
                // to even show an avalonia messagebox, do hacky
                // vbscript instead
                var mshtaArgs = "vbscript:Execute(\"CreateObject(\"\"WScript.Shell\"\").Popup CreateObject(\"\"Scripting.FileSystemObject\"\").OpenTextFile(\"\"uabeacrash.log\"\", 1).ReadAll,,\"\"uabea crash exception (please report this crash with uabeacrash.log)\"\" :close\")";
                Process.Start(new ProcessStartInfo("mshta", mshtaArgs));
            }
            else
            {
                Console.WriteLine("uabea crash exception (please report this crash with uabeacrash.log)");
                Console.WriteLine(ex.ToString());
            }
        }
    }

}
