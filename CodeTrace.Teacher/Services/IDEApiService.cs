using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CodeTrace.Teacher.Models;

namespace CodeTrace.Teacher.Services;

public class IDEApiService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<List<IDEConfiguration>> GetAllAsync()
    {
        var result = await _client.GetFromJsonAsync<List<IDEConfiguration>>("api/IDEConfiguration");
        return result ?? new List<IDEConfiguration>();
    }

    public async Task<IDEConfiguration?> CreateAsync(IDEConfiguration ide)
    {
        var response = await _client.PostAsJsonAsync("api/IDEConfiguration", ide);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IDEConfiguration>();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _client.DeleteAsync($"api/IDEConfiguration/{id}");
        response.EnsureSuccessStatusCode();
    }
}