using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CodeTrace.Student.Services;

/// <summary>
/// Hides the Windows taskbar (all monitors) during the exam and shows it again afterwards.
/// A marker file lets the next start restore the taskbar if the app crashed while it was hidden.
/// </summary>
public sealed class TaskbarHider : IDisposable
{
    private const int SW_HIDE = 0;
    private const int SW_SHOWNA = 8;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    private static string Marker => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodeTrace", "taskbar-hidden.txt");

    private bool _hidden;

    public void Hide()
    {
        if (!OperatingSystem.IsWindows() || _hidden) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Marker)!);
            File.WriteAllText(Marker, "hidden");   // written first, so a crash can be repaired
        }
        catch { }

        SetVisible(false);
        _hidden = true;
    }

    public void Show()
    {
        if (!OperatingSystem.IsWindows() || !_hidden) return;
        SetVisible(true);
        _hidden = false;
        try { if (File.Exists(Marker)) File.Delete(Marker); } catch { }
    }

    /// <summary>Call at app start: shows the taskbar again if an earlier run ended while it was hidden.</summary>
    public static void RecoverFromCrash()
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(Marker)) return;
            SetVisible(true);
            File.Delete(Marker);
        }
        catch { }
    }

    private static void SetVisible(bool visible)
    {
        var cmd = visible ? SW_SHOWNA : SW_HIDE;

        var main = FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero) ShowWindow(main, cmd);

        // Taskbars on other monitors
        var secondary = IntPtr.Zero;
        while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            ShowWindow(secondary, cmd);
    }

    public void Dispose() => Show();
}