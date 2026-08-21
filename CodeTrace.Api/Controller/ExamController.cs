using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
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

    // GET: api/Exams
    [HttpGet]
    public async Task<IActionResult> GetAllExams()
    {
        var exams = await _context.Exams
            .Include(e => e.AllowedIDEs)
            .ToListAsync();

        return Ok(exams);
    }

    // GET: api/Exams/1
    [HttpGet("{id}")]
    public async Task<IActionResult> GetExam(int id)
    {
        var exam = await _context.Exams
            .Include(e => e.AllowedIDEs)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null)
            return NotFound($"Exam with ID {id} not found.");

        return Ok(exam);
    }

    // POST: api/Exams
    [HttpPost]
    public async Task<IActionResult> CreateExam(Exam exam)
    {
        if (string.IsNullOrWhiteSpace(exam.Name))
            return BadRequest("Exam name is required.");

        if (exam.DurationMinutes <= 0)
            return BadRequest("Duration must be greater than zero.");

        exam.Id = 0;

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
        if (id != exam.Id)
            return BadRequest("ID mismatch.");

        var existingExam = await _context.Exams
            .Include(e => e.AllowedIDEs)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (existingExam == null)
            return NotFound($"Exam with ID {id} not found.");

        existingExam.Name = exam.Name;
        existingExam.Description = exam.Description;
        existingExam.DurationMinutes = exam.DurationMinutes;
        existingExam.IsActive = exam.IsActive;

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
        var exam = await _context.Exams
            .Include(e => e.AllowedIDEs)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null)
            return NotFound($"Exam with ID {id} not found.");

        _context.Exams.Remove(exam);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}