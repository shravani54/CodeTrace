using System;
using System.Collections.Generic;

namespace CodeTrace.Teacher;

public class Exam
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;

    // When the exam starts, in UTC. null = no start time (students can join any time).
    public DateTime? StartTime { get; set; }

    public List<ExamIDE> AllowedIDEs { get; set; } = new();
}

public class ExamIDE
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string IDEName { get; set; } = "";
    public bool IsAllowed { get; set; }
}