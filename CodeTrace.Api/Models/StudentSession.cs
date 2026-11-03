namespace CodeTrace.Api.Models;

public class StudentSession
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string StudentName { get; set; } = "";
    public string Status { get; set; } = "InProgress"; // InProgress, Submitted, Disconnected
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}