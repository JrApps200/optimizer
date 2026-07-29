using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JROptimizerPro.Core;

internal sealed class SystemMetrics
{
    private ulong _previousIdle;
    private ulong _previousKernel;
    private ulong _previousUser;
    private bool _hasPreviousSample;
    private readonly Queue<double> _cpuSamples = new();

    public MetricsSnapshot Read()
    {
        var cpu = ReadCpuPercent();
        var memory = ReadMemoryPercent(out var usedMemoryGb, out var totalMemoryGb);
        var disk = ReadSystemDiskPercent(out var freeDiskGb, out var totalDiskGb);

        return new MetricsSnapshot(
            CpuPercent: cpu,
            MemoryPercent: memory,
            UsedMemoryGb: usedMemoryGb,
            TotalMemoryGb: totalMemoryGb,
            DiskPercent: disk,
            FreeDiskGb: freeDiskGb,
            TotalDiskGb: totalDiskGb,
            ProcessCount: ReadProcessCount(),
            Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64));
    }


    private static int ReadProcessCount()
    {
        var processes = Process.GetProcesses();
        try
        {
            return processes.Length;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    private double ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            return 0;

        var idle = ToUInt64(idleTime);
        var kernel = ToUInt64(kernelTime);
        var user = ToUInt64(userTime);

        if (!_hasPreviousSample)
        {
            _previousIdle = idle;
            _previousKernel = kernel;
            _previousUser = user;
            _hasPreviousSample = true;
            return 0;
        }

        var idleDelta = idle - _previousIdle;
        var kernelDelta = kernel - _previousKernel;
        var userDelta = user - _previousUser;
        var totalDelta = kernelDelta + userDelta;

        _previousIdle = idle;
        _previousKernel = kernel;
        _previousUser = user;

        if (totalDelta == 0)
            return 0;

        var busyDelta = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
        var current = Math.Clamp(busyDelta * 100.0 / totalDelta, 0, 100);

        _cpuSamples.Enqueue(current);
        while (_cpuSamples.Count > 3)
            _cpuSamples.Dequeue();

        return _cpuSamples.Average();
    }

    private static double ReadMemoryPercent(out double usedGb, out double totalGb)
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(ref status))
        {
            usedGb = 0;
            totalGb = 0;
            return 0;
        }

        totalGb = BytesToGb(status.TotalPhysical);
        usedGb = BytesToGb(status.TotalPhysical - status.AvailablePhysical);
        return status.MemoryLoad;
    }

    private static double ReadSystemDiskPercent(out double freeGb, out double totalGb)
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
            freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            return drive.TotalSize <= 0
                ? 0
                : Math.Clamp((drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize, 0, 100);
        }
        catch
        {
            freeGb = 0;
            totalGb = 0;
            return 0;
        }
    }

    private static double BytesToGb(ulong bytes) => bytes / 1024d / 1024d / 1024d;

    private static ulong ToUInt64(FileTime fileTime) =>
        ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public MemoryStatusEx()
        {
            this = default;
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }
}

internal sealed record MetricsSnapshot(
    double CpuPercent,
    double MemoryPercent,
    double UsedMemoryGb,
    double TotalMemoryGb,
    double DiskPercent,
    double FreeDiskGb,
    double TotalDiskGb,
    int ProcessCount,
    TimeSpan Uptime);
