using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class SignUpView : UserControl
{
    private static readonly Regex UsernamePattern = new(@"^[A-Za-z0-9._]{3,30}$");

    private readonly AuthApiService _auth = new();
    private bool _busy;

    // Raised with the username when the account was created (the teacher is signed in right away)
    public event EventHandler<string>? AccountCreated;
    public event EventHandler? BackToLoginRequested;

    public SignUpView()
    {
        InitializeComponent();
    }

    private async void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var fullName = (FullNameBox.Text ?? "").Trim();
        var username = (UsernameBox.Text ?? "").Trim();
        var password = PasswordBox.Text ?? "";
        var confirm = ConfirmBox.Text ?? "";
        var accessCode = AccessCodeBox.Text ?? "";

        if (fullName.Length < 2) { ShowError("Please enter your full name."); return; }
        if (!UsernamePattern.IsMatch(username))
        {
            ShowError("Username must be 3 to 30 characters: letters, numbers, . or _ only.");
            return;
        }
        if (password.Length < 6) { ShowError("Password must be at least 6 characters."); return; }
        if (password != confirm) { ShowError("The two passwords do not match."); return; }

        SetBusy(true);
        var (result, error) = await _auth.RegisterAsync(username, password, fullName, accessCode);
        SetBusy(false);

        if (result == null)
        {
            ShowError(error);
            return;
        }

        StatusText.Foreground = new SolidColorBrush(Color.Parse("#6FE3B0"));
        StatusText.Text = "Account created. Signing you in...";

        AccountCreated?.Invoke(this, string.IsNullOrWhiteSpace(result.Username) ? username : result.Username);
    }

    private void BackToLogin_Click(object? sender, RoutedEventArgs e)
    {
        BackToLoginRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CreateButton.IsEnabled = !busy;
        SignUpProgress.IsVisible = busy;
        if (busy) StatusText.Text = "";
    }

    private void ShowError(string message)
    {
        StatusText.Foreground = new SolidColorBrush(Color.Parse("#FF7B8A"));
        StatusText.Text = message;
    }
}