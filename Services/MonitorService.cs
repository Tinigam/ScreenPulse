using System.IO;
using ScreenPulse.Models;

namespace ScreenPulse.Services;

public class MonitorService : IDisposable
{
    private readonly AppSettings _settings;
    private readonly ActivityLogStore _store;
    private readonly RetentionCleanupService _cleanup;
    private readonly SystemMetricsService _systemMetrics = new();
    private System.Timers.Timer? _captureTimer;
    private System.Timers.Timer? _cleanupTimer;
    private System.Timers.Timer? _idleWatchTimer;
    private bool _wasIdle;
    private volatile bool _webcamCaptureInProgress;
    private readonly object _captureLock = new();

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

        // 高频轮询空闲状态,以便捕捉"长时间空闲后第一次操作"这一时刻
        _idleWatchTimer = new System.Timers.Timer(2000);
        _idleWatchTimer.Elapsed += (_, _) => CheckIdleResume();
        _idleWatchTimer.AutoReset = true;
        _idleWatchTimer.Start();

        // 程序刚启动时也拍一张,和"空闲后恢复操作"是同一类事件,只是触发时机不同
        if (_settings.MonitoringEnabled && _settings.CaptureOnIdleResume)
        {
            CaptureWebcamEvent("WebcamStartupTitle");
        }
    }

    private void CheckIdleResume()
    {
        if (!_settings.MonitoringEnabled || !_settings.CaptureOnIdleResume)
        {
            _wasIdle = false;
            return;
        }

        double thresholdMs = _settings.IdleSkipThresholdMinutes * 60 * 1000;
        var idleTime = IdleDetector.GetIdleTime();

        if (idleTime.TotalMilliseconds >= thresholdMs)
        {
            _wasIdle = true;
            return;
        }

        if (_wasIdle)
        {
            _wasIdle = false;
            CaptureIdleResumePhoto();
        }
    }

    private void CaptureIdleResumePhoto() => CaptureWebcamEvent("WebcamResumeTitle");

    // 拍摄摄像头照片,并且顺带截一次屏——两者共用同一个 now/metrics,
    // 这样查看记录时能按"结束时间相同"把同一时刻的屏幕和摄像头画面配对展示
    private void CaptureWebcamEvent(string titleKey)
    {
        if (_webcamCaptureInProgress) return;
        _webcamCaptureInProgress = true;

        Task.Run(() =>
        {
            try
            {
                var now = DateTime.Now;
                var metrics = _systemMetrics.Sample();

                var folder = _store.ScreenshotFolderFor(now.Date);
                var webcamPath = Path.Combine(folder, $"{now:HH-mm-ss}_webcam.jpg");

                if (WebcamService.TryCapturePhoto(webcamPath))
                {
                    var entry = new LogEntry
                    {
                        StartTime = now,
                        EndTime = now,
                        ScreenIndex = -1,
                        ProcessName = Loc.T("WebcamLabel"),
                        WindowTitle = Loc.T(titleKey),
                        ScreenshotPath = webcamPath,
                        DuplicateCount = 1,
                        CpuPercent = metrics.CpuPercent,
                        MemoryPercent = metrics.MemoryPercent,
                        GpuPercent = metrics.GpuPercent,
                        NetworkKBps = metrics.NetworkKBps
                    };
                    _store.AppendEntry(entry);
                    EntryRecorded?.Invoke(entry);
                }

                if (_settings.MonitoringEnabled)
                {
                    var activeWindow = ActiveWindowService.GetActiveWindowInfo();
                    var captures = ScreenshotService.CaptureAllScreens();
                    try
                    {
                        lock (_captureLock)
                        {
                            foreach (var capture in captures)
                            {
                                ProcessScreenCapture(capture, activeWindow, now, metrics);
                            }
                        }
                    }
                    finally
                    {
                        foreach (var capture in captures) capture.Image.Dispose();
                    }
                }
            }
            finally
            {
                _webcamCaptureInProgress = false;
            }
        });
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
        var metrics = _systemMetrics.Sample();
        var captures = ScreenshotService.CaptureAllScreens();
        try
        {
            lock (_captureLock)
            {
                foreach (var capture in captures)
                {
                    ProcessScreenCapture(capture, activeWindow, now, metrics);
                }
            }
        }
        finally
        {
            foreach (var capture in captures) capture.Image.Dispose();
        }
    }

    private void ProcessScreenCapture(ScreenCapture capture, ActiveWindowInfo activeWindow, DateTime now, SystemMetricsSnapshot metrics)
    {
        bool hasPrevious = _lastCaptureByScreen.TryGetValue(capture.ScreenIndex, out var previous);
        if (hasPrevious)
        {
            double similarity = ScreenshotService.CompareThumbprints(previous.Thumbprint, capture.Thumbprint);
            bool sameWindow = previous.Entry.ProcessName == activeWindow.ProcessName
                               && previous.Entry.WindowTitle == activeWindow.WindowTitle;

            if (sameWindow && similarity >= _settings.DuplicateSimilarityThreshold)
            {
                // 画面基本没变化:只延长上一条记录的结束时间和刷新资源占用快照,不重复存图
                previous.Entry.EndTime = now;
                previous.Entry.DuplicateCount++;
                previous.Entry.CpuPercent = metrics.CpuPercent;
                previous.Entry.MemoryPercent = metrics.MemoryPercent;
                previous.Entry.GpuPercent = metrics.GpuPercent;
                previous.Entry.NetworkKBps = metrics.NetworkKBps;
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
            DuplicateCount = 1,
            CpuPercent = metrics.CpuPercent,
            MemoryPercent = metrics.MemoryPercent,
            GpuPercent = metrics.GpuPercent,
            NetworkKBps = metrics.NetworkKBps
        };
        _store.AppendEntry(entry);
        _lastCaptureByScreen[capture.ScreenIndex] = (capture.Thumbprint, entry);
        EntryRecorded?.Invoke(entry);
    }

    public ActivityLogStore Store => _store;

    public void ClearAllData()
    {
        _lastCaptureByScreen.Clear();
        _store.DeleteAllDays();
    }

    public void Dispose()
    {
        _captureTimer?.Dispose();
        _cleanupTimer?.Dispose();
        _idleWatchTimer?.Dispose();
        _systemMetrics.Dispose();
    }
}
