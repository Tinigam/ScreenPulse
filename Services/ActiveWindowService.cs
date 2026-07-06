using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenPulse.Services;

public record ActiveWindowInfo(string ProcessName, string WindowTitle);

public static class ActiveWindowService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static ActiveWindowInfo GetActiveWindowInfo()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return new ActiveWindowInfo("未知", "未知");
        }

        int length = GetWindowTextLength(hWnd);
        var sb = new StringBuilder(length + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        string title = sb.Length > 0 ? sb.ToString() : "(无标题)";

        string processName = "未知";
        try
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            using var process = Process.GetProcessById((int)pid);
            processName = process.ProcessName;
        }
        catch
        {
            // 部分受保护进程无法访问,忽略
        }

        return new ActiveWindowInfo(processName, title);
    }
}
