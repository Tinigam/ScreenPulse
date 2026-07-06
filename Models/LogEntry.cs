namespace ScreenPulse.Models;

public class LogEntry
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ScreenIndex { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string ScreenshotPath { get; set; } = string.Empty;
    public int DuplicateCount { get; set; } = 1;

    public string ToCsvLine() =>
        string.Join(",",
            StartTime.ToString("O"),
            EndTime.ToString("O"),
            ScreenIndex.ToString(),
            CsvEscape(ProcessName),
            CsvEscape(WindowTitle),
            CsvEscape(ScreenshotPath),
            DuplicateCount.ToString());

    public static LogEntry? FromCsvLine(string line)
    {
        var parts = SplitCsvLine(line);
        if (parts.Count < 7) return null;
        try
        {
            return new LogEntry
            {
                StartTime = DateTime.Parse(parts[0]),
                EndTime = DateTime.Parse(parts[1]),
                ScreenIndex = int.Parse(parts[2]),
                ProcessName = parts[3],
                WindowTitle = parts[4],
                ScreenshotPath = parts[5],
                DuplicateCount = int.Parse(parts[6])
            };
        }
        catch
        {
            return null;
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    current.Append(c);
                }
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
