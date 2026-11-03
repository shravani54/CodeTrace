using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CodeTrace.Student.Views;

// A small always-on-top box with a lock icon, placed over the close (X) button of the docked IDE.
// The student cannot close it: it only goes away when the Student app sets AllowClose and closes it.
public class CloseShieldWindow : Window
{
    public bool AllowClose { get; set; }

    public CloseShieldWindow()
    {
        WindowDecorations = WindowDecorations.None;   // no title bar, no border
        ShowInTaskbar = false;
        ShowActivated = false;                        // never steal focus from the IDE
        Topmost = true;
        CanResize = false;
        Background = new SolidColorBrush(Color.Parse("#7F1D1D"));

        Content = new TextBlock
        {
            Text = "🔒",
            FontSize = 18,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        ToolTip.SetTip(this, "Closing the IDE is locked during the exam");

        // Alt+F4 or any other attempt to close the shield is ignored
        Closing += (_, e) =>
        {
            if (!AllowClose) e.Cancel = true;
        };
    }
}