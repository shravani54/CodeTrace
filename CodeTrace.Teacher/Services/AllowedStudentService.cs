using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class AllowedStudentItem
{
    public string Name { get; set; } = "";
    public string Roll { get; set; } = "";
}

public class ImportResult
{
    public int Added { get; set; }
    public int Updated { get; set; }
}

public class AllowedStudentService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/"),
        Timeout = TimeSpan.FromSeconds(20)
    };

    // null = the server cannot be reached
    public async Task<List<AllowedStudentItem>?> GetAllAsync()
    {
        try
        {
            return await _client.GetFromJsonAsync<List<AllowedStudentItem>>("api/AllowedStudents")
                   ?? new List<AllowedStudentItem>();
        }
        catch
        {
            return null;
        }
    }

    // Returns "" when added, otherwise the message to show
    public async Task<string> AddAsync(string name, string roll)
    {
        try
        {
            var response = await _client.PostAsJsonAsync("api/AllowedStudents", new { name, roll });
            if (response.IsSuccessStatusCode) return "";

            var text = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
            return string.IsNullOrWhiteSpace(text) ? "Could not add the student." : text;
        }
        catch
        {
            return "Cannot reach the server.";
        }
    }

    public async Task<ImportResult?> ImportAsync(List<AllowedStudentItem> students)
    {
        try
        {
            var response = await _client.PostAsJsonAsync("api/AllowedStudents/bulk", students);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ImportResult>();
        }
        catch
        {
            return null;
        }
    }

    public async Task RemoveAsync(string roll)
    {
        try { await _client.DeleteAsync($"api/AllowedStudents/{Uri.EscapeDataString(roll)}"); }
        catch { /* ignore */ }
    }

    public async Task ClearAsync()
    {
        try { await _client.DeleteAsync("api/AllowedStudents"); }
        catch { /* ignore */ }
    }
}