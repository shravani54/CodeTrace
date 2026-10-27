using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubmissionController : ControllerBase
{
    private readonly CodeTraceDbContext _context;
    public SubmissionController(CodeTraceDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _context.Submissions.OrderByDescending(s => s.SubmittedAt).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Submission submission)
    {
        submission.Id = 0;
        submission.SubmittedAt = DateTime.UtcNow;
        _context.Submissions.Add(submission);
        await _context.SaveChangesAsync();
        return Ok(submission);
    }
}