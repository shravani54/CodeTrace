using System;
using System.Diagnostics;
using System.IO;

namespace CodeTrace.Student.Services;

/// <summary>
/// Starts an IDE the same way everywhere (the IDE button and the automatic reopen).
/// For VS Code it opens the private exam folder with a locked-down profile.
/// Other IDEs are started normally.
/// </summary>
public static class IdeLauncher
{
    /// <summary>The private folder where this student's exam files live. Null if it cannot be created.</summary>
    public static string? GetExamFolder(string examName, string studentName)
    {
        try
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var folder = Path.Combine(documents, "CodeTrace", Sanitize($"{examName}_{studentName}"));
            Directory.CreateDirectory(folder);
            return folder;
        }
        catch
        {
            return null;
        }
    }

    public static void Start(string exePath, string? examFolder)
    {
        var startInfo = new ProcessStartInfo(exePath) { UseShellExecute = true };

        var isVsCode = Path.GetFileName(exePath).Equals("Code.exe", StringComparison.OrdinalIgnoreCase);
        if (isVsCode && examFolder != null)
        {
            var profile = PrepareVsCodeProfile();
            if (profile != null)
            {
                startInfo.Arguments = $"--user-data-dir \"{profile}\" --new-window \"{examFolder}\"";
                startInfo.WorkingDirectory = examFolder;
            }
        }

        Process.Start(startInfo);
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    // Separate VS Code profile for exams. It does not touch the student's normal VS Code settings.
    private static string? PrepareVsCodeProfile()
    {
        try
        {
            var profile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodeTrace", "vscode-exam-profile");
            var user = Path.Combine(profile, "User");
            Directory.CreateDirectory(user);

            // Menu bar hidden, no recent folders, no welcome page, no updates
            File.WriteAllText(Path.Combine(user, "settings.json"), @"{
  ""window.menuBarVisibility"": ""hidden"",
  ""window.restoreWindows"": ""none"",
  ""window.openFilesInNewWindow"": ""off"",
  ""window.openFoldersInNewWindow"": ""off"",
  ""workbench.startupEditor"": ""none"",
  ""workbench.tips.enabled"": false,
  ""security.workspace.trust.enabled"": false,
  ""update.mode"": ""none"",
  ""telemetry.telemetryLevel"": ""off""
}");

            // Turns off the shortcuts that open another file or folder
            File.WriteAllText(Path.Combine(user, "keybindings.json"), @"[
  { ""key"": ""ctrl+o"",        ""command"": ""-workbench.action.files.openFile"" },
  { ""key"": ""ctrl+k ctrl+o"", ""command"": ""-workbench.action.files.openFolder"" },
  { ""key"": ""ctrl+r"",        ""command"": ""-workbench.action.openRecent"" },
  { ""key"": ""ctrl+k f"",      ""command"": ""-workbench.action.closeFolder"" }
]");

            return profile;
        }
        catch
        {
            return null;   // falls back to starting VS Code normally
        }
    }
}