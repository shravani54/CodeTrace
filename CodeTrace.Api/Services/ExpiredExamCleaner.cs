using CodeTrace.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeTrace.Api.Services;

// Runs inside the API and deletes every examination that is older than 24 hours
public class ExpiredExamCleaner : BackgroundService
{
    // How long an exam lives after the teacher creates it
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    // How often the cleaner looks for expired exams
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredExamCleaner> _logger;

    public ExpiredExamCleaner(IServiceScopeFactory scopeFactory, ILogger<ExpiredExamCleaner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete expired examinations.");
            }

            try
            {
                await Task.Delay(CheckEvery, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DeleteExpiredAsync(CancellationToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CodeTraceDbContext>();

        var cutoff = DateTime.UtcNow - Lifetime;

        var expired = await context.Exams
            .Include(e => e.AllowedIDEs)
            .Where(e => e.CreatedAt < cutoff)
            .ToListAsync(token);

        foreach (var exam in expired)
        {
            try
            {
                context.Exams.Remove(exam);
                await context.SaveChangesAsync(token);
                _logger.LogInformation("Deleted expired examination {Id} ({Name}).", exam.Id, exam.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // For example when other data still depends on this exam. It stays hidden and is tried again later.
                _logger.LogWarning(ex, "Could not delete examination {Id}.", exam.Id);
                context.ChangeTracker.Clear();
            }
        }
    }
}