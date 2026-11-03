using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class AppAlert
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Roll { get; set; } = "";
    public string AppName { get; set; } = "";
    public DateTime Time { get; set; }
}

public class AppAlertBatch
{
    public long LatestId { get; set; }
    public List<AppAlert> Alerts { get; set; } = new();
}

public class AppAlertService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/"),
        Timeout = TimeSpan.FromSeconds(5)
    };

    // Alerts newer than afterId. Returns null when the server cannot be reached.
    public async Task<AppAlertBatch?> GetAfterAsync(long afterId)
    {
        try
        {
            return await _client.GetFromJsonAsync<AppAlertBatch>($"api/AppAlerts?after={afterId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearAsync()
    {
        try { await _client.DeleteAsync("api/AppAlerts"); }
        catch { /* ignore */ }
    }
}