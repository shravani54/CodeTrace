using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;

namespace CodeTrace.Teacher;

public partial class ManageExamsWindow : Window
{
    public ManageExamsWindow()
    {
        InitializeComponent();
        LoadExams();
    }

    private void LoadExams()
    {
        ExamListBox.ItemsSource = ExamStore.Exams
            .Select(exam =>
                $"{exam.Name} | {exam.DurationMinutes} minutes | {exam.Description}")
            .ToList();
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        LoadExams();
    }
}