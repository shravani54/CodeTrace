using System;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class SignUpView : UserControl
{
    // Letters (any language), spaces, . ' - ; must start with a letter
    private static readonly Regex NamePattern = new(@"^\p{L}[\p{L} .'\-]{1,59}$");

    // Starts with a letter, then letters/numbers/./_ ; 3 to 30 characters
    private static readonly Regex UsernamePattern = new(@"^[A-Za-z][A-Za-z0-9._]{2,29}$");

    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 64;

    private static readonly IBrush NeutralBrush = Brush.Parse("#64748B");
    private static readonly IBrush OkBrush = Brush.Parse("#047857");
    private static readonly IBrush ErrorBrush = Brush.Parse("#BE123C");

    private readonly AuthApiService _auth = new();
    private bool _busy;

    // Raised with the username when the account was created (the teacher is signed in right away)
    public event EventHandler<string>? AccountCreated;
    public event EventHandler? BackToLoginRequested;

    public SignUpView()
    {
        InitializeComponent();

        UsernameBox.TextChanged += (_, _) => UpdateUsernameHint();
        PasswordBox.TextChanged += (_, _) => { UpdatePasswordRules(); UpdateMatchHint(); };
        ConfirmBox.TextChanged += (_, _) => UpdateMatchHint();

        UpdatePasswordRules();
    }

    // ---------- Validation helpers ----------

    private static bool HasSpecial(string s) =>
        s.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));

    private static bool IsValidUsername(string u) =>
        UsernamePattern.IsMatch(u) && !u.EndsWith('.') && !u.EndsWith('_');

    // Returns an error message, or null when the password is fine
    private static string? CheckPassword(string password, string username)
    {
        if (password.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters.";
        if (password.Length > MaxPasswordLength)
            return $"Password cannot be longer than {MaxPasswordLength} characters.";
        if (password.Any(char.IsWhiteSpace))
            return "Password cannot contain spaces.";
        if (!password.Any(char.IsUpper))
            return "Password needs at least one uppercase letter (A-Z).";
        if (!password.Any(char.IsLower))
            return "Password needs at least one lowercase letter (a-z).";
        if (!password.Any(char.IsDigit))
            return "Password needs at least one number (0-9).";
        if (!HasSpecial(password))
            return "Password needs at least one special symbol, for example ! @ # $ % & *";
        if (username.Length >= 3 && password.Contains(username, StringComparison.OrdinalIgnoreCase))
            return "Password cannot contain your username.";
        return null;
    }

    // ---------- Live hints ----------

    private static void SetRule(TextBlock block, bool ok, string text)
    {
        block.Text = (ok ? "✓  " : "○  ") + text;
        block.Foreground = ok ? OkBrush : NeutralBrush;
    }

    private void UpdatePasswordRules()
    {
        var p = PasswordBox.Text ?? "";

        SetRule(RuleLength, p.Length >= MinPasswordLength, $"At least {MinPasswordLength} characters");
        SetRule(RuleUpper, p.Any(char.IsUpper), "One uppercase letter (A-Z)");
        SetRule(RuleLower, p.Any(char.IsLower), "One lowercase letter (a-z)");
        SetRule(RuleDigit, p.Any(char.IsDigit), "One number (0-9)");
        SetRule(RuleSpecial, HasSpecial(p), "One special symbol (! @ # $ % & * ...)");
        SetRule(RuleSpace, p.Length > 0 && !p.Any(char.IsWhiteSpace), "No spaces");
    }

    private void SetUsernameHint(string text, IBrush brush)
    {
        UsernameHint.Text = text;
        UsernameHint.Foreground = brush;
    }

    private void UpdateUsernameHint()
    {
        var u = (UsernameBox.Text ?? "").Trim();

        if (u.Length == 0)
            SetUsernameHint("3 to 30 characters. Must start with a letter.", NeutralBrush);
        else if (!char.IsAsciiLetter(u[0]))
            SetUsernameHint("Username must start with a letter.", ErrorBrush);
        else if (u.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '.' || c == '_')))
            SetUsernameHint("Use letters, numbers, . or _ only.", ErrorBrush);
        else if (u.Length < 3)
            SetUsernameHint("Too short. Use at least 3 characters.", ErrorBrush);
        else if (u.EndsWith('.') || u.EndsWith('_'))
            SetUsernameHint("Username cannot end with . or _", ErrorBrush);
        else
            SetUsernameHint("✓  Looks good", OkBrush);
    }

    private void UpdateMatchHint()
    {
        var p = PasswordBox.Text ?? "";
        var c = ConfirmBox.Text ?? "";

        if (c.Length == 0)
        {
            MatchHint.IsVisible = false;
            return;
        }

        MatchHint.IsVisible = true;

        if (p == c)
        {
            MatchHint.Text = "✓  Passwords match";
            MatchHint.Foreground = OkBrush;
        }
        else
        {
            MatchHint.Text = "Passwords do not match yet";
            MatchHint.Foreground = ErrorBrush;
        }
    }

    // ---------- Create account ----------

    private async void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;

        // Collapse repeated spaces in the name: "Prof.   Sharma" -> "Prof. Sharma"
        var fullName = Regex.Replace((FullNameBox.Text ?? "").Trim(), @"\s+", " ");
        var username = (UsernameBox.Text ?? "").Trim();
        var password = PasswordBox.Text ?? "";
        var confirm = ConfirmBox.Text ?? "";

        // Full name
        if (fullName.Length == 0) { ShowError("Please enter your full name."); return; }
        if (fullName.Length < 2) { ShowError("Full name must be at least 2 characters."); return; }
        if (!NamePattern.IsMatch(fullName))
        {
            ShowError("Full name can only have letters, spaces, . ' and -, and must start with a letter.");
            return;
        }

        // Username
        if (username.Length == 0) { ShowError("Please choose a username."); return; }
        if (username.Length < 3) { ShowError("Username must be at least 3 characters."); return; }
        if (!char.IsAsciiLetter(username[0])) { ShowError("Username must start with a letter."); return; }
        if (!IsValidUsername(username))
        {
            ShowError("Username can only have letters, numbers, . or _ and cannot end with . or _");
            return;
        }

        // Password
        if (password.Length == 0) { ShowError("Please create a password."); return; }
        var passwordError = CheckPassword(password, username);
        if (passwordError != null) { ShowError(passwordError); return; }

        // Confirm
        if (confirm.Length == 0) { ShowError("Please type the password again to confirm it."); return; }
        if (password != confirm) { ShowError("The two passwords do not match."); return; }

        SetBusy(true);
        // The access code is no longer asked for, so an empty one is sent
        var (result, error) = await _auth.RegisterAsync(username, password, fullName, "");
        SetBusy(false);

        if (result == null)
        {
            ShowError(error);
            return;
        }

        StatusText.Foreground = OkBrush;
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
        StatusText.Foreground = ErrorBrush;
        StatusText.Text = "⚠ " + message;
    }
}