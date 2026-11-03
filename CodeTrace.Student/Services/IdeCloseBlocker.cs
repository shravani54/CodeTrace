using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CodeTrace.Student.Services;

/// <summary>
/// Stops the student from closing the allowed IDE windows:
///  - swallows mouse clicks on the top-right (close button) corner of an IDE window
///  - swallows Alt+F4 and Alt+Space while an IDE window is in the foreground
/// Works for every IDE, including ones that draw their own title bar (VS Code, JetBrains, Java IDEs).
/// Must be started on the UI thread (the hooks need its message loop).
/// </summary>
public sealed class IdeCloseBlocker : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_ALTDOWN = 0x20;
    private const uint VK_F4 = 0x73;
    private const uint VK_SPACE = 0x20;
    private const uint GA_ROOT = 2;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    // Size of the close-button area at 100% scaling (pixels); scaled with the window's DPI
    private const int CloseAreaWidth = 52;
    private const int CloseAreaHeight = 40;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extra; }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT pt);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT rect, int size);

    private readonly IdeProcessMatcher _matcher;
    private readonly Dictionary<uint, (bool Match, long Tick)> _pidCache = new();
    private HookProc? _mouseProc;      // kept in fields so the garbage collector cannot free them
    private HookProc? _keyboardProc;
    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private DateTime _lastNotice = DateTime.MinValue;
    private bool _disposed;

    /// <summary>Raised (at most every 3 seconds) when a close attempt was blocked.</summary>
    public event Action? Blocked;

    public IdeCloseBlocker(IdeProcessMatcher matcher)
    {
        _matcher = matcher;
    }

    public void Start()
    {
        if (!OperatingSystem.IsWindows() || _disposed || _matcher.IsEmpty || _mouseHook != IntPtr.Zero) return;

        _mouseProc = MouseHook;
        _keyboardProc = KeyboardHook;
        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
    }

    // ---------- hooks ----------

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP || msg == WM_LBUTTONDBLCLK)
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (IsOnCloseButton(data.pt))
                    {
                        if (msg == WM_LBUTTONDOWN) Notify();
                        return (IntPtr)1;   // swallow the click
                    }
                }
            }
        }
        catch { /* never break the mouse */ }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                if (msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var alt = (data.flags & LLKHF_ALTDOWN) != 0;
                    if (alt && (data.vkCode == VK_F4 || data.vkCode == VK_SPACE) && ForegroundIsIde())
                    {
                        if (msg == WM_SYSKEYDOWN) Notify();
                        return (IntPtr)1;   // swallow Alt+F4 / Alt+Space
                    }
                }
            }
        }
        catch { /* never break the keyboard */ }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    // ---------- checks ----------

    private bool ForegroundIsIde()
    {
        var root = GetRootIdeWindow(GetForegroundWindow());
        return root != IntPtr.Zero;
    }

    private bool IsOnCloseButton(POINT pt)
    {
        var root = GetRootIdeWindow(WindowFromPoint(pt));
        if (root == IntPtr.Zero) return false;

        if (DwmGetWindowAttribute(root, DWMWA_EXTENDED_FRAME_BOUNDS, out var r, Marshal.SizeOf<RECT>()) != 0 &&
            !GetWindowRect(root, out r))
            return false;

        var dpi = GetDpiForWindow(root);
        if (dpi == 0) dpi = 96;
        var width = CloseAreaWidth * (int)dpi / 96;
        var height = CloseAreaHeight * (int)dpi / 96;

        return pt.X >= r.Right - width && pt.X <= r.Right &&
               pt.Y >= r.Top && pt.Y <= r.Top + height;
    }

    // Returns the top-level window if it belongs to an allowed IDE (and is not a dialog), otherwise Zero
    private IntPtr GetRootIdeWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return IntPtr.Zero;

        var root = GetAncestor(hWnd, GA_ROOT);
        if (root == IntPtr.Zero) return IntPtr.Zero;

        var cls = new StringBuilder(64);
        GetClassName(root, cls, cls.Capacity);
        if (cls.ToString() == "#32770") return IntPtr.Zero;   // dialogs can always be cancelled

        GetWindowThreadProcessId(root, out var pid);
        return IsIdeProcess(pid) ? root : IntPtr.Zero;
    }

    // Cached for 5 seconds, because hook callbacks must return quickly
    private bool IsIdeProcess(uint pid)
    {
        var now = Environment.TickCount64;
        if (_pidCache.TryGetValue(pid, out var entry) && now - entry.Tick < 5000)
            return entry.Match;

        var match = _matcher.IsMatch(pid);
        _pidCache[pid] = (match, now);
        return match;
    }

    private void Notify()
    {
        if (DateTime.UtcNow - _lastNotice < TimeSpan.FromSeconds(3)) return;
        _lastNotice = DateTime.UtcNow;
        Blocked?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        _mouseHook = IntPtr.Zero;
        _keyboardHook = IntPtr.Zero;
        _mouseProc = null;
        _keyboardProc = null;
    }
}