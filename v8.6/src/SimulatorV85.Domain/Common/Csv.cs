using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SimulatorV85.Domain;

public static class Csv
{
    public static string D(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static string Escape(string? value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public static void AppendLine(string path, IEnumerable<string> fields)
        => File.AppendAllText(path, string.Join(',', fields.Select(Escape)) + Environment.NewLine);

    public static IReadOnlyList<Dictionary<string, string>> Read(string path)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
        if (lines.Length == 0) return Array.Empty<Dictionary<string, string>>();
        var headers = SplitLine(lines[0]);
        var rows = new List<Dictionary<string, string>>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = SplitLine(lines[i]);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Count; c++) row[headers[c]] = c < cells.Count ? cells[c] : string.Empty;
            rows.Add(row);
        }
        return rows;
    }

    private static List<string> SplitLine(string line)
    {
        var result = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cur.Append(ch);
            }
            else
            {
                if (ch == ',') { result.Add(cur.ToString()); cur.Clear(); }
                else if (ch == '"') quoted = true;
                else cur.Append(ch);
            }
        }
        result.Add(cur.ToString());
        return result;
    }
}
