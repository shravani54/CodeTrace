using System.Net;
using System.Text.Json;

namespace CodeTrace.Student.Services;

public static class ApiConfig
{
    public const int ApiPort = 5080;

    private static readonly string SavedPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodeTrace", "server.json");

    // Empty until the student enters the server IP (or one was saved earlier)
    public static string BaseUrl { get; private set; } = Load();

    public static bool HasServer => !string.IsNullOrEmpty(BaseUrl);

    // Accepts "10.1.180.25" or "10.1.180.25:5080". Returns false if it is not a valid IPv4 address.
    public static bool TryBuildUrl(string input, out string url)
    {
        url = "";
        input = input.Trim();
        var host = input;
        var port = ApiPort;

        var colon = input.LastIndexOf(':');
        if (colon > 0)
        {
            host = input[..colon];
            if (!int.TryParse(input[(colon + 1)..], out port) || port < 1 || port > 65535)
                return false;
        }

        if (host.Split('.').Length != 4) return false;
        if (!IPAddress.TryParse(host, out var ip)) return false;
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;

        url = $"http://{host}:{port}";
        return true;
    }

    public static void SetServer(string url)
    {
        BaseUrl = url.TrimEnd('/');
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavedPath)!);
            File.WriteAllText(SavedPath, JsonSerializer.Serialize(new { ApiBaseUrl = BaseUrl }));
        }
        catch { /* remembering the address is optional */ }
    }

    private static string Load() =>
        ReadFile(SavedPath)                                                    // last IP the student used
        ?? ReadFile(Path.Combine(AppContext.BaseDirectory, "apiconfig.json"))  // optional default next to the exe
        ?? "";

    private static string? ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var url = doc.RootElement.GetProperty("ApiBaseUrl").GetString();
            return string.IsNullOrWhiteSpace(url) ? null : url.TrimEnd('/');
        }
        catch { return null; }
    }
}