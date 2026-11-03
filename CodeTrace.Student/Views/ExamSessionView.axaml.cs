using System;
using System.Collections.Generic;
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
    // IDEs that can be told to open a folder from the command line
    private static readonly HashSet<string> FolderCapableIdes = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "idea64", "idea", "pycharm64", "pycharm", "studio64",
        "webstorm64", "clion64", "rider64", "goland64"
    };

    private readonly StudentApiService _api = new();
    private readonly Exam _exam;
    private readonly StudentSession _session;
    private readonly string _studentName;
    private readonly string _projectFolder;     // where this student's code lives
    private DateTime _endTime;
    private DispatcherTimer? _timer;

    private bool _ended;          // true once the submission has been sent successfully
    private bool _submitting;     // true while a submit request is in flight
    private bool _launching;      // true while an IDE is being launched and docked
    private bool _warnedEmpty;    // the "folder is empty" warning was already shown once
    private int? _submissionId;   // set once the submission record exists (so a retry does not create a second one)
    private bool _zipUploaded;    // true once the code ZIP reached the server

    // Camera
    private readonly CameraService _camera = new();
    private bool _cameraStarted;

    public event EventHandler? Submitted;

    public ExamSessionView(Exam exam, StudentSession session, string studentName)
    {
        InitializeComponent();

        _exam = exam;
        _session = session;
        _studentName = studentName;
        _projectFolder = ProjectFolderService.GetFolder(exam.Id, exam.Name, studentName);

        ExamNameText.Text = exam.Name;
        StudentNameText.Text = $"Signed in as {studentName}";

        BuildIDEButtons();
        StartTimer();
        StartCamera();
    }

    // Starts sending a photo to the API every 3 seconds
    private void StartCamera()
    {
        try
        {
            // The ID in the URL must be the same one the Teacher app uses to ask for the photo.
            // Here it is the session ID.
            var studentId = _session.Id.ToString();

            _cameraStarted = _camera.Start(async bytes =>
            {
                await _api.SendCameraFrameAsync(studentId, bytes);
            });

            if (!_cameraStarted)
                SetStatus("Camera not found or blocked. Check Windows camera permission.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Camera error: {ex.Message}", false);
        }
    }

    // Stops the camera (the camera light turns off)
    private void StopCamera()
    {
        try { _camera.Dispose(); } catch { /* ignore */ }
        _cameraStarted = false;
    }

    // Also stop the camera if this screen is removed
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopCamera();
        base.OnDetachedFromVisualTree(e);
    }

    // Shows a warning in the bar (used when another app is blocked or an IDE is closed)
    public void ShowNotice(string message) => SetStatus(message, false);

    private void BuildIDEButtons()
    {
        IDEButtonsPanel.Children.Clear();

        foreach (var allowed in _exam.AllowedIDEs)
        {
            var button = new Button
            {
                Content = "▶  " + allowed.IDEName,
                Classes = { "secondary" },
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
        if (_launching) return;
        _launching = true;

        try
        {
            // Already open? Just bring it to the front (this is how the student switches between IDEs)
            if (TopLevel.GetTopLevel(this) is MainWindow open && open.ActivateIdeIfOpen(ideName))
            {
                SetStatus("", true);
                return;
            }

            var allIdes = await _api.GetIDEsAsync();
            var match = allIdes.Find(i => i.Name == ideName && i.IsActive);

            if (match == null)
            {
                SetStatus($"Could not find configuration for {ideName}.", false);
                return;
            }

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

            // 3. Remember the IDE windows that are already open, so they are never docked
            var processNames = IdeDockService.ProcessNamesFor(ideName, path);
            var existingWindows = IdeDockService.GetWindowHandles(processNames);

            // 4. Launch the IDE inside the student's exam folder.
            //    VS Code is always opened in a NEW window, even if it is already running.
            var exeName = Path.GetFileNameWithoutExtension(path);
            var opensFolder = FolderCapableIdes.Contains(exeName);

            var startInfo = new ProcessStartInfo(path) { UseShellExecute = true };
            if (exeName.Equals("Code", StringComparison.OrdinalIgnoreCase))
                startInfo.Arguments = $"--new-window \"{_projectFolder}\"";
            else if (opensFolder)
                startInfo.Arguments = $"\"{_projectFolder}\"";

            Process.Start(startInfo);

            // 5. Turn the Student window into a top bar and dock the new IDE window below it
            SetStatus($"Opening {ideName}...", true);

            var docked = false;
            if (TopLevel.GetTopLevel(this) is MainWindow main)
                docked = await main.DockIdeAsync(ideName, path, existingWindows);

            if (!docked)
                SetStatus($"{ideName} opened, but it could not be placed under this bar.", false);
            else if (!opensFolder)
                // This IDE cannot open a folder by itself: tell the student where to save
                SetStatus($"Save your files in: {_projectFolder}", true);
            else
                SetStatus("", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to launch {ideName}: {ex.Message}", false);
        }
        finally
        {
            _launching = false;
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
        SetStatus("Time is up. Submitting your code...", true);
        await DoSubmitAsync("Submitted", manual: false);
    }

    private async void SubmitButton_Click(object? sender, RoutedEventArgs e)
    {
        await DoSubmitAsync("Submitted", manual: true);
    }

    /// <summary>
    /// Called by MainWindow when the student tries to close the app mid-exam.
    /// Marks the session as exited and still sends the code. It does NOT raise Submitted,
    /// because the window is already closing.
    /// </summary>
    public async Task EndExamAsync(string reason)
    {
        if (_ended || _submitting) return;

        _timer?.Stop();
        StopCamera();
        try
        {
            await SendSubmissionAsync("Exited");
        }
        catch
        {
            // never block closing because the server is unreachable
        }
    }

    private async Task DoSubmitAsync(string status, bool manual)
    {
        if (_ended || _submitting) return;

        // Pressed Submit with nothing saved in the exam folder: warn once
        if (manual && !_warnedEmpty && !ProjectFolderService.HasFiles(_projectFolder))
        {
            _warnedEmpty = true;
            SetStatus($"No code found in {_projectFolder}. Save your files there, or press Submit again to submit without code.", false);
            return;
        }

        SubmitButton.IsEnabled = false;
        SubmitButton.Content = "Submitting...";
        SetStatus("Zipping and uploading your code...", true);

        var ok = await SendSubmissionAsync(status);

        if (!ok)
        {
            SetStatus("Submission failed. Please try again.", false);
            SubmitButton.IsEnabled = true;
            SubmitButton.Content = "Submit Examination";
            return;
        }

        _timer?.Stop();
        StopCamera();   // exam is over, turn the camera off
        SetStatus("Submitted successfully.", true);

        Submitted?.Invoke(this, EventArgs.Empty);   // MainWindow closes the app
    }

    private async Task<bool> SendSubmissionAsync(string sessionStatus)
    {
        _submitting = true;
        try
        {
            // 1. The submission record (created only once, even if the upload has to be retried)
            if (_submissionId == null)
            {
                var content = "Work Completed in IDE";
                var result = await _api.SubmitAsync(_exam.Id, _studentName, content);
                if (result == null) return false;
                _submissionId = result.Id;
            }

            // 2. Zip the exam folder and upload it
            if (!_zipUploaded)
            {
                var zipPath = await ProjectFolderService.CreateZipAsync(_projectFolder);

                if (zipPath != null)
                {
                    try
                    {
                        var uploaded = false;
                        for (var attempt = 0; attempt < 3 && !uploaded; attempt++)
                            uploaded = await _api.UploadSubmissionZipAsync(_submissionId.Value, zipPath);

                        if (!uploaded) return false;
                    }
                    finally
                    {
                        try { File.Delete(zipPath); } catch { /* temp file, not important */ }
                    }
                }

                _zipUploaded = true;
            }

            await _api.UpdateSessionStatusAsync(_session.Id, sessionStatus);
            _ended = true;

            // NEW (status): tell the teacher's Manage Students page, so the row changes from "In exam"
            // to "Submitted" (or "Exited" when the student left early)
            await StudentPresence.ReportAsync(_studentName, sessionStatus == "Exited" ? "Exited" : "Submitted");

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