using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CodeTrace.Student.Services;

/// <summary>
/// Decides whether a process belongs to one of the allowed IDEs.
/// A process matches when its name is in the list, OR when its .exe lives inside the IDE's install folder.
/// The second rule is what makes JetBrains, Eclipse, NetBeans, Visual Studio and others work, because
/// their windows are often owned by a helper process whose name is not the launcher's name.
/// </summary>
public sealed class IdeProcessMatcher
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private readonly HashSet<string> _names = new();
    private readonly List<string> _roots = new();   // install folders, each ending with a backslash

    public bool IsEmpty => _names.Count == 0 && _roots.Count == 0;

    public IdeProcessMatcher(IEnumerable<string> processNames, IEnumerable<string?> exePaths)
    {
        foreach (var name in processNames) AddName(name);

        foreach (var path in exePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            var clean = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

            AddName(Path.GetFileNameWithoutExtension(clean));

            var root = GetInstallRoot(clean);
            if (root != null && !_roots.Contains(root, StringComparer.OrdinalIgnoreCase))
                _roots.Add(root);
        }
    }

    private IdeProcessMatcher() { }

    // ---------- Save / load (used by the crash-repair file) ----------

    public IEnumerable<string> ToLines() =>
        _names.Select(n => "n|" + n).Concat(_roots.Select(r => "r|" + r));

    public static IdeProcessMatcher FromLines(IEnumerable<string> lines)
    {
        var matcher = new IdeProcessMatcher();
        foreach (var line in lines)
        {
            if (line.Length < 3) continue;
            if (line.StartsWith("n|")) matcher._names.Add(line[2..]);
            else if (line.StartsWith("r|")) matcher._roots.Add(line[2..]);
        }
        return matcher;
    }

    // ---------- Matching ----------

    public bool IsMatch(uint pid)
    {
        if (pid == (uint)Environment.ProcessId) return false;   // never match the Student app itself

        try
        {
            using var process = Process.GetProcessById((int)pid);
            if (_names.Contains(process.ProcessName.ToLowerInvariant())) return true;
        }
        catch
        {
            return false;   // process ended or is not accessible
        }

        if (_roots.Count == 0) return false;

        var path = GetImagePath(pid);
        return path != null && _roots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }

    private void AddName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim().Trim('"');
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length > 0) _names.Add(name.ToLowerInvariant());
    }

    private static string? GetImagePath(uint pid)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;

        try
        {
            var buffer = new StringBuilder(1024);
            var size = (uint)buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    // The IDE's install folder: the folder of the .exe, or its parent when the .exe is inside "bin"
    // (JetBrains: ...\IntelliJ IDEA\bin\idea64.exe). Folders that are too general are ignored,
    // so the rule can never match half the PC.
    private static string? GetInstallRoot(string exePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(dir)) return null;

            if (Path.GetFileName(dir).Equals("bin", StringComparison.OrdinalIgnoreCase))
                dir = Path.GetDirectoryName(dir);

            if (string.IsNullOrEmpty(dir)) return null;
            dir = dir.TrimEnd('\\');
            if (dir.Length <= 3) return null;   // a drive root such as C:

            var tooGeneral = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
            };

            if (tooGeneral.Any(g => !string.IsNullOrEmpty(g) &&
                                    g.TrimEnd('\\').Equals(dir, StringComparison.OrdinalIgnoreCase)))
                return null;

            return dir + "\\";
        }
        catch
        {
            return null;
        }
    }
}