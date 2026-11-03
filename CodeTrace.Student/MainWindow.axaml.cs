using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CodeTrace.Student.Services;
using CodeTrace.Student.Views;

namespace CodeTrace.Student;

public partial class MainWindow : Window
{
    // Set to false while you are developing on your own PC, so the guard does not minimise your editor.
    private const bool AppGuardEnabled = true;

    // Hide the Windows taskbar during the exam (only when AppGuardEnabled is true)
    private const bool HideTaskbar = true;

    // Size of the lock box over the IDE's close (X) button, in device-independent pixels.
    // Make it wider or taller if part of the X is still visible around the lock.
    private const double ShieldWidthDip = 48;
    private const double ShieldHeightDip = 36;

    private bool _allowClose;
    private ExamSessionView? _activeSession;   // non-null only while an exam is running

    // One docked window per allowed IDE (keyed by IDE name), plus the guard that blocks all other apps
    private readonly Dictionary<string, IdeDockService> _docks = new();
    private AppGuardService? _guard;
    private DockArea? _dockArea;               // the area under the bar, set the first time an IDE is docked
    private CloseShieldWindow? _shield;        // the lock icon over the IDE's close button

    // Your existing class that hides and restores the Windows taskbar
    private readonly TaskbarHider _taskbar = new();

    // Who is taking the exam, and when each blocked app was last reported
    private string _studentName = "";
    private readonly Dictionary<string, DateTime> _lastReported = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        // If an earlier run ended while the taskbar was hidden, bring it back now
        TaskbarHider.RecoverFromCrash();

        // NEW: release any IDE window that an earlier run left locked
        IdeDockService.UnlockAllKnownIdes();

        InitializeComponent();
        ShowJoin();

        Closing += OnClosing;
        Closed += (_, _) => StopDocking();

