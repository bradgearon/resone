using System.Runtime.InteropServices;

namespace Wds.Resone.Launcher;

internal sealed record AiHardwareProfile(string Platform, string Description);

internal static class AiHardwarePlatformDetector
{
    public static AiHardwareProfile Detect()
    {
        string? forced = Environment.GetEnvironmentVariable("AI_PLATFORM");
        if (!string.IsNullOrWhiteSpace(forced))
            return new(Normalize(forced), "forced by AI_PLATFORM");

        if (OperatingSystem.IsMacOS())
            return new("apple", "Apple platform / Metal");

        if (OperatingSystem.IsWindows())
        {
            var vendors = WindowsDisplayVendors();
            if (vendors.Contains("nvidia")) return new("cuda", "NVIDIA display adapter / CUDA backend");
            if (vendors.Contains("amd")) return new("vulkan", "AMD display adapter / Vulkan backend");
            if (vendors.Contains("intel")) return new("vulkan", "Intel display adapter / Vulkan backend");
        }
        else if (OperatingSystem.IsLinux())
        {
            var vendors = LinuxDrmVendors();
            if (vendors.Contains("nvidia")) return new("cuda", "NVIDIA DRM adapter / CUDA backend");
            if (vendors.Contains("amd")) return new("vulkan", "AMD DRM adapter / Vulkan backend");
            if (vendors.Contains("intel")) return new("vulkan", "Intel DRM adapter / Vulkan backend");
            if (TryLoad("libcuda.so.1")) return new("cuda", "NVIDIA CUDA driver");
        }

        return new("cpu", "CPU fallback");
    }

    private static string Normalize(string value)
    {
        value = value.Trim().ToLowerInvariant();
        return value switch
        {
            "nvidia" or "cuda" => "cuda",
            "amd" or "intel" or "vulkan" or "rocm" or "hip" or "igpu" => "vulkan",
            "metal" => "apple",
            _ => value
        };
    }

    private static bool TryLoad(string name)
    {
        try
        {
            if (!NativeLibrary.TryLoad(name, out nint handle)) return false;
            NativeLibrary.Free(handle);
            return true;
        }
        catch { return false; }
    }

    private static HashSet<string> LinuxDrmVendors()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string path in Directory.EnumerateFiles("/sys/class/drm", "vendor", SearchOption.AllDirectories))
            {
                string value = File.ReadAllText(path).Trim().ToLowerInvariant();
                AddVendor(value, result);
            }
        }
        catch { }
        return result;
    }

    private static HashSet<string> WindowsDisplayVendors()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            for (uint i = 0; ; i++)
            {
                DISPLAY_DEVICE device = new() { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(null, i, ref device, 0)) break;
                string id = (device.DeviceID ?? "").ToUpperInvariant();
                AddVendor(id, result);
            }
        }
        catch { }
        return result;
    }

    private static void AddVendor(string text, HashSet<string> result)
    {
        string upper = text.ToUpperInvariant();
        if (upper.Contains("VEN_10DE") || upper.Contains("0X10DE") || upper.Contains("NVIDIA")) result.Add("nvidia");
        if (upper.Contains("VEN_1002") || upper.Contains("VEN_1022") || upper.Contains("0X1002") || upper.Contains("AMD") || upper.Contains("ATI")) result.Add("amd");
        if (upper.Contains("VEN_8086") || upper.Contains("0X8086") || upper.Contains("INTEL")) result.Add("intel");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string? DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);
}
