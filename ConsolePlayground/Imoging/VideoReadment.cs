using SkiaSharp;

namespace ConsolePlayground.Cmd.Imoging;

public class VideoReadment : IImageSource {
    private bool disposedValue;
    VideoFrameReader? reader;
    TimeSpan duration;

    SKBitmap? frame;
    //public SKBitmap? Frame => frame;
    public  VideoFrameReader? Reader => reader;

    public double FrameRate => reader?.FrameRate ?? 0.0;

    public TimeSpan Duration => duration;

    public VideoReadment(string videoPath) {
        reader = new VideoFrameReader(videoPath);
    }

    public bool IsValid() => reader != null;
    public T Frame<T>(FrameDelegaty<T> delegaty) {
        return delegaty(frame);
    }

    public bool Next(CancellationToken token) {
        if (reader == null || token.IsCancellationRequested) {
            return false;
        }
        frame?.Dispose();
        if (!reader.TryReadFrame(out frame, out duration)) {
            return false;
        }
        return true;
    }

    #region IDisposable

    protected virtual void Dispose(bool disposing) {
        if (!disposedValue) {
            frame?.Dispose();
            reader?.Dispose();
            disposedValue = true;
        }
    }

    public void Dispose() {
        // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
