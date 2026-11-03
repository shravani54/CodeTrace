using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CodeTrace.Student.Services;

/// <summary>
/// During the exam, locks the Close, Minimize and Maximize buttons of the allowed IDE windows,
/// and closes the IDEs when the exam ends. Matching is done by IdeProcessMatcher.
/// </summary>
public sealed class IdeCloseLock : IDisposable
{
    private const uint SC_MINIMIZE = 0xF020;
    private const uint SC_MAXIMIZE = 0xF030;
    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x0000;
    private const uint MF_GRAYED = 0x0001;
    private const uint WM_CLOSE = 0x0010;
    private const int SW_RESTORE = 9;
    private const int GWL_STYLE = -16;
    private const long WS_SYSMENU = 0x00080000L;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_MAXIMIZEBOX = 0x00010000L;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool revert);
    [DllImport("user32.dll")] private static extern bool EnableMenuItem(IntPtr menu, uint item, uint flags);
    [DllImport("user32.dll")] private static extern bool DrawMenuBar(IntPtr hWnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);

    private static string RepairFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodeTrace", "closelock.txt");

    private readonly IdeProcessMatcher _matcher;
    private readonly Dictionary<IntPtr, long> _originalStyles = new();   // window -> style before we changed it
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _disposed;

    // Name-only constructor kept so older calls still compile
    public IdeCloseLock(IEnumerable<string> processNames)
        : this(new IdeProcessMatcher(processNames, Array.Empty<string?>())) { }

    public IdeCloseLock(IdeProcessMatcher matcher)
    {
        _matcher = matcher;
    }

    public void Start()
    {
        if (!OperatingSystem.IsWindows() || _matcher.IsEmpty) return;

        lock (_gate)
        {
            if (_disposed || _timer != null) return;

            // Saved so a crash can be repaired on the next start
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RepairFile)!);
                File.WriteAllLines(RepairFile, _matcher.ToLines());
            }
            catch { }

            _timer = new Timer(_ => Tick(), null, 0, 300);
        }
    }

    private void Tick()
    {
        lock (_gate)
        {
            if (_disposed) return;
            try
            {
                foreach (var hWnd in FindWindows(_matcher))
                {
                    LockWindow(hWnd);

                    // IDEs with their own title bar ignore the styles, so undo minimize / maximize here
                    if (IsIconic(hWnd) || IsZoomed(hWnd))
                        ShowWindow(hWnd, SW_RESTORE);
                }
            }
            catch { }
        }
    }

    private void LockWindow(IntPtr hWnd)
    {
        // 1. Grey out Close / Minimize / Maximize in the system menu
        var menu = GetSystemMenu(hWnd, false);
        if (menu != IntPtr.Zero)
        {
            EnableMenuItem(menu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            EnableMenuItem(menu, SC_MINIMIZE, MF_BYCOMMAND | MF_GRAYED);
            EnableMenuItem(menu, SC_MAXIMIZE, MF_BYCOMMAND | MF_GRAYED);
        }

        // 2. Remove the minimize and maximize buttons (also blocks title-bar double click and Win+Up/Down)
        var style = (long)GetWindowLongPtr(hWnd, GWL_STYLE);
        if (!_originalStyles.ContainsKey(hWnd))
            _originalStyles[hWnd] = style;

        var locked = style & ~(WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
        if (locked != style)
        {
            SetWindowLongPtr(hWnd, GWL_STYLE, new IntPtr(locked));
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        DrawMenuBar(hWnd);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;

            foreach (var (hWnd, original) in _originalStyles)
            {
                try
                {
                    if (!IsWindow(hWnd)) continue;
                    UnlockWindow(hWnd, original & (WS_MINIMIZEBOX | WS_MAXIMIZEBOX));
                }
                catch { }
            }
            _originalStyles.Clear();

            try { if (File.Exists(RepairFile)) File.Delete(RepairFile); } catch { }
        }
    }

    // Gives the window its system menu and title-bar buttons back
    private static void UnlockWindow(IntPtr hWnd, long buttonStyles)
    {
        GetSystemMenu(hWnd, true);   // reset the system menu

        var style = (long)GetWindowLongPtr(hWnd, GWL_STYLE);
        SetWindowLongPtr(hWnd, GWL_STYLE, new IntPtr(style | buttonStyles));
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        DrawMenuBar(hWnd);
    }

    /// <summary>If an earlier run crashed, give the IDE windows their buttons back.</summary>
    public static void RecoverFromCrash()
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(RepairFile)) return;

            var matcher = IdeProcessMatcher.FromLines(File.ReadAllLines(RepairFile));
            foreach (var hWnd in FindWindows(matcher))
                UnlockWindow(hWnd, WS_MINIMIZEBOX | WS_MAXIMIZEBOX);

            File.Delete(RepairFile);
        }
        catch { }
    }

    /// <summary>Closes the IDEs by process name only.</summary>
    public static void CloseAll(IEnumerable<string> processNames) =>
        CloseAll(new IdeProcessMatcher(processNames, Array.Empty<string?>()));

    /// <summary>Asks the IDE windows to close, waits, then force-closes what is left.</summary>
    public static void CloseAll(IdeProcessMatcher matcher)
    {
        if (!OperatingSystem.IsWindows() || matcher.IsEmpty) return;

        try
        {
            foreach (var hWnd in FindWindows(matcher))
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }

        // Give the IDEs up to 4 seconds to close by themselves
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < deadline && FindProcesses(matcher).Count > 0)
            Thread.Sleep(200);

        foreach (var p in FindProcesses(matcher))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            finally { p.Dispose(); }
        }
    }

    // ---------- helpers ----------

    private static List<IntPtr> FindWindows(IdeProcessMatcher matcher)
    {
        var result = new List<IntPtr>();
        var cls = new StringBuilder(64);
        var pidCache = new Dictionary<uint, bool>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            // Only windows that have a title-bar menu (so not tooltips, popups, hidden helpers)
            if (((long)GetWindowLongPtr(hWnd, GWL_STYLE) & WS_SYSMENU) == 0) return true;

            cls.Clear();
            GetClassName(hWnd, cls, cls.Capacity);
            if (cls.ToString() == "#32770") return true;   // dialogs are handled elsewhere

            GetWindowThreadProcessId(hWnd, out var pid);
            if (!pidCache.TryGetValue(pid, out var match))
            {
                match = matcher.IsMatch(pid);
                pidCache[pid] = match;
            }
            if (match) result.Add(hWnd);
            return true;
        }, IntPtr.Zero);

        return result;
    }

    private static List<Process> FindProcesses(IdeProcessMatcher matcher)
    {
        var list = new List<Process>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (matcher.IsMatch((uint)p.Id)) { list.Add(p); continue; }
            }
            catch { }
            p.Dispose();
        }
        return list;
    }
}