using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IDEConfigurationController : ControllerBase
{
    private readonly CodeTraceDbContext _context;

    public IDEConfigurationController(CodeTraceDbContext context)
    {
        _context = context;
    }

    // GET: api/IDEConfiguration
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ides = await _context.IDEConfigurations.ToListAsync();
        return Ok(ides);
    }

    // GET: api/IDEConfiguration/1
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ide = await _context.IDEConfigurations.FindAsync(id);

        if (ide == null)
            return NotFound($"IDE with ID {id} not found.");

        return Ok(ide);
    }

    // POST: api/IDEConfiguration
    [HttpPost]
    public async Task<IActionResult> Create(IDEConfiguration ide)
    {
        if (string.IsNullOrWhiteSpace(ide.Name))
            return BadRequest("IDE name is required.");

        if (string.IsNullOrWhiteSpace(ide.ExecutablePath))
            return BadRequest("Executable path is required.");

        ide.Id = 0;

        _context.IDEConfigurations.Add(ide);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ide.Id }, ide);
    }

    // PUT: api/IDEConfiguration/1
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, IDEConfiguration ide)
    {
        if (id != ide.Id)
            return BadRequest("ID mismatch.");

        var existing = await _context.IDEConfigurations.FindAsync(id);

        if (existing == null)
            return NotFound($"IDE with ID {id} not found.");

        existing.Name = ide.Name;
        existing.ExecutablePath = ide.ExecutablePath;
        existing.IsActive = ide.IsActive;

        await _context.SaveChangesAsync();

        return Ok(existing);
    }

    // DELETE: api/IDEConfiguration/1
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ide = await _context.IDEConfigurations.FindAsync(id);

        if (ide == null)
            return NotFound($"IDE with ID {id} not found.");

        _context.IDEConfigurations.Remove(ide);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}