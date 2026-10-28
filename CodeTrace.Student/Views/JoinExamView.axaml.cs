using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CodeTrace.Student.Models;
using CodeTrace.Student.Services;

namespace CodeTrace.Student.Views;

public partial class JoinExamView : UserControl
{
    private readonly StudentApiService _api = new();
    private List<Exam> _exams = new();

    // Name: letters only, words separated by single spaces
    private static readonly Regex NameRegex = new(@"^\p{L}+(?: \p{L}+)*$");

    // Roll number: digits only
    private static readonly Regex RollRegex = new(@"^\d+$");

    public event EventHandler<(Exam Exam, StudentSession Session, string StudentName)>? ExamJoined;

    public JoinExamView()
    {
        InitializeComponent();

        // Block invalid characters while typing
        NameBox.AddHandler(TextInputEvent, NameBox_TextInput, RoutingStrategies.Tunnel);
        RollBox.AddHandler(TextInputEvent, RollBox_TextInput, RoutingStrategies.Tunnel);

        _ = LoadExamsAsync();
    }

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

    private async Task LoadExamsAsync()
    {
        SetStatus("Loading examinations...", true);

        try
        {
            _exams = await _api.GetActiveExamsAsync();

            if (_exams.Count == 0)
            {
                SetStatus("No active examinations found. Ask your teacher to check the exam is marked active.", false);
            }
            else
            {
                ExamComboBox.ItemsSource = _exams.Select(e => e.Name).ToList();
                SetStatus("", true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Could not load exams: {ex.Message}", false);
        }
    }

    private async void JoinButton_Click(object? sender, RoutedEventArgs e)
    {
        // Clean up extra spaces (e.g. "Ravi   Patil " -> "Ravi Patil")
        var name = Regex.Replace(NameBox.Text?.Trim() ?? "", @"\s+", " ");
        var roll = RollBox.Text?.Trim() ?? "";
        var selectedIndex = ExamComboBox.SelectedIndex;

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

        if (selectedIndex < 0 || selectedIndex >= _exams.Count)
        {
            SetStatus("Please select an examination.", false);
            return;
        }

        JoinButton.IsEnabled = false;
        JoinButton.Content = "Joining...";

        try
        {
            var exam = _exams[selectedIndex];
            var studentName = $"{name} ({roll})";

            // 1. Has this student already finished or exited this exam?
            var ended = await _api.HasEndedAsync(exam.Id, studentName);

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

            // 2. Not ended, so start the session
            var session = await _api.StartSessionAsync(exam.Id, studentName);

            if (session == null)
            {
                SetStatus("Could not join the exam. Please try again.", false);
                return;
            }

            ExamJoined?.Invoke(this, (exam, session, studentName));
        }
        finally
        {
            JoinButton.IsEnabled = true;
            JoinButton.Content = "Join Examination";
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