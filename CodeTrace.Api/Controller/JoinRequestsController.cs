using System.Net;
using CodeTrace.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodeTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JoinRequestsController : ControllerBase
{
    // true  = only the server PC (where the Teacher app runs) can list, approve or reject
    // false = any PC can (not recommended)
    private static readonly bool ServerPcOnly = true;

    private readonly JoinRequestStore _store;
    public JoinRequestsController(JoinRequestStore store) => _store = store;

    public record CreateDto(string StudentName);

    // Student: send a connect request
    [HttpPost]
    public IActionResult Create(CreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.StudentName)) return BadRequest();

        var req = _store.Add(dto.StudentName.Trim());
        return Ok(new { req.Id, Status = req.Status.ToString() });
    }

    // Student: check the answer
    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id)
    {
        var req = _store.Get(id);
        return req == null ? NotFound() : Ok(new { req.Id, Status = req.Status.ToString() });
    }

    // Teacher: list waiting requests
    [HttpGet("pending")]
    public IActionResult Pending()
    {
        if (!AllowedCaller()) return StatusCode(403, "Only the teacher on the server PC can do this.");
        return Ok(_store.Pending());
    }

    [HttpPost("{id:guid}/approve")]
    public IActionResult Approve(Guid id)
    {
        if (!AllowedCaller()) return StatusCode(403, "Only the teacher on the server PC can do this.");
        return _store.SetStatus(id, JoinStatus.Approved) ? Ok() : NotFound();
    }

    [HttpPost("{id:guid}/reject")]
    public IActionResult Reject(Guid id)
    {
        if (!AllowedCaller()) return StatusCode(403, "Only the teacher on the server PC can do this.");
        return _store.SetStatus(id, JoinStatus.Rejected) ? Ok() : NotFound();
    }

    // True when the call comes from the server PC itself (localhost, or the server's own IP)
    private bool AllowedCaller()
    {
        if (!ServerPcOnly) return true;

        var remote = HttpContext.Connection.RemoteIpAddress;
        var local = HttpContext.Connection.LocalIpAddress;
        if (remote == null) return false;

        if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();
        if (local != null && local.IsIPv4MappedToIPv6) local = local.MapToIPv4();

        return IPAddress.IsLoopback(remote) || (local != null && remote.Equals(local));
    }
}