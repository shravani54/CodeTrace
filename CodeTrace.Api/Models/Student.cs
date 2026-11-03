namespace CodeTrace.Api.Models;

public class Student
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string RollNumber { get; set; } = "";

    public string Email { get; set; } = "";

    public bool IsActive { get; set; }
}