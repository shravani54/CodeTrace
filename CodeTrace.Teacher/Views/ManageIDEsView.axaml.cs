using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CodeTrace.Teacher.Models;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class ManageIDEsView : UserControl
{
    private readonly List<IDEConfiguration> _ides = new();
    private readonly IDEApiService _api = new();

    public ManageIDEsView()
    {
        InitializeComponent();
        Styles.Add(LightUi.Include());
        _ = LoadIDEsAsync();
    }

    private async Task LoadIDEsAsync()
    {
        LoadingBar.IsVisible = true;
        IDEListPanel.Children.Clear();

        _ides.Clear();
        _ides.AddRange(await _api.GetAllAsync());

        LoadingBar.IsVisible = false;
        RefreshIDEList();
    }

    private void RefreshIDEList()
    {
        IDEListPanel.Children.Clear();

        if (_ides.Count == 0)
        {
            IDEListPanel.Children.Add(new Border
            {
                Classes = { "listCard" },
                Padding = new Thickness(40),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "🖥️",
                            FontSize = 32,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = "No IDEs added yet",
                            FontSize = 18,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = LightUi.Heading,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 8, 0, 0)
                        },
                        new TextBlock
                        {
                            Text = "Click \"+ Add IDE\" to register the first one.",
                            FontSize = 14,
                            Foreground = LightUi.Muted,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            });
            return;
        }

        foreach (var ide in _ides)
        {
            var card = new Border { Classes = { "listCard" }, Padding = new Thickness(20, 16) };

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };

            var icon = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(12),
                Background = LightUi.Chip,
                BorderBrush = LightUi.ChipBorder,
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "⌨",
                    FontSize = 20,
                    Foreground = LightUi.IconText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Margin = new Thickness(0, 0, 16, 0)
            };
            Grid.SetColumn(icon, 0);

            var textStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

            textStack.Children.Add(new TextBlock
            {
                Text = ide.Name,
                FontSize = 17,
                FontWeight = FontWeight.Bold,
                Foreground = LightUi.Heading
            });

            textStack.Children.Add(new TextBlock
            {
                Text = ide.ExecutablePath,
                FontSize = 13,
                Foreground = LightUi.Muted,
                TextWrapping = TextWrapping.Wrap
            });

            Grid.SetColumn(textStack, 1);

            var statusChip = new Border
            {
                Classes = { ide.IsActive ? "chipSuccess" : "chipMuted" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 10, 0)
            };
            statusChip.Child = new TextBlock { Text = ide.IsActive ? "Active" : "Disabled" };
            Grid.SetColumn(statusChip, 2);

            var deleteButton = new Button
            {
                Content = "Remove",
                Classes = { "danger" },
                VerticalAlignment = VerticalAlignment.Center
            };
            deleteButton.Click += async (_, _) =>
            {
                deleteButton.IsEnabled = false;
                await _api.DeleteAsync(ide.Id);
                await LoadIDEsAsync();
            };
            Grid.SetColumn(deleteButton, 3);

            row.Children.Add(icon);
            row.Children.Add(textStack);
            row.Children.Add(statusChip);
            row.Children.Add(deleteButton);

            card.Child = row;
            IDEListPanel.Children.Add(card);
        }
    }

    private async void AddIDEButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Add IDE",
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        LightUi.StyleDialog(dialog);

        var nameBox = new TextBox { PlaceholderText = "e.g. Visual Studio Code", Height = 44 };
        var pathBox = new TextBox { PlaceholderText = "Path to executable", Height = 44 };
        var browseButton = new Button { Content = "Browse", Classes = { "secondary" } };
        var saveButton = new Button { Content = "Save IDE", Classes = { "primary" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        var cancelButton = new Button { Content = "Cancel", Classes = { "secondary" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        var statusText = new TextBlock
        {
            Foreground = LightUi.Error,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        browseButton.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select IDE executable",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new Avalonia.Platform.Storage.FilePickerFileType("Windows executable")
                        {
                            Patterns = ["*.exe"]
                        }
                    ]
                });

            if (files.Count > 0)
                pathBox.Text = files[0].Path.LocalPath;
        };

        saveButton.Click += async (_, _) =>
        {
            statusText.Text = "";

            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                statusText.Text = "Please enter the IDE name.";
                return;
            }

            if (string.IsNullOrWhiteSpace(pathBox.Text))
            {
                statusText.Text = "Please select the IDE executable.";
                return;
            }

            saveButton.IsEnabled = false;
            saveButton.Content = "Saving...";

            var created = await _api.CreateAsync(new IDEConfiguration
            {
                Name = nameBox.Text.Trim(),
                ExecutablePath = pathBox.Text.Trim(),
                IsActive = true
            });

            if (created != null)
                _ides.Add(created);

            RefreshIDEList();
            dialog.Close();
        };

        cancelButton.Click += (_, _) => dialog.Close();

        var pathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(pathBox, 0);
        Grid.SetColumn(browseButton, 1);
        pathBox.Margin = new Thickness(0, 0, 8, 0);
        pathRow.Children.Add(pathBox);
        pathRow.Children.Add(browseButton);

        var buttonRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10, Margin = new Thickness(0, 20, 0, 0) };
        Grid.SetColumn(cancelButton, 0);
        Grid.SetColumn(saveButton, 1);
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(saveButton);

        // Header bar
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 20, 24, 0) };
        var headerText = new TextBlock { Text = "Add IDE", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = LightUi.Heading };
        var closeButton = new Button { Content = "✕", Classes = { "ghost" } };
        closeButton.Click += (_, _) => dialog.Close();
        Grid.SetColumn(headerText, 0);
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(headerText);
        header.Children.Add(closeButton);

        var body = new StackPanel
        {
            Margin = new Thickness(24, 16, 24, 24),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "IDE Name", Classes = { "fieldLabel" } },
                nameBox,
                new TextBlock { Text = "Executable", Classes = { "fieldLabel" }, Margin = new Thickness(0,8,0,0) },
                pathRow,
                statusText,
                buttonRow
            }
        };

        var shell = new Border
        {
            Classes = { "dialogShell" },
            Child = new StackPanel { Children = { header, body } }
        };

        dialog.Content = shell;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner != null)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
    }
}