using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class JoinRequestItem
{
    public Guid Id { get; set; }
    public string StudentName { get; set; } = "";
}

public class JoinRequestApiService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/"),
        Timeout = TimeSpan.FromSeconds(8)
    };

    public async Task<List<JoinRequestItem>> GetPendingAsync() =>
        await _client.GetFromJsonAsync<List<JoinRequestItem>>("api/JoinRequests/pending") ?? new();

    public async Task<bool> ApproveAsync(Guid id) =>
        (await _client.PostAsync($"api/JoinRequests/{id}/approve", null)).IsSuccessStatusCode;

    public async Task<bool> RejectAsync(Guid id) =>
        (await _client.PostAsync($"api/JoinRequests/{id}/reject", null)).IsSuccessStatusCode;
}