using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class JoinRequestsView : UserControl
{
    private readonly JoinRequestApiService _api = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _busy;
    private string _lastKey = "";

    public JoinRequestsView()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) => await RefreshAsync();
        AttachedToVisualTree += (_, _) => { _timer.Start(); _ = RefreshAsync(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var items = await _api.GetPendingAsync();

            // Rebuild only when the list changed, so buttons don't vanish while being clicked
            var key = string.Join(",", items.Select(i => i.Id));
            if (key == _lastKey) return;
            _lastKey = key;

            RequestPanel.Children.Clear();
            EmptyText.IsVisible = items.Count == 0;
            foreach (var r in items) RequestPanel.Children.Add(BuildRow(r));
        }
        catch
        {
            // server not reachable: keep the last list and try again in 2 seconds
        }
        finally
        {
            _busy = false;
        }
    }

    private Control BuildRow(JoinRequestItem r)
    {
        var approve = new Button { Content = "Approve" };
        var reject = new Button { Content = "Reject" };
        approve.Click += async (_, _) => { await _api.ApproveAsync(r.Id); _lastKey = ""; await RefreshAsync(); };
        reject.Click += async (_, _) => { await _api.RejectAsync(r.Id); _lastKey = ""; await RefreshAsync(); };

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = r.StudentName, FontWeight = FontWeight.SemiBold });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(approve);
        buttons.Children.Add(reject);
        Grid.SetColumn(buttons, 1);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(info);
        grid.Children.Add(buttons);

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.LightGray,
            Child = grid
        };
    }
}