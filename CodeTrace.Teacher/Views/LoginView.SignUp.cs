using System;
using Avalonia.Interactivity;

namespace CodeTrace.Teacher.Views;

public partial class LoginView
{
    // Raised when the teacher clicks "Create an account"
    public event EventHandler? SignUpRequested;

    private void SignUpLink_Click(object? sender, RoutedEventArgs e)
    {
        SignUpRequested?.Invoke(this, EventArgs.Empty);
    }
}