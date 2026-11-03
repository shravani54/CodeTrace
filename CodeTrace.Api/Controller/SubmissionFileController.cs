using Microsoft.AspNetCore.Mvc;

namespace CodeTrace.Api.Controllers;

// Stores and serves the ZIP of a student's code folder, one file per submission.
// Files live in <api folder>/Uploads/submissions/<submissionId>.zip (no database change needed).
[ApiController]
[Route("api/[controller]")]
public class SubmissionFilesController : ControllerBase
{
    private const long MaxZipBytes = 60L * 1024 * 1024;   // students zip at most ~50 MB

    private readonly string _folder;

    public SubmissionFilesController(IWebHostEnvironment env)
    {
        _folder = Path.Combine(env.ContentRootPath, "Uploads", "submissions");
        Directory.CreateDirectory(_folder);
    }

    private string PathFor(int submissionId) => Path.Combine(_folder, $"{submissionId}.zip");

    // POST api/SubmissionFiles/12  (multipart form, field name "file")
    [HttpPost("{submissionId:int}")]
    [RequestSizeLimit(MaxZipBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxZipBytes)]
    public async Task<IActionResult> Upload(int submissionId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Empty file.");

        // A real ZIP starts with the letters "PK"
        await using (var peek = file.OpenReadStream())
        {
            var head = new byte[2];
            var read = await peek.ReadAsync(head, 0, 2);
            if (read < 2 || head[0] != (byte)'P' || head[1] != (byte)'K')
                return BadRequest("Not a ZIP file.");
        }

        var target = PathFor(submissionId);
        var temp = target + ".tmp";

        await using (var output = System.IO.File.Create(temp))
        {
            await file.CopyToAsync(output);
        }

        System.IO.File.Move(temp, target, overwrite: true);
        return Ok();
    }

    // GET api/SubmissionFiles/12  -> downloads the ZIP
    [HttpGet("{submissionId:int}")]
    public IActionResult Download(int submissionId)
    {
        var path = PathFor(submissionId);
        if (!System.IO.File.Exists(path)) return NotFound();

        return PhysicalFile(path, "application/zip", $"submission-{submissionId}.zip");
    }

    // GET api/SubmissionFiles/ids  -> the submission ids that have a ZIP (for the Download button)
    [HttpGet("ids")]
    public IActionResult Ids()
    {
        var ids = Directory.GetFiles(_folder, "*.zip")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => int.TryParse(n, out _))
            .Select(int.Parse)
            .ToList();

        return Ok(ids);
    }
}