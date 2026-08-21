using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentController : ControllerBase
{
    private readonly CodeTraceDbContext _context;

    public StudentController(CodeTraceDbContext context)
    {
        _context = context;
    }

    // GET: api/Student
    [HttpGet]
    public async Task<IActionResult> GetAllStudents()
    {
        var students = await _context.Students.ToListAsync();

        return Ok(students);
    }

    // GET: api/Student/1
    [HttpGet("{id}")]
    public async Task<IActionResult> GetStudent(int id)
    {
        var student = await _context.Students.FindAsync(id);

        if (student == null)
            return NotFound($"Student with ID {id} not found.");

        return Ok(student);
    }

    // POST: api/Student
    [HttpPost]
    public async Task<IActionResult> CreateStudent(Student student)
    {
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetStudent),
            new { id = student.Id },
            student
        );
    }

    // PUT: api/Student/1
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStudent(
        int id,
        Student student)
    {
        if (id != student.Id)
            return BadRequest("ID mismatch.");

        var existingStudent = await _context.Students.FindAsync(id);

        if (existingStudent == null)
            return NotFound($"Student with ID {id} not found.");

        existingStudent.Name = student.Name;
        existingStudent.RollNumber = student.RollNumber;
        existingStudent.Email = student.Email;
        existingStudent.IsActive = student.IsActive;

        await _context.SaveChangesAsync();

        return Ok(existingStudent);
    }

    // DELETE: api/Student/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteStudent(int id)
    {
        var student = await _context.Students.FindAsync(id);

        if (student == null)
            return NotFound($"Student with ID {id} not found.");

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}