using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CodeTrace.Teacher;

class Program
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "crash.log");

    private static void Log(string source, object? error)
    {
        var text = $"[{DateTime.Now}] {source}{Environment.NewLine}{error}{Environment.NewLine}{Environment.NewLine}";
        try { File.AppendAllText(LogPath, text); } catch { }
        Console.Error.WriteLine(text);
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("UnhandledException", e.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, e) =>
            Log("UnobservedTaskException", e.Exception);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log("Main", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}