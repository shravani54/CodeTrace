using CodeTrace.Api.Data;
using CodeTrace.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Lets Student and Teacher PCs on the network reach the server (not only localhost)
builder.WebHost.UseUrls("http://0.0.0.0:5080");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<CodeTraceDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("CodeTraceDatabase")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The teacher's Excel list of students who are approved automatically.
// Must be registered BEFORE JoinRequestStore, because the join store uses it.
builder.Services.AddSingleton<AllowedStudentStore>();
builder.Services.AddSingleton<JoinRequestStore>();

builder.Services.AddHostedService<DiscoveryService>();

// Deletes every examination 24 hours after it was created
builder.Services.AddHostedService<ExpiredExamCleaner>();


var app = builder.Build();

// ===== Adds missing Exams columns on startup (safe to run many times) =====
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CodeTraceDbContext>();
    db.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('Exams','StartTime') IS NULL
    ALTER TABLE Exams ADD StartTime DATETIME2 NULL;
IF COL_LENGTH('Exams','CreatedAt') IS NULL
    ALTER TABLE Exams ADD CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Exams_CreatedAt DEFAULT SYSUTCDATETIME();");
    Console.WriteLine("Database check done: Exams columns are OK.");
}
catch (Exception ex)
{
    Console.WriteLine("Database check failed: " + ex.Message);
}
// ===== END =====

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.MapControllers();

// ===== Camera frames (latest photo per student, kept in memory only) =====
var frames = new System.Collections.Concurrent.ConcurrentDictionary<string, byte[]>();

// Student app sends a photo here
app.MapPost("/api/proctor/{id}/frame", async (string id, HttpRequest req) =>
{
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    if (ms.Length == 0 || ms.Length > 2_000_000) return Results.BadRequest();
    frames[id] = ms.ToArray();
    return Results.Ok();
});

// Teacher app reads the latest photo from here
app.MapGet("/api/proctor/{id}/frame", (string id) =>
    frames.TryGetValue(id, out var b) ? Results.File(b, "image/jpeg") : Results.NotFound());
// ===== END =====

app.Run();