using System;
using System.Collections.Generic;

namespace CodeTrace.Student.Models;

public class Exam
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; }

    // When the exam starts, in UTC. null = no start time (students can join any time).
    public DateTime? StartTime { get; set; }

    public List<ExamIDE> AllowedIDEs { get; set; } = new();
}

public class ExamIDE
{
    public string IDEName { get; set; } = "";
    public bool IsAllowed { get; set; }
}