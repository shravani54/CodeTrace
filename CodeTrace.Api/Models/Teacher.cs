namespace CodeTrace.Api.Models;

public class Teacher
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    public string FullName { get; set; } = "";

    public bool IsActive { get; set; } = true;
}