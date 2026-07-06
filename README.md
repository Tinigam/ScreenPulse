# ScreenPulse

[English](#english) | [简体中文](#简体中文)

---

## 简体中文

个人电脑使用记录工具。长时间未产生新画面时自动跳过,有变化时定时截图并记录当前使用的程序,帮助你事后回溯自己的电脑使用情况。仅供本机个人自用,所有数据只保存在本地,不上传任何服务器。

### 功能特性

- **定时截图 + 程序记录**:按设定间隔(默认 5 分钟)截图,并记录当前活动窗口的程序名和标题。
- **空闲自动跳过**:检测到长时间无键盘鼠标操作时跳过截图,避免记录发呆/挂机画面。
- **画面去重**:相邻两次截图内容基本一致时不会重复保存,只延长上一条记录的结束时间,避免长时间停留在同一界面时堆积大量重复截图。
- **多屏支持**:多显示器环境下每个屏幕单独截图、单独去重。
- **自动清理**:超过保留天数(默认 14 天)的截图和记录自动删除,控制磁盘占用。
- **排除名单**:可将银行、密码管理器等敏感软件加入排除名单,命中时不截图、不记录。
- **开机自启**:可在设置中一键开启/关闭(写入当前用户的注册表 Run 键,无需管理员权限)。
- **托盘常驻**:关闭窗口即最小化到系统托盘,可在托盘菜单暂停/继续监控或退出程序。
- **中英文界面**:设置中可随时切换界面语言。

### 环境要求

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)(仅开发构建需要,发布后的程序不需要用户单独安装运行时,见下方发布说明)

### 快速开始

```powershell
# 还原依赖并以 Debug 模式运行
dotnet run
```

首次运行会在左侧导航栏看到"设置"和"使用记录"两个页面,程序会立即开始按默认参数监控。

### 发布为单文件可执行程序

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

生成的 `ScreenPulse.exe` 位于 `bin\Release\net8.0-windows\win-x64\publish\`,可独立复制到任意 Windows 电脑运行,无需额外安装 .NET 运行时。

### 配置说明

所有设置均可在程序内的"设置"页面调整,保存在:

```
%LocalAppData%\ScreenPulse\settings.json
```

| 设置项 | 说明 | 默认值 |
| --- | --- | --- |
| 截图间隔 | 每隔多少分钟检查并截图一次 | 5 分钟 |
| 空闲跳过阈值 | 无操作超过该时长则跳过本次截图 | 2 分钟 |
| 保留天数 | 超过该天数的截图和记录自动删除 | 14 天 |
| 相似度阈值 | 判定"画面未变化"的相似度下限(0.80~0.999) | 0.985 |
| 开机自启 | 是否随 Windows 登录自动启动 | 关闭 |
| 排除的程序 | 命中的进程名不截图、不记录 | 空 |

### 数据存储结构

```
%LocalAppData%\ScreenPulse\
├── settings.json                  # 设置文件
├── Logs\
│   └── 2026-07-06.csv             # 按天存储的记录(开始/结束时间、屏幕、程序、窗口标题、截图路径)
└── Screenshots\
    └── 2026-07-06\
        └── 14-05-00_screen0.jpg   # 按天分文件夹存储的截图
```

### 隐私说明

- 本程序仅用于个人自我监控,不含任何联网上传、远程控制或隐蔽运行逻辑。
- 截图可能包含敏感信息(密码输入框画面、聊天内容等),建议将相关软件加入设置中的排除名单。
- 卸载/停止使用时,直接删除 `%LocalAppData%\ScreenPulse` 目录即可清除全部历史数据。

### 项目结构

```
ScreenPulse/
├── App.xaml(.cs)              # 应用入口、托盘图标与生命周期管理
├── MainWindow.xaml(.cs)       # 主窗口:设置页 + 使用记录页
├── Models/
│   ├── AppSettings.cs         # 设置的读写与持久化
│   └── LogEntry.cs            # 单条使用记录及 CSV 序列化
├── Services/
│   ├── IdleDetector.cs        # 系统空闲时间检测
│   ├── ActiveWindowService.cs # 当前活动窗口/进程获取
│   ├── ScreenshotService.cs   # 多屏截图与相似度比较
│   ├── ActivityLogStore.cs    # 按天存储的日志读写
│   ├── MonitorService.cs      # 核心调度:定时截图、去重、清理
│   ├── RetentionCleanupService.cs # 过期数据清理
│   ├── AutoStartService.cs    # 开机自启注册表读写
│   └── Loc.cs                 # 中英文文案
└── Resources/
    └── app.ico                # 程序图标
