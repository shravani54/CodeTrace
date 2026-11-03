using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace CodeTrace.Api.Controllers;

public class AppAlertReport
{
    public string StudentName { get; set; } = "";   // sent as "Name (Roll)"
    public string AppName { get; set; } = "";
}

public class AppAlertRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Roll { get; set; } = "";
    public string AppName { get; set; } = "";
    public DateTime Time { get; set; }
}

public class AppAlertBatch
{
    public long LatestId { get; set; }
    public List<AppAlertRecord> Alerts { get; set; } = new();
}

[ApiController]
[Route("api/[controller]")]
public class AppAlertsController : ControllerBase
{
    // Kept in memory: the list is empty again when the API restarts
    private static readonly List<AppAlertRecord> Alerts = new();
    private static readonly object Lock = new();
    private static long _nextId = 1;

    private static readonly Regex NamePattern =
        new(@"^(?<name>.+?)\s*\((?<roll>\d+)\)\s*$");

    // POST api/AppAlerts: a student opened an app that is not allowed
    [HttpPost]
    public IActionResult Report(AppAlertReport report)
    {
        if (string.IsNullOrWhiteSpace(report.AppName))
            return BadRequest("App name is required.");

        var match = NamePattern.Match(report.StudentName ?? "");
        var name = match.Success ? match.Groups["name"].Value.Trim() : (report.StudentName ?? "").Trim();
        var roll = match.Success ? match.Groups["roll"].Value : "";

        lock (Lock)
        {
            Alerts.Add(new AppAlertRecord
            {
                Id = _nextId++,
                Name = name,
                Roll = roll,
                AppName = report.AppName.Trim(),
                Time = DateTime.UtcNow
            });

            if (Alerts.Count > 1000)
                Alerts.RemoveAt(0);
        }

        return Ok();
    }

    // GET api/AppAlerts?after=5: alerts newer than id 5, plus the latest id
    [HttpGet]
    public IActionResult Get([FromQuery] long after = 0)
    {
        lock (Lock)
        {
            return Ok(new AppAlertBatch
            {
                LatestId = _nextId - 1,
                Alerts = Alerts.Where(a => a.Id > after).ToList()
            });
        }
    }

    // DELETE api/AppAlerts: clear the list
    [HttpDelete]
    public IActionResult Clear()
    {
        lock (Lock)
        {
            Alerts.Clear();
        }
        return NoContent();
    }
}