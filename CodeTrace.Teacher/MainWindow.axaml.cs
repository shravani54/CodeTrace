using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CodeTrace.Teacher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        string username = UsernameBox.Text ?? "";
        string password = PasswordBox.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text = "Please enter username and password.";
            return;
        }

        if (username == "teacher" && password == "1234")
        {
            MainContent.Content = new TeacherDashboard();
        }
        else
        {
            StatusText.Text = "Invalid username or password.";
        }
    }
}