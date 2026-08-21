using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CodeTrace.Teacher;

public partial class CreateExamWindow : Window
{
    public CreateExamWindow()
    {
        InitializeComponent();
    }

    private void CreateExamButton_Click(object? sender, RoutedEventArgs e)
    {
        string examName = ExamNameBox.Text ?? "";
        string description = DescriptionBox.Text ?? "";

        if (string.IsNullOrWhiteSpace(examName))
        {
            StatusText.Text = "Please enter an examination name.";
            return;
        }

        StatusText.Text = "Examination created successfully!";
    }
}