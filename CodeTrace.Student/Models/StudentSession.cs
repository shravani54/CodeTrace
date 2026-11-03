namespace CodeTrace.Student.Models;

public class StudentSession
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string StudentName { get; set; } = "";
    public string Status { get; set; } = "InProgress";
    public DateTime StartedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}