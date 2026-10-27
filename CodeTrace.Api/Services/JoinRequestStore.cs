using System.Collections.Concurrent;

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
    private readonly ConcurrentDictionary<Guid, JoinRequest> _items = new();

    public JoinRequest Add(string studentName)
    {
        // Same student clicking Connect again while still waiting: reuse the pending request
        var existing = _items.Values.FirstOrDefault(r =>
            r.Status == JoinStatus.Pending &&
            r.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase));

        if (existing != null) return existing;

        var req = new JoinRequest { StudentName = studentName };
        _items[req.Id] = req;
        return req;
    }

    public JoinRequest? Get(Guid id) => _items.TryGetValue(id, out var r) ? r : null;

    public List<JoinRequest> Pending() =>
        _items.Values.Where(r => r.Status == JoinStatus.Pending)
                     .OrderBy(r => r.CreatedAt).ToList();

    public bool SetStatus(Guid id, JoinStatus status)
    {
        if (!_items.TryGetValue(id, out var r)) return false;
        r.Status = status;
        return true;
    }

    public bool IsApproved(string studentName) =>
        _items.Values.Any(r =>
            r.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase) &&
            r.Status == JoinStatus.Approved);
}