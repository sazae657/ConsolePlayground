#if WINDOWS
using System.Runtime.InteropServices;
using System.Text;
namespace ConsolePlayground.Cmd.Imoging.Platform;

public class WindowInfo {
    public IntPtr Handle { get; set; }
    public string? Title { get; set; }
    public string? ClassName { get; set; }
}

public static class WindowEnumerator {
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const int DWMWA_CLOAKED = 14;

    public static WindowInfo? FindWindow(string subject) {
        var mfs = GetCapturableWindows().FirstOrDefault(m => m.Title?.Contains(subject) ?? false);
        if (mfs != null) {
            return mfs;
        }
        return GetCapturableWindows().FirstOrDefault(m => m.ClassName?.Contains(subject) ?? false);
    }

    public static List<WindowInfo> GetCapturableWindows() {
        var windows = new List<WindowInfo>();

        EnumWindows((hWnd, lParam) => {
            // 可視状態？
            if (!IsWindowVisible(hWnd)) {
                return true;
            }

            // タイトル付いてる？
            var sbTitle = new StringBuilder(256);
            GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
            string title = sbTitle.ToString().Trim();
            if (string.IsNullOrEmpty(title)) {
                return true;
            }

            // ツールチップと裏のウィンドウ(WS_EX_TOOLWINDOW)除外
            long exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) {
                return true;
            }

            // クロークされてたら除外
            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) {
                return true;
            }

            // タスクバーとプログラムマネージャー除外
            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, sbClass.Capacity);
            string className = sbClass.ToString();
            if (className == "Progman" || className == "Shell_TrayWnd") {
                return true;
            }

            windows.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ClassName = className
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }
}
#endif

