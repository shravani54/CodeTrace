using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CodeTrace.Teacher.Views;

public partial class WelcomeView : UserControl
{
    /// <summary>Raised with "create", "manage", "requests" or "monitor" when a card is clicked.</summary>
    public event Action<string>? OpenRequested;

    public WelcomeView() : this("teacher")
    {
    }

    public WelcomeView(string username)
    {
        InitializeComponent();
        UsernameText.Text = string.IsNullOrWhiteSpace(username) ? "teacher" : username;
    }

    private void Tile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string key)
            OpenRequested?.Invoke(key);
    }
}