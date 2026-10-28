using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

namespace CodeTrace.Teacher.Views;

public static class LightUi
{
    public static readonly IBrush Heading    = Brush.Parse("#0C4A6E");
    public static readonly IBrush Muted      = Brush.Parse("#475569");
    public static readonly IBrush Error      = Brush.Parse("#BE123C");
    public static readonly IBrush Chip       = Brush.Parse("#E0F2FE");
    public static readonly IBrush ChipBorder = Brush.Parse("#BAE6FD");
    public static readonly IBrush IconText   = Brush.Parse("#0369A1");

    public static StyleInclude Include() =>
        new(new Uri("avares://CodeTrace.Teacher/"))
        {
            Source = new Uri("avares://CodeTrace.Teacher/Styles/LightTheme.axaml")
        };

    public static void StyleDialog(Window dialog)
    {
        dialog.RequestedThemeVariant = ThemeVariant.Light;
        dialog.Background = Brush.Parse("#FFFFFF");
        dialog.Styles.Add(Include());
    }
}