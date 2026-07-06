using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace ScreenPulse.Services;

public record SystemMetricsSnapshot(double CpuPercent, double MemoryPercent, double GpuPercent, double NetworkKBps);

// 各项指标都是"自上次采样以来"的平均值,和截图间隔(默认 5 分钟)天然对齐,
// 能反映这段时间内电脑的整体负载,而不只是采样瞬间的数值
public class SystemMetricsService : IDisposable
{
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _memAvailableCounter;
    private double _totalMemoryMb;
    private bool _initialized;

    private long _lastNetworkBytes = -1;
    private DateTime _lastNetworkSampleTime;

    // Windows 上 PerformanceCounter 首次创建通常要几秒钟(加载性能计数器子系统),
    // 这里故意不放进构造函数,避免卡住调用方所在的线程(尤其是 UI 线程);
    // 真正的初始化推迟到第一次 Sample() 时才做,而 Sample() 总是在后台线程被调用
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // 第一次调用只是打底,数值不准确
        }
        catch
        {
            _cpuCounter = null;
        }

        try
        {
            _memAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch
        {
            _memAvailableCounter = null;
        }

        _totalMemoryMb = GetTotalPhysicalMemoryMb();
    }

    public SystemMetricsSnapshot Sample()
    {
        EnsureInitialized();
        double cpu = SafeNextValue(_cpuCounter);
        double memPercent = SampleMemoryPercent();
        double network = SampleNetworkKBps();
        double gpu = SampleGpuPercent();
        return new SystemMetricsSnapshot(cpu, memPercent, gpu, network);
    }

    private static double SafeNextValue(PerformanceCounter? counter)
    {
        try
        {
            return counter?.NextValue() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private double SampleMemoryPercent()
    {
        if (_memAvailableCounter is null || _totalMemoryMb <= 0) return 0;
        double availableMb = SafeNextValue(_memAvailableCounter);
        double usedPercent = (1 - availableMb / _totalMemoryMb) * 100;
        return Math.Clamp(usedPercent, 0, 100);
    }

    private double SampleNetworkKBps()
    {
        try
        {
            long currentBytes = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                             && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Sum(ni =>
                {
                    var stats = ni.GetIPv4Statistics();
                    return stats.BytesReceived + stats.BytesSent;
                });

            var now = DateTime.UtcNow;
            double kbps = 0;
            if (_lastNetworkBytes >= 0)
            {
                double elapsedSeconds = (now - _lastNetworkSampleTime).TotalSeconds;
                if (elapsedSeconds > 0)
                {
                    kbps = (currentBytes - _lastNetworkBytes) / 1024.0 / elapsedSeconds;
                }
            }
            _lastNetworkBytes = currentBytes;
            _lastNetworkSampleTime = now;
            return Math.Max(0, kbps);
        }
        catch
        {
            return 0;
        }
    }

    // GPU 没有像 CPU 那样简单的系统总占用率计数器,
    // 借用任务管理器"性能-GPU"页同款的 "GPU Engine" 性能计数器分类,把各进程的 3D 引擎占用率加总
    private static double SampleGpuPercent()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine")) return 0;

            var category = new PerformanceCounterCategory("GPU Engine");
            var instanceNames = category.GetInstanceNames()
                .Where(n => n.Contains("engtype_3D"))
                .ToArray();
            if (instanceNames.Length == 0) return 0;

            var counters = new List<PerformanceCounter>();
            foreach (var name in instanceNames)
            {
                try
                {
                    foreach (var counter in category.GetCounters(name))
                    {
                        if (counter.CounterName == "Utilization Percentage")
                        {
                            counters.Add(counter);
                        }
                    }
                }
                catch
                {
                    // 某个实例在枚举瞬间消失(进程退出)属正常情况,跳过即可
                }
            }

            if (counters.Count == 0) return 0;

            foreach (var c in counters) SafeNextValue(c);
            Thread.Sleep(200); // 这类计数器需要两次采样间隔才能算出有效速率
            double total = counters.Sum(SafeNextValue);

            foreach (var c in counters) c.Dispose();
            return Math.Clamp(total, 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private static double GetTotalPhysicalMemoryMb()
    {
        try
        {
            var status = new MEMORYSTATUSEX();
            status.dwLength = (uint)Marshal.SizeOf(status);
            if (GlobalMemoryStatusEx(ref status))
            {
                return status.ullTotalPhys / 1024.0 / 1024.0;
            }
        }
        catch
        {
            // 忽略,返回 0 表示无法计算内存百分比
        }
        return 0;
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _memAvailableCounter?.Dispose();
    }
}