        // Teacher escape hatch: Ctrl+Shift+Alt+F12 (change to a combo only you know).
        // It closes the Student app but leaves the IDEs open.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F12 &&
                e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt))
            {
                CloseApp(closeIdes: false);
            }
        };
    }

    private void ShowJoin()
    {
        StopDocking();
        _activeSession = null;
        _studentName = "";
        var join = new JoinExamView();
        join.ExamJoined += (_, session) => ShowExamSession(session);
        MainContent.Content = join;
    }

    private void ShowExamSession((Models.Exam Exam, Models.StudentSession Session, string StudentName) info)
    {
        _studentName = info.StudentName;

        var session = new ExamSessionView(info.Exam, info.Session, info.StudentName);

        // Covers both the Submit button and the timer reaching 0:
        // ExamSessionView raises Submitted after a successful submit.
        session.Submitted += (_, _) => CloseApp();

        _activeSession = session;
        MainContent.Content = session;

        // Hide the taskbar for the whole exam
        if (AppGuardEnabled && HideTaskbar)
            _taskbar.Hide();

        // Only the IDEs the teacher allowed may be used from now on
        _ = StartAppGuardAsync(info.Exam);
    }

    // ---------- App guard: only the allowed IDEs ----------
    private async Task StartAppGuardAsync(Models.Exam exam)
    {
        if (!AppGuardEnabled) return;

        try
        {
            var configs = await new StudentApiService().GetIDEsAsync();
            var names = new List<string>();

            foreach (var allowed in exam.AllowedIDEs)
            {
                var config = configs.Find(i => i.Name == allowed.IDEName && i.IsActive);
                var path = config == null
                    ? null
                    : Environment.ExpandEnvironmentVariables(config.ExecutablePath.Trim().Trim('"'));

                names.AddRange(IdeDockService.ProcessNamesFor(allowed.IDEName, path));
            }

            _guard?.Dispose();
            _guard = new AppGuardService(names);
            _guard.Blocked += processName => Dispatcher.UIThread.Post(() =>
            {
                _activeSession?.ShowNotice($"{processName} is not allowed during the exam.");
                ReportBlockedApp(processName);
            });
            _guard.Start();
        }
        catch
        {
            // If the IDE list cannot be loaded, the guard simply stays off
        }
    }

    // Sends the alert to the teacher (the same app is reported at most once every 10 seconds)
    private void ReportBlockedApp(string appName)
    {
        if (_studentName == "") return;

        var now = DateTime.UtcNow;
        if (_lastReported.TryGetValue(appName, out var last) && now - last < TimeSpan.FromSeconds(10))
            return;

        _lastReported[appName] = now;
        _ = StudentPresence.ReportBlockedAppAsync(_studentName, appName);
    }

    // ---------- IDE docking ----------

    // If this IDE is already open, brings it to the front and returns true (used to switch between IDEs)
    public bool ActivateIdeIfOpen(string ideName) =>
        _docks.TryGetValue(ideName, out var dock) && dock.Activate();

    // Turns this window into a bar across the top of the screen (once) and returns the area below it
    private async Task<DockArea> EnterBarModeAsync()
    {
        if (_dockArea != null) return _dockArea;

        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary!;

        // With the taskbar hidden, the IDE may use the whole screen
        var work = (AppGuardEnabled && HideTaskbar) ? screen.Bounds : screen.WorkingArea;   // physical pixels

        // Remove anything that could stop the window from becoming a short bar
        SizeToContent = SizeToContent.Manual;
        MinWidth = 0;
        MinHeight = 0;
        MaxHeight = double.PositiveInfinity;

        WindowState = WindowState.Normal;
        CanResize = false;
        Topmost = true;
        Position = new PixelPoint(work.X, work.Y);
        Width = work.Width / screen.Scaling;
        Height = 110;                             // bar height in DIPs, change to taste

        await Task.Delay(400);                    // let the new size apply

        var barBottom = this.PointToScreen(new Point(0, Bounds.Height)).Y;
        _dockArea = new DockArea(work.X, barBottom, work.Width, work.Bottom - barBottom);

        ShowCloseShield(_dockArea, screen.Scaling);

        return _dockArea;
    }

    // Puts the lock box over the top-right corner of the docked IDE (where its close button is)
    private void ShowCloseShield(DockArea area, double scaling)
    {
        CloseShield();

        var shieldWidthPx = (int)Math.Ceiling(ShieldWidthDip * scaling);

        _shield = new CloseShieldWindow
        {
            Width = ShieldWidthDip,
            Height = ShieldHeightDip,
            Position = new PixelPoint(area.X + area.Width - shieldWidthPx, area.Y)
        };
        _shield.Show(this);   // shown as a child of this window, so Task View / Alt+Tab do not list it
    }

    private void CloseShield()
    {
        if (_shield == null) return;

        _shield.AllowClose = true;   // only the Student app itself may remove the lock box
        _shield.Close();
        _shield = null;
    }

    // Docks a freshly launched IDE under the bar.
    // existingWindows = the IDE windows that were open BEFORE launching, so they are never docked.
    // Returns false if the new IDE window was not found within 30 seconds.
    public async Task<bool> DockIdeAsync(string ideName, string? exePath, HashSet<long>? existingWindows = null)
    {
        var area = await EnterBarModeAsync();

        // Replace an older (closed) window of the same IDE
        if (_docks.TryGetValue(ideName, out var old))
        {
            old.Dispose();
            _docks.Remove(ideName);
        }

        var dock = new IdeDockService();
        dock.IdeWindowLost += () => Dispatcher.UIThread.Post(() =>
        {
            _activeSession?.ShowNotice($"{ideName} was closed. Click its button to open it again.");
            ReportBlockedApp($"{ideName} (closed)");   // the teacher is told that the IDE was closed
        });
        _docks[ideName] = dock;

        return await dock.StartAsync(
            IdeDockService.ProcessNamesFor(ideName, exePath),
            area,
            TimeSpan.FromSeconds(30),
            existingWindows);
    }

    private void StopDocking()
    {
        foreach (var dock in _docks.Values) dock.Dispose();
        _docks.Clear();

        _guard?.Dispose();
        _guard = null;

        CloseShield();
        _taskbar.Show();   // bring the taskbar back (does nothing if it was not hidden)

        _dockArea = null;
        Topmost = false;
    }

    // closeIdes = true asks every IDE that was opened from here to close (after Submit, time up, or leaving)
    public void CloseApp(bool closeIdes = true)
    {
        _allowClose = true;

        if (closeIdes)
        {
            foreach (var dock in _docks.Values)
                dock.RequestClose();
        }

        StopDocking();
        Close();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose) return;

        e.Cancel = true;   // decide below

        if (_activeSession != null)
        {
            // Alt+F4 etc. during an exam: end it for this student, then close
            try { await _activeSession.EndExamAsync("Student exited"); }
            catch { /* close even if the server is unreachable */ }

            CloseApp();
        }
        // On the login/join screen, closing is ignored
    }
}