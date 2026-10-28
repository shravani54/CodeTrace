using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class LoginView : UserControl
{
    private readonly AuthApiService _authApi = new();

    // Username: letters only (A-Z, a-z), 3-30 characters, no digits, spaces or symbols
    private static readonly Regex UsernameRegex = new(@"^[A-Za-z]{3,30}$");

    private bool _sanitizing;

    public event EventHandler<string>? LoginSucceeded;

    public LoginView()
    {
        InitializeComponent();

        // Block digits, spaces and symbols while typing
        UsernameBox.AddHandler(TextInputEvent, UsernameBox_TextInput, RoutingStrategies.Tunnel);

        // Clean pasted text, and clear old errors as soon as the user edits a field
        UsernameBox.TextChanged += UsernameBox_TextChanged;
        PasswordBox.TextChanged += (_, _) => ClearStatus();
    }

    private void UsernameBox_TextInput(object? sender, TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && !e.Text.All(char.IsAsciiLetter))
            e.Handled = true;
    }

    private void UsernameBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_sanitizing) return;

        string text = UsernameBox.Text ?? "";
        string clean = new(text.Where(char.IsAsciiLetter).ToArray());

        if (clean != text)
        {
            _sanitizing = true;
            int caret = UsernameBox.CaretIndex;
            UsernameBox.Text = clean;
            UsernameBox.CaretIndex = Math.Min(caret, clean.Length);
            _sanitizing = false;
        }

        ClearStatus();
    }

    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        string username = UsernameBox.Text?.Trim() ?? "";
        string password = PasswordBox.Text ?? "";

        // ---- Username validation ----
        if (string.IsNullOrWhiteSpace(username))
        {
            SetStatus("Please enter your username.", false, UsernameBox);
            UsernameBox.Focus();
            return;
        }

        if (!UsernameRegex.IsMatch(username))
        {
            SetStatus("Username must be 3-30 letters only. Digits, spaces and symbols are not allowed.", false, UsernameBox);
            UsernameBox.Focus();
            return;
        }

        // Password is not validated, only checked that it is not empty
        if (string.IsNullOrEmpty(password))
        {
            SetStatus("Please enter your password.", false, PasswordBox);
            PasswordBox.Focus();
            return;
        }

        // ---- Sign in ----
        SetLoading(true);
        SetStatus("Signing in...", true);

        try
        {
            var result = await _authApi.LoginAsync(username, password);

            if (result != null)
            {
                LoginSucceeded?.Invoke(this, username);
            }
            else
            {
                SetStatus("Invalid username or password.", false);
            }
        }
        catch (HttpRequestException)
        {
            SetStatus("Cannot reach the server. Check that the API is running and the address in apiconfig.json is correct.", false);
        }
        catch (TaskCanceledException)
        {
            SetStatus("The server took too long to respond. Please try again.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Login failed: {ex.Message}", false);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        LoginButton.IsEnabled = !isLoading;
        LoginProgress.IsVisible = isLoading;
        UsernameBox.IsEnabled = !isLoading;
        PasswordBox.IsEnabled = !isLoading;
    }

    /// <param name="neutral">true = info message, false = error</param>
    /// <param name="badField">optional field to highlight with a red border</param>
    private void SetStatus(string message, bool neutral, TextBox? badField = null)
    {
        UsernameBox.Classes.Set("error", false);
        PasswordBox.Classes.Set("error", false);
        if (!neutral && badField != null)
            badField.Classes.Set("error", true);

        StatusText.Text = message;

        string key = neutral ? "LoginMutedText" : "LoginErrorText";
        if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush brush)
            StatusText.Foreground = brush;
    }

    private void ClearStatus()
    {
        StatusText.Text = "";
        UsernameBox.Classes.Set("error", false);
        PasswordBox.Classes.Set("error", false);
    }
}