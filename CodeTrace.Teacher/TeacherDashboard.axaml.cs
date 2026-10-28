using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeTrace.Teacher.Services;
using CodeTrace.Teacher.Views;

namespace CodeTrace.Teacher;

public partial class TeacherDashboard : UserControl
{
    private List<Button> _navButtons = new();
    private readonly string _username;

    // Alerts about students opening apps that are not allowed
    private readonly AppAlertService _alertService = new();
    private readonly DispatcherTimer _alertTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private long _lastAlertId;
    private bool _alertsReady;

    public event EventHandler? LogoutRequested;

    public TeacherDashboard() : this("Teacher") { }

    public TeacherDashboard(string username)
    {
        InitializeComponent();

        _username = string.IsNullOrWhiteSpace(username) ? "Teacher" : username;

        _navButtons = new List<Button>
        {
            CreateExamNavButton,
            ManageExamsNavButton,
            JoinRequestsNavButton,
            ManageStudentsNavButton,
            MonitorStudentsNavButton,
            ViewSubmissionsNavButton,
            ManageIDEsNavButton,
            ProfileNavButton
        };

        FooterUsernameText.Text = _username;
        FooterInitialText.Text = _username.Substring(0, 1).ToUpper();

        ShowWelcome(_username);

        // Check for new alerts every 2 seconds while the teacher is signed in
        _alertTimer.Tick += async (_, _) => await CheckAlertsAsync();
        AttachedToVisualTree += (_, _) => _alertTimer.Start();
        DetachedFromVisualTree += (_, _) => _alertTimer.Stop();
    }

    // ---------- Welcome page ----------
    private void ShowWelcome(string username)
    {
        var welcome = new WelcomeView(username);

        welcome.OpenRequested += key =>
        {
            switch (key)
            {
                case "create":   CreateExamButton_Click(this, new RoutedEventArgs()); break;
                case "manage":   ManageExamsButton_Click(this, new RoutedEventArgs()); break;
                case "requests": JoinRequestsButton_Click(this, new RoutedEventArgs()); break;
                case "monitor":  MonitorStudentsButton_Click(this, new RoutedEventArgs()); break;
            }
        };

        MainContent.Content = welcome;
    }

    // ---------- Notifications ----------
    private async Task CheckAlertsAsync()
    {
        var batch = await _alertService.GetAfterAsync(_lastAlertId);
        if (batch == null) return;

        // The API was restarted and its numbering started again: begin again from the start
        if (batch.LatestId < _lastAlertId)
        {
            _lastAlertId = 0;
            return;
        }

        // First check: remember what already exists, so old alerts do not pop up
        if (!_alertsReady)
        {
            _lastAlertId = batch.LatestId;
            _alertsReady = true;
            return;
        }

        foreach (var alert in batch.Alerts)
            ShowToast(alert);

        _lastAlertId = batch.LatestId;
    }

    private void ShowToast(AppAlert alert)
    {
        var who = string.IsNullOrWhiteSpace(alert.Roll)
            ? alert.Name
            : $"{alert.Name} (Roll {alert.Roll})";

        var closeButton = new Button
        {
            Content = "✕",
            Classes = { "ghost" },
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(closeButton, 1);

        var text = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = "⚠ Unauthorized app opened",
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Bold,
                    FontSize = 14
                },
                new TextBlock
                {
                    Text = who,
                    Foreground = Brushes.White,
                    FontSize = 15,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = $"App: {alert.AppName}   •   {alert.Time.ToLocalTime():hh:mm:ss tt}",
                    Foreground = Brushes.White,
                    FontSize = 12
                }
            }
        };

        var toast = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#B91C1C")),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12),
            MinWidth = 320,
            MaxWidth = 420,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { text, closeButton }
            }
        };

        void Dismiss() => ToastPanel.Children.Remove(toast);

        closeButton.Click += (_, _) => Dismiss();

        // The notification disappears by itself after 15 seconds
        var dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        dismissTimer.Tick += (_, _) =>
        {
            dismissTimer.Stop();
            Dismiss();
        };
        dismissTimer.Start();

        // Newest on top, at most 5 on screen
        ToastPanel.Children.Insert(0, toast);
        while (ToastPanel.Children.Count > 5)
            ToastPanel.Children.RemoveAt(ToastPanel.Children.Count - 1);
    }

    // ---------- Navigation ----------
    private void SetActiveNav(Button active)
    {
        foreach (var button in _navButtons)
            button.Classes.Remove("active");

        active.Classes.Add("active");
    }

    private void CreateExamButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(CreateExamNavButton);
        MainContent.Content = new CreateExamView();
    }

    private void ManageExamsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(ManageExamsNavButton);
        MainContent.Content = new ManageExamsView();
    }

    // Students who clicked Connect appear here. Keep this screen open while students connect.
    // JoinRequestsView has its own ScrollViewer, so it is not wrapped in another one.
    private void JoinRequestsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(JoinRequestsNavButton);
        MainContent.Content = new JoinRequestsView();
    }

    // Pick an ongoing exam, then see the students who joined it
    private void ManageStudentsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(ManageStudentsNavButton);
        MainContent.Content = new ManageStudentsView();
    }

    // Every time a student opened an app that is not allowed
    private void MonitorStudentsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(MonitorStudentsNavButton);
        MainContent.Content = new MonitorStudentsView();
    }

    // Pick an exam, then see each student's submission and download their code ZIP
    private void ViewSubmissionsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(ViewSubmissionsNavButton);
        MainContent.Content = new ViewSubmissionsView();
    }

    private void ManageIDEsButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(ManageIDEsNavButton);
        MainContent.Content = new Views.ManageIDEsView();
    }

    private void ProfileButton_Click(object? sender, RoutedEventArgs e)
    {
        SetActiveNav(ProfileNavButton);
        var profile = new ProfileView(_username);
        profile.LogoutRequested += async (_, _) =>
        {
            if (await ConfirmSignOutAsync())
                LogoutRequested?.Invoke(this, EventArgs.Empty);
        };
        MainContent.Content = profile;
    }

    // ---------- Sign out ----------
    private async void LogoutButton_Click(object? sender, RoutedEventArgs e)
    {
        if (await ConfirmSignOutAsync())
            LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    // Small "Are you sure?" window. Returns true if the teacher chose Sign out.
    private async Task<bool> ConfirmSignOutAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return true;

        var confirmed = false;

        var dialog = new Window
        {
            Title = "Sign out",
            Width = 380,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#101831"))
        };

        var stay = new Button { Content = "Stay signed in", Padding = new Thickness(16, 8) };
        var leave = new Button
        {
            Content = "Sign out",
            Padding = new Thickness(16, 8),
            Background = new SolidColorBrush(Color.Parse("#B91C1C")),
            Foreground = Brushes.White
        };

        stay.Click += (_, _) => dialog.Close();
        leave.Click += (_, _) =>
        {
            confirmed = true;
            dialog.Close();
        };

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 18,
            Children =
            {
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Sign out of CodeTrace?",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = Brushes.White
                        },
                        new TextBlock
                        {
                            Text = "You will need to sign in again to manage examinations.",
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#98A4D6"))
                        }
                    }
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { stay, leave }
                }
            }
        };

        await dialog.ShowDialog(owner);
        return confirmed;
    }
}