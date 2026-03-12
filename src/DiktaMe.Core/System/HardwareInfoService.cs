
using System.Management;
using System.Runtime.InteropServices;
using Serilog;

namespace DiktaMe.Core.SystemManagement;

/// <summary>Hardware info enriching <see cref="CapabilityReport"/> with GPU name, real-time VRAM, and RAM stats.</summary>
public sealed record HardwareInfo(
    string GpuName,
    double VramTotalGB,
    double VramUsedGB,
    double RamTotalGB,
    double RamAvailableGB);

/// <summary>
/// Queries hardware info (GPU name, VRAM, RAM) via WMI and Win32 APIs.
/// Used by the Ollama Settings page to show real-time VRAM usage.
/// </summary>
public sealed class HardwareInfoService
{
    private readonly OllamaManager _ollamaManager;

    public HardwareInfoService(OllamaManager ollamaManager)
    {
        _ollamaManager = ollamaManager;
    }

    /// <summary>
    /// Gathers GPU name, total VRAM, used VRAM (from Ollama /api/ps), and RAM stats.
    /// </summary>
    public async Task<HardwareInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        // GPU info via WMI
        string gpuName = "Unknown GPU";
        double vramTotalGB = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                string? name = obj["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    gpuName = name;
                    // AdapterRAM is uint32 (capped at 4GB). For >4GB GPUs, use registry or driver queries.
                    if (obj["AdapterRAM"] is uint adapterRam && adapterRam > 0)
                    {
                        vramTotalGB = adapterRam / (1024.0 * 1024.0 * 1024.0);
                    }
                    break; // Take the first discrete GPU
                }
                obj.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "HardwareInfoService: WMI GPU query failed");
        }

        // Real-time VRAM usage from Ollama /api/ps
        double vramUsedGB = 0;
        try
        {
            var running = await _ollamaManager.GetRunningModelsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var model in running)
            {
                vramUsedGB += model.SizeVram / (1024.0 * 1024.0 * 1024.0);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "HardwareInfoService: failed to get VRAM usage from Ollama");
        }

        // RAM via GlobalMemoryStatusEx
        double ramTotalGB = 0;
        double ramAvailableGB = 0;
        try
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                ramTotalGB = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                ramAvailableGB = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "HardwareInfoService: GlobalMemoryStatusEx failed");
        }

        return new HardwareInfo(gpuName, vramTotalGB, vramUsedGB, ramTotalGB, ramAvailableGB);
    }

    // ── Win32 interop ────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
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
}
