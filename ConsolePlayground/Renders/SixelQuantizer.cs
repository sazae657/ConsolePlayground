using SkiaSharp;

namespace ConsolePlayground.Cmd.Renders;

internal static class SixelQuantizer
{
    /// <summary>
    /// 減色(死ぬほど重い)
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns></returns>
    internal static (SKColor[] Palette, byte[] Pixels) Quantize(SKBitmap bitmap)
    {
        var source = bitmap.Pixels;
        var exact = new Dictionary<SKColor, byte>();
        foreach (var color in source) {
            var rounded = ToDisplayColor(color.Red, color.Green, color.Blue);
            if (!exact.ContainsKey(rounded)) {
                if (exact.Count == 256) {
                    break;
                }
                exact.Add(rounded, (byte)exact.Count);
            }
        }
        var pixels = new byte[source.Length];
        if (source.All(color => exact.ContainsKey(ToDisplayColor(color.Red, color.Green, color.Blue)))) {
            for (int i = 0; i < source.Length; i++) {
                pixels[i] = exact[ToDisplayColor(source[i].Red, source[i].Green, source[i].Blue)];
            }
            return (exact.Keys.ToArray(), pixels);
        }

        // ヒストグラムの大きさを固定しとく
        var histogram = new Bin[32768];
        foreach (var color in source) {
            int index = (color.Red >> 3) << 10 | (color.Green >> 3) << 5 | (color.Blue >> 3);
            ref var bin = ref histogram[index];
            bin.Count++;
            bin.Red += color.Red;
            bin.Green += color.Green;
            bin.Blue += color.Blue;
        }
        var boxes = new List<Box> { new(histogram.Where(bin => bin.Count > 0).ToArray()) };
        while (boxes.Count < 256) {
            var candidate = boxes.Where(box => box.Bins.Length > 1).MaxBy(box => box.Score);
            if (candidate is null) {
                break;
            }
            boxes.Remove(candidate);
            var (left, right) = candidate.Split();
            boxes.Add(left);
            boxes.Add(right);
        }
        var palette = boxes.Select(box => box.Average()).ToArray();
        var nearest = new short[64 * 64 * 64];
        Array.Fill(nearest, (short)-1);
        int width = bitmap.Width;
        var current = new float[(width + 2) * 3];
        var next = new float[current.Length];
        for (int y = 0; y < bitmap.Height; y++) {
            int direction = y % 2 == 0 ? 1 : -1;
            for (int step = 0; step < width; step++) {
                int x = direction == 1 ? step : width - 1 - step;
                int offset = (x + 1) * 3;
                var color = source[y * width + x];
                float red = Math.Clamp(color.Red + current[offset], 0, 255);
                float green = Math.Clamp(color.Green + current[offset + 1], 0, 255);
                float blue = Math.Clamp(color.Blue + current[offset + 2], 0, 255);
                int r = Math.Min(63, (int)(red + 2) / 4);
                int g = Math.Min(63, (int)(green + 2) / 4);
                int b = Math.Min(63, (int)(blue + 2) / 4);
                int key = r << 12 | g << 6 | b;
                if (nearest[key] < 0) {
                    int bestDistance = int.MaxValue;
                    for (short i = 0; i < palette.Length; i++) {
                        int dr = r * 4 - palette[i].Red;
                        int dg = g * 4 - palette[i].Green;
                        int db = b * 4 - palette[i].Blue;
                        int distance = dr * dr + dg * dg + db * db;
                        if (distance < bestDistance) {
                            bestDistance = distance;
                            nearest[key] = i;
                        }
                    }
                }
                int selected = nearest[key];
                pixels[y * width + x] = (byte)selected;
                // 蛇行走査
                Spread(0, red - palette[selected].Red);
                Spread(1, green - palette[selected].Green);
                Spread(2, blue - palette[selected].Blue);
                void Spread(int channel, float error)
                {
                    current[offset + direction * 3 + channel] += error * (7f / 16);
                    next[offset - direction * 3 + channel] += error * (3f / 16);
                    next[offset + channel] += error * (5f / 16);
                    next[offset + direction * 3 + channel] += error * (1f / 16);
                }
            }
            (current, next) = (next, current);
            Array.Clear(next);
        }
        return (palette, pixels);
    }

    private static SKColor ToDisplayColor(int red, int green, int blue)
    {
        static byte Round(int value) => (byte)(((value * 100 + 127) / 255 * 255 + 50) / 100);
        return new SKColor(Round(red), Round(green), Round(blue));
    }

    private struct Bin
    {
        public int Count;
        public long Red;
        public long Green;
        public long Blue;
        public int Component(int axis) => (int)((axis == 0 ? Red : axis == 1 ? Green : Blue) / Count);
    }

    private sealed class Box
    {
        public Bin[] Bins { get; }
        private readonly int axis;
        public long Score { get; }

        public Box(Bin[] bins)
        {
            Bins = bins;
            int range = -1;
            for (int channel = 0; channel < 3; channel++) {
                int extent = bins.Max(bin => bin.Component(channel)) - bins.Min(bin => bin.Component(channel));
                if (extent > range) {
                    range = extent;
                    axis = channel;
                }
            }
            Score = (long)range * range * bins.Sum(bin => (long)bin.Count);
        }

        public (Box Left, Box Right) Split()
        {
            int channel = axis;
            var sorted = Bins.OrderBy(bin => bin.Component(channel)).ToArray();
            long half = (sorted.Sum(bin => (long)bin.Count) + 1) / 2;
            long count = 0;
            int split = 0;
            do {
                count += sorted[split++].Count;
            } while (count < half && split < sorted.Length - 1);
            return (new Box(sorted[..split]), new Box(sorted[split..]));
        }

        public SKColor Average()
        {
            long count = Bins.Sum(bin => (long)bin.Count);
            return ToDisplayColor((int)((Bins.Sum(bin => bin.Red) + count / 2) / count),
                (int)((Bins.Sum(bin => bin.Green) + count / 2) / count),
                (int)((Bins.Sum(bin => bin.Blue) + count / 2) / count));
        }
    }
}
