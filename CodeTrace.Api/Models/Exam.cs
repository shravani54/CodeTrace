using System;
using System.Collections.Generic;

namespace CodeTrace.Api.Models;

public class Exam
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; }

    // Optional: when the exam opens for students (stored in UTC). Null = can join any time.
    public DateTime? StartTime { get; set; }

    // When the teacher created the exam (UTC). The exam is deleted 24 hours after this.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ExamIDE> AllowedIDEs { get; set; } = new();
}