using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Teacher.Services;

public class LoginResult
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
}

public class AuthApiService
{
    private static readonly HttpClient _client = new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl + "/")
    };

    public async Task<LoginResult?> LoginAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("api/Auth/login", new
        {
            username,
            password
        });

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<LoginResult>();
    }

    // Creates a teacher account. Returns the account, or null plus a message for the screen.
    public async Task<(LoginResult? Result, string Error)> RegisterAsync(
        string username, string password, string fullName, string accessCode)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/Auth/register")
            {
                Content = JsonContent.Create(new { username, password, fullName })
            };

            if (!string.IsNullOrWhiteSpace(accessCode))
                request.Headers.Add("X-Signup-Code", accessCode.Trim());

            using var response = await _client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResult>();
                return (result, "");
            }

            var text = (await response.Content.ReadAsStringAsync()).Trim().Trim('"');
            return (null, string.IsNullOrWhiteSpace(text) ? "Could not create the account." : text);
        }
        catch
        {
            return (null, "Cannot reach the server.");
        }
    }
}