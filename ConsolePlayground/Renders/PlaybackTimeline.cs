namespace ConsolePlayground.Cmd.Renders;

internal sealed class PlaybackTimeline(double frameRate)
{
    public TimeSpan FrameInterval { get; } = TimeSpan.FromSeconds(
        double.IsFinite(frameRate) && frameRate > 0 ? 1 / frameRate : 1 / 30.0);
    private TimeSpan? firstTimestamp;
    private TimeSpan target;

    public TimeSpan Advance(TimeSpan timestamp)
    {
        if (firstTimestamp is not { } first) {
            firstTimestamp = timestamp;
            return TimeSpan.Zero;
        }
        var position = timestamp - first;
        target = position > target ? position : target + FrameInterval;
        return target;
    }
}
