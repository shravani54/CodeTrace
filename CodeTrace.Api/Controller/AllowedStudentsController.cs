using CodeTrace.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AllowedStudentsController : ControllerBase
{
    private readonly AllowedStudentStore _store;

    public AllowedStudentsController(AllowedStudentStore store) => _store = store;

    // GET api/AllowedStudents
    [HttpGet]
    public IActionResult GetAll() => Ok(_store.GetAll());

    // POST api/AllowedStudents  (one student, added by hand)
    [HttpPost]
    public IActionResult Add(AllowedStudent student)
    {
        if (!AllowedStudentStore.IsValidRoll(student.Roll))
            return BadRequest("Roll number must be 1 to 10 digits.");

        _store.UpsertMany(new[] { student });
        return Ok();
    }

    // POST api/AllowedStudents/bulk  (the rows read from the Excel file)
    [HttpPost("bulk")]
    public IActionResult Bulk(List<AllowedStudent> students)
    {
        if (students == null || students.Count == 0)
            return BadRequest("The list is empty.");

        if (students.Count > 5000)
            return BadRequest("Too many rows (limit is 5000).");

        var (added, updated) = _store.UpsertMany(students);
        return Ok(new { added, updated });
    }

    // DELETE api/AllowedStudents/2024045
    [HttpDelete("{roll}")]
    public IActionResult Remove(string roll) =>
        _store.Remove(roll) ? NoContent() : NotFound();

    // DELETE api/AllowedStudents  (clear everything)
    [HttpDelete]
    public IActionResult Clear()
    {
        _store.Clear();
        return NoContent();
    }
}