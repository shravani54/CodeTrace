using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeTrace.Api.Services;

public class AllowedStudent
{
    public string Name { get; set; } = "";
    public string Roll { get; set; } = "";
}

// The teacher's list of students who are approved automatically. Saved in AppData/allowed-students.json.
public class AllowedStudentStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, AllowedStudent> _items = new();
    private readonly string _path;

    public AllowedStudentStore(IWebHostEnvironment env)
    {
        var folder = Path.Combine(env.ContentRootPath, "AppData");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "allowed-students.json");
        Load();
    }

    public static bool IsValidRoll(string? roll) =>
        Regex.IsMatch((roll ?? "").Trim(), @"^\d{1,10}$");

    // "007" and "7" count as the same roll number (Excel often drops leading zeros)
    public static string Key(string? roll)
    {
        var key = (roll ?? "").Trim().TrimStart('0');
        return key.Length == 0 ? "0" : key;
    }

    public List<AllowedStudent> GetAll()
    {
        lock (_lock)
        {
            return _items.Values
                .OrderBy(s => s.Roll.Length)
                .ThenBy(s => s.Roll, StringComparer.Ordinal)
                .Select(s => new AllowedStudent { Name = s.Name, Roll = s.Roll })
                .ToList();
        }
    }

    public bool Contains(string? roll)
    {
        lock (_lock) return _items.ContainsKey(Key(roll));
    }

    public (int Added, int Updated) UpsertMany(IEnumerable<AllowedStudent> students)
    {
        int added = 0, updated = 0;

        lock (_lock)
        {
            foreach (var s in students)
            {
                var roll = (s.Roll ?? "").Trim();
                if (!IsValidRoll(roll)) continue;

                var name = (s.Name ?? "").Trim();
                var key = Key(roll);

                if (_items.TryGetValue(key, out var existing))
                {
                    if (name.Length > 0) existing.Name = name;
                    existing.Roll = roll;
                    updated++;
                }
                else
                {
                    _items[key] = new AllowedStudent { Name = name, Roll = roll };
                    added++;
                }
            }

            Save();
        }

        return (added, updated);
    }

    public bool Remove(string? roll)
    {
        lock (_lock)
        {
            var removed = _items.Remove(Key(roll));
            if (removed) Save();
            return removed;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var list = JsonSerializer.Deserialize<List<AllowedStudent>>(File.ReadAllText(_path));
            foreach (var s in list ?? new List<AllowedStudent>())
                if (IsValidRoll(s.Roll))
                    _items[Key(s.Roll)] = s;
        }
        catch
        {
            // A damaged file just means an empty list
        }
    }

    private void Save()
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_items.Values.ToList()));
        File.Move(temp, _path, overwrite: true);
    }
}