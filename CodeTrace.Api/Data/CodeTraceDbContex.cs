using CodeTrace.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Data;

public class CodeTraceDbContext : DbContext
{
    public CodeTraceDbContext(
        DbContextOptions<CodeTraceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Exam> Exams => Set<Exam>();

    public DbSet<ExamIDE> ExamIDEs => Set<ExamIDE>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<IDEConfiguration> IDEConfigurations => Set<IDEConfiguration>();

    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<StudentSession> StudentSessions => Set<StudentSession>();
    public DbSet<Violation> Violations => Set<Violation>();
}