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

public class LogEntryViewModel
{
    public LogEntry Entry { get; }
    public LogEntryViewModel(LogEntry entry) => Entry = entry;

    public string StartTimeDisplay => Entry.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string EndTimeDisplay => Entry.EndTime.ToString("yyyy-MM-dd HH:mm:ss");
    public int ScreenIndex => Entry.ScreenIndex;
    public string ProcessName => Entry.ProcessName;
    public string WindowTitle => Entry.WindowTitle;
    public string ScreenshotPath => Entry.ScreenshotPath;
}

public partial class MainWindow : FluentWindow
{
    private readonly AppSettings _settings;
    private readonly MonitorService _monitor;
    private bool _isLoadingSettings;
    private bool _showingLogPanel;

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
        RetentionLabelText.Text = Loc.T("RetentionLabel");
        SimilarityLabelText.Text = Loc.T("SimilarityLabel");
        AutoStartCheck.Content = Loc.T("AutoStart");
        LanguageLabelText.Text = Loc.T("LanguageLabel");
        ExcludedLabelText.Text = Loc.T("ExcludedLabel");
        StorageLabelText.Text = Loc.T("StorageLabel");
        OpenFolderButton.Content = Loc.T("OpenFolder");

        ColStart.Header = Loc.T("ColStart");
        ColEnd.Header = Loc.T("ColEnd");
        ColScreen.Header = Loc.T("ColScreen");
        ColProcess.Header = Loc.T("ColProcess");
        ColTitle.Header = Loc.T("ColTitle");
        RefreshButton.Content = Loc.T("Refresh");

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
        PageHeading.Text = Loc.T("SettingsHeading");
        UpdateNavHighlight();
    }

    private void ShowLogPanel()
    {
        _showingLogPanel = true;
        SettingsPanel.Visibility = Visibility.Collapsed;
        LogPanel.Visibility = Visibility.Visible;
        PageHeading.Text = Loc.T("NavLog");
        UpdateNavHighlight();
        RefreshLogGrid();
    }

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

    private void RefreshLogGrid()
    {
        var entries = _monitor.Store.ReadEntries(DateTime.Today.AddDays(-_settings.RetentionDays), DateTime.Today);
        LogDataGrid.ItemsSource = entries.Select(en => new LogEntryViewModel(en)).ToList();
    }

    private void LogDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LogDataGrid.SelectedItem is LogEntryViewModel vm && File.Exists(vm.ScreenshotPath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(vm.ScreenshotPath);
            bitmap.EndInit();
            PreviewImage.Source = bitmap;
        }
        else
        {
            PreviewImage.Source = null;
        }
    }

    private void FluentWindow_Closing(object? sender, CancelEventArgs e)
    {
        // 关闭窗口时最小化到托盘,而不是真正退出程序
        e.Cancel = true;
        Hide();
    }
}
