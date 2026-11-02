using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class AllowedStudentsView : UserControl
{
    private readonly AllowedStudentService _service = new();

    private bool _confirmClear;
    private DispatcherTimer? _clearTimer;

    public AllowedStudentsView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) => await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var list = await _service.GetAllAsync();

        if (list == null)
        {
            ShowError("Cannot reach the server.");
            return;
        }

        RowsPanel.Children.Clear();

        var number = 1;
        foreach (var s in list)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("50,*,160,100"),
                Margin = new Thickness(8, 4)
            };

            row.Children.Add(Cell(number.ToString(), 0));
            row.Children.Add(Cell(string.IsNullOrWhiteSpace(s.Name) ? "—" : s.Name, 1));
            row.Children.Add(Cell(s.Roll, 2));

            var remove = new Button { Content = "Remove" };
            remove.Classes.Add("rowRemove");
            remove.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(remove, 3);

            var roll = s.Roll;
            remove.Click += async (_, _) =>
            {
                await _service.RemoveAsync(roll);
                await RefreshAsync();
            };

            row.Children.Add(remove);
            RowsPanel.Children.Add(row);
            number++;
        }

        CountText.Text = $"Students on the list: {list.Count}";
        EmptyText.IsVisible = list.Count == 0;
    }

    private static TextBlock Cell(string text, int column)
    {
        var cell = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(cell, column);
        return cell;
    }

    // ---------- Import from Excel / CSV ----------
    private async void ImportButton_Click(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the student list",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Excel or CSV files") { Patterns = new[] { "*.xlsx", "*.csv" } }
            }
        });

        if (files.Count == 0) return;

        ImportButton.IsEnabled = false;

        try
        {
            await using var input = await files[0].OpenReadAsync();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory);
            memory.Position = 0;

            var read = StudentSheetReader.Read(memory, files[0].Name);

            if (read.Students.Count == 0)
            {
                ShowError("No valid roll numbers were found. Check that the file has a \"Roll Number\" column with digits.");
                return;
            }

            var result = await _service.ImportAsync(read.Students);

            if (result == null)
            {
                ShowError("Could not save the list. Is the server running?");
                return;
            }

            var message = $"Imported {read.Students.Count} students: {result.Added} new, {result.Updated} updated.";
            if (read.Skipped > 0)
                message += $" {read.Skipped} rows were skipped because they had no valid roll number.";

            ShowOk(message);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ShowError("Could not read that file: " + ex.Message);
        }
        finally
        {
            ImportButton.IsEnabled = true;
        }
    }

    // ---------- Add one student by hand ----------
    private async void AddButton_Click(object? sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? "").Trim();
        var roll = (RollBox.Text ?? "").Trim();

        if (!Regex.IsMatch(roll, @"^\d{1,10}$"))
        {
            ShowError("Enter the roll number using digits only (up to 10).");
            return;
        }

        AddButton.IsEnabled = false;
        var error = await _service.AddAsync(name, roll);
        AddButton.IsEnabled = true;

        if (error != "")
        {
            ShowError(error);
            return;
        }

        NameBox.Text = "";
        RollBox.Text = "";
        ShowOk($"Roll number {roll} was added.");
        await RefreshAsync();
        NameBox.Focus();
    }

    // ---------- Clear the whole list (asks twice) ----------
    private async void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_confirmClear)
        {
            _confirmClear = true;
            ClearButton.Content = "Click again to confirm";

            _clearTimer?.Stop();
            _clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _clearTimer.Tick += (_, _) => ResetClearButton();
            _clearTimer.Start();
            return;
        }

        ResetClearButton();
        await _service.ClearAsync();
        ShowOk("The list was cleared.");
        await RefreshAsync();
    }

    private void ResetClearButton()
    {
        _clearTimer?.Stop();
        _confirmClear = false;
        ClearButton.Content = "Clear all";
    }

    private void ShowOk(string message)
    {
        StatusText.Foreground = new SolidColorBrush(Color.Parse("#6FE3B0"));
        StatusText.Text = message;
        StatusText.IsVisible = true;
    }

    private void ShowError(string message)
    {
        StatusText.Foreground = new SolidColorBrush(Color.Parse("#FF9FB0"));
        StatusText.Text = message;
        StatusText.IsVisible = true;
    }
}