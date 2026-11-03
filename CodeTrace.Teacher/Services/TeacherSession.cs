namespace CodeTrace.Teacher.Services;

// Remembers which teacher is logged in, so every exam request can say who is asking.
public static class TeacherSession
{
    public static int TeacherId { get; set; }
}