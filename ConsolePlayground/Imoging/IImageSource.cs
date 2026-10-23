using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsolePlayground.Cmd.Imoging;

public delegate T FrameDelegaty<T>(SKBitmap? frame);

public interface IImageSource : IDisposable {
    //SKBitmap? Frame { get; }
    double FrameRate { get; }
    TimeSpan Duration { get; }
    bool Next(CancellationToken token);

    bool IsValid();

    T Frame<T>(FrameDelegaty<T> delegaty);
}
