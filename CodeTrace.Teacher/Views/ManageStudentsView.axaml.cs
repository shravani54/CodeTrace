using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class ManageStudentsView : UserControl
{
    private readonly StudentListService _service = new();
    private readonly ExamApiService _examService = new();
    private readonly DispatcherTimer _timer;

    // null = showing the exam list; 0 = "not in a listed exam" group; otherwise the selected exam id
    private int? _selectedExamId;
    private string _selectedExamName = "";

    private List<ConnectedStudent> _lastStudents = new();
    private HashSet<int> _knownExamIds = new();   // ids of the exams shown in the list
    private bool _refreshing;

    public ManageStudentsView()
    {
        InitializeComponent();

        // Refresh every 3 seconds while this page is on screen
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        AttachedToVisualTree += (_, _) =>
        {
            ApplyLayout();
            _ = RefreshAsync();
            _timer.Start();
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        try
        {
            var students = await _service.GetAllAsync();

            if (students == null)
            {
                ErrorText.Text = "Cannot reach the server.";
                ErrorText.IsVisible = true;
                return;
            }

            ErrorText.IsVisible = false;
            _lastStudents = students;

            if (_selectedExamId == null)
                await ShowExamListAsync(students);
            else
                ShowStudents(students);
        }
        catch (Exception ex)
        {
            // Shows the real problem under the card instead of failing silently
            ErrorText.Text = "Error: " + ex.Message;
            ErrorText.IsVisible = true;
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ---------- Step 1: all examinations ----------

    private static DateTime ToUtc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private async Task ShowExamListAsync(List<ConnectedStudent> students)
    {
        var exams = await _examService.GetAllAsync();
        var now = DateTime.UtcNow;

        // Every exam is listed (the server already hides exams older than 24 hours), newest first
        var ordered = exams.OrderByDescending(e => e.Id).ToList();
        _knownExamIds = ordered.Select(e => e.Id).ToHashSet();

        ExamsPanel.Children.Clear();

        foreach (var e in ordered)
        {
            string status;
            if (!e.IsActive)
            {
                status = "Not active";
            }
            else if (e.StartTime is not DateTime start)
            {
                status = "Open (no start time)";
            }
            else
            {
                var startUtc = ToUtc(start);
                var startLocal = startUtc.ToLocalTime();

                if (startUtc > now)
                    status = $"Starts {startLocal:hh:mm tt}";
                else if (startUtc.AddMinutes(e.DurationMinutes) > now)
                    status = $"Ongoing, started {startLocal:hh:mm tt}";
                else
                    status = "Ended";
            }

            var info = $"{status}  •  {e.DurationMinutes} min";
            var count = students.Count(s => s.ExamId == e.Id);
            ExamsPanel.Children.Add(BuildExamCard(e.Id, e.Name, info, count));
        }

        // Students who are not in any listed exam: not joined yet, or their exam no longer exists
        var other = students.Count(s => s.ExamId == null || !_knownExamIds.Contains(s.ExamId.Value));
        if (other > 0)
            ExamsPanel.Children.Add(BuildExamCard(0, "Not in a listed exam", "Logged in, but not in an exam above", other));

        ExamsCountText.Text = $"Examinations: {ordered.Count}";
        NoExamsText.Text = "No examinations yet. Create one first.";
        NoExamsText.IsVisible = ExamsPanel.Children.Count == 0;
    }

    private Button BuildExamCard(int id, string name, string info, int count)
    {
        var title = new TextBlock { Text = name };
        title.Classes.Add("examName");   // dark, bold (style in the XAML)

        var sub = new TextBlock
        {
            Text = info,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#475569"))
        };

        var left = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(title);
        left.Children.Add(sub);

        var countText = new TextBlock
        {
            Text = count == 1 ? "1 student" : $"{count} students",
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#0369A1")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0)
        };

        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = 24,
            Foreground = new SolidColorBrush(Color.Parse("#0369A1")),
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        Grid.SetColumn(countText, 1);
        Grid.SetColumn(arrow, 2);
        grid.Children.Add(left);
        grid.Children.Add(countText);
        grid.Children.Add(arrow);

        var button = new Button { Content = grid };
        button.Classes.Add("examCard");
        button.Click += (_, _) => OpenExam(id, name);
        return button;
    }

    private void OpenExam(int id, string name)
    {
        _selectedExamId = id;
        _selectedExamName = name;
        ApplyLayout();
        ShowStudents(_lastStudents);
    }

    // ---------- Step 2: students of one exam ----------

    private void ShowStudents(List<ConnectedStudent> students)
    {
        var selected = students
            .Where(s => _selectedExamId == 0
                ? (s.ExamId == null || !_knownExamIds.Contains(s.ExamId.Value))
                : s.ExamId == _selectedExamId)
            .ToList();

        RowsPanel.Children.Clear();

        var number = 1;
        foreach (var s in selected)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("50,*,140,180,110"),
                Margin = new Thickness(8, 6)
            };

            row.Children.Add(Cell(number.ToString(), 0));
            row.Children.Add(Cell(s.Name, 1));
            row.Children.Add(Cell(s.Roll, 2));
            row.Children.Add(Cell(s.Status, 3));
            row.Children.Add(Cell(s.LoggedInAt.ToLocalTime().ToString("hh:mm tt"), 4));

            RowsPanel.Children.Add(row);
            number++;
        }

        ExamTitleText.Text = _selectedExamName;
        CountText.Text = $"Students in this exam: {selected.Count}";
        EmptyText.IsVisible = selected.Count == 0;
    }

    private static TextBlock Cell(string text, int column)
    {
        var cell = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(cell, column);
        return cell;
    }

    // Shows either the exam list or the student table
    private void ApplyLayout()
    {
        var showingExams = _selectedExamId == null;

        ExamsSection.IsVisible = showingExams;
        StudentsSection.IsVisible = !showingExams;

        SubtitleText.Text = showingExams
            ? "Pick an exam to see the students who joined it."
            : $"Students who joined {_selectedExamName}.";
    }

    private async void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        _selectedExamId = null;
        _selectedExamName = "";
        ApplyLayout();
        await RefreshAsync();
    }

    private async void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        // Clears only the students of the exam that is open
        await _service.ClearAsync(_selectedExamId);
        await RefreshAsync();
    }
}