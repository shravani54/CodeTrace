using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeTrace.Student.Services;

// Keeps the student inside the allowed IDEs: any other app window is minimised (or closed).
// Windows only. It cannot control windows that run as administrator.
public sealed class AppGuardService : IDisposable
{
    // ---------- Win32 ----------
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

    private const uint TH32CS_SNAPPROCESS = 0x2;
    private const uint GW_OWNER = 4;
    private const int SW_MINIMIZE = 6;
    private const uint WM_CLOSE = 0x0010;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80;
    private const int DWMWA_CLOAKED = 14;

    private static long GetExStyle(IntPtr hWnd) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64() : GetWindowLong32(hWnd, GWL_EXSTYLE);

    // ---------- Rules ----------

    // Windows system processes that must never be touched (taskbar, Start menu, volume flyout, UAC...)
    private static readonly HashSet<string> SystemAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm", "csrss", "winlogon", "logonui", "lockapp", "consent", "fontdrvhost", "sihost", "ctfmon",
        "shellexperiencehost", "startmenuexperiencehost", "searchhost", "searchapp", "textinputhost", "widgets"
    };

    // Always blocked, even if they were started from inside an allowed IDE's terminal
    private static readonly HashSet<string> AlwaysBlocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "iexplore",
        "whatsapp", "whatsapp.root", "telegram", "discord", "teams", "ms-teams", "slack", "zoom", "skype",
        "anydesk", "teamviewer"
    };

    private readonly HashSet<string> _allowed;     // process names of the IDEs the teacher allowed
    private readonly uint _ownPid;
    private readonly bool _closeBlocked;
    private CancellationTokenSource? _cts;

    // Raised (on a background thread) with the process name when a window gets blocked
    public event Action<string>? Blocked;

    public AppGuardService(IEnumerable<string> allowedProcessNames, bool closeBlockedWindows = false)
    {
        _allowed = new HashSet<string>(allowedProcessNames, StringComparer.OrdinalIgnoreCase);
        _ownPid = (uint)Environment.ProcessId;
        _closeBlocked = closeBlockedWindows;
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Tick();
                    await Task.Delay(400, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // never let one bad tick stop the guard
                }
            }
        });
    }

    private void Tick()
    {
        var map = GetProcessMap();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
            if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero) return true;      // dialogs owned by another window
            if (GetWindowTextLength(hwnd) == 0) return true;                // untitled helper windows
            if ((GetExStyle(hwnd) & WS_EX_TOOLWINDOW) != 0) return true;   // tool windows
            if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;                                                // hidden UWP windows

            GetWindowThreadProcessId(hwnd, out var pid);
            if (!map.TryGetValue(pid, out var info)) return true;

            if (!IsBlocked(hwnd, pid, info.Name, map)) return true;

            if (_closeBlocked) PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            else ShowWindow(hwnd, SW_MINIMIZE);

            Blocked?.Invoke(info.Name);
            return true;
        }, IntPtr.Zero);
    }

    private bool IsBlocked(IntPtr hwnd, uint pid, string name, Dictionary<uint, (uint Parent, string Name)> map)
    {
        if (pid == _ownPid) return false;                       // the Student app itself
        if (AlwaysBlocked.Contains(name)) return true;

        if (name.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            // Taskbar and desktop are fine; File Explorer windows are not
            var cls = GetClass(hwnd);
            return cls == "CabinetWClass" || cls == "ExploreWClass";
        }

        if (_allowed.Contains(name)) return false;               // an allowed IDE
        if (SystemAllowed.Contains(name)) return false;          // Windows itself
        return !IsStartedByAllowedApp(pid, map);                 // programs started by an allowed IDE are fine
    }

    private bool IsStartedByAllowedApp(uint pid, Dictionary<uint, (uint Parent, string Name)> map)
    {
        var current = pid;
        for (var i = 0; i < 12; i++)
        {
            if (!map.TryGetValue(current, out var entry)) return false;
            if (entry.Parent == 0 || entry.Parent == current) return false;
            if (entry.Parent == _ownPid) return true;            // started by the Student app
            if (!map.TryGetValue(entry.Parent, out var parent)) return false;
            if (_allowed.Contains(parent.Name)) return true;      // started by an allowed IDE
            current = entry.Parent;
        }
        return false;
    }

    private static string GetClass(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    // pid -> (parent pid, process name without .exe)
    private static Dictionary<uint, (uint Parent, string Name)> GetProcessMap()
    {
        var map = new Dictionary<uint, (uint Parent, string Name)>();
        var snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return map;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32FirstW(snap, ref entry))
            {
                do
                {
                    var name = entry.szExeFile ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 4);
                    map[entry.th32ProcessID] = (entry.th32ParentProcessID, name);
                }
                while (Process32NextW(snap, ref entry));
            }
        }
        finally
        {
            CloseHandle(snap);
        }

        return map;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();
}