using System.Runtime.InteropServices;
using ConsolePlayground.Cmd.Renders;

namespace ConsolePlayground.Cmd.Terminators;

/// <summary>
/// ioctl(TIOCGWINSZ)で端末のピクセルサイズを取得する
/// </summary>
internal static class TerminalWindowSize
{
    private const nuint AppleRequest = 0x40087468;
    private const nuint LinuxRequest = 0x5413;
    private static bool unavailable = OperatingSystem.IsWindows();

    public static TerminalCellSize? CellSize()
    {
        if (unavailable) {
            return null;
        }
        try {
            foreach (int descriptor in (ReadOnlySpan<int>)[1, 0]) {
                if (Query(descriptor) is { } size
                    && TerminalCellSize.FromArea(size.Width, size.Height, size.Columns, size.Rows) is { } cell) {
                    return cell;
                }
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) {
            unavailable = true;
        }
        return null;
    }

    private static WindowSize? Query(int descriptor)
    {
        int result;
        WindowSize size;
        if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
            // 糞林檎のarm64では可変長引数がスタック渡しになるので、ダミー引数でx2〜x7レジスタを埋め、構造体へのポインタをスタックに載せる
            result = IoControlOnAppleArm64(descriptor, AppleRequest, 0, 0, 0, 0, 0, 0, out size);
        }
        else {
            result = IoControl(descriptor, OperatingSystem.IsMacOS() ? AppleRequest : LinuxRequest, out size);
        }
        return result == 0 ? size : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowSize
    {
        public ushort Rows;
        public ushort Columns;
        public ushort Width;
        public ushort Height;
    }

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int IoControl(int descriptor, nuint request, out WindowSize size);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int IoControlOnAppleArm64(int descriptor, nuint request,
        nint x2, nint x3, nint x4, nint x5, nint x6, nint x7, out WindowSize size);
}
