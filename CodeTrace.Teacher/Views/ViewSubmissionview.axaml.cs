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

public partial class ViewSubmissionsView : UserControl
{
    private readonly ExamApiService _examService = new();
    private readonly SubmissionListService _submissionService = new();
    private readonly SubmissionZipService _zipService = new();
    private readonly DispatcherTimer _timer;

    // null = showing the exam list, otherwise the exam whose submissions are open
    private int? _selectedExamId;
    private string _selectedExamName = "";

    private string _lastRowsSignature = "";
    private bool _refreshing;

    public ViewSubmissionsView()
    {
        InitializeComponent();

        // Refresh every 5 seconds while this page is on screen
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
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
            var exams = await _examService.GetAllAsync();
            var submissions = await _submissionService.GetAllAsync();

            ErrorText.IsVisible = false;

            if (_selectedExamId == null)
                ShowExamList(exams, submissions);
            else
                await ShowSubmissionsAsync(submissions);
        }
        catch (Exception ex)
        {
            ErrorText.Text = "Cannot load submissions: " + ex.Message;
            ErrorText.IsVisible = true;
        }
        finally
        {
            _refreshing = false;
        }
    }

    // ---------- Step 1: exams ----------

    private void ShowExamList(List<Exam> exams, List<SubmissionItem> submissions)
    {
        ExamsPanel.Children.Clear();

        foreach (var e in exams.OrderByDescending(x => x.Id))
        {
            var count = submissions.Count(s => s.ExamId == e.Id);
            ExamsPanel.Children.Add(BuildExamCard(e.Id, e.Name, $"{e.DurationMinutes} min", count));
        }

        // Submissions of exams that were already deleted (exams are removed after 24 hours)
        var knownIds = exams.Select(x => x.Id).ToHashSet();
        foreach (var group in submissions.Where(s => !knownIds.Contains(s.ExamId)).GroupBy(s => s.ExamId))
            ExamsPanel.Children.Add(BuildExamCard(group.Key, $"Exam #{group.Key} (removed)", "Exam no longer exists", group.Count()));

        ExamsCountText.Text = $"Exams: {ExamsPanel.Children.Count}";
        NoExamsText.IsVisible = ExamsPanel.Children.Count == 0;
    }

    private Button BuildExamCard(int id, string name, string info, int count)
    {
        var title = new TextBlock
        {
            Text = name,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
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
            Text = count == 1 ? "1 submission" : $"{count} submissions",
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
        button.Click += async (_, _) =>
        {
            _selectedExamId = id;
            _selectedExamName = name;
            _lastRowsSignature = "";
            ApplyLayout();
            await RefreshAsync();
        };
        return button;
    }

    // ---------- Step 2: submissions of one exam ----------

    private async Task ShowSubmissionsAsync(List<SubmissionItem> all)
    {
        var selected = all
            .Where(s => s.ExamId == _selectedExamId)
            .OrderByDescending(s => s.SubmittedAt)
            .ToList();

        var zipIds = await _zipService.GetIdsWithZipAsync();

        ExamTitleText.Text = _selectedExamName;
        CountText.Text = $"Submissions: {selected.Count}";
        EmptyText.IsVisible = selected.Count == 0;

        // Rebuild the rows only when something changed, so a running download is not interrupted
        var signature = string.Join(",", selected.Select(s => $"{s.Id}:{(zipIds.Contains(s.Id) ? 1 : 0)}"));
        if (signature == _lastRowsSignature) return;
        _lastRowsSignature = signature;

        RowsPanel.Children.Clear();

        var number = 1;
        foreach (var s in selected)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("50,*,150,170"),
                Margin = new Thickness(8, 6)
            };

            row.Children.Add(Cell(number.ToString(), 0));
            row.Children.Add(Cell(s.StudentName, 1));
            row.Children.Add(Cell(AsLocal(s.SubmittedAt).ToString("dd MMM, hh:mm tt"), 2));

            Control codeCell;
            if (zipIds.Contains(s.Id))
            {
                codeCell = SubmissionZipButton.Create(s.Id, s.StudentName);
                codeCell.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else
            {
                codeCell = new TextBlock
                {
                    Text = "No code ZIP",
                    Foreground = new SolidColorBrush(Color.Parse("#7F8BC4")),
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            Grid.SetColumn(codeCell, 3);
            row.Children.Add(codeCell);

            RowsPanel.Children.Add(row);
            number++;
        }
    }

    // The server stores UTC; show the teacher's local time
    private static DateTime AsLocal(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();

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

    private void ApplyLayout()
    {
        var showingExams = _selectedExamId == null;

        ExamsSection.IsVisible = showingExams;
        SubmissionsSection.IsVisible = !showingExams;

        SubtitleText.Text = showingExams
            ? "Pick an exam to see what students submitted."
            : $"Submissions for {_selectedExamName}.";
    }

    private async void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        _selectedExamId = null;
        _selectedExamName = "";
        _lastRowsSignature = "";
        ApplyLayout();
        await RefreshAsync();
    }
}