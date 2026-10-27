using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
using CodeTrace.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentSessionController : ControllerBase
{
    private readonly CodeTraceDbContext _context;
    private readonly JoinRequestStore _joinRequests;

    public StudentSessionController(CodeTraceDbContext context, JoinRequestStore joinRequests)
    {
        _context = context;
        _joinRequests = joinRequests;
    }

    private static bool IsEnded(string status) =>
        status == "Submitted" || status == "Exited";

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.StudentSessions.ToListAsync());

    // GET api/StudentSession/ended?examId=1&studentName=supriya (25)
    [HttpGet("ended")]
    public async Task<ActionResult<bool>> HasEnded(int examId, string studentName)
    {
        var ended = await _context.StudentSessions.AnyAsync(s =>
            s.ExamId == examId &&
            s.StudentName == studentName &&
            (s.Status == "Submitted" || s.Status == "Exited"));

        return Ok(ended);
    }

    [HttpPost]
    public async Task<IActionResult> Create(StudentSession session)
    {
        // The teacher must have approved this student's join request for this exam
        if (!_joinRequests.IsApproved(session.StudentName))
    return StatusCode(403, "Teacher approval is required.");
        var existing = await _context.StudentSessions
            .Where(s => s.ExamId == session.ExamId && s.StudentName == session.StudentName)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            // Already finished or exited: cannot rejoin
            if (IsEnded(existing.Status))
                return Conflict("Examination has ended for this student.");

            // Still in progress (app was killed): resume the SAME session,
            // so StartedAt is not reset
            existing.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        session.Id = 0;
        session.StartedAt = DateTime.UtcNow;
        session.LastSeenAt = DateTime.UtcNow;
        _context.StudentSessions.Add(session);
        await _context.SaveChangesAsync();
        return Ok(session);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] string status)
    {
        var session = await _context.StudentSessions.FindAsync(id);
        if (session == null) return NotFound();
        session.Status = status;
        session.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(session);
    }
}