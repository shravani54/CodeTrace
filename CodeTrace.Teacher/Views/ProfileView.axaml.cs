using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CodeTrace.Teacher.Views;

public partial class ProfileView : UserControl
{
    public event EventHandler? LogoutRequested;

    public ProfileView() : this("Teacher") { }

    public ProfileView(string username)
    {
        InitializeComponent();

        var display = string.IsNullOrWhiteSpace(username) ? "Teacher" : username;

        UsernameText.Text = display;
        UsernameDetailText.Text = display;
        InitialText.Text = display.Substring(0, 1).ToUpper();
    }

    private void LogoutButton_Click(object? sender, RoutedEventArgs e)
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }
}