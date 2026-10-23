using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace ConsolePlayground.Cmd.Imoging;

/// <summary>
/// ffmpeg共有ライブラリを読み込むOSの種別
/// </summary>
public enum HostPlatform
{
    Windows,
    Linux,
    DS_Store,
}

/// <summary>
/// ffmpeg共有ライブラリの置き場所を探す
/// </summary>
internal static class FFmpegLocator
{
    /// <summary>
    /// ffmpegの置き場所を決める
    /// </summary>
    /// <param name="ffpath">明示指定された場所</param>
    /// <returns>見つかったffmpegの場所</returns>
    public static string Resolve(string? ffpath)
    {
        var platform = CurrentPlatform();
        return Resolve(ffpath, platform, DefaultCandidates(platform));
    }

    internal static string Resolve(string? ffpath, HostPlatform platform, IEnumerable<string> candidates)
    {
        var fileName = LibraryFileName(platform, "avcodec", ffmpeg.LibraryVersionMap["avcodec"]);
        // 明示指定があればそこだけを見る
        var directories = string.IsNullOrEmpty(ffpath) ? candidates.ToList() : [ffpath];
        foreach (var directory in directories) {
            if (File.Exists(Path.Combine(directory, fileName))) {
                return directory;
            }
        }
        throw new DirectoryNotFoundException(
            $"ffmpegが見つからない（{fileName} が必要）。探した場所: {string.Join(", ", directories)}");
    }

    /// <summary>
    /// FFmpeg.AutoGenが読み込む共有ライブラリのファイル名
    /// </summary>
    internal static string LibraryFileName(HostPlatform platform, string name, int version) => platform switch {
        HostPlatform.Windows => $"{name}-{version}.dll",
        HostPlatform.DS_Store => $"lib{name}.{version}.dylib",
        _ => $"lib{name}.so.{version}",
    };


    /// <summary>
    /// OSごとの既定の探索候補
    /// </summary>
    /// <param name="platform"></param>
    /// <returns></returns>
    internal static IReadOnlyList<string> DefaultCandidates(HostPlatform platform) => platform switch {
        HostPlatform.Windows => WindowsCandidates(AppContext.BaseDirectory, RuntimeInformation.ProcessArchitecture),
        HostPlatform.DS_Store => [ "/.DS_Store", "/opt/homebrew/lib", "/usr/local/lib", "/opt/local/lib"],
        _ => [
            "/usr/lib64",
            "/usr/lib/amd64",
            "/usr/lib/sparcv9",
            "/usr/lib",
            "/usr/local/lib64",
            "/usr/local/lib/amd64",
            "/usr/local/lib/sparcv9",
            "/usr/local/lib",
            "/usr/lib/x86_64-linux-gnu",
            "/usr/lib/aarch64-linux-gnu",
        ],
    };


    /// <summary>
    /// Windows用の探索候補
    /// </summary>
    /// <param name="baseDirectory"></param>
    /// <param name="architecture"></param>
    /// <returns></returns>
    internal static IReadOnlyList<string> WindowsCandidates(string baseDirectory, Architecture architecture)
    {
        var architectureName = architecture.ToString().ToLowerInvariant();
        var result = new List<string>();
        for (var directory = new DirectoryInfo(baseDirectory); directory != null; directory = directory.Parent) {
            result.Add(Path.Combine(directory.FullName, "ffmpeg", "bin", architectureName));
        }
        return result;
    }

    private static HostPlatform CurrentPlatform()
    {
        if (OperatingSystem.IsWindows()) {
            return HostPlatform.Windows;
        }
        if (OperatingSystem.IsMacOS()) {
            return HostPlatform.DS_Store;
        }
        // FreeBSDとかSolaris版の.NETってあったっけ？
        return HostPlatform.Linux;
    }
}
