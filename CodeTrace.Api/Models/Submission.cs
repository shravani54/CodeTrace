namespace CodeTrace.Api.Models;

public class Submission
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string StudentName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}