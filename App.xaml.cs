using System.Windows;
using System.Windows.Controls;
using ScreenPulse.Models;
using ScreenPulse.Services;
using H.NotifyIcon;

namespace ScreenPulse;

public partial class App : Application
{
    private AppSettings _settings = null!;
    private MonitorService? _monitor;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MenuItem? _openMenuItem;
    private MenuItem? _pauseMenuItem;
    private MenuItem? _exitMenuItem;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settings = AppSettings.Load();
        Loc.Language = _settings.Language;
        Loc.LanguageChanged += RefreshTrayText;

        _monitor = new MonitorService(_settings);
        _monitor.Start();

        SetupTrayIcon();

        _mainWindow = new MainWindow(_settings, _monitor);
        bool startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
        {
            _mainWindow.Show();
        }
    }

    private static string IconPath => System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "app.ico");

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = System.IO.File.Exists(IconPath)
                ? new System.Drawing.Icon(IconPath)
                : System.Drawing.SystemIcons.Application
        };

        var menu = new ContextMenu();

        _openMenuItem = new MenuItem();
        _openMenuItem.Click += (_, _) => ShowMainWindow();

        _pauseMenuItem = new MenuItem();
        _pauseMenuItem.Click += (_, _) =>
        {
            _settings.MonitoringEnabled = !_settings.MonitoringEnabled;
            _settings.Save();
            RefreshTrayText();
        };

        _exitMenuItem = new MenuItem();
        _exitMenuItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(_openMenuItem);
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_exitMenuItem);

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        RefreshTrayText();
    }

    private void RefreshTrayText()
    {
        if (_trayIcon is null) return;
        _trayIcon.ToolTipText = Loc.T("TrayTooltip");
        if (_openMenuItem is not null) _openMenuItem.Header = Loc.T("TrayOpen");
        if (_pauseMenuItem is not null)
        {
            _pauseMenuItem.Header = _settings.MonitoringEnabled
                ? Loc.T("TrayPause")
                : Loc.T("TrayResume");
        }
        if (_exitMenuItem is not null) _exitMenuItem.Header = Loc.T("TrayExit");
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(_settings, _monitor!);
        }
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _monitor?.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }
}
