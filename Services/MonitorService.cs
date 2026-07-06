using System.IO;
using ScreenPulse.Models;

namespace ScreenPulse.Services;

public class MonitorService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly ActivityLogStore _store;
    private readonly RetentionCleanupService _cleanup;
    private System.Timers.Timer? _captureTimer;
    private System.Timers.Timer? _cleanupTimer;

    // 每个屏幕保存最近一次的指纹与对应日志条目,用于判断画面是否发生变化
    private readonly Dictionary<int, (byte[] Thumbprint, LogEntry Entry)> _lastCaptureByScreen = new();

    public event Action<LogEntry>? EntryRecorded;

    public MonitorService(AppSettings settings)
    {
        _settings = settings;
        _store = new ActivityLogStore(_settings.StorageFolder);
        _cleanup = new RetentionCleanupService(_store);
    }

    public void Start()
    {
        _cleanup.CleanupOlderThan(_settings.RetentionDays);

        _captureTimer = new System.Timers.Timer(_settings.CaptureIntervalMinutes * 60 * 1000);
        _captureTimer.Elapsed += (_, _) => Tick();
        _captureTimer.AutoReset = true;
        _captureTimer.Start();

        _cleanupTimer = new System.Timers.Timer(TimeSpan.FromHours(24).TotalMilliseconds);
        _cleanupTimer.Elapsed += (_, _) => _cleanup.CleanupOlderThan(_settings.RetentionDays);
        _cleanupTimer.AutoReset = true;
        _cleanupTimer.Start();
    }

    public void RestartWithNewInterval()
    {
        _captureTimer?.Stop();
        _captureTimer!.Interval = _settings.CaptureIntervalMinutes * 60 * 1000;
        _captureTimer.Start();
    }

    public void Tick()
    {
        if (!_settings.MonitoringEnabled) return;

        var idleTime = IdleDetector.GetIdleTime();
        if (idleTime.TotalMinutes >= _settings.IdleSkipThresholdMinutes)
        {
            return; // 空闲状态,跳过本次截图
        }

        var activeWindow = ActiveWindowService.GetActiveWindowInfo();
        if (_settings.ExcludedProcesses.Any(p =>
                string.Equals(p, activeWindow.ProcessName, StringComparison.OrdinalIgnoreCase)))
        {
            return; // 命中排除名单,不记录也不截图
        }

        var now = DateTime.Now;
        var captures = ScreenshotService.CaptureAllScreens();
        try
        {
            foreach (var capture in captures)
            {
                ProcessScreenCapture(capture, activeWindow, now);
            }
        }
        finally
        {
            foreach (var capture in captures) capture.Image.Dispose();
        }
    }

    private void ProcessScreenCapture(ScreenCapture capture, ActiveWindowInfo activeWindow, DateTime now)
    {
        bool hasPrevious = _lastCaptureByScreen.TryGetValue(capture.ScreenIndex, out var previous);
        if (hasPrevious)
        {
            double similarity = ScreenshotService.CompareThumbprints(previous.Thumbprint, capture.Thumbprint);
            bool sameWindow = previous.Entry.ProcessName == activeWindow.ProcessName
                               && previous.Entry.WindowTitle == activeWindow.WindowTitle;

            if (sameWindow && similarity >= _settings.DuplicateSimilarityThreshold)
            {
                // 画面基本没变化:只延长上一条记录的结束时间,不重复存图
                previous.Entry.EndTime = now;
                previous.Entry.DuplicateCount++;
                _store.UpdateLastEntry(previous.Entry.StartTime.Date, previous.Entry);
                _lastCaptureByScreen[capture.ScreenIndex] = (previous.Thumbprint, previous.Entry);
                return;
            }
        }

        // 画面发生变化(或首次采集):保存新截图并新建一条记录
        var folder = _store.ScreenshotFolderFor(now.Date);
        var fileName = $"{now:HH-mm-ss}_screen{capture.ScreenIndex}.jpg";
        var fullPath = Path.Combine(folder, fileName);
        ScreenshotService.SaveAsJpeg(capture.Image, fullPath);

        var entry = new LogEntry
        {
            StartTime = now,
            EndTime = now,
            ScreenIndex = capture.ScreenIndex,
            ProcessName = activeWindow.ProcessName,
            WindowTitle = activeWindow.WindowTitle,
            ScreenshotPath = fullPath,
            DuplicateCount = 1
        };
        _store.AppendEntry(entry);
        _lastCaptureByScreen[capture.ScreenIndex] = (capture.Thumbprint, entry);
        EntryRecorded?.Invoke(entry);
    }

    public ActivityLogStore Store => _store;

    public void Dispose()
    {
        _captureTimer?.Dispose();
        _cleanupTimer?.Dispose();
    }
}
