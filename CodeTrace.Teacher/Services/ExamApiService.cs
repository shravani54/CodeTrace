using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class ExamApiService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/")
    };

    // Every request carries the logged-in teacher's id
    private static HttpRequestMessage Build(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add("X-Teacher-Id", TeacherSession.TeacherId.ToString());
        return request;
    }

    public async Task<List<Exam>> GetAllAsync()
    {
        using var response = await _client.SendAsync(Build(HttpMethod.Get, "api/Exams"));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<Exam>>();
        return result ?? new List<Exam>();
    }

    public async Task<Exam?> CreateAsync(Exam exam)
    {
        using var response = await _client.SendAsync(
            Build(HttpMethod.Post, "api/Exams", JsonContent.Create(exam)));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Exam>();
    }

    public async Task<Exam?> UpdateAsync(Exam exam)
    {
        using var response = await _client.SendAsync(
            Build(HttpMethod.Put, $"api/Exams/{exam.Id}", JsonContent.Create(exam)));
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<Exam>();
    }

    public async Task DeleteAsync(int id)
    {
        using var response = await _client.SendAsync(Build(HttpMethod.Delete, $"api/Exams/{id}"));
        response.EnsureSuccessStatusCode();
    }
}