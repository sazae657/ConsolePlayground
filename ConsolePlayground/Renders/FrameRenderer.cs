using SkiaSharp;
using ConsolePlayground.Cmd.Terminators;

namespace ConsolePlayground.Cmd.Renders;

internal enum RenderMode
{
    Ascii,
    Sixel
}

internal interface IFrameRenderer
{
    void EnableRecord(bool boo);
    void ClearRecord();
    int GetRecorded { get; }

    IEnumerable<string> GetRecord();

    string LastFrame { get; }

    string Encode(SKBitmap frame);
    void Render(string frame, int index, TimeSpan timestamp, double frameRate);
}

internal sealed class AsciiFrameRenderer(bool color, bool fill, RenderParams renderParam, bool fullWidth = false,
    BoxDrawingConverter? boxDrawingConverter = null) : IFrameRenderer
{
    public string Encode(SKBitmap frame)
    {
        var (rows, columns) = CalculateGrid(renderParam.RenderWidth, renderParam.RenderHeight,
            frame.Width, frame.Height, fullWidth, renderParam.ShowInfo);
        if (boxDrawingConverter != null) {
            return boxDrawingConverter.Convert(frame, rows, columns, color);
        }
        return fill
            ? AsciiArtConverter.ConvertColor(frame, rows, columns)
            : AsciiArtConverter.Convert(frame, rows, columns, color, fullWidth);
    }

    bool record = false;
    string lastFrame = string.Empty;
    public string LastFrame => lastFrame;

    List<string> recordBuffer = new();

    public void EnableRecord(bool boo) {
        record = boo;
        recordBuffer.Clear();
    }

    public int GetRecorded => recordBuffer.Count;
    public void ClearRecord() => recordBuffer.Clear();
    public IEnumerable<string> GetRecord() => recordBuffer;

    /// <summary>
    /// 端末サイズに収まる文字数(行・列)を計算する
    /// </summary>
    /// <param name="fullWidth">1文字が2セル幅を占める全角表示の場合はtrue。</param>
    internal static (int Rows, int Columns) CalculateGrid(int windowWidth, int windowHeight,
        int frameWidth, int frameHeight, bool fullWidth, bool showInfo)
    {
        int sy = showInfo ? 2 : 1;
        // セルは縦長(およそ1:2)なので、半角は1文字で縦横比2、全角は2セル幅で縦横比1とみなす
        int cellWidth = fullWidth ? 2 : 1;
        double aspect = 2.0 / cellWidth;
        int columns = Math.Max(1, (windowWidth - 1) / cellWidth);
        int rows = Math.Max(1, windowHeight - sy);
        double scale = Math.Min((double)columns / frameWidth, rows * aspect / frameHeight);
        columns = Math.Clamp((int)Math.Round(frameWidth * scale), 1, columns);
        rows = Math.Clamp((int)Math.Round(frameHeight * scale / aspect), 1, rows);
        return (rows, columns);
    }

    public void Render(string frame, int index, TimeSpan timestamp, double frameRate)
    {
        var output = renderParam.ShowInfo ?
            new System.Text.StringBuilder(TerminalSession.Status(index, timestamp, frame.Length, frameRate, renderParam.RenderWidth)) : new System.Text.StringBuilder();
        if (record) {
            recordBuffer.Add(frame);
        }
        int row = renderParam.ShowInfo ? 2 : 1;
        foreach (var line in frame.Split(Environment.NewLine)) {
            //output.Append($"\x1b[{row};1H\x1b[2K").Append(line);
            output.Append($"\x1b[{row};1H").Append(line);
            row++;
        }
        output.Append("\x1b[0m\x1b[J");
        lastFrame = output.ToString();
        if (!renderParam.Headless) {
            Console.Write(lastFrame);
        }
    }
}

// probe はヘッドレス時は null(端末へ問い合わせない)
internal sealed class SixelFrameRenderer(int? maxWidth, int? maxHeight, TerminalSizeProbe? probe, RenderParams renderParam, bool colorReduction = false) : IFrameRenderer
{
    bool record = false;
    List<string> recordBuffer = new();

    public void EnableRecord(bool boo) {
        record = boo;
        recordBuffer.Clear();
    }
    string lastFrame = string.Empty;
    public string LastFrame => lastFrame;

    public int GetRecorded => recordBuffer.Count;
    public void ClearRecord() => recordBuffer.Clear();
    public IEnumerable<string> GetRecord() => recordBuffer;

    public string Encode(SKBitmap frame)
    {
        probe?.Poll();
        var bounds = SixelDisplaySize.Calculate(
            renderParam.RenderWidth, renderParam.RenderHeight, probe?.CellSize, maxWidth, maxHeight);
        return bounds is { } size ? SixelEncoder.Encode(frame, size.Width, size.Height, colorReduction) : string.Empty;
    }

    public void Render(string frame, int index, TimeSpan timestamp, double frameRate)
    {
        if (record) {
            recordBuffer.Add(frame);
        }
        if (renderParam.ShowInfo) {
            lastFrame = TerminalSession.Status(index, timestamp, frame.Length, frameRate, renderParam.RenderWidth) + "\x1b[2;1H" + frame;
        }
        else {
            lastFrame = "\x1b[1;1H" + frame;
        }
        if (!renderParam.Headless) {
            Console.Write(lastFrame);
        }
    }
}
