using System.Globalization;
using System.Text;
using SkiaSharp;

namespace ConsolePlayground.Cmd.Renders;

public static class AsciiArtConverter {

    /// <summary>
    /// カラー版(XTerm)
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="rows"></param>
    /// <param name="columns"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <param name="colors">減色後の色数(2～256)。null なら減色しない</param>
    public static string ConvertColor(SKBitmap frame, int rows, int columns, int? colors = null) {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        if (frame.Width <= 0 || frame.Height <= 0 || frame.GetPixels() == IntPtr.Zero) {
            throw new ArgumentException("画像が空", nameof(frame));
        }

        var reduced = colors is { } count
            ? ReduceCellColors(rows, columns, (row, column) => SampleCell(frame, rows, columns, row, column), count)
            : null;
        var result = new StringBuilder(EstimateCapacity(rows, columns, 1, color: true));
        for (int row = 0; row < rows; row++) {
            if (row > 0) {
                result.Append(Environment.NewLine);
            }

            int lastColor = NoColor;
            for (int column = 0; column < columns; column++) {
                var pixel = reduced?[row * columns + column] ?? SampleCell(frame, rows, columns, row, column);
                AppendColor(result, pixel, ref lastColor);
                result.Append('▓');
            }

            result.Append("\x1b[0m");
        }

        return result.ToString();
    }

    // 直前の色が無いことフラグ(24ビット色と重ならない値)
    internal const int NoColor = -1;

    /// <summary>
    /// セルの中心の画素を取る
    /// </summary>
    private static SKColor SampleCell(SKBitmap frame, int rows, int columns, int row, int column)
        => frame.GetPixel((int)((column + 0.5) * frame.Width / columns), (int)((row + 0.5) * frame.Height / rows));

    /// <summary>
    /// colors色に減色した色を返す
    /// </summary>
    /// <param name="rows"></param>
    /// <param name="columns"></param>
    /// <param name="sample">(行, 列) からセルの代表色を返す</param>
    /// <param name="colors">減色後の色数(2～256)</param>
    /// <returns></returns>
    internal static SKColor[] ReduceCellColors(int rows, int columns, Func<int, int, SKColor> sample, int colors)
    {
        var cells = new SKColor[rows * columns];
        for (int row = 0; row < rows; row++) {
            for (int column = 0; column < columns; column++) {
                var pixel = sample(row, column);
                cells[row * columns + column] = new SKColor(
                    (byte)((pixel.Red * pixel.Alpha + 127) / 255),
                    (byte)((pixel.Green * pixel.Alpha + 127) / 255),
                    (byte)((pixel.Blue * pixel.Alpha + 127) / 255));
            }
        }
        var (palette, pixels) = SixelQuantizer.Quantize(cells, columns, colors);
        for (int i = 0; i < cells.Length; i++) {
            cells[i] = palette[pixels[i]];
        }
        return cells;
    }


    /// <summary>
    /// 黒背景に合成した前景色のエスケープシーケンスを追記
    /// </summary>
    /// <param name="result"></param>
    /// <param name="pixel"></param>
    /// <param name="lastColor"></param>
    internal static void AppendColor(StringBuilder result, SKColor pixel, ref int lastColor)
    {
        int red = (pixel.Red * pixel.Alpha + 127) / 255;
        int green = (pixel.Green * pixel.Alpha + 127) / 255;
        int blue = (pixel.Blue * pixel.Alpha + 127) / 255;
        int packed = red << 16 | green << 8 | blue;
        if (packed == lastColor) {
            return;
        }
        lastColor = packed;
        result.Append(CultureInfo.InvariantCulture, $"\x1b[38;2;{red};{green};{blue}m");
    }


