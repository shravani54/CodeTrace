using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CodeTrace.Student.Services;

/// <summary>
/// Closes "Open file / Open folder / Select folder / Browse" dialogs of the allowed IDEs.
/// Uses IdeProcessMatcher, so it works for every IDE and not only VS Code.
/// </summary>
public sealed class FileDialogBlocker : IDisposable
{
    private const uint WM_CLOSE = 0x0010;
    private const string DialogClass = "#32770";

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int max);

    private static readonly string[] BlockedTitleStarts =
    {
        "open", "select folder", "select file", "choose", "browse", "add folder", "import"
    };

    private readonly IdeProcessMatcher _matcher;
    private readonly bool _blockSaveDialogs;
    private readonly object _gate = new();
    private Timer? _timer;
    private bool _disposed;
    private DateTime _lastNotice = DateTime.MinValue;

    public event Action<string>? Blocked;

    public FileDialogBlocker(IdeProcessMatcher matcher, bool blockSaveDialogs = false)
    {
        _matcher = matcher;
        _blockSaveDialogs = blockSaveDialogs;
    }

    public void Start()
    {
        if (!OperatingSystem.IsWindows() || _matcher.IsEmpty) return;
        lock (_gate)
        {
            if (_disposed || _timer != null) return;
            _timer = new Timer(_ => Tick(), null, 0, 150);
        }
    }

    private bool IsBlockedTitle(string title)
    {
        if (title.Length == 0) return false;
        if (_blockSaveDialogs && title.StartsWith("save", StringComparison.OrdinalIgnoreCase)) return true;
        return BlockedTitleStarts.Any(p => title.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private void Tick()
    {
        lock (_gate)
        {
            if (_disposed) return;
            try
            {
                var cls = new StringBuilder(64);
                var title = new StringBuilder(256);
                string? blockedTitle = null;

                EnumWindows((hWnd, _) =>
                {
                    if (!IsWindowVisible(hWnd)) return true;

                    cls.Clear();
                    GetClassName(hWnd, cls, cls.Capacity);
                    if (cls.ToString() != DialogClass) return true;

                    GetWindowThreadProcessId(hWnd, out var pid);
                    if (!_matcher.IsMatch(pid)) return true;

                    title.Clear();
                    GetWindowText(hWnd, title, title.Capacity);
                    var text = title.ToString().Trim();
                    if (!IsBlockedTitle(text)) return true;

                    PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    blockedTitle = text;
                    return true;
                }, IntPtr.Zero);

                if (blockedTitle != null && DateTime.UtcNow - _lastNotice > TimeSpan.FromSeconds(3))
                {
                    _lastNotice = DateTime.UtcNow;
                    Blocked?.Invoke(blockedTitle);
                }
            }
            catch { /* never crash the app */ }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}