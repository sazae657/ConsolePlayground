using System.Diagnostics;
using System.Globalization;

namespace ConsolePlayground.Cmd.Common;

/// <summary>
/// 再生処理の所要時間の内訳(--benchmark用)
/// </summary>
internal sealed class PlaybackStats
{
    private readonly Stopwatch total = new();
    private long decodeTicks;
    private long encodeTicks;
    private long renderTicks;

    public long Frames { get; private set; }
    public long OutputChars { get; private set; }

    public void Start() => total.Start();
    public void Stop() => total.Stop();

    public void AddDecode(long startTimestamp) => decodeTicks += Stopwatch.GetTimestamp() - startTimestamp;
    public void AddEncode(long startTimestamp) => encodeTicks += Stopwatch.GetTimestamp() - startTimestamp;

    public void AddRender(long startTimestamp, int outputLength)
    {
        renderTicks += Stopwatch.GetTimestamp() - startTimestamp;
        OutputChars += outputLength;
        Frames++;
    }

    public override string ToString()
    {
        double seconds = total.Elapsed.TotalSeconds;
        long frames = Math.Max(1, Frames);
        double PerFrame(long ticks) => ticks * 1000.0 / Stopwatch.Frequency / frames;
        return string.Create(CultureInfo.InvariantCulture,
            $"frames={Frames} elapsed={seconds:F2}s fps={Frames / Math.Max(seconds, 1e-9):F1}\n" +
            $"decode={PerFrame(decodeTicks):F2}ms encode={PerFrame(encodeTicks):F2}ms render={PerFrame(renderTicks):F2}ms (per frame)\n" +
            $"output={OutputChars / 1024.0 / frames:F1}KiB/frame");
    }
}
