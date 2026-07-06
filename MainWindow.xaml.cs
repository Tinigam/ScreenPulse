using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using ScreenPulse.Models;
using ScreenPulse.Services;
using Wpf.Ui.Controls;

namespace ScreenPulse;

public class PreviewItem
{
    public required string Label { get; init; }
    public required string Path { get; init; }
    public required BitmapImage Thumbnail { get; init; }
}

public class LogEntryViewModel
{
    public LogEntry Entry { get; }
    public LogEntryViewModel(LogEntry entry) => Entry = entry;

    public string StartTimeDisplay => Entry.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string EndTimeDisplay => Entry.EndTime.ToString("yyyy-MM-dd HH:mm:ss");
    // 截图实际拍摄的时刻——即使这条记录后来被"画面未变化"延长过,图片本身还是这一刻拍的
    public string TimeDisplay => Entry.StartTime.ToString("HH:mm:ss");

    // 同一时刻(结束时间相同)的多条记录只在第一行显示时间,后面留空,视觉上像合并了单元格
    public bool ShowTimeColumn { get; set; } = true;
    public string TimeColumnText => ShowTimeColumn ? TimeDisplay : string.Empty;
    public string ScreenDisplay => Entry.ScreenIndex < 0 ? "-" : Entry.ScreenIndex.ToString();
    public string ProcessName => Entry.ProcessName;
    public string WindowTitle => Entry.WindowTitle;
    public string ScreenshotPath => Entry.ScreenshotPath;

    public string CpuDisplay => $"{Entry.CpuPercent:0}%";
    public string MemoryDisplay => $"{Entry.MemoryPercent:0}%";
    public string GpuDisplay => $"{Entry.GpuPercent:0}%";
    public string NetworkDisplay => Entry.NetworkKBps >= 1024
        ? $"{Entry.NetworkKBps / 1024:0.0} MB/s"
        : $"{Entry.NetworkKBps:0} KB/s";
}

public partial class MainWindow : FluentWindow
{
    private readonly AppSettings _settings;
    private readonly MonitorService _monitor;
    private bool _isLoadingSettings;
    private bool _showingLogPanel;
    private bool _isLoadingFilters;
    private bool _filtersInitialized;
    private const string AllProcessesTag = "__all__";

