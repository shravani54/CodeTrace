using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CodeTrace.Student.Models;

namespace CodeTrace.Student.Services;

public class StudentApiService
{
    private static HttpClient _client = CreateClient(TimeSpan.FromSeconds(8));

    // Separate client with a long timeout, because a ZIP upload takes longer than a normal request
    private static HttpClient _uploadClient = CreateClient(TimeSpan.FromMinutes(5));

    private static HttpClient CreateClient(TimeSpan timeout) => new HttpClient
    {
        BaseAddress = new Uri((ApiConfig.HasServer ? ApiConfig.BaseUrl : "http://localhost:5080") + "/"),
        Timeout = timeout
    };

    // Call after the student enters a new server IP
    public static void Reconfigure()
    {
        _client = CreateClient(TimeSpan.FromSeconds(8));
        _uploadClient = CreateClient(TimeSpan.FromMinutes(5));
    }

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

    // ---- Connect request (teacher approval) ----
    // Sent when the student clicks Connect. Returns the request id, or null if the server is unreachable.
    public async Task<Guid?> CreateJoinRequestAsync(string studentName)
    {
        try
        {
            var resp = await _client.PostAsJsonAsync("api/JoinRequests", new { studentName });
            if (!resp.IsSuccessStatusCode) return null;
            var dto = await resp.Content.ReadFromJsonAsync<JoinRequestDto>();
            return dto?.Id;
        }
        catch { return null; }
    }

    // Returns "Pending", "Approved" or "Rejected", or null if the check failed
    public async Task<string?> GetJoinStatusAsync(Guid id)
    {
        try
        {
            var dto = await _client.GetFromJsonAsync<JoinRequestDto>($"api/JoinRequests/{id}");
            return dto?.Status;
        }
        catch { return null; }
    }

    public async Task<StudentSession?> StartSessionAsync(int examId, string studentName)
    {
        try
        {
            var response = await _client.PostAsJsonAsync("api/StudentSession", new StudentSession
            {
                ExamId = examId,
                StudentName = studentName,
                Status = "InProgress",
                StartedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            });

            // 403 = not approved by the teacher, 409 = exam already ended; any other failure = null too
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<StudentSession>();
        }
        catch
        {
            return null;
        }
    }

    // Like StartSessionAsync, but also returns the HTTP status:
    // 200 = started, 403 = not approved, 425 = exam has not started yet, 409 = already ended, 0 = no connection
    public async Task<StartResult> TryStartSessionAsync(int examId, string studentName)
    {
        try
        {
            var response = await _client.PostAsJsonAsync("api/StudentSession", new StudentSession
            {
                ExamId = examId,
                StudentName = studentName,
                Status = "InProgress",
                StartedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            });

            if (!response.IsSuccessStatusCode)
                return new StartResult(null, (int)response.StatusCode);

            var session = await response.Content.ReadFromJsonAsync<StudentSession>();
            return new StartResult(session, 200);
        }
        catch
        {
            return new StartResult(null, 0);
        }
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

    // Uploads the ZIP of the student's code folder for this submission. true = the server saved it.
    public async Task<bool> UploadSubmissionZipAsync(int submissionId, string zipPath)
    {
        try
        {
            await using var stream = File.OpenRead(zipPath);
            using var form = new MultipartFormDataContent();

            var file = new StreamContent(stream);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            form.Add(file, "file", "submission.zip");

            var response = await _uploadClient.PostAsync($"api/SubmissionFiles/{submissionId}", form);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ===== NEW: camera =====
    // Sends one JPEG photo of the student to the API. A failed photo is ignored,
    // because the next one is sent a few seconds later.
    public async Task SendCameraFrameAsync(string studentId, byte[] jpegBytes)
    {
        try
        {
            using var content = new ByteArrayContent(jpegBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            await _client.PostAsync($"api/proctor/{Uri.EscapeDataString(studentId)}/frame", content);
        }
        catch
        {
            // ignore
        }
    }
    // ===== END NEW =====
}

public record JoinRequestDto(Guid Id, string Status);

public record StartResult(StudentSession? Session, int StatusCode);