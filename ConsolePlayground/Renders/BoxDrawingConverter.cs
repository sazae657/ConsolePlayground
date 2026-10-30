using System.Text;
using SkiaSharp;

namespace ConsolePlayground.Cmd.Renders;

/// <summary>
/// 罫線素片で輪郭を描く版
/// </summary>
public sealed class BoxDrawingConverter {
    internal const int CellWidth = 4;
    internal const int CellHeight = 8;
    private const byte None = 0;
    private const byte Light = 1;
    private const byte Heavy = 2;

    // [上×27 + 下×9 + 左×3 + 右] → 罫線素片(各腕は 0:なし 1:細 2:太)
    private const string Pieces =
        " ╶╺╴─╼╸╾━╷┌┍┐┬┮┑┭┯╻┎┏┒┰┲┓┱┳╵└┕┘┴┶┙┵┷│├┝┤┼┾┥┽┿╽┟┢┧╁╆┪╅╈╹┖┗┚┸┺┛┹┻╿┞┡┦╀╄┩╃╇┃┠┣┨╂╊┫╉╋";

    private static readonly double DiagonalAngle = Math.Atan2(CellHeight, CellWidth);
    private static readonly float HorizontalRatio = (float)Math.Tan(Math.PI / 2 - DiagonalAngle / 2);
    private static readonly float VerticalRatio = (float)Math.Tan((Math.PI / 2 - DiagonalAngle) / 2);

    /// <summary>
    /// 線を引き始める強度
    /// </summary>
    public double OnThreshold { get; set; } = 0.15;

    /// <summary>
    /// 前フレームで線があった所を消す強度
    /// </summary>
    public double OffThreshold { get; set; } = 0.10;

    /// <summary>
    /// 太線にする強度
    /// </summary>
    public double HeavyThreshold { get; set; } = 0.5;

    /// <summary>
    /// 前フレームの強度を混ぜる割合
    /// </summary>
    public double Smoothing { get; set; } = 0.5;

    /// <summary>
    /// 細線のL字の角を角丸にする
    /// </summary>
    public bool RoundCorners { get; set; }

    /// <summary>
    /// 斜めのエッジを╱╲╳で描く
    /// </summary>
    public bool Diagonals { get; set; } = true;

    private string? fillCharacters;
    private string[] fillGlyphs = [];

    /// <summary>
    /// 線の無いセルを明るさで塗る文字
    /// </summary>
    public string? FillCharacters {
        get => fillCharacters;
        set {
            fillCharacters = value;
            fillGlyphs = string.IsNullOrEmpty(value) ? [] : AsciiArtConverter.SplitGlyphs(value, fullWidth: false);
        }
    }

    // 前フレームの状態(グリッドが変わったら破棄)
    private int lastRows;
    private int lastColumns;
    private float[] horizontalStrength = [];
    private float[] verticalStrength = [];
    private float[] slashStrength = [];
    private float[] backslashStrength = [];
    private byte[] horizontalWeight = [];
    private byte[] verticalWeight = [];
    private byte[] slashWeight = [];
    private byte[] backslashWeight = [];

    /// <summary>
    /// 前フレームの状態を破棄
    /// </summary>
    public void Reset() {
        lastRows = 0;
        lastColumns = 0;
    }

    /// <summary>
    /// 変換
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="rows"></param>
    /// <param name="columns"></param>
    /// <param name="color">ANSIカラーで各文字を着色する場合はtrue。</param>
    /// <param name="colors">着色時の減色後の色数(2～256)。null なら減色しない</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public string Convert(SKBitmap frame, int rows, int columns, bool color = false, int? colors = null) {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columns);
        if (frame.Width <= 0 || frame.Height <= 0 || frame.GetPixels() == IntPtr.Zero) {
            throw new ArgumentException("画像が空", nameof(frame));
        }

