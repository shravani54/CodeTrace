using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CodeTrace.Teacher.Services;

namespace CodeTrace.Teacher.Views;

// Ready-made "Download ZIP" button for one submission: asks where to save, then downloads the ZIP.
public static class SubmissionZipButton
{
    public static Button Create(int submissionId, string studentName)
    {
        var button = new Button { Content = "Download ZIP", Classes = { "secondary" } };

        button.Click += async (_, _) =>
        {
            var top = TopLevel.GetTopLevel(button);
            if (top == null) return;

            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the student's code",
                SuggestedFileName = SafeName(studentName) + ".zip",
                DefaultExtension = "zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("ZIP archive") { Patterns = new[] { "*.zip" } }
                }
            });

            if (file == null) return;

            button.IsEnabled = false;
            button.Content = "Downloading...";

            try
            {
                await using var stream = await file.OpenWriteAsync();
                var ok = await new SubmissionZipService().DownloadAsync(submissionId, stream);
                button.Content = ok ? "Saved ✓" : "No ZIP found";
            }
            catch
            {
                button.Content = "Download failed";
            }
            finally
            {
                button.IsEnabled = true;
            }
        };

        return button;
    }

    private static string SafeName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string((name ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "submission" : clean;
    }
}