using LibreHardwareMonitor.Hardware;
using System.Management;

namespace JROptimizerPro.Core;

internal sealed class TemperatureMonitor : IDisposable
{
    private readonly Computer _computer = new()
    {
        IsCpuEnabled = true,
        IsMotherboardEnabled = true
    };
    private bool _opened;

    public float? ReadCpuTemperature()
    {
        try
        {
            if (!_opened)
            {
                _computer.Open();
                _opened = true;
            }

            var temperatures = new List<float>();
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                ReadHardware(hardware, temperatures);
            }

            if (temperatures.Count > 0)
                return temperatures.Max();

            return ReadAcpiTemperature();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Sensor de temperatura indisponível.", ex);
            return null;
        }
    }

    private static float? ReadAcpiTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            var values = new List<float>();
            foreach (ManagementObject item in searcher.Get())
            {
                if (item["CurrentTemperature"] is not null)
                {
                    var celsius = Convert.ToSingle(item["CurrentTemperature"]) / 10F - 273.15F;
                    if (celsius is >= 10F and <= 120F)
                        values.Add(celsius);
                }
                item.Dispose();
            }
            return values.Count == 0 ? null : values.Max();
        }
        catch
        {
            return null;
        }
    }

    private static void ReadHardware(IHardware hardware, ICollection<float> values)
    {
        if (hardware.HardwareType == HardwareType.Cpu)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature && sensor.Value is float value)
                    values.Add(value);
            }
        }

        foreach (var child in hardware.SubHardware)
        {
            child.Update();
            ReadHardware(child, values);
        }
    }

    public void Dispose()
    {
        if (_opened)
            _computer.Close();
    }
}
