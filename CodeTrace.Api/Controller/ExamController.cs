using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
using CodeTrace.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private readonly CodeTraceDbContext _context;

    public ExamsController(CodeTraceDbContext context)
    {
        _context = context;
    }

    // The Teacher app sends this header with every request. The Student app does not.
    private int? CurrentTeacherId =>
        int.TryParse(Request.Headers["X-Teacher-Id"].ToString(), out var id) ? id : null;

    // GET: api/Exams
    // Teacher: only their own exams. Student (no header): every exam, so they can join any of them.
    [HttpGet]
    public async Task<IActionResult> GetAllExams()
    {
        var cutoff = DateTime.UtcNow - ExpiredExamCleaner.Lifetime;

        var query = _context.Exams
            .Include(e => e.AllowedIDEs)
            .Where(e => e.CreatedAt >= cutoff);

        var teacherId = CurrentTeacherId;
        if (teacherId != null)
            query = query.Where(e => e.TeacherId == teacherId);

        var exams = await query.ToListAsync();

        return Ok(exams);
    }

    // GET: api/Exams/1
    [HttpGet("{id}")]
    public async Task<IActionResult> GetExam(int id)
    {
        var cutoff = DateTime.UtcNow - ExpiredExamCleaner.Lifetime;

        var exam = await _context.Exams
            .Include(e => e.AllowedIDEs)
            .FirstOrDefaultAsync(e => e.Id == id && e.CreatedAt >= cutoff);

        if (exam == null)
            return NotFound($"Exam with ID {id} not found.");

        return Ok(exam);
    }

    // POST: api/Exams
    [HttpPost]
    public async Task<IActionResult> CreateExam(Exam exam)
    {
        var teacherId = CurrentTeacherId;
        if (teacherId == null)
            return Unauthorized("Teacher login required.");

        if (string.IsNullOrWhiteSpace(exam.Name))
            return BadRequest("Exam name is required.");

        if (exam.DurationMinutes <= 0)
            return BadRequest("Duration must be greater than zero.");

        exam.Id = 0;
        exam.CreatedAt = DateTime.UtcNow;
        exam.TeacherId = teacherId;          // the exam belongs to the logged-in teacher

        foreach (var ide in exam.AllowedIDEs)
        {
            ide.Id = 0;
            ide.ExamId = 0;
        }

        _context.Exams.Add(exam);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetExam),
            new { id = exam.Id },
            exam
        );
    }

    // PUT: api/Exams/1
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExam(
        int id,
        Exam exam)
    {
        var teacherId = CurrentTeacherId;
        if (teacherId == null)
            return Unauthorized("Teacher login required.");

        if (id != exam.Id)
            return BadRequest("ID mismatch.");

        var existingExam = await _context.Exams
            .Include(e => e.AllowedIDEs)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (existingExam == null)
            return NotFound($"Exam with ID {id} not found.");

        if (existingExam.TeacherId != teacherId)
            return StatusCode(403, "This exam belongs to another teacher.");

        existingExam.Name = exam.Name;
        existingExam.Description = exam.Description;
        existingExam.DurationMinutes = exam.DurationMinutes;
        existingExam.IsActive = exam.IsActive;
        existingExam.StartTime = exam.StartTime;
        // CreatedAt and TeacherId are not changed here

        // Replace the allowed IDE list
        _context.ExamIDEs.RemoveRange(existingExam.AllowedIDEs);

        existingExam.AllowedIDEs = exam.AllowedIDEs
            .Select(ide => new ExamIDE
            {
                IDEName = ide.IDEName,
                IsAllowed = ide.IsAllowed
            })
            .ToList();

        await _context.SaveChangesAsync();

        return Ok(existingExam);
    }

    // DELETE: api/Exams/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExam(int id)
    {
        var teacherId = CurrentTeacherId;
        if (teacherId == null)
            return Unauthorized("Teacher login required.");

        var exam = await _context.Exams
            .Include(e => e.AllowedIDEs)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null)
            return NotFound($"Exam with ID {id} not found.");

        if (exam.TeacherId != teacherId)
            return StatusCode(403, "This exam belongs to another teacher.");

        _context.Exams.Remove(exam);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}