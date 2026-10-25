using System.Globalization;
using System.Text;
using SkiaSharp;

namespace ConsolePlayground.Cmd.Renders;

public static class SixelEncoder
{
    public const int MaximumDimension = 4096;
    public const int MinimumColors = 2;
    public const int MaximumColors = 256;

    /// <param name="colors">減色後の色数(2～256)。null なら固定パレット</param>
    public static string Encode(SKBitmap frame, int maxWidth, int maxHeight, int? colors = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Width <= 0 || frame.Height <= 0 || frame.GetPixels() == IntPtr.Zero) {
            throw new ArgumentException("画像が空", nameof(frame));
        }
        ValidateDimension(maxWidth, nameof(maxWidth));
        ValidateDimension(maxHeight, nameof(maxHeight));
        if (colors is { } requested) {
            ValidateColors(requested, nameof(colors));
        }

        // 縦横比を保ったまま枠いっぱいにリサイズ
        int width;
        int height;
        if ((long)maxWidth * frame.Height <= (long)maxHeight * frame.Width) {
            width = maxWidth;
            height = (int)Math.Max(1, (long)maxWidth * frame.Height / frame.Width);
        }
        else {
            height = maxHeight;
            width = (int)Math.Max(1, (long)maxHeight * frame.Width / frame.Height);
        }
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap)) {
            canvas.Clear(SKColors.Black);
            using var image = SKImage.FromBitmap(frame);
            canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear));
        }

        var (palette, pixels) = colors is { } reduced ? SixelQuantizer.Quantize(bitmap, reduced) : QuantizeFixed(bitmap);
        var usedColors = new bool[palette.Length];
        foreach (byte index in pixels) {
            usedColors[index] = true;
        }

        var output = new StringBuilder("\x1bP0;0;0q\"1;1;");
        AppendNumber(output, width);
        output.Append(';');
        AppendNumber(output, height);
        for (int index = 0; index < usedColors.Length; index++) {
            if (!usedColors[index]) {
                continue;
            }
            output.Append('#');
            AppendNumber(output, index);
            output.Append(";2;");
            AppendNumber(output, (palette[index].Red * 100 + 127) / 255);
            output.Append(';');
            AppendNumber(output, (palette[index].Green * 100 + 127) / 255);
            output.Append(';');
            AppendNumber(output, (palette[index].Blue * 100 + 127) / 255);
        }

        var masks = new byte[256 * width];
        var ends = new int[256];
        for (int top = 0; top < height; top += 6) {
            Array.Fill(ends, -1);
            for (int y = top; y < Math.Min(top + 6, height); y++) {
                for (int x = 0; x < width; x++) {
                    int index = pixels[y * width + x];
                    masks[index * width + x] |= (byte)(1 << (y - top));
                    ends[index] = Math.Max(ends[index], x);
                }
            }

            bool first = true;
            for (int index = 0; index < ends.Length; index++) {
                if (ends[index] < 0) {
                    continue;
                }
                if (!first) {
                    output.Append('$');
                }
                first = false;
                output.Append('#');
                AppendNumber(output, index);
                int start = index * width;
                for (int x = 0; x <= ends[index];) {
                    byte mask = masks[start + x];
                    int count = 1;
                    while (x + count <= ends[index] && masks[start + x + count] == mask) {
                        count++;
                    }
                    char value = (char)('?' + mask);
                    if (count > 3) {
                        output.Append('!');
                        AppendNumber(output, count);
                        output.Append(value);
                    }
                    else {
                        output.Append(value, count);
                    }
                    x += count;
                }
                Array.Clear(masks, start, ends[index] + 1);
            }
            if (top + 6 < height) {
                output.Append('-');
            }
        }
        return output.Append("\x1b\\").ToString();
    }

    private static readonly SKColor[] FixedPalette = CreateFixedPalette();

    private static SKColor[] CreateFixedPalette()
    {
        var palette = new SKColor[256];
        static byte FromPercent(int percent) => (byte)((percent * 255 + 50) / 100);
        for (int index = 0; index < palette.Length; index++) {
            palette[index] = new SKColor(
                FromPercent((((index >> 5) & 7) * 100 + 3) / 7),
                FromPercent((((index >> 2) & 7) * 100 + 3) / 7),
                FromPercent(((index & 3) * 100 + 1) / 3));
        }
        return palette;
    }

    private static (SKColor[] Palette, byte[] Pixels) QuantizeFixed(SKBitmap bitmap)
    {
        var source = bitmap.GetPixelSpan();
        var pixels = new byte[bitmap.Width * bitmap.Height];
        for (int i = 0; i < pixels.Length; i++) {
            int blue = (source[i * 4] * 3 + 127) / 255;
            int green = (source[i * 4 + 1] * 7 + 127) / 255;
            int red = (source[i * 4 + 2] * 7 + 127) / 255;
            pixels[i] = (byte)((red << 5) | (green << 2) | blue);
        }
        return (FixedPalette, pixels);
    }
    private static void ValidateDimension(int value, string name)
    {
        if (value < 1 || value > MaximumDimension) {
            throw new ArgumentOutOfRangeException(name, $"寸法は1～{MaximumDimension}で指定");
        }
    }

    internal static void ValidateColors(int value, string name)
    {
        if (value < MinimumColors || value > MaximumColors) {
            throw new ArgumentOutOfRangeException(name, $"色数は{MinimumColors}～{MaximumColors}で指定");
        }
    }

    private static void AppendNumber(StringBuilder output, int value)
    {
        output.Append(CultureInfo.InvariantCulture, $"{value}");
    }
}
