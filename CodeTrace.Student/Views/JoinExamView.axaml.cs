using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CodeTrace.Student.Models;
using CodeTrace.Student.Services;

namespace CodeTrace.Student.Views;

public partial class JoinExamView : UserControl
{
    private readonly StudentApiService _api = new();
    private List<Exam> _exams = new();

    // Set only after the teacher accepts the connect request ("Name (Roll)")
    private string _approvedName = "";

    // Used while the student waits for the exam start time
    private CancellationTokenSource? _waitCts;
    private Window? _popup;

    // Name: letters only, words separated by single spaces
    private static readonly Regex NameRegex = new(@"^\p{L}+(?: \p{L}+)*$");

    // Roll number: digits only
    private static readonly Regex RollRegex = new(@"^\d+$");

    public event EventHandler<(Exam Exam, StudentSession Session, string StudentName)>? ExamJoined;

    public JoinExamView()
    {
        InitializeComponent();

        NameBox.AddHandler(TextInputEvent, NameBox_TextInput, RoutingStrategies.Tunnel);
        RollBox.AddHandler(TextInputEvent, RollBox_TextInput, RoutingStrategies.Tunnel);
        ServerBox.AddHandler(TextInputEvent, ServerBox_TextInput, RoutingStrategies.Tunnel);

        // F5 = Refresh (clear the whole form)
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F5 && RefreshButton.IsEnabled)
            {
                ResetForm();
                e.Handled = true;
            }
        };

        // Server IP box starts blank: the student must type it every time
        ServerBox.Text = string.Empty;

        SetStatus("Enter the server IP, your name and roll number, then click Connect.", true);
    }

    // ---- Refresh: reset everything to blank ----
    private void RefreshButton_Click(object? sender, RoutedEventArgs e) => ResetForm();

    /// <summary>
    /// Clears every field and returns the view to its initial state.
    /// Can also be called from outside (for example after returning from an exam).
    /// </summary>
    public void ResetForm()
    {
        // Stop any "waiting for start time" countdown and close its popup
        _waitCts?.Cancel();
        _popup?.Close();
        _popup = null;

        // Forget approval, exam list and selection
        ResetApproval();

        // Blank all inputs
        ServerBox.Text = string.Empty;
        NameBox.Text = string.Empty;
        RollBox.Text = string.Empty;
        ExamComboBox.SelectedIndex = -1;

        // Buttons back to their initial state
        JoinButton.Content = "Join Examination";
        ConnectButton.IsEnabled = true;
        RefreshButton.IsEnabled = true;

        SetStatus("Enter the server IP, your name and roll number, then click Connect.", true);
        ServerBox.Focus();
    }

    // ---- Start time helpers ----
    private static DateTime ToUtc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    // Returns the "exam will start at..." message, or null if the exam has started (or has no start time)
    private static string? NotStartedMessage(Exam exam)
    {
        if (exam.StartTime is not DateTime start) return null;

        var startUtc = ToUtc(start);
        var left = startUtc - DateTime.UtcNow;
        if (left <= TimeSpan.Zero) return null;

        var when = startUtc.ToLocalTime().ToString("hh:mm tt");
        var wait = left.TotalHours >= 1
            ? $"{(int)left.TotalHours} h {left.Minutes} min"
            : $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))} min";

        return $"Your exam will start at {when} (in {wait}).";
    }

    private static string FormatLeft(TimeSpan left)
    {
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        return left.TotalHours >= 1
            ? $"{(int)left.TotalHours}h {left.Minutes:00}m {left.Seconds:00}s"
            : $"{left.Minutes:00}m {left.Seconds:00}s";
    }

    // ---- Popup message (non-blocking, closed automatically when the exam starts) ----
    private void ShowPopup(string title, string message)
    {
        _popup?.Close();

        var okButton = new Button
        {
            Content = "OK",
            MinWidth = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, FontSize = 15 },
                    okButton
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();
        dialog.Closed += (_, _) =>
        {
            if (ReferenceEquals(_popup, dialog)) _popup = null;
        };

        _popup = dialog;

        if (TopLevel.GetTopLevel(this) is Window owner)
            dialog.Show(owner);
        else
            dialog.Show();
    }

    // ---- Live typing filters ----
    private void NameBox_TextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && !e.Text.All(c => char.IsLetter(c) || c == ' '))
            e.Handled = true;
    }

    private void RollBox_TextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && !e.Text.All(char.IsDigit))
            e.Handled = true;
    }

    private void ServerBox_TextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && !e.Text.All(c => char.IsDigit(c) || c == '.' || c == ':'))
            e.Handled = true;
    }

    // ---- Small UI helpers ----
    private void SetExamStepEnabled(bool enabled)
    {
        ExamComboBox.IsEnabled = enabled;
        JoinButton.IsEnabled = enabled;
    }

    // Before approval the details can be edited. After approval they are locked,
    // and the Connect button becomes "Refresh exams".
    private void SetApprovedState(bool approved)
    {
        ServerBox.IsEnabled = NameBox.IsEnabled = RollBox.IsEnabled = !approved;
        ConnectButton.Content = approved ? "Refresh exams" : "Connect";
    }

    private void SetBusy(bool busy)
    {
        ConnectButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;   // don't reset while a request is in flight
        if (_approvedName == "")
            ServerBox.IsEnabled = NameBox.IsEnabled = RollBox.IsEnabled = !busy;
    }

    private void ResetApproval()
    {
        _approvedName = "";
        _exams = new List<Exam>();
        ExamComboBox.ItemsSource = null;
        SetExamStepEnabled(false);
        SetApprovedState(false);
    }

    // ---- Step 1: Connect = send the request and wait for the teacher ----
    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        // Already approved: the button only reloads the exam list
        if (_approvedName != "")
        {
            ConnectButton.IsEnabled = false;
            await LoadExamsAsync();
            ConnectButton.IsEnabled = true;
            return;
        }

        var name = Regex.Replace(NameBox.Text?.Trim() ?? "", @"\s+", " ");
        var roll = RollBox.Text?.Trim() ?? "";

        if (!ApiConfig.TryBuildUrl(ServerBox.Text ?? "", out var url))
        {
            SetStatus("Enter a valid server IP address, for example 10.1.180.25", false);
            return;
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(roll))
        {
            SetStatus("Please enter your name and roll number.", false);
            return;
        }

        if (!NameRegex.IsMatch(name))
        {
            SetStatus("Name can contain only letters and spaces.", false);
            return;
        }

        if (!RollRegex.IsMatch(roll))
        {
            SetStatus("Roll number can contain only digits.", false);
            return;
        }

        SetBusy(true);
        try
        {
            ApiConfig.SetServer(url);
            StudentApiService.Reconfigure();

            var studentName = $"{name} ({roll})";

            SetStatus("Sending your request to the teacher...", true);
            var requestId = await _api.CreateJoinRequestAsync(studentName);

            if (requestId == null)
            {
                SetStatus("Cannot reach the server at that address. Check the IP and that you are on the same network.", false);
                return;
            }

            // NEW: tell the server (and the teacher's Manage Students page) that this student logged in
            await StudentPresence.ReportAsync(studentName, "Waiting for approval");

            SetStatus("Request sent. Waiting for the teacher to accept...", true);
            var decision = await WaitForDecisionAsync(requestId.Value);

            if (decision == "Rejected")
            {
                await StudentPresence.ReportAsync(studentName, "Rejected");   // NEW
                SetStatus("The teacher rejected your request.", false);
                return;
            }

            if (decision != "Approved")
            {
                SetStatus("No response from the teacher. Please click Connect again.", false);
                return;
            }

            // Approved: lock the details and load the exams
            _approvedName = studentName;
            await StudentPresence.ReportAsync(studentName, "Approved");   // NEW
            SetApprovedState(true);
            await LoadExamsAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<string?> WaitForDecisionAsync(Guid requestId)
    {
        // Ask every 2 seconds, give up after 3 minutes
        for (int i = 0; i < 90; i++)
        {
            var status = await _api.GetJoinStatusAsync(requestId);
            if (status == "Approved" || status == "Rejected") return status;
            await Task.Delay(2000);
        }
        return null;
    }

    private async Task LoadExamsAsync()
    {
        SetStatus("Loading examinations...", true);

        try
        {
            _exams = await _api.GetActiveExamsAsync();

            if (_exams.Count == 0)
            {
                ExamComboBox.ItemsSource = null;
                SetExamStepEnabled(false);
                SetStatus("The teacher accepted you, but no examination is active yet. Ask the teacher to mark an exam active, then click Refresh exams.", false);
            }
            else
            {
                // The start time (if any) is shown next to the exam name
                ExamComboBox.ItemsSource = _exams.Select(ex =>
                    ex.StartTime is DateTime s
                        ? $"{ex.Name}  (starts {ToUtc(s).ToLocalTime():hh:mm tt})"
                        : ex.Name).ToList();

                SetExamStepEnabled(true);
                SetStatus("The teacher accepted your request. Select your examination and click Join.", true);
            }
        }
        catch (Exception ex)
        {
            SetExamStepEnabled(false);
            SetStatus($"Could not load the examinations. Click Refresh exams to try again. ({ex.Message})", false);
        }
    }

    // ---- Step 2: Join the selected exam (only possible after approval) ----
    private async void JoinButton_Click(object? sender, RoutedEventArgs e)
    {
        // While waiting for the start time, this button works as "Cancel waiting"
        if (_waitCts != null)
        {
            _waitCts.Cancel();
            return;
        }

        if (_approvedName == "")
        {
            SetStatus("Click Connect and wait for the teacher to accept first.", false);
            return;
        }

        var selectedIndex = ExamComboBox.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= _exams.Count)
        {
            SetStatus("Please select an examination.", false);
            return;
        }

        var exam = _exams[selectedIndex];

        // Exam has a start time that is still in the future:
        // show the popup, wait with a countdown, then join automatically
        var notStarted = NotStartedMessage(exam);
        if (notStarted != null)
        {
            await WaitForStartAsync(exam, notStarted);
            return;
        }

        await JoinExamAsync(exam);
    }

    // Shows the popup, counts down, and joins automatically when the start time arrives
    private async Task WaitForStartAsync(Exam exam, string message)
    {
        var startUtc = ToUtc(exam.StartTime!.Value);

        _waitCts = new CancellationTokenSource();
        var token = _waitCts.Token;

        ConnectButton.IsEnabled = false;
        ExamComboBox.IsEnabled = false;
        JoinButton.Content = "Cancel waiting";

        ShowPopup("Exam not started yet",
            message + "\n\nKeep this app open. Your exam will open automatically when it starts.");

        var cancelled = false;
        try
        {
            while (DateTime.UtcNow < startUtc)
            {
                var left = startUtc - DateTime.UtcNow;
                SetStatus($"Exam starts at {startUtc.ToLocalTime():hh:mm tt}. Time left: {FormatLeft(left)}. Keep this window open.", true);
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        finally
        {
            _popup?.Close();
            _popup = null;
            _waitCts?.Dispose();
            _waitCts = null;

            JoinButton.Content = "Join Examination";
            ConnectButton.IsEnabled = true;

            // If the form was reset while waiting, keep the exam step disabled
            ExamComboBox.IsEnabled = _approvedName != "";
        }

        if (cancelled)
        {
            // The form was reset: leave the blank form and its status message untouched
            if (_approvedName == "") return;

            SetStatus("Waiting cancelled. Select your examination and click Join when you are ready.", true);
            return;
        }

        SetStatus("The exam has started. Joining...", true);
        await JoinExamAsync(exam);
    }

    // Starts the session on the server and opens the exam window
    private async Task JoinExamAsync(Exam exam)
    {
        JoinButton.IsEnabled = false;
        ConnectButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        JoinButton.Content = "Joining...";

        try
        {
            // Has this student already finished or exited this exam?
            var ended = await _api.HasEndedAsync(exam.Id, _approvedName);

            if (ended == null)
            {
                SetStatus("Could not verify your exam status. Check the network and try again.", false);
                return;
            }

            if (ended == true)
            {
                SetStatus("Your examination has ended. You cannot rejoin this exam.", false);
                return;
            }

            var result = await _api.TryStartSessionAsync(exam.Id, _approvedName);

            if (result.StatusCode == 425)
            {
                // The server says it is too early (the server clock decides)
                SetStatus(NotStartedMessage(exam) ?? "The server says the exam has not started yet. Wait a few seconds and click Join again.", false);
                return;
            }

            if (result.StatusCode == 403)
            {
                // The server forgot the approval (for example it was restarted)
                ResetApproval();
                SetStatus("Your approval is no longer valid. Click Connect to ask the teacher again.", false);
                return;
            }

            if (result.Session == null)
            {
                SetStatus("Could not join the exam. Please try again.", false);
                return;
            }

            await StudentPresence.ReportAsync(_approvedName, "In exam");   // NEW
            ExamJoined?.Invoke(this, (exam, result.Session, _approvedName));
        }
        finally
        {
            JoinButton.Content = "Join Examination";
            ConnectButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
            JoinButton.IsEnabled = _approvedName != "";
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