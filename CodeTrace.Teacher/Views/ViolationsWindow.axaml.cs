using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using CodeTrace.Teacher.Models;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class ViolationsWindow : Window
{
    // Address of the API. Use the same address your Teacher app already uses.
    private const string ServerUrl = "http://localhost:5080";

    private readonly int _examId;
    private readonly ViolationApiService _api = new(ServerUrl);
    private readonly ObservableCollection<Violation> _items = new();
    private readonly DispatcherTimer _timer;
    private int _lastId;
    private bool _busy;

    // Needed by the Avalonia designer
    public ViolationsWindow() : this(0) { }

    public ViolationsWindow(int examId)
    {
        InitializeComponent();
        _examId = examId;
        Title = $"Live violations - exam {examId}";
        ViolationList.ItemsSource = _items;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();

        Closed += (_, _) => _timer.Stop();
        _ = PollAsync();
    }

    private async System.Threading.Tasks.Task PollAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var list = await _api.GetAsync(_examId, _lastId);
            foreach (var v in list)
            {
                _items.Insert(0, v);   // newest on top
                if (v.Id > _lastId) _lastId = v.Id;
            }
            StatusText.Text = $"{_items.Count} event(s). Last update {DateTime.Now:HH:mm:ss}";
        }
        catch
        {
            StatusText.Text = "Cannot reach the server. Retrying...";
        }
        finally
        {
            _busy = false;
        }
    }
}