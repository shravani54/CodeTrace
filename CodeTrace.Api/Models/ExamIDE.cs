namespace CodeTrace.Api.Models;

public class ExamIDE
{
    public int Id { get; set; }

    public int ExamId { get; set; }

    public string IDEName { get; set; } = "";

    public bool IsAllowed { get; set; }

    public Exam? Exam { get; set; }
}