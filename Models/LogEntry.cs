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

    // 该条记录最后一次更新时刻的系统资源占用快照
    public double CpuPercent { get; set; }
    public double MemoryPercent { get; set; }
    public double GpuPercent { get; set; }
    public double NetworkKBps { get; set; }

    public string ToCsvLine() =>
        string.Join(",",
            StartTime.ToString("O"),
            EndTime.ToString("O"),
            ScreenIndex.ToString(),
            CsvEscape(ProcessName),
            CsvEscape(WindowTitle),
            CsvEscape(ScreenshotPath),
            DuplicateCount.ToString(),
            CpuPercent.ToString("0.0"),
            MemoryPercent.ToString("0.0"),
            GpuPercent.ToString("0.0"),
            NetworkKBps.ToString("0.0"));

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
                DuplicateCount = int.Parse(parts[6]),
                // 早期版本的日志文件没有这几列,读不到时按 0 处理,不影响旧记录的显示
                CpuPercent = parts.Count > 7 ? ParseOrZero(parts[7]) : 0,
                MemoryPercent = parts.Count > 8 ? ParseOrZero(parts[8]) : 0,
                GpuPercent = parts.Count > 9 ? ParseOrZero(parts[9]) : 0,
                NetworkKBps = parts.Count > 10 ? ParseOrZero(parts[10]) : 0
            };
        }
        catch
        {
            return null;
        }
    }

    private static double ParseOrZero(string value) =>
        double.TryParse(value, out var result) ? result : 0;

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
