namespace CodeTrace.Teacher.Models;

public class IDEConfiguration
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string ExecutablePath { get; set; } = "";

    public bool IsActive { get; set; } = true;
}