using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;

namespace CodeTrace.Teacher.Views;

public static class DarkUi
{
    public static readonly IBrush Heading = new SolidColorBrush(Color.Parse("#FFFFFF"));
    public static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#98A4D6"));
    public static readonly IBrush Error = new SolidColorBrush(Color.Parse("#FF9FB0"));
    public static readonly IBrush Chip = new SolidColorBrush(Color.Parse("#131B4A"));
    public static readonly IBrush ChipBorder = new SolidColorBrush(Color.Parse("#2A3A8C"));
    public static readonly IBrush IconText = new SolidColorBrush(Color.Parse("#9DB6FF"));
    public static readonly IBrush Page = new SolidColorBrush(Color.Parse("#070B1F"));

    /// <summary>Makes a dialog window dark and loads the shared dark styles into it.</summary>
    public static void StyleDialog(Window dialog)
    {
        dialog.Background = Page;
        dialog.Styles.Add(new StyleInclude(new Uri("avares://CodeTrace.Teacher/"))
        {
            Source = new Uri("avares://CodeTrace.Teacher/Styles/CodeTraceDark.axaml")
        });
    }
}