    /// <summary>
    /// 出力サイズお見積
    /// </summary>
    /// <param name="rows"></param>
    /// <param name="columns"></param>
    /// <param name="glyphLength"></param>
    /// <param name="color"></param>
    /// <returns></returns>
    internal static int EstimateCapacity(int rows, int columns, int glyphLength, bool color)
    {
        // 色指定は最長で "\x1b[38;2;255;255;255m" の19文字。行末のリセットと改行も含める
        long perCell = glyphLength + (color ? 19 : 0);
        long capacity = rows * (columns * perCell + 4 + Environment.NewLine.Length);
        return (int)Math.Min(capacity, 16 * 1024 * 1024);
    }

    private const string CharactersDefault =" `.:-=;*?+#%@"; //@%#+?*;=-:.` 　░▒▓█"; //
    private static string characters = CharactersDefault;
    private static string[] glyphs = SplitGlyphs(CharactersDefault, fullWidth: false);
    private static string[] fullWidthGlyphs = SplitGlyphs(CharactersDefault, fullWidth: true);

    /// <summary>
    /// 明るさ順(暗→明)に並べた表示用の文字
    /// </summary>
    public static string Characters {
        get => characters;
        set {
            ArgumentNullException.ThrowIfNull(value);
            characters = value;
            glyphs = SplitGlyphs(value, fullWidth: false);
            fullWidthGlyphs = SplitGlyphs(value, fullWidth: true);
        }
    }

    /// <summary>
    /// 文字列を書記素単位に分割
    /// </summary>
    /// <param name="text"></param>
    /// <param name="fullWidth"></param>
    /// <returns></returns>
    internal static string[] SplitGlyphs(string text, bool fullWidth) {
        var result = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) {
            string element = enumerator.GetTextElement();
            result.Add(fullWidth ? ToFullWidth(element) : element);
        }
        return result.ToArray();
    }


    /// <summary>
    /// 全角変換
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string ToFullWidth(string text) {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length);
        foreach (char c in text) {
            if (c == ' ') {
                result.Append('　');
            }
            else if (c >= '!' && c <= '~') {
                // U+0021～U+007E は U+FF01～U+FF5E に一対一で対応する
                result.Append((char)(c + 0xFEE0));
            }
            else {
                result.Append(c);
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Ascii版
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="rows"></param>
    /// <param name="columns"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <param name="color">ANSIカラーで各文字を着色する場合はtrue。</param>
    /// <param name="fullWidth">半角文字を全角に変換して表示する場合はtrue。</param>
    /// <param name="colors">着色時の減色後の色数(2～256)。null なら減色しない。文字の選択には影響しない</param>
    public static string Convert(SKBitmap frame, int rows, int columns, bool color = false, bool fullWidth = false,
        int? colors = null) {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        if (frame.Width <= 0 || frame.Height <= 0 || frame.GetPixels() == IntPtr.Zero) {
            throw new ArgumentException("画像が空", nameof(frame));
        }

        var palette = fullWidth ? fullWidthGlyphs : glyphs;
        if (palette.Length == 0) {
            throw new InvalidOperationException("表示用の文字が空");
        }

        var reduced = color && colors is { } count
            ? ReduceCellColors(rows, columns, (row, column) => SampleCell(frame, rows, columns, row, column), count)
            : null;
        var result = new StringBuilder(EstimateCapacity(rows, columns, palette.Max(glyph => glyph.Length), color));
        for (int row = 0; row < rows; row++) {
            if (row > 0) {
                result.Append(Environment.NewLine);
            }

            int lastColor = NoColor;
            for (int column = 0; column < columns; column++) {
                var pixel = SampleCell(frame, rows, columns, row, column);
                double brightness = (0.2126 * pixel.Red + 0.7152 * pixel.Green + 0.0722 * pixel.Blue)
                    * pixel.Alpha / 255.0;
                int index = (int)Math.Round(brightness / 255.0 * (palette.Length - 1));
                if (color) {
                    // 文字は元の色の明るさで選び、色だけ減色後のものにする
                    AppendColor(result, reduced?[row * columns + column] ?? pixel, ref lastColor);
                }
                result.Append(palette[index]);
            }
            if (color) {
                result.Append("\x1b[0m");
            }
        }

        return result.ToString();
    }
}