        int width = columns * CellWidth;
        int height = rows * CellHeight;
        using var work = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        if (!frame.ScalePixels(work, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))) {
            throw new InvalidOperationException("作業用画像への縮小に失敗");
        }

        var luminance = ToLuminance(work);
        var edges = DetectEdges(luminance, width, height, Diagonals);
        bool first = rows != lastRows || columns != lastColumns;
        if (first) {
            lastRows = rows;
            lastColumns = columns;
            horizontalStrength = new float[rows * (columns + 1)];
            verticalStrength = new float[(rows + 1) * columns];
            slashStrength = new float[rows * columns];
            backslashStrength = new float[rows * columns];
            horizontalWeight = new byte[horizontalStrength.Length];
            verticalWeight = new byte[verticalStrength.Length];
            slashWeight = new byte[slashStrength.Length];
            backslashWeight = new byte[backslashStrength.Length];
        }

        // 横の辺 boundary: (row, boundary - 1) と (row, boundary) の中心を結ぶ(0とcolumnsは画像の端までの半分)
        var horizontalProfile = ProfileRows(edges.Horizontal, width, rows);
        for (int row = 0; row < rows; row++) {
            for (int boundary = 0; boundary <= columns; boundary++) {
                float strength = MeasureHalves(horizontalProfile, row * width, boundary * CellWidth, CellWidth / 2, width);
                Update(horizontalStrength, horizontalWeight, row * (columns + 1) + boundary, strength, first);
            }
        }
        // 縦の辺 boundary: (boundary - 1, column) と (boundary, column) の中心を結ぶ
        var verticalProfile = ProfileColumns(edges.Vertical, width, height, columns);
        for (int boundary = 0; boundary <= rows; boundary++) {
            for (int column = 0; column < columns; column++) {
                float strength = MeasureHalves(verticalProfile, column * height, boundary * CellHeight, CellHeight / 2, height);
                Update(verticalStrength, verticalWeight, boundary * columns + column, strength, first);
            }
        }
        // 斜線はセル単位
        if (Diagonals) {
            for (int row = 0; row < rows; row++) {
                for (int column = 0; column < columns; column++) {
                    int index = row * columns + column;
                    Update(slashStrength, slashWeight, index, MeasureDiagonal(edges.Slash, width, row, column), first);
                    Update(backslashStrength, backslashWeight, index,
                        MeasureDiagonal(edges.Backslash, width, row, column), first);
                }
            }
        }

        var reduced = color && colors is { } count
            ? AsciiArtConverter.ReduceCellColors(rows, columns, (row, column) => SampleCell(work, row, column), count)
            : null;
        var result = new StringBuilder(AsciiArtConverter.EstimateCapacity(rows, columns, 1, color));
        for (int row = 0; row < rows; row++) {
            if (row > 0) {
                result.Append(Environment.NewLine);
            }

            int lastColor = AsciiArtConverter.NoColor;
            for (int column = 0; column < columns; column++) {
                int up = verticalWeight[row * columns + column];
                int down = verticalWeight[(row + 1) * columns + column];
                int left = horizontalWeight[row * (columns + 1) + column];
                int right = horizontalWeight[row * (columns + 1) + column + 1];
                if (color) {
                    var pixel = reduced?[row * columns + column] ?? SampleCell(work, row, column);
                    AsciiArtConverter.AppendColor(result, pixel, ref lastColor);
                }

                int index = row * columns + column;
                int arms = (up > 0 ? 1 : 0) + (down > 0 ? 1 : 0) + (left > 0 ? 1 : 0) + (right > 0 ? 1 : 0);
                bool diagonal = Diagonals && (slashWeight[index] != None || backslashWeight[index] != None);
                // 腕が1本だけの切れ端は斜めのエッジの段差や画像の角で出やすいので斜線を優先
                if (arms > 1 || (arms == 1 && !diagonal)) {
                    result.Append(SelectPiece(up, down, left, right));
                }
                else if (diagonal) {
                    result.Append(slashWeight[index] == None ? '╲' : backslashWeight[index] == None ? '╱' : '╳');
                }
                else if (fillGlyphs.Length > 0) {
                    float brightness = CellBrightness(luminance, width, row, column);
                    result.Append(fillGlyphs[(int)Math.Round(brightness * (fillGlyphs.Length - 1))]);
                }
                else {
                    result.Append(' ');
                }
            }
            if (color) {
                result.Append("\x1b[0m");
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// 作業用画像からセルの中心の画素を取る
    /// </summary>
    private static SKColor SampleCell(SKBitmap work, int row, int column)
        => work.GetPixel(column * CellWidth + CellWidth / 2, row * CellHeight + CellHeight / 2);


    /// <summary>
    /// 上下左右の腕の太さから罫線素片を決定
    /// </summary>
    /// <param name="up"></param>
    /// <param name="down"></param>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns></returns>
    private char SelectPiece(int up, int down, int left, int right) {
        char piece = Pieces[up * 27 + down * 9 + left * 3 + right];
        if (!RoundCorners) {
            return piece;
        }
        // 角丸は細線しか無い
        return piece switch {
            '┌' => '╭',
            '┐' => '╮',
            '└' => '╰',
            '┘' => '╯',
            _ => piece
        };
    }


    /// <summary>
    /// 強度を前フレームと混ぜてヒステリシス付きで線の有無と太さを決める
    /// </summary>
    /// <param name="strengths"></param>
    /// <param name="weights"></param>
    /// <param name="index"></param>
    /// <param name="strength"></param>
    /// <param name="first"></param>
    private void Update(float[] strengths, byte[] weights, int index, float strength, bool first) {
        if (!first) {
            strength = (float)(strengths[index] * Smoothing + strength * (1 - Smoothing));
        }
        strengths[index] = strength;
        double threshold = !first && weights[index] != None ? OffThreshold : OnThreshold;
        if (strength < threshold) {
            weights[index] = None;
        }
        else {
            weights[index] = strength >= HeavyThreshold ? Heavy : Light;
        }
    }


    /// <summary>
    /// 黒背景に合成した輝度(0～1)
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns></returns>
    private static float[] ToLuminance(SKBitmap bitmap) {
        var pixels = bitmap.GetPixelSpan();
        int width = bitmap.Width;
        int height = bitmap.Height;
        int stride = bitmap.RowBytes;
        var result = new float[width * height];
        for (int y = 0; y < height; y++) {
            int offset = y * stride;
            for (int x = 0; x < width; x++) {
                int p = offset + x * 4;
                // Bgra8888 なので B,G,R,A の順
                float value = (0.0722f * pixels[p] + 0.7152f * pixels[p + 1] + 0.2126f * pixels[p + 2])
                    * pixels[p + 3] / (255f * 255f);
                result[y * width + x] = value;
            }
        }
        return result;
    }

    /// <summary>
    /// 向きごとに振り分けたエッジ強度
    /// </summary>
    /// <param name="Horizontal">横向きの線</param>
    /// <param name="Vertical">縦向きの線</param>
    /// <param name="Slash">右上がりの線</param>
    /// <param name="Backslash">右下がりの線</param>
    private readonly record struct EdgeMaps(float[] Horizontal, float[] Vertical, float[] Slash, float[] Backslash);


    /// <summary>
    /// 勾配を求めて線の向きで振り分けて非極大抑制したエッジ強度を返す
    /// </summary>
    /// <param name="luminance"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="diagonals"></param>
    /// <returns></returns>
    private static EdgeMaps DetectEdges(float[] luminance, int width, int height, bool diagonals) {
        var gx = new float[luminance.Length];
        var gy = new float[luminance.Length];
        var magnitude = new float[luminance.Length];
        for (int y = 0; y < height; y++) {
            int y0 = Math.Max(y - 1, 0) * width;
            int y1 = y * width;
            int y2 = Math.Min(y + 1, height - 1) * width;
            for (int x = 0; x < width; x++) {
                int x0 = Math.Max(x - 1, 0);
                int x2 = Math.Min(x + 1, width - 1);
                float right = luminance[y0 + x2] + 2 * luminance[y1 + x2] + luminance[y2 + x2];
                float left = luminance[y0 + x0] + 2 * luminance[y1 + x0] + luminance[y2 + x0];
                float bottom = luminance[y2 + x0] + 2 * luminance[y2 + x] + luminance[y2 + x2];
                float top = luminance[y0 + x0] + 2 * luminance[y0 + x] + luminance[y0 + x2];
                // 重みの合計4で割って輝度差(0～1)に揃える
                gx[y1 + x] = (right - left) / 4;
                gy[y1 + x] = (bottom - top) / 4;
                magnitude[y1 + x] = MathF.Sqrt(gx[y1 + x] * gx[y1 + x] + gy[y1 + x] * gy[y1 + x]);
            }
        }

        // 斜線を使わない時は45°で横と縦に分ける
        float horizontalRatio = diagonals ? HorizontalRatio : 1;
        float verticalRatio = diagonals ? VerticalRatio : 1;

        // 勾配方向にだけ非極大抑制し、1本のエッジが隣のセルにはみ出して二重線になるのを防ぐ
        var maps = new EdgeMaps(new float[luminance.Length], new float[luminance.Length],
            new float[luminance.Length], new float[luminance.Length]);
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int i = y * width + x;
                float ax = Math.Abs(gx[i]);
                float ay = Math.Abs(gy[i]);
                if (ay > ax * horizontalRatio) {
                    float above = y > 0 ? Math.Abs(gy[i - width]) : 0;
                    float below = y < height - 1 ? Math.Abs(gy[i + width]) : 0;
                    if (ay > above && ay >= below) {
                        maps.Horizontal[i] = ay;
                    }
                }
                else if (ay <= ax * verticalRatio) {
                    float before = x > 0 ? Math.Abs(gx[i - 1]) : 0;
                    float after = x < width - 1 ? Math.Abs(gx[i + 1]) : 0;
                    if (ax > before && ax >= after) {
                        maps.Vertical[i] = ax;
                    }
                }
                else {
                    // 斜めは勾配の主な軸に沿って抑制する
                    float previous;
                    float next;
                    if (ax >= ay) {
                        previous = x > 0 ? magnitude[i - 1] : 0;
                        next = x < width - 1 ? magnitude[i + 1] : 0;
                    }
                    else {
                        previous = y > 0 ? magnitude[i - width] : 0;
                        next = y < height - 1 ? magnitude[i + width] : 0;
                    }
                    if (magnitude[i] > previous && magnitude[i] >= next) {
                        // 画像座標はyが下向きなので、勾配の成分が同符号なら右上がりの線
                        if (gx[i] * gy[i] > 0) {
                            maps.Slash[i] = magnitude[i];
                        }
                        else {
                            maps.Backslash[i] = magnitude[i];
                        }
                    }
                }
            }
        }
        return maps;
    }


    /// <summary>
    /// 各セル行について、xごとにセル高さの帯内の最大値を取る
    /// </summary>
    /// <param name="edge"></param>
    /// <param name="width"></param>
    /// <param name="rows"></param>
    /// <returns></returns>
    private static float[] ProfileRows(float[] edge, int width, int rows) {
        var result = new float[rows * width];
        for (int row = 0; row < rows; row++) {
            for (int y = row * CellHeight; y < (row + 1) * CellHeight; y++) {
                for (int x = 0; x < width; x++) {
                    result[row * width + x] = Math.Max(result[row * width + x], edge[y * width + x]);
                }
            }
        }
        return result; // [row * width + x]
    }


    /// <summary>
    /// 各セル列について、yごとにセル幅の帯内の最大値を取る
    /// </summary>
    /// <param name="edge"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="columns"></param>
    /// <returns></returns>
    private static float[] ProfileColumns(float[] edge, int width, int height, int columns) {
        var result = new float[columns * height];
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int index = x / CellWidth * height + y;
                result[index] = Math.Max(result[index], edge[y * width + x]);
            }
        }
        return result; //[column * height + y]
    }


    /// <summary>
    /// 辺の強度
    /// </summary>
    /// <param name="profile"></param>
    /// <param name="offset"></param>
    /// <param name="middle"></param>
    /// <param name="half"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    private static float MeasureHalves(float[] profile, int offset, int middle, int half, int length) {
        float result = float.MaxValue;
        if (middle - half >= 0) {
            float sum = 0;
            for (int i = middle - half; i < middle; i++) {
                sum += profile[offset + i];
            }
            result = Math.Min(result, sum / half);
        }
        if (middle + half <= length) {
            float sum = 0;
            for (int i = middle; i < middle + half; i++) {
                sum += profile[offset + i];
            }
            result = Math.Min(result, sum / half);
        }
        return result;
    }


    /// <summary>
    /// セルを斜めに横切るエッジの強度
    /// </summary>
    /// <param name="edge"></param>
    /// <param name="width"></param>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    private static float MeasureDiagonal(float[] edge, int width, int row, int column) {
        int left = column * CellWidth;
        int top = row * CellHeight;
        float upper = 0;
        float lower = 0;
        for (int y = top; y < top + CellHeight; y++) {
            float max = 0;
            for (int x = left; x < left + CellWidth; x++) {
                max = Math.Max(max, edge[y * width + x]);
            }
            if (y < top + CellHeight / 2) {
                upper += max;
            }
            else {
                lower += max;
            }
        }
        return Math.Min(upper, lower) / (CellHeight / 2);
    }


    /// <summary>
    /// セル内の平均輝度
    /// </summary>
    /// <param name="luminance"></param>
    /// <param name="width"></param>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    private static float CellBrightness(float[] luminance, int width, int row, int column) {
        float sum = 0;
        for (int y = row * CellHeight; y < (row + 1) * CellHeight; y++) {
            for (int x = column * CellWidth; x < (column + 1) * CellWidth; x++) {
                sum += luminance[y * width + x];
            }
        }
        return Math.Clamp(sum / (CellWidth * CellHeight), 0, 1);
    }
}
