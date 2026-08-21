using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CodeTrace.Teacher.Models;

namespace CodeTrace.Teacher.Views;

public partial class ManageIDEsView : UserControl
{
    private readonly List<IDEConfiguration> _ides = new();

    public ManageIDEsView()
    {
        InitializeComponent();

        LoadSampleIDEs();
        RefreshIDEList();
    }

    private void LoadSampleIDEs()
    {
        _ides.Add(new IDEConfiguration
        {
            Id = 1,
            Name = "Visual Studio Code",
            ExecutablePath = @"C:\Program Files\Microsoft VS Code\Code.exe",
            IsActive = true
        });

        _ides.Add(new IDEConfiguration
        {
            Id = 2,
            Name = "Android Studio",
            ExecutablePath = @"C:\Program Files\Android\Android Studio\bin\studio64.exe",
            IsActive = true
        });
    }

    private void RefreshIDEList()
    {
        IDEListPanel.Children.Clear();

        foreach (var ide in _ides)
        {
            var nameText = new TextBlock
            {
                Text = ide.Name,
                FontSize = 20,
                FontWeight = FontWeight.Bold
            };

            var pathText = new TextBlock
            {
                Text = ide.ExecutablePath,
                TextWrapping = TextWrapping.Wrap
            };

            var statusText = new TextBlock
            {
                Text = ide.IsActive ? "Active" : "Disabled"
            };

            var panel = new StackPanel
            {
                Spacing = 5
            };

            panel.Children.Add(nameText);
            panel.Children.Add(pathText);
            panel.Children.Add(statusText);

            var border = new Border
            {
                Padding = new Avalonia.Thickness(15),
                Child = panel
            };

            IDEListPanel.Children.Add(border);
        }
    }

    private async void AddIDEButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Add IDE",
            Width = 500,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var nameBox = new TextBox
        {
            PlaceholderText = "IDE name"
        };

        var pathBox = new TextBox
        {
            PlaceholderText = "Executable path"
        };

        var browseButton = new Button
        {
            Content = "Browse..."
        };

        var saveButton = new Button
        {
            Content = "Save IDE"
        };

        var cancelButton = new Button
        {
            Content = "Cancel"
        };

        var statusText = new TextBlock();

        browseButton.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider
                .OpenFilePickerAsync(
                    new Avalonia.Platform.Storage.FilePickerOpenOptions
                    {
                        Title = "Select IDE executable",
                        AllowMultiple = false,
                        FileTypeFilter =
                        [
                            new Avalonia.Platform.Storage.FilePickerFileType(
                                "Windows executable")
                            {
                                Patterns = ["*.exe"]
                            }
                        ]
                    });

            if (files.Count > 0)
            {
                pathBox.Text = files[0].Path.LocalPath;
            }
        };

        saveButton.Click += (_, _) =>
        {
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

            _ides.Add(new IDEConfiguration
            {
                Id = _ides.Count + 1,
                Name = nameBox.Text.Trim(),
                ExecutablePath = pathBox.Text.Trim(),
                IsActive = true
            });

            RefreshIDEList();

            dialog.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            dialog.Close();
        };

        var content = new StackPanel
        {
            Margin = new Avalonia.Thickness(30),
            Spacing = 12
        };

        content.Children.Add(new TextBlock
        {
            Text = "IDE Name",
            FontWeight = FontWeight.Bold
        });

        content.Children.Add(nameBox);

        content.Children.Add(new TextBlock
        {
            Text = "Executable",
            FontWeight = FontWeight.Bold
        });

        var pathPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10
        };

        pathPanel.Children.Add(pathBox);
        pathPanel.Children.Add(browseButton);

        content.Children.Add(pathPanel);
        content.Children.Add(saveButton);
        content.Children.Add(cancelButton);
        content.Children.Add(statusText);

        dialog.Content = content;

        var owner = TopLevel.GetTopLevel(this) as Window;

        if (owner != null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }
}