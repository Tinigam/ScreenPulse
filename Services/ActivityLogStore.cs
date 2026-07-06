using System.IO;
using ScreenPulse.Models;

namespace ScreenPulse.Services;

// 每天一个 CSV 日志文件 + 对应的截图子文件夹,方便按日期检索和统一清理
public class ActivityLogStore
{
    private readonly string _rootFolder;

    public ActivityLogStore(string rootFolder)
    {
        _rootFolder = rootFolder;
    }

    public string ScreenshotFolderFor(DateTime day) =>
        Path.Combine(_rootFolder, "Screenshots", day.ToString("yyyy-MM-dd"));

    private string LogFileFor(DateTime day) =>
        Path.Combine(_rootFolder, "Logs", $"{day:yyyy-MM-dd}.csv");

    public void AppendEntry(LogEntry entry)
    {
        var path = LogFileFor(entry.StartTime.Date);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, entry.ToCsvLine() + Environment.NewLine);
    }

    // 更新“最后一行”的结束时间与重复次数(用于合并未变化的画面),按天独立文件所以只需重写当天最后一行
    public void UpdateLastEntry(DateTime day, LogEntry updatedEntry)
    {
        var path = LogFileFor(day);
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path).ToList();
        if (lines.Count == 0) return;
        lines[^1] = updatedEntry.ToCsvLine();
        File.WriteAllLines(path, lines);
    }

    public List<LogEntry> ReadEntries(DateTime day)
    {
        var path = LogFileFor(day);
        if (!File.Exists(path)) return new List<LogEntry>();
        return File.ReadAllLines(path)
            .Select(LogEntry.FromCsvLine)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
    }

    public List<LogEntry> ReadEntries(DateTime from, DateTime to)
    {
        var result = new List<LogEntry>();
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            result.AddRange(ReadEntries(day));
        }
        return result.OrderByDescending(e => e.StartTime).ToList();
    }

    public void DeleteDay(DateTime day)
    {
        var logPath = LogFileFor(day);
        if (File.Exists(logPath)) File.Delete(logPath);

        var screenshotFolder = ScreenshotFolderFor(day);
        if (Directory.Exists(screenshotFolder)) Directory.Delete(screenshotFolder, recursive: true);
    }

    public IEnumerable<DateTime> GetAllLoggedDays()
    {
        var logsFolder = Path.Combine(_rootFolder, "Logs");
        if (!Directory.Exists(logsFolder)) yield break;
        foreach (var file in Directory.GetFiles(logsFolder, "*.csv"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(name, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var day))
            {
                yield return day;
            }
        }
    }
}
