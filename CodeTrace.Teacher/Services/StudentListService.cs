using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class ConnectedStudent
{
    public string Name { get; set; } = "";
    public string Roll { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime LoggedInAt { get; set; }

    // null = approved but has not chosen an exam yet
    public int? ExamId { get; set; }
}

public class StudentListService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/"),
        Timeout = TimeSpan.FromSeconds(5)
    };

    // Returns null when the server cannot be reached
    public async Task<List<ConnectedStudent>?> GetAllAsync()
    {
        try
        {
            return await _client.GetFromJsonAsync<List<ConnectedStudent>>("api/Students")
                   ?? new List<ConnectedStudent>();
        }
        catch
        {
            return null;
        }
    }

    // examId = null clears everyone; 0 clears students who have no exam yet; otherwise only that exam's students
    public async Task ClearAsync(int? examId = null)
    {
        try
        {
            var url = examId == null ? "api/Students" : $"api/Students?examId={examId}";
            await _client.DeleteAsync(url);
        }
        catch { /* ignore */ }
    }
}