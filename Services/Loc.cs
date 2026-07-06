namespace ScreenPulse.Services;

public static class Loc
{
    public static event Action? LanguageChanged;

    private static string _language = "zh";
    public static string Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            LanguageChanged?.Invoke();
        }
    }

    private static readonly Dictionary<string, (string Zh, string En)> Strings = new()
    {
        ["AppSubtitle"] = ("个人使用记录", "Personal Activity Log"),
        ["NavSettings"] = ("设置", "Settings"),
        ["NavLog"] = ("使用记录", "Activity Log"),

        ["SettingsHeading"] = ("截图与记录设置", "Capture Settings"),
        ["MonitoringEnabled"] = ("启用监控(暂停后不再截图/记录)", "Enable monitoring (pausing stops capture/logging)"),
        ["IntervalLabel"] = ("截图间隔(分钟)", "Capture interval (minutes)"),
        ["IdleThresholdLabel"] = ("空闲多少分钟后跳过截图", "Skip capture after idle for (minutes)"),
        ["RetentionLabel"] = ("截图保留天数(超过自动删除)", "Keep screenshots for (days, auto-deleted after)"),
        ["SimilarityLabel"] = ("画面相似度阈值(0.80~0.999,越高越容易判定为“有变化”而重新保存)",
            "Similarity threshold (0.80~0.999, higher = more sensitive to change)"),
        ["AutoStart"] = ("开机自动启动", "Start automatically at login"),
        ["CaptureOnIdleResume"] = ("长时间空闲后,第一次操作时拍一张摄像头照片", "Take a webcam photo on the first action after being idle"),
        ["WebcamLabel"] = ("摄像头", "Webcam"),
        ["WebcamResumeTitle"] = ("空闲后恢复操作", "Resumed after idle"),
        ["WebcamStartupTitle"] = ("程序启动", "App started"),
        ["ExcludedLabel"] = ("排除的程序(不截图,每行一个进程名,例如 chrome)", "Excluded programs (one process name per line, e.g. chrome)"),
        ["StorageLabel"] = ("存储位置", "Storage location"),
        ["OpenFolder"] = ("打开文件夹", "Open Folder"),
        ["LanguageLabel"] = ("界面语言", "Language"),

        ["ColStart"] = ("开始时间", "Start Time"),
        ["ColEnd"] = ("结束时间", "End Time"),
        ["ColScreen"] = ("屏幕", "Screen"),
        ["ColProcess"] = ("程序", "Process"),
        ["ColTime"] = ("时间", "Time"),
        ["ColTitle"] = ("窗口标题", "Window Title"),
        ["PreviewHint"] = ("点击图片查看原图", "Click image to view full size"),
        ["ColCpu"] = ("CPU", "CPU"),
        ["ColMemory"] = ("内存", "Memory"),
        ["ColGpu"] = ("GPU", "GPU"),
        ["ColNetwork"] = ("网络", "Network"),
        ["Refresh"] = ("刷新", "Refresh"),
        ["FilterToggle"] = ("筛选", "Filter"),
        ["ClearLog"] = ("清空日志", "Clear Log"),
        ["ClearLogConfirmTitle"] = ("确认清空", "Confirm Clear"),
        ["ClearLogConfirmMessage"] = ("这将永久删除所有使用记录以及对应的截图/照片文件,且无法恢复。确定要继续吗?",
            "This will permanently delete all activity records and their screenshot/photo files. This cannot be undone. Continue?"),
        ["FilterFromLabel"] = ("从", "From"),
        ["FilterToLabel"] = ("到", "To"),
        ["FilterProcessLabel"] = ("程序", "Process"),
        ["FilterProcessAll"] = ("全部程序", "All programs"),
        ["FilterTitleLabel"] = ("标题搜索", "Search title"),

        ["TrayOpen"] = ("打开主界面", "Open"),
        ["TrayPause"] = ("暂停监控", "Pause Monitoring"),
        ["TrayResume"] = ("继续监控", "Resume Monitoring"),
        ["TrayExit"] = ("退出", "Exit"),
        ["TrayTooltip"] = ("ScreenPulse - 个人使用记录中", "ScreenPulse - Activity Monitoring"),
    };

    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var pair)) return key;
        return _language == "en" ? pair.En : pair.Zh;
    }
}
