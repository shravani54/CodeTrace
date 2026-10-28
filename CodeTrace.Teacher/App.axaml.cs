using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace CodeTrace.Teacher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Logs crashes from button clicks and async void handlers
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            var text = $"[{DateTime.Now}] UIThreadUnhandled{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}";
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), text); } catch { }
            Console.Error.WriteLine(text);
            e.Handled = true; // keeps the app open so you can read the error
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}