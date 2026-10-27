using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeTrace.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Controllers;

public class StudentReport
{
    public string StudentName { get; set; } = "";   // sent as "Name (Roll)"
    public string Status { get; set; } = "";
}

public class ConnectedStudent
{
    public string Name { get; set; } = "";
    public string Roll { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime LoggedInAt { get; set; }

    // The exam this student joined (from their latest session). null = approved but no exam chosen yet.
    public int? ExamId { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly CodeTraceDbContext _context;

    public StudentsController(CodeTraceDbContext context)
    {
        _context = context;
    }

    // Kept in memory: the list is empty again when the API restarts
    private static readonly ConcurrentDictionary<string, ConnectedStudent> Students = new();

    private static readonly Regex NamePattern =
        new(@"^(?<name>.+?)\s*\((?<roll>\d+)\)\s*$");

    // POST api/Students/report: a student logs in or changes status
    [HttpPost("report")]
    public IActionResult Report(StudentReport report)
    {
        var match = NamePattern.Match(report.StudentName ?? "");
        if (!match.Success)
            return BadRequest("Student name must look like: Name (Roll)");

        var name = match.Groups["name"].Value.Trim();
        var roll = match.Groups["roll"].Value;
        var key = $"{roll}|{name.ToLowerInvariant()}";

        Students.AddOrUpdate(
            key,
            _ => new ConnectedStudent
            {
                Name = name,
                Roll = roll,
                Status = report.Status,
                LoggedInAt = DateTime.UtcNow
            },
            (_, existing) =>
            {
                existing.Status = report.Status;
                return existing;
            });

        return Ok();
    }

    // GET api/Students            -> everyone, each with the ExamId they joined
    // GET api/Students?examId=5   -> only students of exam 5 (examId=0 -> students with no exam yet)
    [HttpGet]
    public async Task<IActionResult> GetAll(int? examId = null)
    {
        var map = await LatestExamIdsAsync();

        var list = Students.Values
            .OrderBy(s => s.LoggedInAt)
            .Select(s => new ConnectedStudent
            {
                Name = s.Name,
                Roll = s.Roll,
                Status = s.Status,
                LoggedInAt = s.LoggedInAt,
                ExamId = ExamIdFor(map, s)
            })
            .ToList();

        if (examId.HasValue)
            list = list.Where(s => examId == 0 ? s.ExamId == null : s.ExamId == examId).ToList();

        return Ok(list);
    }

    // DELETE api/Students            -> clear everyone
    // DELETE api/Students?examId=5   -> clear only the students of exam 5 (0 = students with no exam yet)
    [HttpDelete]
    public async Task<IActionResult> Clear(int? examId = null)
    {
        if (examId == null)
        {
            Students.Clear();
            return NoContent();
        }

        var map = await LatestExamIdsAsync();

        foreach (var pair in Students)
        {
            var id = ExamIdFor(map, pair.Value);
            var matches = examId == 0 ? id == null : id == examId;
            if (matches) Students.TryRemove(pair.Key, out _);
        }

        return NoContent();
    }

    // "name (roll)" (lower case) -> exam id of that student's latest session
    private async Task<Dictionary<string, int>> LatestExamIdsAsync()
    {
        var sessions = await _context.StudentSessions
            .AsNoTracking()
            .Select(s => new { s.StudentName, s.ExamId, s.StartedAt })
            .ToListAsync();

        return sessions
            .GroupBy(s => (s.StudentName ?? "").Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.StartedAt).First().ExamId);
    }

    private static int? ExamIdFor(Dictionary<string, int> map, ConnectedStudent s) =>
        map.TryGetValue($"{s.Name} ({s.Roll})".ToLowerInvariant(), out var id) ? id : null;
}