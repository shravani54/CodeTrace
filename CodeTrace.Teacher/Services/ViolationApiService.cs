using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CodeTrace.Teacher.Models;

namespace CodeTrace.Teacher.Services;

public sealed class ViolationApiService
{
    private readonly HttpClient _http;

    public ViolationApiService(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<List<Violation>> GetAsync(int examId, int afterId)
    {
        var result = await _http.GetFromJsonAsync<List<Violation>>(
            $"api/Violations?examId={examId}&afterId={afterId}");
        return result ?? new List<Violation>();
    }
}