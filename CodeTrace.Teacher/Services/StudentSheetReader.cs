using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace CodeTrace.Teacher.Services;

public record SheetReadResult(List<AllowedStudentItem> Students, int Skipped);

// Reads a student list from .xlsx or .csv.
// Best format: a first row with the headings "Name" and "Roll Number", then one student per row.
// Without headings: first column = name, second column = roll number (one column = roll numbers only).
public static class StudentSheetReader
{
    private const int MaxRows = 5000;
    private const int MaxCols = 10;

    public static SheetReadResult Read(Stream stream, string fileName)
    {
        var grid = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            ? ReadCsv(stream)
            : ReadExcel(stream);

        return Parse(grid);
    }

    private static List<string[]> ReadExcel(Stream stream)
    {
        var grid = new List<string[]>();

        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, MaxRows);
        var lastCol = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, MaxCols);

        for (var r = 1; r <= lastRow; r++)
        {
            var cells = new string[lastCol];
            for (var c = 1; c <= lastCol; c++)
                cells[c - 1] = sheet.Cell(r, c).GetFormattedString().Trim();

            grid.Add(cells);
        }

        return grid;
    }

    private static List<string[]> ReadCsv(Stream stream)
    {
        var grid = new List<string[]>();

        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        char? delimiter = null;
        string? line;

        while ((line = reader.ReadLine()) != null && grid.Count < MaxRows)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            delimiter ??= line.Contains('\t') ? '\t'
                        : line.Contains(';') && !line.Contains(',') ? ';'
                        : ',';

            grid.Add(line.Split(delimiter.Value)
                         .Select(x => x.Trim().Trim('"').Trim())
                         .Take(MaxCols)
                         .ToArray());
        }

        return grid;
    }

    private static SheetReadResult Parse(List<string[]> grid)
    {
        var width = grid.Count == 0 ? 0 : grid.Max(r => r.Length);
        int nameCol = -1, rollCol = -1, start = 0;

        // Look for a heading row in the first 5 rows
        for (var i = 0; i < Math.Min(grid.Count, 5) && rollCol < 0; i++)
        {
            for (var c = 0; c < grid[i].Length; c++)
            {
                var text = grid[i][c].ToLowerInvariant();
                if (text.Contains("roll")) rollCol = c;
                else if (text.Contains("name")) nameCol = c;
            }

            if (rollCol >= 0) start = i + 1;
            else nameCol = -1;
        }

        if (rollCol < 0)
        {
            // No headings: name first, roll number second
            start = 0;
            rollCol = width >= 2 ? 1 : 0;
            nameCol = width >= 2 ? 0 : -1;
        }
        else if (nameCol < 0 && width >= 2)
        {
            nameCol = rollCol == 0 ? 1 : 0;
        }

        var students = new Dictionary<string, AllowedStudentItem>();
        var skipped = 0;

        for (var i = start; i < grid.Count; i++)
        {
            var row = grid[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            var roll = CleanRoll(Cell(row, rollCol));
            if (roll == null)
            {
                skipped++;
                continue;
            }

            var key = roll.TrimStart('0');
            if (key.Length == 0) key = "0";

            students[key] = new AllowedStudentItem
            {
                Name = nameCol >= 0 ? Cell(row, nameCol) : "",
                Roll = roll
            };
        }

        return new SheetReadResult(students.Values.ToList(), skipped);
    }

    private static string Cell(string[] row, int column) =>
        column >= 0 && column < row.Length ? row[column] : "";

    // Roll numbers are digits only (up to 10). A number stored as "2024045.0" is cleaned up.
    private static string? CleanRoll(string text)
    {
        var t = Regex.Replace(text ?? "", @"\s+", "");
        t = Regex.Replace(t, @"\.0+$", "");
        return Regex.IsMatch(t, @"^\d{1,10}$") ? t : null;
    }
}