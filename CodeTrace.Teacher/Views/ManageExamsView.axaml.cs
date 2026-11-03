using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

public partial class ManageExamsView : UserControl
{
    private readonly ExamApiService _examApi = new();
    private readonly IDEApiService _ideApi = new();

    public ManageExamsView()
    {
        InitializeComponent();
        Styles.Add(LightUi.Include());
        _ = LoadExamsAsync();
    }

    private async Task LoadExamsAsync()
    {
        LoadingBar.IsVisible = true;
        ExamListPanel.Children.Clear();

        var exams = await _examApi.GetAllAsync();

        LoadingBar.IsVisible = false;

        if (exams.Count == 0)
        {
            ExamListPanel.Children.Add(BuildEmptyState());
            return;
        }

        foreach (var exam in exams)
        {
            ExamListPanel.Children.Add(BuildExamCard(exam));
        }
    }

    private Border BuildEmptyState()
    {
        var stack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        stack.Children.Add(new TextBlock
        {
            Text = "📋",
            FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        stack.Children.Add(new TextBlock
        {
            Text = "No examinations yet",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = LightUi.Heading,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Create one from the Create Examination page.",
            FontSize = 14,
            Foreground = LightUi.Muted,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        return new Border
        {
            Classes = { "card" },
            Padding = new Thickness(40),
            Child = stack
        };
    }

    private Border BuildExamCard(Exam exam)
    {
        var card = new Border
        {
            Classes = { "listCard" },
            Padding = new Thickness(24, 20)
        };

        var layout = new StackPanel { Spacing = 12 };

        var headerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var nameText = new TextBlock
        {
            Text = exam.Name,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = LightUi.Heading,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(nameText, 0);

        var durationChip = new Border
        {
            Classes = { "chip" },
            HorizontalAlignment = HorizontalAlignment.Right
        };
        durationChip.Child = new TextBlock { Text = $"⏱ {exam.DurationMinutes} min" };
        Grid.SetColumn(durationChip, 1);

        headerRow.Children.Add(nameText);
        headerRow.Children.Add(durationChip);

        // Description: bold, dark, clearly visible
        var descriptionText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(exam.Description)
                ? "No description provided."
                : exam.Description,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brush.Parse("#334155"),
            TextWrapping = TextWrapping.Wrap
        };

        var ideLabel = new TextBlock
        {
            Text = "ALLOWED IDES",
            Classes = { "sectionLabel" },
            Margin = new Thickness(0, 6, 0, 0)
        };

        var ideChips = new WrapPanel { LineSpacing = 6 };

        if (exam.AllowedIDEs.Count == 0)
        {
            ideChips.Children.Add(new Border
            {
                Classes = { "chipMuted" },
                Child = new TextBlock { Text = "None selected" }
            });
        }
        else
        {
            foreach (var ide in exam.AllowedIDEs)
            {
                var chip = new Border
                {
                    Classes = { "chip" },
                    Margin = new Thickness(0, 0, 8, 0)
                };
                chip.Child = new TextBlock { Text = ide.IDEName };
                ideChips.Children.Add(chip);
            }
        }

        var divider = new Border { Classes = { "divider" }, Margin = new Thickness(0, 6, 0, 0) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var editButton = new Button
        {
            Content = "Edit",
            Classes = { "secondary" }
        };

        var deleteButton = new Button
        {
            Content = "Delete",
            Classes = { "danger" }
        };

        deleteButton.Click += async (_, _) =>
        {
            deleteButton.IsEnabled = false;
            await _examApi.DeleteAsync(exam.Id);
            await LoadExamsAsync();
        };

        editButton.Click += async (_, _) =>
        {
            await OpenEditDialogAsync(exam);
        };

        buttons.Children.Add(editButton);
        buttons.Children.Add(deleteButton);

        layout.Children.Add(headerRow);
        layout.Children.Add(descriptionText);
        layout.Children.Add(ideLabel);
        layout.Children.Add(ideChips);
        layout.Children.Add(divider);
        layout.Children.Add(buttons);

        card.Child = layout;

        return card;
    }

    private async Task OpenEditDialogAsync(Exam exam)
    {
        var dialog = new Window
        {
            Title = "Edit Examination",
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        LightUi.StyleDialog(dialog);

        var nameBox = new TextBox { Text = exam.Name, Height = 44 };
        var durationBox = new NumericUpDown
        {
            Value = exam.DurationMinutes,
            Minimum = 1,
            Maximum = 300,
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 220
        };
        var descriptionBox = new TextBox
        {
            Text = exam.Description,
            Height = 100,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };

        var ideCheckboxesPanel = new WrapPanel { ItemSpacing = 10, LineSpacing = 10 };
        var checkboxes = new List<CheckBox>();

        var allowedNames = exam.AllowedIDEs.Select(a => a.IDEName).ToHashSet();
        var allIdes = await _ideApi.GetAllAsync();

        foreach (var ide in allIdes.Where(i => i.IsActive))
        {
            var checkBox = new CheckBox
            {
                Content = ide.Name,
                Tag = ide.Name,
                IsChecked = allowedNames.Contains(ide.Name),
                Foreground = Brush.Parse("#0F172A")
            };
            checkboxes.Add(checkBox);
            ideCheckboxesPanel.Children.Add(checkBox);
        }

        var statusText = new TextBlock
        {
            Foreground = LightUi.Error,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };

        var saveButton = new Button { Content = "Save Changes", Classes = { "primary" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        var cancelButton = new Button { Content = "Cancel", Classes = { "secondary" }, HorizontalAlignment = HorizontalAlignment.Stretch };

        saveButton.Click += async (_, _) =>
        {
            statusText.Text = "";

            var name = nameBox.Text?.Trim() ?? "";
            var description = descriptionBox.Text?.Trim() ?? "";
            var duration = durationBox.Value.HasValue ? (int)durationBox.Value.Value : 0;

            if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
            {
                statusText.Text = "Please enter a name of at least 3 characters.";
                return;
            }

            if (duration <= 0 || duration > 300)
            {
                statusText.Text = "Duration must be between 1 and 300 minutes.";
                return;
            }

            var allowed = checkboxes
                .Where(cb => cb.IsChecked == true)
                .Select(cb => new ExamIDE { IDEName = (string)cb.Tag!, IsAllowed = true })
                .ToList();

            if (allowed.Count == 0)
            {
                statusText.Text = "Please select at least one allowed IDE.";
                return;
            }

            saveButton.IsEnabled = false;
            saveButton.Content = "Saving...";

            exam.Name = name;
            exam.Description = description;
            exam.DurationMinutes = duration;
            exam.AllowedIDEs = allowed;

            var updated = await _examApi.UpdateAsync(exam);

            if (updated == null)
            {
                statusText.Text = "Could not save changes. Please try again.";
                saveButton.IsEnabled = true;
                saveButton.Content = "Save Changes";
                return;
            }

            dialog.Close();
            await LoadExamsAsync();
        };

        cancelButton.Click += (_, _) => dialog.Close();

        var buttonRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10, Margin = new Thickness(0, 20, 0, 0) };
        Grid.SetColumn(cancelButton, 0);
        Grid.SetColumn(saveButton, 1);
        buttonRow.Children.Add(cancelButton);
        buttonRow.Children.Add(saveButton);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(24, 20, 24, 0) };
        var headerText = new TextBlock { Text = "Edit Examination", FontSize = 20, FontWeight = FontWeight.Bold, Foreground = LightUi.Heading };
        var closeButton = new Button { Content = "✕", Classes = { "ghost" } };
        closeButton.Click += (_, _) => dialog.Close();
        Grid.SetColumn(headerText, 0);
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(headerText);
        header.Children.Add(closeButton);

        var body = new StackPanel
        {
            Margin = new Thickness(24, 16, 24, 24),
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Examination Name", Classes = { "fieldLabel" } },
                nameBox,
                new TextBlock { Text = "Duration (minutes)", Classes = { "fieldLabel" }, Margin = new Thickness(0,12,0,0) },
                durationBox,
                new TextBlock { Text = "Description", Classes = { "fieldLabel" }, Margin = new Thickness(0,12,0,0) },
                descriptionBox,
                new TextBlock { Text = "Allowed IDEs", Classes = { "fieldLabel" }, Margin = new Thickness(0,12,0,0) },
                ideCheckboxesPanel,
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