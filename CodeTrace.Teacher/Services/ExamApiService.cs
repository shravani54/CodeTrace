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

    public async Task<List<Exam>> GetAllAsync()
    {
        var result = await _client.GetFromJsonAsync<List<Exam>>("api/Exams");
        return result ?? new List<Exam>();
    }

    public async Task<Exam?> CreateAsync(Exam exam)
    {
        var response = await _client.PostAsJsonAsync("api/Exams", exam);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Exam>();
    }

    public async Task<Exam?> UpdateAsync(Exam exam)
    {
        var response = await _client.PutAsJsonAsync($"api/Exams/{exam.Id}", exam);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<Exam>();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _client.DeleteAsync($"api/Exams/{id}");
        response.EnsureSuccessStatusCode();
    }
}