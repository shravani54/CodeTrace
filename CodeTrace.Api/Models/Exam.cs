using System.Collections.Generic;

namespace CodeTrace.Api.Models;

public class Exam
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; }

    public List<ExamIDE> AllowedIDEs { get; set; } = new();
}