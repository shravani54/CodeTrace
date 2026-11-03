using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CodeTrace.Student.Services;

// Tells the server which student is logged in, what they are doing, and which blocked apps they open,
// so the teacher can see it
public static class StudentPresence
{
    private static HttpClient CreateClient() => new()
    {
        BaseAddress = new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(5)
    };

    public static async Task ReportAsync(string studentName, string status)
    {
        try
        {
            using var client = CreateClient();
            await client.PostAsJsonAsync("api/Students/report", new { StudentName = studentName, Status = status });
        }
        catch
        {
            // Never stop the student because the teacher's list could not be updated
        }
    }

    // The student opened an app that is not allowed: the teacher gets a notification
    public static async Task ReportBlockedAppAsync(string studentName, string appName)
    {
        try
        {
            using var client = CreateClient();
            await client.PostAsJsonAsync("api/AppAlerts", new { StudentName = studentName, AppName = appName });
        }
        catch
        {
            // Never stop the student because the alert could not be sent
        }
    }
}