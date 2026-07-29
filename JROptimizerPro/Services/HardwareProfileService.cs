using System.Runtime.InteropServices;
using JROptimizerPro.Models;
using Microsoft.Win32;

namespace JROptimizerPro.Services;

internal static class HardwareProfileService
{
    public static HardwareProfile Detect()
    {
        var processor = ReadProcessorName();
        var memoryGb = ReadMemoryGb();
        var logical = Environment.ProcessorCount;
        var hasBattery = SystemInformation.PowerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery;
        var entryNames = new[] { "Celeron", "Pentium", "Atom", "N4020", "N4000", "N4500", "Athlon Silver" };
        var entry = logical <= 2 || entryNames.Any(name => processor.Contains(name, StringComparison.OrdinalIgnoreCase));
        var lowMemory = memoryGb <= 5;
        var recommended = hasBattery && !entry && memoryGb >= 8
            ? PerformanceProfileType.DayToDay
            : PerformanceProfileType.DayToDay;

        return new HardwareProfile(processor, memoryGb, logical, hasBattery, lowMemory, entry, recommended);
    }

    private static string ReadProcessorName()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString",
                "Processador não identificado")?.ToString()?.Trim() ?? "Processador não identificado";
        }
        catch
        {
            return "Processador não identificado";
        }
    }

    private static double ReadMemoryGb()
    {
        var status = new MemoryStatusEx();
        return GlobalMemoryStatusEx(ref status)
            ? Math.Round(status.TotalPhysical / 1024d / 1024d / 1024d, 1)
            : 0;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [StructLayout(LayoutKind.Sequential)]
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