```

### 技术栈

- .NET 8 / WPF
- [WPF-UI](https://github.com/lepoco/wpfui) — Fluent Design(Windows 11 风格)控件与窗口
- [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) — 系统托盘图标

---

## English

A personal activity-logging tool for Windows. It automatically skips capturing when nothing has changed on screen for a while, and periodically captures a screenshot plus the currently active program whenever the screen does change — helping you look back at how you've used your computer. Built for personal, single-machine use only: all data stays local and nothing is ever uploaded.

### Features

- **Periodic capture + program logging**: Takes a screenshot at a configurable interval (default 5 minutes) and records the active window's process name and title.
- **Idle skip**: Skips capture automatically after a configurable period of no keyboard/mouse input, so idle/AFK time isn't logged.
- **Duplicate detection**: If consecutive captures look essentially the same, the screenshot isn't saved again — the previous log entry's end time is simply extended, preventing long stretches on one screen from flooding your history with near-identical images.
- **Multi-monitor support**: Each display is captured and deduplicated independently.
- **Automatic retention cleanup**: Screenshots and log entries older than the retention period (default 14 days) are deleted automatically to keep disk usage in check.
- **Exclusion list**: Add sensitive apps (banking software, password managers, etc.) to a list so they're never captured or logged.
- **Launch at login**: Toggle from Settings; writes to the current user's registry Run key, no admin rights required.
- **System tray**: Closing the window minimizes it to the tray; pause/resume monitoring or exit from the tray menu.
- **Bilingual UI**: Switch between Chinese and English at any time from Settings.

### Requirements

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (only needed to build from source; a published build doesn't require end users to install the runtime — see publishing instructions below)

### Getting Started

```powershell
# Restore dependencies and run in Debug mode
dotnet run
```

On first launch you'll see two pages in the left navigation, "Settings" and "Activity Log", and monitoring starts immediately with the default parameters.

### Publishing a Single-File Executable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The resulting `ScreenPulse.exe` is at `bin\Release\net8.0-windows\win-x64\publish\` and can be copied to any Windows machine and run standalone — no separate .NET runtime install needed.

### Configuration

All settings can be changed from the in-app Settings page and are stored at:

```
%LocalAppData%\ScreenPulse\settings.json
```

| Setting | Description | Default |
| --- | --- | --- |
| Capture interval | How often (minutes) to check and capture | 5 minutes |
| Idle skip threshold | Skip capture if idle for longer than this | 2 minutes |
| Retention days | Screenshots/entries older than this are auto-deleted | 14 days |
| Similarity threshold | Lower bound for "unchanged" screen detection (0.80~0.999) | 0.985 |
| Launch at login | Whether to start automatically with Windows | Off |
| Excluded programs | Process names that are never captured or logged | Empty |

### Data Layout

```
%LocalAppData%\ScreenPulse\
├── settings.json                  # Settings file
├── Logs\
│   └── 2026-07-06.csv             # Daily log (start/end time, screen, process, window title, screenshot path)
└── Screenshots\
    └── 2026-07-06\
        └── 14-05-00_screen0.jpg   # Screenshots grouped into per-day folders
```

### Privacy Notes

- This tool is intended for personal self-monitoring only — it has no networking, remote control, or hidden-execution logic.
- Screenshots may capture sensitive information (password fields, chat content, etc.); add relevant apps to the exclusion list in Settings.
- To uninstall or wipe all history, simply delete the `%LocalAppData%\ScreenPulse` folder.

### Project Structure

```
ScreenPulse/
├── App.xaml(.cs)              # App entry point, tray icon, lifecycle management
├── MainWindow.xaml(.cs)       # Main window: Settings page + Activity Log page
├── Models/
│   ├── AppSettings.cs         # Settings persistence
│   └── LogEntry.cs            # A single log entry and its CSV (de)serialization
├── Services/
│   ├── IdleDetector.cs        # System idle-time detection
│   ├── ActiveWindowService.cs # Active window/process lookup
│   ├── ScreenshotService.cs   # Multi-monitor capture and similarity comparison
│   ├── ActivityLogStore.cs    # Per-day log file read/write
│   ├── MonitorService.cs      # Core scheduler: capture, dedupe, cleanup
│   ├── RetentionCleanupService.cs # Expired-data cleanup
│   ├── AutoStartService.cs    # Registry Run key read/write
│   └── Loc.cs                 # Chinese/English UI strings
└── Resources/
    └── app.ico                # App icon
```

### Tech Stack

- .NET 8 / WPF
- [WPF-UI](https://github.com/lepoco/wpfui) — Fluent Design (Windows 11 style) controls and window chrome
- [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) — System tray icon
