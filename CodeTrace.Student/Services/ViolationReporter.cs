using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodeTrace.Student.Services;

public sealed record ViolationReport(
    int ExamId, string StudentName, string RollNumber, string Type, string Detail, DateTime TimeUtc);

/// <summary>Sends "unauthorised activity" events to the server so the teacher can see them live.</summary>
public sealed class ViolationReporter : IDisposable
{
    private readonly HttpClient _http;
    private readonly int _examId;
    private readonly string _student;
    private readonly string _roll;
    private readonly ConcurrentQueue<ViolationReport> _queue = new();
    private readonly Dictionary<string, DateTime> _lastSent = new();
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts = new();

    /// <param name="baseUrl">for example "http://192.168.1.10:5080" (the teacher PC's address, not localhost, in a lab)</param>
    public ViolationReporter(string baseUrl, int examId, string studentName, string rollNumber)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(5) };
        _examId = examId;
        _student = studentName;
        _roll = rollNumber;
        _ = Task.Run(SendLoopAsync);
    }

    public void Report(string type, string detail)
    {
        var key = type + "|" + detail;
        lock (_gate)
        {
            if (_lastSent.TryGetValue(key, out var last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(10))
                return;
            _lastSent[key] = DateTime.UtcNow;
        }
        _queue.Enqueue(new ViolationReport(_examId, _student, _roll, type, detail, DateTime.UtcNow));
    }

    private async Task SendLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                while (_queue.TryPeek(out var item))
                {
                    var response = await _http.PostAsJsonAsync("api/Violations", item, _cts.Token);
                    if (!response.IsSuccessStatusCode) break;   // try again later
                    _queue.TryDequeue(out _);
                }
                await Task.Delay(1000, _cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch { try { await Task.Delay(3000, _cts.Token); } catch { break; } }   // server unreachable
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _http.Dispose();
    }
}