    private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x60, 0x9C, 0xFF));
    private static readonly Brush UnselectedBrush = Brushes.Transparent;

    public MainWindow(AppSettings settings, MonitorService monitor)
    {
        InitializeComponent();
        _settings = settings;
        _monitor = monitor;
        Loc.Language = _settings.Language;

        LoadSettingsIntoUi();
        ApplyLocalization();
        ShowSettingsPanel();
    }

    private void LoadSettingsIntoUi()
    {
        _isLoadingSettings = true;
        MonitoringEnabledCheck.IsChecked = _settings.MonitoringEnabled;
        IntervalTextBox.Text = _settings.CaptureIntervalMinutes.ToString();
        IdleThresholdTextBox.Text = _settings.IdleSkipThresholdMinutes.ToString();
        CaptureOnIdleResumeCheck.IsChecked = _settings.CaptureOnIdleResume;
        RetentionTextBox.Text = _settings.RetentionDays.ToString();
        SimilarityTextBox.Text = _settings.DuplicateSimilarityThreshold.ToString("0.###");
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled();
        ExcludedProcessesTextBox.Text = string.Join(Environment.NewLine, _settings.ExcludedProcesses);
        StorageFolderText.Text = _settings.StorageFolder;
        LangZhRadio.IsChecked = _settings.Language == "zh";
        LangEnRadio.IsChecked = _settings.Language == "en";
        _isLoadingSettings = false;
    }

    private void ApplyLocalization()
    {
        Title = "ScreenPulse";
        AppTitleBar.Title = $"ScreenPulse - {Loc.T("AppSubtitle")}";

        NavSettingsButton.Content = Loc.T("NavSettings");
        NavLogButton.Content = Loc.T("NavLog");
        PageHeading.Text = _showingLogPanel ? Loc.T("NavLog") : Loc.T("SettingsHeading");

        MonitoringEnabledCheck.Content = Loc.T("MonitoringEnabled");
        IntervalLabelText.Text = Loc.T("IntervalLabel");
        IdleThresholdLabelText.Text = Loc.T("IdleThresholdLabel");
        CaptureOnIdleResumeCheck.Content = Loc.T("CaptureOnIdleResume");
        RetentionLabelText.Text = Loc.T("RetentionLabel");
        SimilarityLabelText.Text = Loc.T("SimilarityLabel");
        AutoStartCheck.Content = Loc.T("AutoStart");
        LanguageLabelText.Text = Loc.T("LanguageLabel");
        ExcludedLabelText.Text = Loc.T("ExcludedLabel");
        StorageLabelText.Text = Loc.T("StorageLabel");
        OpenFolderButton.Content = Loc.T("OpenFolder");

        ColTime.Header = Loc.T("ColTime");
        ColScreen.Header = Loc.T("ColScreen");
        ColProcess.Header = Loc.T("ColProcess");
        ColCpu.Header = Loc.T("ColCpu");
        ColMemory.Header = Loc.T("ColMemory");
        ColGpu.Header = Loc.T("ColGpu");
        ColNetwork.Header = Loc.T("ColNetwork");
        ColTitle.Header = Loc.T("ColTitle");
        RefreshButton.Content = Loc.T("Refresh");
        FilterToggleButton.Content = Loc.T("FilterToggle");
        ClearLogButton.Content = Loc.T("ClearLog");
        PreviewHintText.Text = Loc.T("PreviewHint");

        FilterFromLabel.Text = Loc.T("FilterFromLabel");
        FilterToLabel.Text = Loc.T("FilterToLabel");
        FilterProcessLabel.Text = Loc.T("FilterProcessLabel");
        FilterTitleLabel.Text = Loc.T("FilterTitleLabel");
        if (FilterProcessCombo.Items.Count > 0)
        {
            ((System.Windows.Controls.ComboBoxItem)FilterProcessCombo.Items[0]).Content = Loc.T("FilterProcessAll");
        }

        UpdateNavHighlight();
    }

    private void UpdateNavHighlight()
    {
        NavSettingsButton.Background = _showingLogPanel ? UnselectedBrush : SelectedBrush;
        NavLogButton.Background = _showingLogPanel ? SelectedBrush : UnselectedBrush;
    }

    private void ShowSettingsPanel()
    {
        _showingLogPanel = false;
        SettingsPanel.Visibility = Visibility.Visible;
        LogPanel.Visibility = Visibility.Collapsed;
        RefreshButton.Visibility = Visibility.Collapsed;
        FilterToggleButton.Visibility = Visibility.Collapsed;
        ClearLogButton.Visibility = Visibility.Collapsed;
        PageHeading.Text = Loc.T("SettingsHeading");
        UpdateNavHighlight();
    }

    private void ShowLogPanel()
    {
        _showingLogPanel = true;
        SettingsPanel.Visibility = Visibility.Collapsed;
        LogPanel.Visibility = Visibility.Visible;
        RefreshButton.Visibility = Visibility.Visible;
        FilterToggleButton.Visibility = Visibility.Visible;
        ClearLogButton.Visibility = Visibility.Visible;
        PageHeading.Text = Loc.T("NavLog");
        UpdateNavHighlight();
        InitializeFiltersIfNeeded();
        RefreshLogGrid();
    }

    private void InitializeFiltersIfNeeded()
    {
        if (_filtersInitialized) return;
        _filtersInitialized = true;

        _isLoadingFilters = true;
        FilterFromDate.SelectedDate = DateTime.Today.AddDays(-_settings.RetentionDays);
        FilterFromTimeTextBox.Text = "00:00";
        FilterToDate.SelectedDate = DateTime.Today;
        FilterToTimeTextBox.Text = "23:59";
        _isLoadingFilters = false;
    }

    private void FilterToggleButton_Click(object sender, RoutedEventArgs e)
    {
        FilterBar.Visibility = FilterToggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingFilters) return;
        RefreshLogGrid();
    }

    // 宽松解析 "HH:mm" 之类的时间文本,解析不了就退回默认值
    private static TimeSpan ParseTimeOrDefault(string text, TimeSpan fallback) =>
        TimeSpan.TryParse(text, out var result) ? result : fallback;

    private void NavSettingsButton_Click(object sender, RoutedEventArgs e) => ShowSettingsPanel();

    private void NavLogButton_Click(object sender, RoutedEventArgs e) => ShowLogPanel();

    private void LanguageChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;

        string newLanguage = LangEnRadio.IsChecked == true ? "en" : "zh";
        if (newLanguage == _settings.Language) return;

        _settings.Language = newLanguage;
        _settings.Save();
        Loc.Language = newLanguage;
        ApplyLocalization();
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;

        _settings.MonitoringEnabled = MonitoringEnabledCheck.IsChecked == true;

        if (int.TryParse(IntervalTextBox.Text, out int interval) && interval > 0)
        {
            bool intervalChanged = interval != _settings.CaptureIntervalMinutes;
            _settings.CaptureIntervalMinutes = interval;
            if (intervalChanged) _monitor.RestartWithNewInterval();
        }

        if (int.TryParse(IdleThresholdTextBox.Text, out int idleThreshold) && idleThreshold > 0)
        {
            _settings.IdleSkipThresholdMinutes = idleThreshold;
        }

        _settings.CaptureOnIdleResume = CaptureOnIdleResumeCheck.IsChecked == true;

        if (int.TryParse(RetentionTextBox.Text, out int retention) && retention > 0)
        {
            _settings.RetentionDays = retention;
        }

        if (double.TryParse(SimilarityTextBox.Text, out double similarity) && similarity is > 0 and <= 1)
        {
            _settings.DuplicateSimilarityThreshold = similarity;
        }

        _settings.ExcludedProcesses.Clear();
        foreach (var line in ExcludedProcessesTextBox.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _settings.ExcludedProcesses.Add(line);
        }

        _settings.Save();
    }

    private void AutoStartChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;

        bool enabled = AutoStartCheck.IsChecked == true;
        if (enabled) AutoStartService.Enable();
        else AutoStartService.Disable();
        _settings.AutoStartEnabled = enabled;
        _settings.Save();
    }

    private void OpenStorageFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_settings.StorageFolder);
        Process.Start(new ProcessStartInfo
        {
            FileName = _settings.StorageFolder,
            UseShellExecute = true
        });
    }

    private void RefreshLog_Click(object sender, RoutedEventArgs e) => RefreshLogGrid();

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            Loc.T("ClearLogConfirmMessage"),
            Loc.T("ClearLogConfirmTitle"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        _monitor.ClearAllData();
        PreviewList.ItemsSource = null;
        PreviewHintText.Visibility = Visibility.Collapsed;
        RefreshLogGrid();
    }

    private void RefreshLogGrid()
    {
        var fromDate = FilterFromDate.SelectedDate ?? DateTime.Today.AddDays(-_settings.RetentionDays);
        var toDate = FilterToDate.SelectedDate ?? DateTime.Today;
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

        // 读取按天存储的日志文件用日期就够了,时分精度在读出来之后再做内存过滤
        var entries = _monitor.Store.ReadEntries(fromDate, toDate);

        var fromTime = ParseTimeOrDefault(FilterFromTimeTextBox.Text, TimeSpan.Zero);
        var toTime = ParseTimeOrDefault(FilterToTimeTextBox.Text, new TimeSpan(23, 59, 59));
        var preciseFrom = fromDate.Date + fromTime;
        var preciseTo = toDate.Date + toTime;
        entries = entries.Where(en => en.StartTime >= preciseFrom && en.StartTime <= preciseTo).ToList();

        RepopulateProcessFilterOptions(entries.Select(en => en.ProcessName));

        string? selectedProcess = (FilterProcessCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
        if (!string.IsNullOrEmpty(selectedProcess) && selectedProcess != AllProcessesTag)
        {
            entries = entries.Where(en => en.ProcessName == selectedProcess).ToList();
        }

        string titleFilter = FilterTitleTextBox.Text.Trim();
        if (titleFilter.Length > 0)
        {
            entries = entries.Where(en => en.WindowTitle.Contains(titleFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var viewModels = entries.Select(en => new LogEntryViewModel(en)).ToList();
        for (int i = 1; i < viewModels.Count; i++)
        {
            if (viewModels[i].Entry.EndTime == viewModels[i - 1].Entry.EndTime)
            {
                viewModels[i].ShowTimeColumn = false;
            }
        }

        LogDataGrid.ItemsSource = viewModels;
    }

    private void RepopulateProcessFilterOptions(IEnumerable<string> processNames)
    {
        string? previouslySelected = (FilterProcessCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;

        _isLoadingFilters = true;
        FilterProcessCombo.Items.Clear();
        FilterProcessCombo.Items.Add(new System.Windows.Controls.ComboBoxItem
        {
            Content = Loc.T("FilterProcessAll"),
            Tag = AllProcessesTag
        });

        foreach (var name in processNames.Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            FilterProcessCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = name, Tag = name });
        }

        var match = FilterProcessCombo.Items.Cast<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(item => (string)item.Tag == previouslySelected);
        FilterProcessCombo.SelectedItem = match ?? FilterProcessCombo.Items[0];
        _isLoadingFilters = false;
    }

    private void LogDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LogDataGrid.SelectedItem is not LogEntryViewModel selected)
        {
            PreviewList.ItemsSource = null;
            PreviewHintText.Visibility = Visibility.Collapsed;
            return;
        }

        // 同一次事件里一起拍下的屏幕截图和摄像头照片,结束时间是完全相同的(见 MonitorService),
        // 借此把它们配对展示在一起,而不是只显示当前选中的这一张
        var allItems = (LogDataGrid.ItemsSource as IEnumerable<LogEntryViewModel>) ?? Enumerable.Empty<LogEntryViewModel>();
        var group = allItems
            .Where(vm => vm.Entry.EndTime == selected.Entry.EndTime)
            .OrderBy(vm => vm.Entry.ScreenIndex)
            .ToList();
        if (group.Count == 0) group.Add(selected);

        var previewItems = new List<PreviewItem>();
        foreach (var vm in group)
        {
            if (!File.Exists(vm.ScreenshotPath)) continue;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(vm.ScreenshotPath);
            bitmap.EndInit();

            string label = vm.Entry.ScreenIndex < 0
                ? vm.ProcessName
                : $"{Loc.T("ColScreen")} {vm.Entry.ScreenIndex}";

            previewItems.Add(new PreviewItem { Label = label, Path = vm.ScreenshotPath, Thumbnail = bitmap });
        }

        PreviewList.ItemsSource = previewItems;
        PreviewHintText.Visibility = previewItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PreviewThumbnail_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement { Tag: string path } && File.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }

    private void FluentWindow_Closing(object? sender, CancelEventArgs e)
    {
        // 关闭窗口时最小化到托盘,而不是真正退出程序
        e.Cancel = true;
        Hide();
    }
}
