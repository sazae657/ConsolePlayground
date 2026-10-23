#if WINDOWS

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ConsolePlayground.Cmd.Imoging.Platform;

/// <summary>
/// モニター情報
/// </summary>
public class MonitorInfo
{
    public IntPtr HMonitor { get; set; }
    public RECT MonitorArea { get; set; }
    public RECT WorkArea { get; set; }
    public bool IsPrimary { get; set; }
    public string DeviceName { get; set; } = string.Empty;
}

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct MONITORINFOEX
{
    public int CbSize;
    public RECT Monitor;
    public RECT Work;
    public uint Flags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Device;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAY_DEVICE {
    public uint cb;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;
    public uint StateFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

[Flags]
public enum DisplayDeviceStateFlags : uint {
    DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1,
    DISPLAY_DEVICE_MULTI_DRIVER = 0x2,
    DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4,
    DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8,
    DISPLAY_DEVICE_VGA_COMPATIBLE = 0x10,
    DISPLAY_DEVICE_REMOVABLE = 0x20,
    DISPLAY_DEVICE_MODESPRUNED = 0x8000000,
    DISPLAY_DEVICE_REMOTE = 0x4000000,
    DISPLAY_DEVICE_DISCONNECT = 0x2000000
}

public class MonitorEnumerator
{

    private const uint MONITORINFOF_PRIMARY = 0x00000001;
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags
    );

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);


    public static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) => {
            var info = new MONITORINFOEX();
            info.CbSize = Marshal.SizeOf<MONITORINFOEX>();

            if (GetMonitorInfo(hMonitor, ref info)) {

                //DISPLAY_DEVICE monitor = new DISPLAY_DEVICE();
                //monitor.cb = (uint)Marshal.SizeOf(monitor);
                //EnumDisplayDevices(info.Device, 0, ref monitor, 0);
                monitors.Add(new MonitorInfo {
                    HMonitor = hMonitor,
                    MonitorArea = info.Monitor,
                    WorkArea = info.Work,
                    IsPrimary = (info.Flags & MONITORINFOF_PRIMARY) != 0,
                    DeviceName = info.Device
                });
            }

            return true; 
        }, IntPtr.Zero);

        return monitors;
    }
}

#endif
