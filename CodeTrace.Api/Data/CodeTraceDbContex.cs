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
}