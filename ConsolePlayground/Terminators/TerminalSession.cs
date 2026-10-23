using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ConsolePlayground.Cmd.Terminators;

/// <summary>
/// セッション
/// </summary>
internal sealed class TerminalSession : IDisposable
{
    private readonly IntPtr outputHandle;
    private readonly uint originalMode;
    private bool disposed;

    public TerminalSession()
    {
        if (Console.IsOutputRedirected) {
            throw new InvalidOperationException("標準出力のリダイレクトは不可");
        }
        if (OperatingSystem.IsWindows()) {
            outputHandle = GetStdHandle(-11);
            if (!GetConsoleMode(outputHandle, out originalMode) || !SetConsoleMode(outputHandle, originalMode | 0x0004)) {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "端末のVT出力を有効にできなかった");
            }
        }
        try {
            Console.Write("\x1b[?1049h\x1b[?25l\x1b[2J\x1b[H");
        }
        catch {
            Dispose();
            throw;
        }
    }

    public static string Status(int index, TimeSpan timestamp, int size, double frameRate, int windowWidth)
    {
        var dsp = timestamp.ToString(@"hh\:mm\:ss");
        string text = $"index={index} payload={ToReadableSize(size * 2)} duration={dsp}" ;
        int length = Math.Max(0, windowWidth - 1);
        return "\x1b[H\x1b[2K" + text[..Math.Min(text.Length, length)];
        //return "\x1b[H\x1b[2K" + text[..Math.Min(text.Length, length)];
    }

    static string ToReadableSize(long bytes) {
        // 単位
        string[] suffixes = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

        if (bytes == 0)
            return "0B";

        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));

        if (place >= suffixes.Length)
            place = suffixes.Length - 1;

        double num = Math.Round(bytes / Math.Pow(1024, place), 2);

        return $"{num:F0}{suffixes[place]}";
    }

    public void Dispose()
    {
        if (disposed) {
            return;
        }
        disposed = true;
        try {
            Console.Write("\x1b\\\x1b[0m\x1b[?25h\x1b[?1049l");
        }
        finally {
            if (OperatingSystem.IsWindows()) {
                SetConsoleMode(outputHandle, originalMode);
            }
        }
    }

    // Windows専用
    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);
}
