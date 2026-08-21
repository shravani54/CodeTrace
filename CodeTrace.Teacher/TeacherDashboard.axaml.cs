using Avalonia.Controls;
using Avalonia.Interactivity;
using CodeTrace.Teacher.Views;

namespace CodeTrace.Teacher;

public partial class TeacherDashboard : UserControl
{
    public TeacherDashboard()
    {
        InitializeComponent();

        MainContent.Content = new TextBlock
        {
            Text = "Welcome to the Teacher Dashboard",
            FontSize = 30
        };
    }

    private void CreateExamButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        MainContent.Content = new CreateExamView();
    }

    private void ManageExamsButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        MainContent.Content = new ManageExamsView();
    }

    private void MonitorStudentsButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        MainContent.Content = new TextBlock
        {
            Text = "Monitor Students",
            FontSize = 30
        };
    }

    private void ViewSubmissionsButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        MainContent.Content = new TextBlock
        {
            Text = "View Submissions",
            FontSize = 30
        };
    }

    private void ManageIDEsButton_Click(
    object? sender,
    RoutedEventArgs e)
{
    MainContent.Content = new Views.ManageIDEsView();
}
}