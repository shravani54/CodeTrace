using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class CreateExamView : UserControl
{
    private readonly IDEApiService _ideApi = new();
    private readonly ExamApiService _examApi = new();
    private readonly List<CheckBox> _ideCheckboxes = new();

    // Date picker state
    private DateTime? _startDate;
    private DateTime _viewMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    // Time picker state
    private int? _hour12;           // 1..12
    private int? _minute;           // 0..59
    private bool? _isPm;
    private TimeSpan? _startTime;   // set only when hour, minute and AM/PM are all chosen
    private bool _updating;

    private static readonly IBrush PlaceholderBrush = Brush.Parse("#64748B");
    private static readonly IBrush ValueBrush = Brush.Parse("#0F172A");

    public CreateExamView()
    {
        InitializeComponent();

        DatePopup.PlacementTarget = DateButton;
        TimePopup.PlacementTarget = TimeButton;

        BuildTimeLists();

        HourList.SelectionChanged += OnTimePartChanged;
        MinuteList.SelectionChanged += OnTimePartChanged;
        AmPmList.SelectionChanged += OnTimePartChanged;

        _ = LoadIDEsAsync();
    }

    // ---------- IDEs ----------

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

    // ---------- Custom calendar ----------

    private void DateButton_Click(object? sender, RoutedEventArgs e)
    {
        TimePopup.IsOpen = false;

        var basis = _startDate ?? DateTime.Today;
        _viewMonth = new DateTime(basis.Year, basis.Month, 1);
        BuildCalendar();

        DatePopup.IsOpen = true;
    }

    private void BuildCalendar()
    {
        MonthTitle.Text = _viewMonth.ToString("MMMM yyyy");

        // No going back before the current month
        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        PrevMonthButton.IsEnabled = _viewMonth > thisMonth;

        DaysGrid.Children.Clear();

        // Sunday-first grid, always 6 rows x 7 columns
        var gridStart = _viewMonth.AddDays(-(int)_viewMonth.DayOfWeek);

        for (int i = 0; i < 42; i++)
        {
            var day = gridStart.AddDays(i);

            // Days from the previous / next month are left empty
            if (day.Month != _viewMonth.Month)
            {
                DaysGrid.Children.Add(new Border());
                continue;
            }

            var btn = new Button
            {
                Content = day.Day.ToString(),
                Tag = day,
                IsEnabled = day >= DateTime.Today
            };
            btn.Classes.Add("dayBtn");

            if (day == DateTime.Today) btn.Classes.Add("today");
            if (_startDate == day) btn.Classes.Add("selected");

            btn.Click += DayButton_Click;
            DaysGrid.Children.Add(btn);
        }

        TodayButton.IsEnabled = true;
    }

    private void PrevMonth_Click(object? sender, RoutedEventArgs e)
    {
        _viewMonth = _viewMonth.AddMonths(-1);
        BuildCalendar();
    }

    private void NextMonth_Click(object? sender, RoutedEventArgs e)
    {
        _viewMonth = _viewMonth.AddMonths(1);
        BuildCalendar();
    }

    private void TodayButton_Click(object? sender, RoutedEventArgs e) => SelectDate(DateTime.Today);

    private void DayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateTime day })
            SelectDate(day);
    }

    private void SelectDate(DateTime date)
    {
        _startDate = date.Date;
        DateText.Text = _startDate.Value.ToString("dd MMM yyyy");
        DateText.Foreground = ValueBrush;

        DatePopup.IsOpen = false;

        // Past times depend on the date, so re-check them
        RefreshTimeOptions();
    }

    // ---------- Custom time picker ----------

    private static ListBoxItem MakeItem(string text, object tag) => new ListBoxItem
    {
        Content = text,
        Tag = tag,
        HorizontalContentAlignment = HorizontalAlignment.Center
    };

    private void BuildTimeLists()
    {
        for (int h = 1; h <= 12; h++)
            HourList.Items.Add(MakeItem(h.ToString("00"), h));

        for (int m = 0; m < 60; m++)
            MinuteList.Items.Add(MakeItem(m.ToString("00"), m));

        AmPmList.Items.Add(MakeItem("AM", false));
        AmPmList.Items.Add(MakeItem("PM", true));
    }

    private static int To24(int hour12, bool pm) => (hour12 % 12) + (pm ? 12 : 0);

    // No date chosen yet counts as today, so past times are blocked from the start
    private bool IsTodaySelected() => _startDate == null || _startDate.Value.Date == DateTime.Today;

    private static int? TagAsInt(object? item) =>
        (item as ListBoxItem)?.Tag is int v ? v : (int?)null;

    private void OnTimePartChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updating) return;

        _hour12 = TagAsInt(HourList.SelectedItem);
        _minute = TagAsInt(MinuteList.SelectedItem);
        _isPm = (AmPmList.SelectedItem as ListBoxItem)?.Tag as bool?;

        TimePopupError.IsVisible = false;
        RefreshTimeOptions();
    }

    // Greys out and disables every option that is in the past (only when the date is today).
    // Clears the selection if it has become invalid.
    private void RefreshTimeOptions()
    {
        _updating = true;

        var today = IsTodaySelected();
        var now = DateTime.Now;

        // AM / PM
        foreach (var item in AmPmList.Items.OfType<ListBoxItem>())
        {
            var pm = (bool)item.Tag!;
            item.IsEnabled = !today || pm || now.Hour < 12;
        }
        if (AmPmList.SelectedItem is ListBoxItem selAmPm && !selAmPm.IsEnabled)
        {
            AmPmList.SelectedItem = null;
            _isPm = null;
        }

        // Hours
        foreach (var item in HourList.Items.OfType<ListBoxItem>())
        {
            var h = (int)item.Tag!;
            if (!today || _isPm == null)
            {
                item.IsEnabled = true;
            }
            else
            {
                var h24 = To24(h, _isPm.Value);
                item.IsEnabled = h24 > now.Hour || (h24 == now.Hour && now.Minute < 59);
            }
        }
        if (HourList.SelectedItem is ListBoxItem selHour && !selHour.IsEnabled)
        {
            HourList.SelectedItem = null;
            _hour12 = null;
        }

        // Minutes
        foreach (var item in MinuteList.Items.OfType<ListBoxItem>())
        {
            var m = (int)item.Tag!;
            if (!today || _hour12 == null || _isPm == null)
            {
                item.IsEnabled = true;
            }
            else
            {
                var h24 = To24(_hour12.Value, _isPm.Value);
                item.IsEnabled = h24 > now.Hour || (h24 == now.Hour && m > now.Minute);
            }
        }
        if (MinuteList.SelectedItem is ListBoxItem selMin && !selMin.IsEnabled)
        {
            MinuteList.SelectedItem = null;
            _minute = null;
        }

        _updating = false;

        UpdateStartTime();
    }

    private void UpdateStartTime()
    {
        if (_hour12 != null && _minute != null && _isPm != null)
        {
            var h24 = To24(_hour12.Value, _isPm.Value);
            _startTime = new TimeSpan(h24, _minute.Value, 0);
            TimeText.Text = $"{_hour12.Value:00}:{_minute.Value:00} {(_isPm.Value ? "PM" : "AM")}";
            TimeText.Foreground = ValueBrush;
        }
        else
        {
            _startTime = null;
            TimeText.Text = "Select time";
            TimeText.Foreground = PlaceholderBrush;
        }
    }

    private void TimeButton_Click(object? sender, RoutedEventArgs e)
    {
        DatePopup.IsOpen = false;

        // Start on the current period (AM or PM) the first time it is opened
        if (_isPm == null)
        {
            _updating = true;
            var pm = DateTime.Now.Hour >= 12;
            AmPmList.SelectedIndex = pm ? 1 : 0;
            _isPm = pm;
            _updating = false;
        }

        TimePopupError.IsVisible = false;
        RefreshTimeOptions();
        TimePopup.IsOpen = true;
    }

    private void TimeDoneButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_startTime == null)
        {
            TimePopupError.Text = "Pick an hour, a minute and AM or PM.";
            TimePopupError.IsVisible = true;
            return;
        }

        TimePopup.IsOpen = false;
    }

    private void ClearStartTime()
    {
        _updating = true;
        HourList.SelectedItem = null;
        MinuteList.SelectedItem = null;
        AmPmList.SelectedItem = null;
        _hour12 = null;
        _minute = null;
        _isPm = null;
        _updating = false;

        RefreshTimeOptions();
    }

    private void ClearStartDate()
    {
        _startDate = null;
        DateText.Text = "Select date";
        DateText.Foreground = PlaceholderBrush;
    }

    private void ClearStartButton_Click(object? sender, RoutedEventArgs e)
    {
        DatePopup.IsOpen = false;
        TimePopup.IsOpen = false;
        ClearStartDate();
        ClearStartTime();
    }

    // ---------- Start time for the API ----------

    // Both the date and the time are required.
    // Returns false and a message if one is missing or the result is already in the past.
    private bool TryGetStartTimeUtc(out DateTime? startUtc, out string error)
    {
        startUtc = null;
        error = "";

        if (_startDate == null)
        {
            error = "Please select a start date.";
            return false;
        }

        if (_startTime == null)
        {
            error = "Please select a start time.";
            return false;
        }

        var local = DateTime.SpecifyKind(_startDate.Value.Date + _startTime.Value, DateTimeKind.Local);

        if (local <= DateTime.Now)
        {
            error = "That start time has already passed. Pick a later time.";
            return false;
        }

        startUtc = local.ToUniversalTime();
        return true;
    }

    // ---------- Create ----------

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
        ClearStartDate();
        ClearStartTime();
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