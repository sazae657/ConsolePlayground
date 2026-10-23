using System.Globalization;
using System.Text;

namespace ConsolePlayground.Cmd.Renders;

internal readonly record struct TerminalCellSize(int Width, int Height)
{
    public const int MaximumDimension = 4096;

    public static TerminalCellSize? FromArea(int width, int height, int columns, int rows)
    {
        if (width <= 0 || height <= 0 || columns <= 0 || rows <= 0) {
            return null;
        }
        int cellWidth = width / columns;
        int cellHeight = height / rows;
        return cellWidth is > 0 and <= MaximumDimension && cellHeight is > 0 and <= MaximumDimension
            ? new TerminalCellSize(cellWidth, cellHeight)
            : null;
    }
}

/// <summary>
/// XTWINOPS の応答(6ならセル、4ならテキスト領域のピクセルサイズ)
/// </summary>
/// <param name="Code"></param>
/// <param name="Width"></param>
/// <param name="Height"></param>
internal readonly record struct TerminalSizeReport(int Code, int Width, int Height)
{
    public const int CellCode = 6;
    public const int AreaCode = 4;

    public TerminalCellSize? ToCellSize(int columns, int rows) => Code == CellCode
        ? new TerminalCellSize(Width, Height)
        : TerminalCellSize.FromArea(Width, Height, columns, rows);
}

internal sealed class TerminalSizeResponse
{
    private const int MaximumAreaDimension = 65535;
    private readonly StringBuilder response = new();

    public TerminalSizeReport? Push(char value)
    {
        if (value == '\x1b' || value == '\u009b') {
            response.Clear();
            response.Append(value == '\x1b' ? "\x1b" : "\x1b[");
            return null;
        }
        if (response.Length == 0) {
            return null;
        }
        response.Append(value);
        if (response.Length > 40) {
            response.Clear();
            return null;
        }
        if (value != 't') {
            return null;
        }
        string text = response.ToString();
        response.Clear();
        int code;
        int limit;
        if (text.StartsWith("\x1b[6;", StringComparison.Ordinal)) {
            (code, limit) = (TerminalSizeReport.CellCode, TerminalCellSize.MaximumDimension);
        }
        else if (text.StartsWith("\x1b[4;", StringComparison.Ordinal)) {
            (code, limit) = (TerminalSizeReport.AreaCode, MaximumAreaDimension);
        }
        else {
            return null;
        }
        var parts = text[4..^1].Split(';');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int height)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
            && width > 0 && width <= limit && height > 0 && height <= limit) {
            return new TerminalSizeReport(code, width, height);
        }
        return null;
    }
}

internal static class SixelDisplaySize
{
    public static (int Width, int Height)? Calculate(int columns, int rows, TerminalCellSize? cell,
        int? maxWidth, int? maxHeight)
    {
        if (columns < 2 || rows < 3) {
            return null;
        }
        long availableWidth = cell is { } size ? (long)(columns - 1) * size.Width : 320;
        long availableHeight = cell is { } dimensions ? (long)(rows - 2) * dimensions.Height : 240;
        int width = (int)Math.Min(availableWidth, maxWidth ?? SixelEncoder.MaximumDimension);
        int height = (int)Math.Min(availableHeight, maxHeight ?? SixelEncoder.MaximumDimension);
        if (cell == null) {
            width = maxWidth ?? 320;
            height = maxHeight ?? 240;
        }
        return (Math.Clamp(width, 1, SixelEncoder.MaximumDimension),
            Math.Clamp(height, 1, SixelEncoder.MaximumDimension));
    }
}
