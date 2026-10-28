using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class CreateExamView : UserControl
{
    private readonly IDEApiService _ideApi = new();
    private readonly ExamApiService _examApi = new();
    private readonly List<CheckBox> _ideCheckboxes = new();

    public CreateExamView()
    {
        InitializeComponent();
        _ = LoadIDEsAsync();
    }

    private async Task LoadIDEsAsync()
    {
        IDELoadingBar.IsVisible = true;
        AllowedIDEsPanel.Children.Clear();
        _ideCheckboxes.Clear();

        var ides = await _ideApi.GetAllAsync();

        IDELoadingBar.IsVisible = false;

        if (ides.Count == 0)
        {
            AllowedIDEsPanel.Children.Add(new TextBlock
            {
                Text = "No IDEs configured yet. Add some under Manage IDEs first.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Muted()
            });
            return;
        }

        foreach (var ide in ides.Where(i => i.IsActive))
        {
            var checkBox = new CheckBox { Content = ide.Name, Tag = ide.Name };
            _ideCheckboxes.Add(checkBox);
            AllowedIDEsPanel.Children.Add(checkBox);
        }
    }

    // Reads the Start Time box. Blank = no start time (students can join any time).
    // Accepts "09:00", "9:00", "9:00 AM", "09:00 PM". The time means today on this PC's clock.
    // Returns false and a message if the text is not a valid time or the time has already passed.
    private bool TryGetStartTimeUtc(out DateTime? startUtc, out string error)
    {
        startUtc = null;
        error = "";

        var text = StartTimeBox.Text?.Trim() ?? "";
        if (text == "") return true;

        var formats = new[] { "H:mm", "HH:mm", "h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt" };

        if (!DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            error = "Enter the start time like 09:00 or 9:00 AM.";
            return false;
        }

        var local = DateTime.Today + parsed.TimeOfDay;

        if (local <= DateTime.Now)
        {
            error = "That start time has already passed today. Enter a later time, or leave it blank.";
            return false;
        }

        startUtc = local.ToUniversalTime();
        return true;
    }

    private async void CreateExamButton_Click(object? sender, RoutedEventArgs e)
    {
        string examName = ExamNameBox.Text?.Trim() ?? "";
        string description = DescriptionBox.Text?.Trim() ?? "";
        int? duration = DurationBox.Value.HasValue ? (int)DurationBox.Value.Value : null;

        if (string.IsNullOrWhiteSpace(examName)) { ShowError("Please enter an examination name."); return; }
        if (examName.Length < 3) { ShowError("Examination name must be at least 3 characters."); return; }
        if (duration == null || duration <= 0) { ShowError("Please enter a valid duration."); return; }
        if (duration > 300) { ShowError("Duration cannot exceed 300 minutes."); return; }

        if (!TryGetStartTimeUtc(out var startUtc, out var startError)) { ShowError(startError); return; }

        if (string.IsNullOrWhiteSpace(description)) { ShowError("Please enter a description."); return; }

        var allowedIDEs = _ideCheckboxes
            .Select(cb => new ExamIDE { IDEName = (string)cb.Tag!, IsAllowed = cb.IsChecked == true })
            .Where(ide => ide.IsAllowed)
            .ToList();

        if (allowedIDEs.Count == 0) { ShowError("Please select at least one allowed IDE."); return; }

        var exam = new Exam
        {
            Name = examName,
            Description = description,
            DurationMinutes = duration.Value,
            StartTime = startUtc,
            IsActive = true,
            AllowedIDEs = allowedIDEs
        };

        SubmitButton.IsEnabled = false;
        SubmitButton.Content = "Creating...";

        var created = await _examApi.CreateAsync(exam);

        SubmitButton.IsEnabled = true;
        SubmitButton.Content = "Create Examination";

        if (created != null)
        {
            ShowSuccess("Examination created successfully.");
            ResetForm();
        }
        else
        {
            ShowError("Something went wrong creating the examination. Please try again.");
        }
    }

    private void ResetForm()
    {
        ExamNameBox.Text = "";
        DescriptionBox.Text = "";
        StartTimeBox.Text = "";
        DurationBox.Value = 60;
        foreach (var checkBox in _ideCheckboxes)
            checkBox.IsChecked = false;
    }

    private void ShowError(string message)
    {
        StatusBanner.Classes.Clear();
        StatusBanner.Classes.Add("bannerError");
        StatusText.Text = "⚠ " + message;
        StatusBanner.IsVisible = true;
    }

    private void ShowSuccess(string message)
    {
        StatusBanner.Classes.Clear();
        StatusBanner.Classes.Add("bannerSuccess");
        StatusText.Text = "✓ " + message;
        StatusBanner.IsVisible = true;
    }

    private static IBrush Muted() =>
        (IBrush?)Application.Current!.FindResource("MutedText") ?? Brushes.Gray;
}