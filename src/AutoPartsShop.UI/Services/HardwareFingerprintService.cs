using System.Management;
using AutoPartsShop.Application.Interfaces;

namespace AutoPartsShop.UI.Services;

/// <summary>
/// Generates a unique hardware fingerprint using CPU ID, Motherboard serial,
/// BIOS serial, Disk volume serial, and Windows Machine GUID.
/// </summary>
public class HardwareFingerprintService : IHardwareFingerprintService
{
    public async Task<string> GetFingerprintAsync()
    {
        return await Task.Run(() =>
        {
            var components = new List<string>();

            // 1. CPU ProcessorId
            var cpuId = GetWmiValue("Win32_Processor", "ProcessorId");
            if (!string.IsNullOrEmpty(cpuId))
                components.Add($"CPU:{cpuId}");

            // 2. Motherboard SerialNumber
            var mbSerial = GetWmiValue("Win32_BaseBoard", "SerialNumber");
            if (!string.IsNullOrEmpty(mbSerial))
                components.Add($"MB:{mbSerial}");

            // 3. BIOS SerialNumber
            var biosSerial = GetWmiValue("Win32_BIOS", "SerialNumber");
            if (!string.IsNullOrEmpty(biosSerial))
                components.Add($"BIOS:{biosSerial}");

            // 4. Windows Machine GUID from registry
            var machineGuid = GetMachineGuid();
            if (!string.IsNullOrEmpty(machineGuid))
                components.Add($"GUID:{machineGuid}");

            // 5. System Drive Volume Serial
            var diskSerial = GetVolumeSerial();
            if (!string.IsNullOrEmpty(diskSerial))
                components.Add($"DISK:{diskSerial}");

            // Combine all components and hash
            var combined = string.Join("|", components);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
            return Convert.ToHexString(hashBytes);
        });
    }

    public async Task<HardwareInfo> GetHardwareInfoAsync()
    {
        return await Task.Run(() => new HardwareInfo
        {
            CpuId = GetWmiValue("Win32_Processor", "ProcessorId") ?? "N/A",
            MotherboardSerial = GetWmiValue("Win32_BaseBoard", "SerialNumber") ?? "N/A",
            BiosSerial = GetWmiValue("Win32_BIOS", "SerialNumber") ?? "N/A",
            DiskSerial = GetVolumeSerial() ?? "N/A",
            MachineGuid = GetMachineGuid() ?? "N/A",
            CombinedFingerprint = GetFingerprintAsync().GetAwaiter().GetResult()
        });
    }

    private static string? GetWmiValue(string wmiClass, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {wmiClass}");
            foreach (var obj in searcher.Get())
            {
                var value = obj[propertyName]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }
        catch
        {
            // WMI might fail on some systems
        }
        return null;
    }

    private static string? GetMachineGuid()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Cryptography", false);
            return key?.GetValue("MachineGuid")?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetVolumeSerial()
    {
        try
        {
            var drive = System.IO.Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(drive)) return null;

            var disk = new ManagementObject($"Win32_LogicalDisk.DeviceID=\"{drive.TrimEnd('\\')}\"");
            disk.Get();
            return disk["VolumeSerialNumber"]?.ToString()?.Trim();
        }
        catch
        {
            return null;
        }
    }
}
