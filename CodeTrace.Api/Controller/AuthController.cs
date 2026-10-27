using CodeTrace.Api.Data;
using CodeTrace.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CodeTraceDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(CodeTraceDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // POST: api/Auth/register
    // Creates a new teacher account.
    // If "TeacherSignupCode" is set in appsettings.json, the request must send the same value
    // in the X-Signup-Code header. If it is empty or missing, anyone can sign up.
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var requiredCode = _config["TeacherSignupCode"];
        if (!string.IsNullOrWhiteSpace(requiredCode))
        {
            var supplied = Request.Headers["X-Signup-Code"].ToString().Trim();
            if (supplied != requiredCode.Trim())
                return StatusCode(403, "Invalid teacher access code.");
        }

        var username = (request.Username ?? "").Trim();
        var fullName = (request.FullName ?? "").Trim();
        var password = request.Password ?? "";

        if (username.Length < 3 || username.Length > 30)
            return BadRequest("Username must be 3 to 30 characters.");

        if (fullName.Length < 2)
            return BadRequest("Full name is required.");

        if (password.Length < 6)
            return BadRequest("Password must be at least 6 characters.");

        var exists = await _context.Teachers
            .AnyAsync(t => t.Username == username);

        if (exists)
            return BadRequest("That username is already taken.");

        var teacher = new Teacher
        {
            Username = username,
            FullName = fullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true
        };

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync();

        return Ok(new LoginResponse
        {
            Id = teacher.Id,
            Username = teacher.Username,
            FullName = teacher.FullName
        });
    }

    // POST: api/Auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var teacher = await _context.Teachers
            .FirstOrDefaultAsync(t => t.Username == request.Username);

        if (teacher == null || !teacher.IsActive)
            return Unauthorized("Invalid username or password.");

        bool passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, teacher.PasswordHash);

        if (!passwordOk)
            return Unauthorized("Invalid username or password.");

        return Ok(new LoginResponse
        {
            Id = teacher.Id,
            Username = teacher.Username,
            FullName = teacher.FullName
        });
    }
}