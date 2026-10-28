using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CodeTrace.Student.Models;
using CodeTrace.Student.Services;

namespace CodeTrace.Student.Views;

public partial class ExamSessionView : UserControl
{
    private readonly StudentApiService _api = new();
    private readonly Exam _exam;
    private readonly StudentSession _session;
    private readonly string _studentName;
    private DateTime _endTime;
    private DispatcherTimer? _timer;

    private bool _ended;        // true once the submission has been sent successfully
    private bool _submitting;   // true while a submit request is in flight

    public event EventHandler? Submitted;

    public ExamSessionView(Exam exam, StudentSession session, string studentName)
    {
        InitializeComponent();

        _exam = exam;
        _session = session;
        _studentName = studentName;

        ExamNameText.Text = exam.Name;
        StudentNameText.Text = $"Signed in as {studentName}";

        BuildIDEButtons();
        StartTimer();
    }

    private void BuildIDEButtons()
    {
        IDEButtonsPanel.Children.Clear();

        foreach (var allowed in _exam.AllowedIDEs)
        {
            var button = new Button
            {
                Content = "▶  " + allowed.IDEName,
                Classes = { "secondary" },
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            button.Click += (_, _) => LaunchIde(allowed.IDEName);
            IDEButtonsPanel.Children.Add(button);
        }

        if (_exam.AllowedIDEs.Count == 0)
        {
            IDEButtonsPanel.Children.Add(new TextBlock
            {
                Text = "No IDEs configured for this exam.",
                Classes = { "pageSubtitle" },
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private async void LaunchIde(string ideName)
    {
        var allIdes = await _api.GetIDEsAsync();
        var match = allIdes.Find(i => i.Name == ideName && i.IsActive);

        if (match == null)
        {
            SetStatus($"Could not find configuration for {ideName}.", false);
            return;
        }

        try
        {
            // 1. Path from the IDE settings (%LOCALAPPDATA% etc. are expanded)
            var path = Environment.ExpandEnvironmentVariables(match.ExecutablePath.Trim().Trim('"'));

            // 2. If missing on this PC and it is VS Code, try the usual install folders
            if (!File.Exists(path) &&
                Path.GetFileName(path).Equals("Code.exe", StringComparison.OrdinalIgnoreCase))
            {
                var candidates = new[]
                {
                    @"C:\Program Files\Microsoft VS Code\Code.exe",
                    @"C:\Program Files (x86)\Microsoft VS Code\Code.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 @"Programs\Microsoft VS Code\Code.exe")
                };
                path = candidates.FirstOrDefault(File.Exists) ?? path;
            }

            if (!File.Exists(path))
            {
                SetStatus($"{ideName} was not found on this PC ({path}). Ask your teacher for help.", false);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            SetStatus("", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to launch {ideName}: {ex.Message}", false);
        }
    }

    private void StartTimer()
    {
        // Count from the server's StartedAt so reopening the app doesn't reset the timer.
        DateTime start = _session.StartedAt.Year < 2000
            ? DateTime.Now
            : DateTime.SpecifyKind(_session.StartedAt, DateTimeKind.Utc).ToLocalTime();

        _endTime = start.AddMinutes(_exam.DurationMinutes);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var remaining = _endTime - DateTime.Now;

            if (remaining <= TimeSpan.Zero)
            {
                _timer?.Stop();
                TimerText.Text = "00:00";
                _ = AutoSubmitAsync();
                return;
            }

            TimerText.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
        };
        _timer.Start();
    }

    private async Task AutoSubmitAsync()
    {
        SetStatus("Time is up. Submitting automatically...", true);
        await DoSubmitAsync("Submitted");
    }

    private async void SubmitButton_Click(object? sender, RoutedEventArgs e)
    {
        await DoSubmitAsync("Submitted");
    }

    /// <summary>
    /// Called by MainWindow when the student tries to close the app mid-exam.
    /// Marks the session as exited. It does NOT raise Submitted, because the window is already closing.
    /// </summary>
    public async Task EndExamAsync(string reason)
    {
        if (_ended || _submitting) return;

        _timer?.Stop();
        try
        {
            await SendSubmissionAsync("Exited");
        }
        catch
        {
            // never block closing because the server is unreachable
        }
    }

    private async Task DoSubmitAsync(string status)
    {
        if (_ended || _submitting) return;

        SubmitButton.IsEnabled = false;
        SubmitButton.Content = "Submitting...";

        var ok = await SendSubmissionAsync(status);

        if (!ok)
        {
            SetStatus("Submission failed. Please try again.", false);
            SubmitButton.IsEnabled = true;
            SubmitButton.Content = "Submit Examination";
            return;
        }

        _timer?.Stop();
        SetStatus("Submitted successfully.", true);

        Submitted?.Invoke(this, EventArgs.Empty);   // MainWindow closes the app
    }

    private async Task<bool> SendSubmissionAsync(string sessionStatus)
    {
        _submitting = true;
        try
        {
            var content = "Work Completed in IDE";   // TODO: get actual content from the IDE or a file
            var result = await _api.SubmitAsync(_exam.Id, _studentName, content);
            if (result == null) return false;

            await _api.UpdateSessionStatusAsync(_session.Id, sessionStatus);
            _ended = true;
            return true;
        }
        finally
        {
            _submitting = false;
        }
    }

    private void SetStatus(string message, bool neutral)
    {
        StatusText.Text = message;
        StatusText.Foreground = neutral
            ? (IBrush)Application.Current!.FindResource("MutedText")!
            : (IBrush)Application.Current!.FindResource("ErrorBrush")!;
    }
}