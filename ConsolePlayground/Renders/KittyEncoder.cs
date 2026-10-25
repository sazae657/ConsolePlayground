using System.Globalization;
using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace ConsolePlayground.Cmd.Renders;

/// <summary>
/// Kitty Graphics Protocol のエンコーダ(direct転送のみ)
/// </summary>
public static class KittyEncoder
{
    /// <summary>
    /// 毎フレーム置き換える画像ID
    /// </summary>
    public const int ImageId = 1;

    /// <summary>
    /// 毎フレーム置き換える配置ID
    /// </summary>
    public const int PlacementId = 1;

    /// <summary>
    /// 1チャンクあたりのbase64文字数の上限(仕様上4096)
    /// </summary>
    public const int ChunkSize = 4096;

    /// <summary>
    /// 終了時に画像を端末から解放するシーケンス
    /// </summary>
    public static readonly string DeleteSequence = $"\x1b_Ga=d,d=I,i={ImageId},q=2\x1b\\";

    public static string Encode(SKBitmap frame, int maxWidth, int maxHeight)
    {
        SixelEncoder.ValidateFrame(frame, maxWidth, maxHeight);
        using var bitmap = SixelEncoder.ResizeToFit(frame, maxWidth, maxHeight);
        int width = bitmap.Width;
        int height = bitmap.Height;

        // BGRA を RGB 24bit に詰めて zlib 圧縮
        var source = bitmap.GetPixelSpan();
        var rgb = new byte[width * height * 3];
        for (int i = 0, j = 0; j < rgb.Length; i += 4, j += 3) {
            rgb[j] = source[i + 2];
            rgb[j + 1] = source[i + 1];
            rgb[j + 2] = source[i];
        }
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) {
            zlib.Write(rgb);
        }
        string payload = Convert.ToBase64String(compressed.GetBuffer(), 0, (int)compressed.Length);

        var output = new StringBuilder(payload.Length + payload.Length / ChunkSize * 16 + 96);
        for (int offset = 0; offset < payload.Length || offset == 0; offset += ChunkSize) {
            int length = Math.Min(ChunkSize, payload.Length - offset);
            bool more = offset + length < payload.Length;
            output.Append("\x1b_G");
            if (offset == 0) {
                // a=T: 転送して表示、C=1: カーソルを動かさない、q=2: 応答を抑止
                output.Append(CultureInfo.InvariantCulture,
                    $"a=T,f=24,o=z,s={width},v={height},i={ImageId},p={PlacementId},C=1,q=2,");
            }
            else {
                // 後続チャンクに付けて良いのは m と q だけ
                output.Append("q=2,");
            }
            output.Append(more ? "m=1;" : "m=0;");
            output.Append(payload, offset, length);
            output.Append("\x1b\\");
        }
        return output.ToString();
    }
}
