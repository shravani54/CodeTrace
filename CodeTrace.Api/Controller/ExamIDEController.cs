using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamIDEController : ControllerBase
{
    private readonly CodeTraceDbContext _context;

    public ExamIDEController(CodeTraceDbContext context)
    {
        _context = context;
    }

    // GET: api/ExamIDE/exam/1
    [HttpGet("exam/{examId}")]
    public async Task<IActionResult> GetExamIDEs(int examId)
    {
        var examExists = await _context.Exams
            .AnyAsync(e => e.Id == examId);

        if (!examExists)
            return NotFound($"Exam with ID {examId} not found.");

        var ides = await _context.ExamIDEs
            .Where(e => e.ExamId == examId)
            .ToListAsync();

        return Ok(ides);
    }

    // POST: api/ExamIDE
    [HttpPost]
    public async Task<IActionResult> AddExamIDE(ExamIDE examIDE)
    {
        var examExists = await _context.Exams
            .AnyAsync(e => e.Id == examIDE.ExamId);

        if (!examExists)
            return NotFound($"Exam with ID {examIDE.ExamId} not found.");

        _context.ExamIDEs.Add(examIDE);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetExamIDEs),
            new { examId = examIDE.ExamId },
            examIDE
        );
    }

    // PUT: api/ExamIDE/1
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExamIDE(
        int id,
        ExamIDE examIDE)
    {
        if (id != examIDE.Id)
            return BadRequest("ID mismatch.");

        var existingIDE = await _context.ExamIDEs
            .FindAsync(id);

        if (existingIDE == null)
            return NotFound($"ExamIDE with ID {id} not found.");

        existingIDE.IDEName = examIDE.IDEName;
        existingIDE.IsAllowed = examIDE.IsAllowed;

        await _context.SaveChangesAsync();

        return Ok(existingIDE);
    }

    // DELETE: api/ExamIDE/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExamIDE(int id)
    {
        var examIDE = await _context.ExamIDEs
            .FindAsync(id);

        if (examIDE == null)
            return NotFound($"ExamIDE with ID {id} not found.");

        _context.ExamIDEs.Remove(examIDE);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}