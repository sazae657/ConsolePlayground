using ConsolePlayground.Cmd.Imoging;
using SkiaSharp;
namespace ConsolePlayground.Cmd.Renders;

internal static class GrayscaleFilter
{
    /// <summary>
    /// グレースケール化
    /// </summary>
    /// <param name="bitmap">ビットマップ</param>
    /// <param name="bcgyr">BCGYRにする？</param>
    public static void Apply(SKBitmap bitmap, bool bcgyr)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (bitmap.ColorType != SKColorType.Bgra8888) {
            throw new NotSupportedException($"{bitmap.ColorType} は非対応");
        }
        var pixels = bitmap.GetPixelSpan();
        if (pixels.IsEmpty) {
            return;
        }
        // GetPixelSpan は読み取り専用なので書き換え用に取り直す
        Span<byte> span;
        unsafe {
            span = new Span<byte>((void*)bitmap.GetPixels(), pixels.Length);
        }
        int width = bitmap.Width;
        int height = bitmap.Height;
        int stride = bitmap.RowBytes;
        var colorRef = new ColorRef();
        for (int y = 0; y < height; y++) {
            int offset = y * stride;
            for (int x = 0; x < width; x++) {
                int p = offset + x * 4;
                // Bgra8888 なので B,G,R,A の順
                var gray = Math.Round(0.0722 * span[p] + 0.7152 * span[p + 1] + 0.2126 * span[p + 2]);
                if (bcgyr) {
                    //colorRef
                    ColorScale.BCGYR(gray / 255.0, colorRef);
                    span[p] = colorRef.B;
                    span[p + 1] = colorRef.G;
                    span[p + 2] = colorRef.R;
                }
                else {
                    span[p] = (byte)gray;
                    span[p + 1] = (byte)gray;
                    span[p + 2] = (byte)gray;
                }
            }
        }
        bitmap.NotifyPixelsChanged();
    }
}
