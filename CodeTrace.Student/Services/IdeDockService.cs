using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CodeTrace.Student.Services;

// The screen area (in physical pixels) the IDE window must fill
public sealed record DockArea(int X, int Y, int Width, int Height);

// Finds an IDE's main window and keeps it resized to a fixed screen area (Windows only).
// The IDE is launched elsewhere (your existing code); this class only finds and docks it.
public sealed class IdeDockService : IDisposable
{
    // ---------- Win32 ----------
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT rect, int size);

    // System menu functions, used to grey out the close (X) button
    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
    [DllImport("user32.dll")] private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
    [DllImport("user32.dll")] private static extern uint GetMenuState(IntPtr hMenu, uint uId, uint uFlags);
    [DllImport("user32.dll")] private static extern bool DrawMenuBar(IntPtr hWnd);

    // Used to ask the IDE to close when the exam ends
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private const uint WM_CLOSE = 0x0010;

    private const int SW_RESTORE = 9;
    private const uint GW_OWNER = 4;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint SC_CLOSE = 0xF060;
    private const uint MF_BYCOMMAND = 0x0;
    private const uint MF_ENABLED = 0x0;
    private const uint MF_GRAYED = 0x1;

    private static long GetExStyle(IntPtr hWnd) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64() : GetWindowLong32(hWnd, GWL_EXSTYLE);

    // ---------- State ----------
    private CancellationTokenSource? _cts;
    private IntPtr _hwnd = IntPtr.Zero;
    private volatile DockArea _area = new(0, 0, 0, 0);
    private string[] _names = Array.Empty<string>();
    private HashSet<long> _ignore = new();   // windows that were already open before the IDE was launched
    private volatile bool _closing;          // true once the IDE is being closed, so the lock is not applied again

    // true = the IDE's close (X) button is greyed out while the exam runs. Set to false to allow closing.
    public bool DisableCloseButton { get; set; } = true;

    // Process names of the IDEs the Student app knows. Used to release windows left locked by an earlier run.
    private static readonly string[] KnownIdeProcesses =
    {
        "code", "devenv", "idea64", "pycharm64", "studio64", "eclipse", "javaw", "codeblocks", "devcpp", "notepad++"
    };

    // Raised (once) when the IDE window has been missing for about 10 seconds (the student closed the IDE).
    // It is raised on a background thread: use Dispatcher.UIThread.Post(...) before touching the UI.
    public event Action? IdeWindowLost;

    // Change the docked area (for example if the Student bar height changes)
    public void SetArea(DockArea area) => _area = area;

    // Brings this IDE's docked window to the front. Returns false if the IDE window no longer exists.
    public bool Activate()
    {
        var hwnd = _hwnd;
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;

        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
        return true;
    }

    // Asks the IDE to close, like clicking its X. If it has unsaved files, the IDE shows its own "Save changes?" prompt.
    // The lock is released first. Some IDEs (VS Code and other Electron apps) refuse to close while it is on.
    public void RequestClose()
    {
        var hwnd = _hwnd;
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;

        _closing = true;      // stop the docking loop from locking the window again
        _cts?.Cancel();

        UnlockClose(hwnd);    // re-enable the Close command before asking the IDE to close
        PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    // Releases the Close command of every known IDE window.
    // Call it when the Student app starts, so an IDE left locked by an earlier run can be closed again.
    public static void UnlockAllKnownIdes()
    {
        try
        {
            foreach (var (hwnd, _) in EnumerateCandidates(KnownIdeProcesses))
                UnlockClose(hwnd);
        }
        catch
        {
            // never stop the app because of this
        }
    }

    // Maps an IDE name / exe path to the process names to look for.
    // Add a line here for any IDE whose process name differs from its exe file name.
    public static string[] ProcessNamesFor(string ideName, string? exePath)
    {
        var names = new List<string>();

        if (!string.IsNullOrWhiteSpace(exePath))
            names.Add(Path.GetFileNameWithoutExtension(exePath));

        var n = (ideName ?? "").ToLowerInvariant();
        if (n.Contains("vs code") || n.Contains("vscode") || n.Contains("visual studio code")) names.Add("Code");
        else if (n.Contains("visual studio")) names.Add("devenv");
        else if (n.Contains("intellij")) names.Add("idea64");
        else if (n.Contains("pycharm")) names.Add("pycharm64");
        else if (n.Contains("android studio")) names.Add("studio64");
        else if (n.Contains("eclipse")) names.Add("eclipse");
        else if (n.Contains("code::blocks") || n.Contains("codeblocks")) names.Add("codeblocks");
        else if (n.Contains("dev-c++") || n.Contains("dev c++")) names.Add("devcpp");
        else if (n.Contains("notepad++")) names.Add("notepad++");

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // The windows of these processes that are open right now.
    // Call this BEFORE launching the IDE and pass the result to StartAsync,
    // so that windows the student already had open are never docked.
    public static HashSet<long> GetWindowHandles(string[] processNames)
    {
        var names = processNames.Select(p => p.ToLowerInvariant()).ToArray();
        return EnumerateCandidates(names).Select(c => c.Hwnd.ToInt64()).ToHashSet();
    }

    // Waits up to findTimeout for a NEW IDE window, docks it, then keeps it docked until Dispose/Stop.
    // Returns false if the window was not found in time.
    public async Task<bool> StartAsync(
        string[] processNames,
        DockArea area,
        TimeSpan findTimeout,
        HashSet<long>? ignoreWindows = null)
    {
        Stop();

        _closing = false;
        _names = processNames.Select(p => p.ToLowerInvariant()).ToArray();
        _area = area;
        _ignore = ignoreWindows != null ? new HashSet<long>(ignoreWindows) : new HashSet<long>();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var deadline = DateTime.UtcNow + findTimeout;
            while (DateTime.UtcNow < deadline)
            {
                _hwnd = FindMainWindow();
                if (_hwnd != IntPtr.Zero) break;
                await Task.Delay(500, token);
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        // CHANGED: no NEW window appeared. Single-instance IDEs (like Dev-C++) reuse the window that
        // is already open, so dock that one instead of giving up.
        if (_hwnd == IntPtr.Zero)
        {
            _ignore.Clear();
            _hwnd = FindMainWindow();
        }

        // CHANGED: keep watching even if nothing was found yet (a slow first start docks as soon as the window appears)
        _ = Task.Run(() => KeepDockedAsync(token));

        if (_hwnd == IntPtr.Zero) return false;

        Dock(_hwnd, _area);
        if (DisableCloseButton && !_closing) LockClose(_hwnd);

        return true;
    }

    private async Task KeepDockedAsync(CancellationToken token)
    {
        DateTime? missingSince = null;
        var lostRaised = false;
        var startedAt = DateTime.UtcNow;   // CHANGED: gives a slow IDE time to start

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_hwnd == IntPtr.Zero || !IsWindow(_hwnd))
                    _hwnd = FindMainWindow();

                if (_hwnd == IntPtr.Zero)
                {
                    missingSince ??= DateTime.UtcNow;

                    // CHANGED: only report "closed" after a 2-minute start-up grace period
                    if (!lostRaised
                        && DateTime.UtcNow - startedAt > TimeSpan.FromSeconds(120)
                        && DateTime.UtcNow - missingSince > TimeSpan.FromSeconds(10))
                    {
                        lostRaised = true;
                        IdeWindowLost?.Invoke();
                    }
                }
                else
                {
                    missingSince = null;
                    lostRaised = false;
                    Dock(_hwnd, _area);
                    if (DisableCloseButton && !_closing) LockClose(_hwnd);   // apps can reset it, so keep it greyed out
                }

                await Task.Delay(250, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Never let one bad tick stop the docking loop
            }
        }
    }

    // Greys out the close (X) button and disables Alt+F4 / "Close" in the window's system menu
    private static void LockClose(IntPtr hwnd)
    {
        var menu = GetSystemMenu(hwnd, false);
        if (menu == IntPtr.Zero) return;

        var state = GetMenuState(menu, SC_CLOSE, MF_BYCOMMAND);
        if (state != 0xFFFFFFFF && (state & MF_GRAYED) != 0) return;   // already locked

        EnableMenuItem(menu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
        DrawMenuBar(hwnd);
    }

    // Gives the IDE its Close command back. It enables the item directly, which also works
    // for windows of other programs (resetting the system menu did not for VS Code).
    private static void UnlockClose(IntPtr hwnd)
    {
        if (!IsWindow(hwnd)) return;

        var menu = GetSystemMenu(hwnd, false);
        if (menu != IntPtr.Zero)
            EnableMenuItem(menu, SC_CLOSE, MF_BYCOMMAND | MF_ENABLED);

        DrawMenuBar(hwnd);
    }

    // Puts the window exactly on the area (compensating for Windows' invisible resize borders)
    private static void Dock(IntPtr hwnd, DockArea area)
    {
        // A minimised or maximised window ignores moves, so restore it first
        if (IsIconic(hwnd) || IsZoomed(hwnd))
            ShowWindow(hwnd, SW_RESTORE);

        if (!GetWindowRect(hwnd, out var win)) return;

        int l = 0, t = 0, r = 0, b = 0;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, Marshal.SizeOf<RECT>()) == 0)
        {
            l = frame.Left - win.Left;
            t = frame.Top - win.Top;
            r = win.Right - frame.Right;
            b = win.Bottom - frame.Bottom;
        }

        var x = area.X - l;
        var y = area.Y - t;
        var w = area.Width + l + r;
        var h = area.Height + t + b;

        // Already in place: do nothing (avoids flicker)
        if (win.Left == x && win.Top == y && win.Right - win.Left == w && win.Bottom - win.Top == h)
            return;

        SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    // All visible top-level windows that belong to one of the IDE process names
    private static List<(IntPtr Hwnd, long Size)> EnumerateCandidates(string[] names)
    {
        var result = new List<(IntPtr Hwnd, long Size)>();
        var pidNames = new Dictionary<uint, string?>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero) return true;     // skip dialogs owned by another window
            if (GetWindowTextLength(hwnd) == 0) return true;               // skip windows with no title
            if ((GetExStyle(hwnd) & WS_EX_TOOLWINDOW) != 0) return true;   // skip tool windows

            GetWindowThreadProcessId(hwnd, out var pid);
            if (!pidNames.TryGetValue(pid, out var name))
            {
                try { name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); }
                catch { name = null; }
                pidNames[pid] = name;
            }

            if (name == null || !names.Contains(name)) return true;
            if (!GetWindowRect(hwnd, out var rc)) return true;

            result.Add((hwnd, (long)(rc.Right - rc.Left) * (rc.Bottom - rc.Top)));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    // The biggest matching window that was NOT already open before the IDE was launched
    private IntPtr FindMainWindow()
    {
        var best = IntPtr.Zero;
        long bestSize = 0;

        foreach (var (hwnd, size) in EnumerateCandidates(_names))
        {
            if (_ignore.Contains(hwnd.ToInt64())) continue;
            if (size > bestSize)
            {
                bestSize = size;
                best = hwnd;
            }
        }

        return best;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        // Give the IDE its close button back when docking stops (exam over)
        if (DisableCloseButton && _hwnd != IntPtr.Zero)
            UnlockClose(_hwnd);

        _hwnd = IntPtr.Zero;
    }

    public void Dispose() => Stop();
}