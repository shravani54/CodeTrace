using System.Net.Http;
using System.Net.Http.Json;
using CodeTrace.Student.Models;

namespace CodeTrace.Student.Services;

public class StudentApiService
{
    private static readonly HttpClient _client = new HttpClient
{
    BaseAddress = new Uri(ApiConfig.BaseUrl + "/")
};

    public async Task<List<Exam>> GetActiveExamsAsync()
    {
        var result = await _client.GetFromJsonAsync<List<Exam>>("api/Exams");
        return (result ?? new List<Exam>()).Where(e => e.IsActive).ToList();
    }

    public async Task<List<IDEConfiguration>> GetIDEsAsync()
    {
        var result = await _client.GetFromJsonAsync<List<IDEConfiguration>>("api/IDEConfiguration");
        return result ?? new List<IDEConfiguration>();
    }

    // true = student already submitted/exited this exam
    // false = student may join
    // null = could not check (network/server problem)
    public async Task<bool?> HasEndedAsync(int examId, string studentName)
    {
        try
        {
            var url = $"api/StudentSession/ended?examId={examId}&studentName={Uri.EscapeDataString(studentName)}";
            return await _client.GetFromJsonAsync<bool>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<StudentSession?> StartSessionAsync(int examId, string studentName)
    {
        var response = await _client.PostAsJsonAsync("api/StudentSession", new StudentSession
        {
            ExamId = examId,
            StudentName = studentName,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        });

        // 409 Conflict = exam already ended for this student; any other failure = null too
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<StudentSession>();
    }

    public async Task UpdateSessionStatusAsync(int sessionId, string status)
    {
        await _client.PutAsJsonAsync($"api/StudentSession/{sessionId}", status);
    }

    public async Task<Submission?> SubmitAsync(int examId, string studentName, string content)
    {
        var response = await _client.PostAsJsonAsync("api/Submission", new Submission
        {
            ExamId = examId,
            StudentName = studentName,
            Content = content
        });

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Submission>();
    }
}