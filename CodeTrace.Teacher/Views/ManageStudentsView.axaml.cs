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

    // null = showing the exam list; 0 = "no exam chosen yet" group; otherwise the selected exam id
    private int? _selectedExamId;
    private string _selectedExamName = "";

    private List<ConnectedStudent> _lastStudents = new();
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

    // ---------- Step 1: ongoing exams ----------

    private async Task ShowExamListAsync(List<ConnectedStudent> students)
    {
        var exams = await _examService.GetAllAsync();
        var now = DateTime.UtcNow;

        // Ongoing = active, and its time has not run out yet
        var ongoing = exams
            .Where(e => e.IsActive &&
                        (e.StartTime == null || e.StartTime.Value.AddMinutes(e.DurationMinutes) > now))
            .OrderBy(e => e.StartTime ?? DateTime.MinValue)
            .ToList();

        ExamsPanel.Children.Clear();

        foreach (var e in ongoing)
        {
            string info;
            if (e.StartTime == null)
                info = $"Ongoing  •  {e.DurationMinutes} min";
            else if (e.StartTime.Value > now)
                info = $"Starts {e.StartTime.Value.ToLocalTime():hh:mm tt}  •  {e.DurationMinutes} min";
            else
                info = $"Started {e.StartTime.Value.ToLocalTime():hh:mm tt}  •  {e.DurationMinutes} min";

            var count = students.Count(s => s.ExamId == e.Id);
            ExamsPanel.Children.Add(BuildExamCard(e.Id, e.Name, info, count));
        }

        // Students who are approved but have not picked an exam yet
        var waiting = students.Count(s => s.ExamId == null);
        if (waiting > 0)
            ExamsPanel.Children.Add(BuildExamCard(0, "Not in an exam yet", "Approved, still choosing an exam", waiting));

        ExamsCountText.Text = $"Ongoing exams: {ongoing.Count}";
        NoExamsText.IsVisible = ExamsPanel.Children.Count == 0;
    }

    private Button BuildExamCard(int id, string name, string info, int count)
    {
        var title = new TextBlock
    {
        Text = name,
        FontSize = 18,
        FontWeight = FontWeight.Bold,
        Foreground = Brush.Parse("#0C4A6E")
    };

    var sub = new TextBlock
    {
        Text = info,
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.Parse("#98A4D6"))
    };

        var left = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(title);
        left.Children.Add(sub);

        var countText = new TextBlock
        {
            Text = count == 1 ? "1 student" : $"{count} students",
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#6EA8FF")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0)
        };

        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = 24,
            Foreground = new SolidColorBrush(Color.Parse("#7F8BC4")),
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
            .Where(s => _selectedExamId == 0 ? s.ExamId == null : s.ExamId == _selectedExamId)
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