using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CodeTrace.Api.Services;

public enum JoinStatus { Pending, Approved, Rejected }

public class JoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StudentName { get; set; } = "";   // "Name (RollNumber)"
    public JoinStatus Status { get; set; } = JoinStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class JoinRequestStore
{
    private static readonly Regex RollPattern = new(@"\((?<roll>\d+)\)\s*$");

    private readonly ConcurrentDictionary<Guid, JoinRequest> _items = new();
    private readonly AllowedStudentStore _allowed;

    public JoinRequestStore(AllowedStudentStore allowed) => _allowed = allowed;

    // Is the roll number inside "Name (Roll)" on the teacher's list?
    private bool IsListed(string? studentName)
    {
        var match = RollPattern.Match(studentName ?? "");
        return match.Success && _allowed.Contains(match.Groups["roll"].Value);
    }

    // A waiting request becomes approved as soon as its roll number is on the list
    private void ApplyList(JoinRequest request)
    {
        if (request.Status == JoinStatus.Pending && IsListed(request.StudentName))
            request.Status = JoinStatus.Approved;
    }

    public JoinRequest Add(string studentName)
    {
        // Same student clicking Connect again while still waiting: reuse the pending request
        var existing = _items.Values.FirstOrDefault(r =>
            r.Status == JoinStatus.Pending &&
            r.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            ApplyList(existing);
            return existing;
        }

        var req = new JoinRequest { StudentName = studentName };
        ApplyList(req);   // on the list = approved straight away
        _items[req.Id] = req;
        return req;
    }

    public JoinRequest? Get(Guid id)
    {
        if (!_items.TryGetValue(id, out var r)) return null;
        ApplyList(r);
        return r;
    }

    public List<JoinRequest> Pending()
    {
        foreach (var r in _items.Values) ApplyList(r);

        return _items.Values.Where(r => r.Status == JoinStatus.Pending)
                            .OrderBy(r => r.CreatedAt).ToList();
    }

    public bool SetStatus(Guid id, JoinStatus status)
    {
        if (!_items.TryGetValue(id, out var r)) return false;
        r.Status = status;
        return true;
    }

    public bool IsApproved(string studentName) =>
        IsListed(studentName) ||
        _items.Values.Any(r =>
            r.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase) &&
            r.Status == JoinStatus.Approved);
}