using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CodeTrace.Teacher.Views;

public partial class CreateExamView : UserControl
{
    public CreateExamView()
    {
        InitializeComponent();
    }

    private void CreateExamButton_Click(object? sender, RoutedEventArgs e)
    {
        string examName = ExamNameBox.Text ?? "";
        string description = DescriptionBox.Text ?? "";

        int duration = (int)(DurationBox.Value ?? 60);

        if (string.IsNullOrWhiteSpace(examName))
        {
            StatusText.Text = "Please enter an examination name.";
            return;
        }

        var exam = new Exam
        {
            Name = examName,
            Description = description,
            DurationMinutes = duration
        };

        ExamStore.Exams.Add(exam);

        StatusText.Text = "Examination created successfully!";
    }
}