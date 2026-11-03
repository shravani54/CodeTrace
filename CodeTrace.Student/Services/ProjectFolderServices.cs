using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace CodeTrace.Student.Services;

// The student's code folder: Documents\CodeTrace\<exam>-<id>\<student name (roll)>
// It is created when the exam starts, opened inside the IDE, and zipped on submit.
public static class ProjectFolderService
{
    private const long MaxFileBytes = 10L * 1024 * 1024;    // bigger single files are left out
    private const long MaxTotalBytes = 50L * 1024 * 1024;   // total of all files in the ZIP

    // Build output and tool folders that do not belong in a submission
    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".idea", "__pycache__", "venv", ".venv"
    };

    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".obj", ".o", ".class"
    };

    public static string GetFolder(int examId, string examName, string studentName)
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var path = Path.Combine(root, "CodeTrace", $"{Safe(examName)}-{examId}", Safe(studentName));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Safe(string text)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string((text ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        if (clean.Length > 60) clean = clean.Substring(0, 60).Trim();
        return clean.Length == 0 ? "exam" : clean;
    }

    // true if the folder has at least one file that would go into the ZIP
    public static bool HasFiles(string folder) =>
        Directory.Exists(folder) && EnumerateFiles(folder).Any();

    // Walks the folder but never enters excluded folders such as node_modules
    private static IEnumerable<FileInfo> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();

            string[] subDirs;
            string[] files;
            try
            {
                subDirs = Directory.GetDirectories(dir);
                files = Directory.GetFiles(dir);
            }
            catch
            {
                continue;
            }

            foreach (var sub in subDirs)
                if (!ExcludedDirs.Contains(Path.GetFileName(sub)))
                    pending.Push(sub);

            foreach (var file in files)
            {
                if (ExcludedExtensions.Contains(Path.GetExtension(file))) continue;
                yield return new FileInfo(file);
            }
        }
    }

    // Zips the folder into a temp file and returns its path, or null if there is nothing to zip.
    // The caller deletes the temp file after uploading.
    public static Task<string?> CreateZipAsync(string folder) => Task.Run<string?>(() =>
    {
        if (!Directory.Exists(folder)) return null;

        var files = EnumerateFiles(folder).ToList();
        if (files.Count == 0) return null;

        var zipPath = Path.Combine(Path.GetTempPath(), $"codetrace-{Guid.NewGuid():N}.zip");
        var skipped = new List<string>();
        long total = 0;

        using (var fs = new FileStream(zipPath, FileMode.Create))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(folder, file.FullName).Replace('\\', '/');

                if (file.Length > MaxFileBytes || total + file.Length > MaxTotalBytes)
                {
                    skipped.Add($"{relative} (too large: {file.Length / 1024 / 1024} MB)");
                    continue;
                }

                try
                {
                    // The IDE may still have the file open, so allow sharing
                    using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                                                     FileShare.ReadWrite | FileShare.Delete);
                    var entry = zip.CreateEntry(relative, CompressionLevel.Optimal);
                    using var output = entry.Open();
                    input.CopyTo(output);
                    total += file.Length;
                }
                catch
                {
                    skipped.Add($"{relative} (could not be read)");
                }
            }

            if (skipped.Count > 0)
            {
                var note = zip.CreateEntry("_CodeTrace_skipped_files.txt");
                using var writer = new StreamWriter(note.Open());
                writer.WriteLine("These files were not included in the submission:");
                foreach (var s in skipped) writer.WriteLine(s);
            }
        }

        return zipPath;
    });
}