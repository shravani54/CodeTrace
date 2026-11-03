using System;

namespace CodeTrace.Teacher.Models;

public class Violation
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string StudentName { get; set; } = "";
    public string RollNumber { get; set; } = "";
    public string Type { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime TimeUtc { get; set; }

    public string TimeLocal => TimeUtc.ToLocalTime().ToString("HH:mm:ss");
}