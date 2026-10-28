using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class SubmissionItem
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string StudentName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
}

public class SubmissionListService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/"),
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<List<SubmissionItem>> GetAllAsync()
    {
        var result = await _client.GetFromJsonAsync<List<SubmissionItem>>("api/Submission");
        return result ?? new List<SubmissionItem>();
    }
}