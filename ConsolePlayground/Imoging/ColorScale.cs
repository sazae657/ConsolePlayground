using System.Runtime.CompilerServices;

namespace ConsolePlayground.Cmd.Imoging;

/// <summary>
/// いろ
/// </summary>
internal sealed class ColorRef {
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
}

internal static class ColorScale {

    /// <summary>
    /// ～1.0をBCGYRに
    /// </summary>
    /// <param name="indy"></param>
    /// <param name="color"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BCGYR(double indy, ColorRef color) {
        double volume = indy;
        double pour = Math.Cos(4 * Math.PI * volume);
        int cpi = (int)((-pour / 2 + 0.5) * 255);

        var (r, g, b) = volume switch
        {
            >= 1.00 => (255, 0, 0),
            >= 0.75 => (255, cpi, 0),
            >= 0.50 => (cpi, 255, 0),
            >= 0.25 => (0, 255, cpi),
            >= 0.00 => (0, cpi, 255),
            _ => (0, 0, 255)
        };
        color.R = (byte)r;
        color.G = (byte)g;
        color.B = (byte)b;
    }
}
