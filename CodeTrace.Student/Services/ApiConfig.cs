using System.Text.Json;

namespace CodeTrace.Student.Services;

public static class ApiConfig
{
    private const string Fallback = "http://localhost:5080";

    public static string BaseUrl { get; } = Load();

    private static string Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "apiconfig.json");
            if (!File.Exists(path)) return Fallback;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var url = doc.RootElement.GetProperty("ApiBaseUrl").GetString();
            return string.IsNullOrWhiteSpace(url) ? Fallback : url.TrimEnd('/');
        }
        catch
        {
            return Fallback;
        }
    }
}