  // where your DbContext lives
using CodeTrace.Api.Models;
using CodeTrace.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace CodeTrace.Api.Controllers;

public record ViolationDto(
    int ExamId, string StudentName, string RollNumber, string Type, string Detail, DateTime TimeUtc);

[ApiController]
[Route("api/[controller]")]
public class ViolationsController : ControllerBase
{
       private readonly CodeTraceDbContext _db;

    public ViolationsController(CodeTraceDbContext db) => _db = db;
    // Student app posts an event
    [HttpPost]
    public async Task<IActionResult> Post(ViolationDto dto)
    {
        _db.Violations.Add(new Violation
        {
            ExamId = dto.ExamId,
            StudentName = dto.StudentName,
            RollNumber = dto.RollNumber,
            Type = dto.Type,
            Detail = dto.Detail,
            TimeUtc = dto.TimeUtc
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // Teacher app live feed: only events newer than afterId
    [HttpGet]
    public async Task<IActionResult> Get(int examId, int afterId = 0) =>
        Ok(await _db.Violations
            .Where(v => v.ExamId == examId && v.Id > afterId)
            .OrderBy(v => v.Id)
            .ToListAsync());

    // Report after the exam: all events of one student (by roll number)
    [HttpGet("student")]
    public async Task<IActionResult> ByStudent(int examId, string rollNumber) =>
        Ok(await _db.Violations
            .Where(v => v.ExamId == examId && v.RollNumber == rollNumber)
            .OrderBy(v => v.TimeUtc)
            .ToListAsync());
}