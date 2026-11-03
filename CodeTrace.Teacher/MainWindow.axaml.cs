using Avalonia.Controls;
using CodeTrace.Teacher.Views;

namespace CodeTrace.Teacher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowLogin();
    }

    private void ShowLogin()
    {
        var login = new LoginView();
        login.LoginSucceeded += (_, username) => ShowDashboard(username);
        login.SignUpRequested += (_, _) => ShowSignUp();
        MainContent.Content = login;
    }

    private void ShowSignUp()
    {
        var signUp = new SignUpView();
        signUp.AccountCreated += (_, username) => ShowDashboard(username);
        signUp.BackToLoginRequested += (_, _) => ShowLogin();
        MainContent.Content = signUp;
    }

    private void ShowDashboard(string username)
    {
        var dashboard = new TeacherDashboard(username);
        dashboard.LogoutRequested += (_, _) => ShowLogin();
        MainContent.Content = dashboard;
    }
}