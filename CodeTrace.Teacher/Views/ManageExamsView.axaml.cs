using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CodeTrace.Teacher.Views;

public partial class ManageExamsView : UserControl
{
    private StackPanel? _examListPanel;

    public ManageExamsView()
    {
        InitializeComponent();

        _examListPanel = this.FindControl<StackPanel>("ExamListPanel");

        LoadExams();
    }

    private void LoadExams()
    {
        if (_examListPanel == null)
            return;

        _examListPanel.Children.Clear();

        if (ExamStore.Exams.Count == 0)
        {
            var emptyMessage = new TextBlock
            {
                Text = "No examinations created yet.",
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50)
            };

            _examListPanel.Children.Add(emptyMessage);
            return;
        }

        foreach (var exam in ExamStore.Exams)
        {
            CreateExamCard(exam);
        }
    }

    private void CreateExamCard(Exam exam)
    {
        if (_examListPanel == null)
            return;

        var card = new Border
        {
            Padding = new Thickness(20),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var layout = new StackPanel
        {
            Spacing = 10
        };

        var nameText = new TextBlock
        {
            Text = exam.Name,
            FontSize = 22,
            FontWeight = FontWeight.Bold
        };

        var durationText = new TextBlock
        {
            Text = $"Duration: {exam.DurationMinutes} minutes",
            FontSize = 15
        };

        var descriptionText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(exam.Description)
                ? "No description provided."
                : exam.Description,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var editButton = new Button
        {
            Content = "Edit",
            Width = 100
        };

        var deleteButton = new Button
        {
            Content = "Delete",
            Width = 100
        };

        var questionsButton = new Button
        {
            Content = "Add Questions",
            Width = 140
        };

        // Delete examination
        deleteButton.Click += (_, _) =>
        {
            ExamStore.Exams.Remove(exam);
            LoadExams();
        };

        // These will be implemented next.
        editButton.Click += (_, _) =>
        {
            // Edit functionality will be added next.
        };

        questionsButton.Click += (_, _) =>
        {
            // Add Questions functionality will be added next.
        };

        buttons.Children.Add(editButton);
        buttons.Children.Add(deleteButton);
        buttons.Children.Add(questionsButton);

        layout.Children.Add(nameText);
        layout.Children.Add(durationText);
        layout.Children.Add(descriptionText);
        layout.Children.Add(buttons);

        card.Child = layout;

        _examListPanel.Children.Add(card);
    }
}