using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CodeTrace.Student.Services;

/// <summary>
/// Small borderless always-on-top window placed over the top-right corner of the docked IDE,
/// hiding the IDE's own Close (X) button. Clicks land on this window, not on the IDE.
/// </summary>
public sealed class CloseButtonCover : Window
{
    public CloseButtonCover(PixelPoint position, double widthDip, double heightDip)
    {
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = false;
        ShowActivated = false;      // never steals focus from the IDE
        Topmost = true;
        CanResize = false;
        Focusable = false;
        Position = position;
        Width = widthDip;
        Height = heightDip;

        // Match the IDE's title bar colour (dark here). Change it if your IDEs use a light theme.
        Background = new SolidColorBrush(Color.Parse("#1E1F22"));
        Content = new TextBlock
        {
            Text = "🔒",
            Foreground = Brushes.Gray,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
    }
}