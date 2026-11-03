using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class SubmissionZipService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/"),
        Timeout = TimeSpan.FromMinutes(5)
    };

    // The submission ids that have a ZIP on the server
    public async Task<HashSet<int>> GetIdsWithZipAsync()
    {
        try
        {
            var ids = await _client.GetFromJsonAsync<List<int>>("api/SubmissionFiles/ids");
            return (ids ?? new List<int>()).ToHashSet();
        }
        catch
        {
            return new HashSet<int>();
        }
    }

    // Copies the ZIP of one submission into the stream. false = no ZIP or a network error.
    public async Task<bool> DownloadAsync(int submissionId, Stream destination)
    {
        try
        {
            using var response = await _client.GetAsync($"api/SubmissionFiles/{submissionId}",
                                                        HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return false;

            await using var source = await response.Content.ReadAsStreamAsync();
            await source.CopyToAsync(destination);
            return true;
        }
        catch
        {
            return false;
        }
    }
}