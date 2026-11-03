namespace CodeTrace.Api.Models;   // use your server's namespace

public class Violation
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string StudentName { get; set; } = "";
    public string RollNumber { get; set; } = "";
    public string Type { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime TimeUtc { get; set; }
}