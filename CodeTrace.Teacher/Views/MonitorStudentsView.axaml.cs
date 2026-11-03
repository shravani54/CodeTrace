using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class MonitorStudentsView : UserControl
{
    private readonly AppAlertService _service = new();
    private readonly DispatcherTimer _timer;

    public MonitorStudentsView()
    {
        InitializeComponent();

        // Refresh the list every 3 seconds while this page is on screen
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        AttachedToVisualTree += (_, _) =>
        {
            _ = RefreshAsync();
            _timer.Start();
        };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        var batch = await _service.GetAfterAsync(0);

        if (batch == null)
        {
            ErrorText.IsVisible = true;
            return;
        }

        ErrorText.IsVisible = false;
        RowsPanel.Children.Clear();

        // Newest first
        var alerts = batch.Alerts.OrderByDescending(a => a.Id).ToList();

        var number = 1;
        foreach (var a in alerts)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("50,110,*,120,180"),
                Margin = new Thickness(8, 6)
            };

            row.Children.Add(Cell(number.ToString(), 0));
            row.Children.Add(Cell(a.Time.ToLocalTime().ToString("hh:mm:ss tt"), 1));
            row.Children.Add(Cell(a.Name, 2));
            row.Children.Add(Cell(a.Roll, 3));
            row.Children.Add(Cell(a.AppName, 4, bold: true, red: true));

            RowsPanel.Children.Add(row);
            number++;
        }

        EmptyText.IsVisible = alerts.Count == 0;
        CountText.Text = $"Alerts: {alerts.Count}";
    }

    private static TextBlock Cell(string text, int column, bool bold = false, bool red = false)
    {
        var cell = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (bold) cell.FontWeight = FontWeight.SemiBold;
        if (red) cell.Foreground = new SolidColorBrush(Color.Parse("#B91C1C"));

        Grid.SetColumn(cell, column);
        return cell;
    }

    private async void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        await _service.ClearAsync();
        await RefreshAsync();
    }
}