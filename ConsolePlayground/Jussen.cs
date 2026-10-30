using FFmpeg.AutoGen;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ConsolePlayground.Cmd.Common;
using ConsolePlayground.Cmd.Imoging;
using ConsolePlayground.Cmd.Renders;
using ConsolePlayground.Cmd.Terminators;

namespace ConsolePlayground.Cmd;

/// <summary>
/// レンダリング設定
/// </summary>
internal sealed class RenderParams
{
    public bool ShowInfo { get; set; }
    public bool Headless { get; set; }
    public int HeadlessWidth { get; set; }
    public int HeadlessHeight { get; set; }

    public int RenderWidth {
        get {
            if (!Headless) {
                return Console.WindowWidth;
            }
            return HeadlessWidth > 1 ? HeadlessWidth : ConsoleSizeOr(() => Console.WindowWidth, 80);
        }
    }

    public int RenderHeight {
        get {
            if (!Headless) {
                return Console.WindowHeight;
            }
            return HeadlessHeight > 1 ? HeadlessHeight : ConsoleSizeOr(() => Console.WindowHeight, 25);
        }
    }

    /// <summary>
    /// 端末の寸法が取れない時用
    /// </summary>
    private static int ConsoleSizeOr(Func<int> size, int fallback)
    {
        try {
            int value = size();
            return value > 0 ? value : fallback;
        }
        catch (IOException) {
            return fallback;
        }
    }
}

/// <summary>
/// 記録形式
/// </summary>
enum RecordingFormat
{
    Text, Base64, BSV
}

/// <summary>
/// メイン
/// </summary>
internal sealed class Jussen : IAsyncDisposable
{
    private readonly List<string> movies = new();
    private readonly string characters;
    private readonly bool color;
    private readonly bool fill;
    private readonly bool fullWidth;
    // 罫線モードは前フレームの状態を持つ
    private readonly BoxDrawingConverter? boxDrawing;
    // Sixel減色の色数(null なら固定パレット)
    private readonly int? colorReduction;
    private readonly bool infinite;
    private readonly RenderMode renderMode;
    private readonly int? imageWidth;
    private readonly int? imageHeight;
    private readonly Stopwatch elaptingGk = new();
    /// <summary>
    /// 記録先
    /// </summary>
    public FileInfo? RecordFile { get; set; }

    /// <summary>
    /// 記録形式
    /// </summary>
    public RecordingFormat RecordingFormat { get; set; } = RecordingFormat.Text;

    /// <summary>
    /// 待ち受けアドレス
    /// </summary>
    public string? Listen { get; set; }

    /// <summary>
    /// ループバック以外のアドレスでの待ち受けを許可する
    /// </summary>
    public bool ListenRemote { get; set; }

    /// <summary>
    /// レンダリング設定
    /// </summary>
    public RenderParams RenderParams { get; } = new();

    /// <summary>
    /// ベンチマークモード
    /// </summary>
    public PlaybackStats? Stats { get; set; }

    /// <summary>
    /// 輝度を使ってグレースケールで表示する
    /// </summary>
    public bool Brightness { get; set; }

    /// <summary>
    /// BCGYR
    /// </summary>
    public bool Bcgyr { get; set; }

    Services.TelnetServer? server;

#if WINDOWS
    public const string WindowPrefix = "w://";
    public const string MonitorPrefix = "m://";

    public Imoging.Platform.WindowCaptureOptions WindowCaptureOptions { get; set; } = new();
#endif

    readonly Regex WCLASS = new Regex(@"\A0x[0-9A-Fa-f]+\Z", RegexOptions.Compiled);

    public Jussen(string characters, string? ffpath, bool color, bool fill, bool infinite, IEnumerable<string> movies,
        RenderMode renderMode = RenderMode.Ascii, int? imageWidth = null, int? imageHeight = null,
        bool fullWidth = false, int? colorReduction = null, BoxDrawingConverter? boxDrawing = null)
    {
        var ffmpegRoot = FFmpegLocator.Resolve(ffpath);
        this.characters = characters;
        this.color = color;
        this.fill = fill;
        this.fullWidth = fullWidth;
        this.boxDrawing = boxDrawing;
        this.colorReduction = colorReduction;
        this.infinite = infinite;
        this.renderMode = renderMode;
        this.imageWidth = imageWidth;
        this.imageHeight = imageHeight;
        foreach (var path in movies) {
#if WINDOWS
            if (path.StartsWith(WindowPrefix) ||
                path.StartsWith(MonitorPrefix)) {
                this.movies.Add(path);
                continue;
            }
#endif
            var file = new FileInfo(Path.GetFullPath(path));
            if (!file.Exists) {
                throw new FileNotFoundException($"{file} ファイルが無い");
            }
            this.movies.Add(file.FullName);
        }
        ffmpeg.RootPath = ffmpegRoot;
        DynamicallyLoadedBindings.Initialize();
    }

    Task<IImageSource> CreateReader(string path) {
#if WINDOWS
        if (path.StartsWith(WindowPrefix)) {
            return CreateWindowSource(path);
        }
        if (path.StartsWith(MonitorPrefix)) {
            return CreateMonitorSource(path);
        }
#endif
        return Task.FromResult<IImageSource>(new VideoReadment(path));
    }

#if WINDOWS
    async Task<IImageSource> CreateWindowSource(string path)
    {
        var ms = path.Substring(WindowPrefix.Length).Trim();
        IntPtr handle;
        if (WCLASS.IsMatch(ms)) {
            handle = (IntPtr)Convert.ToInt64(ms, 16);
        }
        else {
            var wt = Imoging.Platform.WindowEnumerator.FindWindow(ms);
            if (wt == null) {
                throw new Exception($"{ms} を含むウインドウがない");
            }
            handle = wt.Handle;
        }
        var mc = new Imoging.Platform.WindowCapture(WindowCaptureOptions, handle);
        await mc.Start();
        return mc;
    }

    async Task<IImageSource> CreateMonitorSource(string path)
    {
        var ms = @"\\.\" + path.Substring(MonitorPrefix.Length).Trim();
        var wt = Imoging.Platform.MonitorEnumerator.GetMonitors().FirstOrDefault(w => w.DeviceName == ms);
        if (wt == null) {
            throw new Exception($"{ms} そのモニターない");
        }
        var mc = new Imoging.Platform.WindowCapture(WindowCaptureOptions, wt);
        await mc.Start();
        return mc;
    }
#endif

    /// <summary>
    /// 動画再生
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task Run(CancellationToken token)
    {
        if (movies.Count == 0) {
            return;
        }

        await using var recordWriter = RecordFile != null
            ? new StreamWriter(RecordFile.FullName, append: false, new UTF8Encoding(false))
            : null;
        if (Listen != null) {
            if (!IPEndPoint.TryParse(Listen, out IPEndPoint? endPoint)) {
                throw new Exception($"IPEndPointのパースに失敗: {Listen}");
            }
            if (!IPAddress.IsLoopback(endPoint.Address) && !ListenRemote) {
                throw new Exception($"{endPoint.Address} はループバックではない。外部へ公開するなら --listen-remote を指定");
            }
            server = new Services.TelnetServer
            {
                Address = endPoint.Address,
                Port = endPoint.Port
            };
            server.Start();
        }

        using var terminal = RenderParams.Headless ? null : new TerminalSession();
        using var probe = renderMode is RenderMode.Sixel or RenderMode.Kitty && !RenderParams.Headless ? new TerminalSizeProbe() : null;
        AsciiArtConverter.Characters = characters;
        var stopwatch = Stopwatch.StartNew();
        Stats?.Start();
        try {
            if (probe != null) {
                await probe.InitializeAsync(token);
            }
            IFrameRenderer renderer = renderMode switch {
                RenderMode.Ascii => new AsciiFrameRenderer(color, fill, RenderParams, fullWidth, boxDrawing, colorReduction),
                RenderMode.Sixel => ImageFrameRenderer.Sixel(imageWidth, imageHeight, probe, RenderParams, colorReduction),
                RenderMode.Kitty => ImageFrameRenderer.Kitty(imageWidth, imageHeight, probe, RenderParams),
                _ => throw new ArgumentOutOfRangeException(nameof(renderMode))
            };
            renderer.EnableRecord(RecordFile != null);
            do {
                token.ThrowIfCancellationRequested();
                foreach (var movie in movies) {
                    token.ThrowIfCancellationRequested();
                    using var reader = await CreateReader(movie);
                    if (!reader.IsValid()) {
                        continue;
                    }
                    var timeline = new PlaybackTimeline(reader.FrameRate);
                    var clock = new Stopwatch();
                    int index = 0;
                    if (!RenderParams.Headless) {
                        Console.Write("\x1b[2J\x1b[H");
                    }
                    renderer.ClearRecord();
                    // 動画が変わったら前フレームの線を引き継がない
                    boxDrawing?.Reset();
                    stopwatch.Restart();
                    elaptingGk.Restart();
                    while (!token.IsCancellationRequested) {
                        long decodeStart = Stopwatch.GetTimestamp();
                        if (!reader.Next(token)) {
                            break;
                        }
                        Stats?.AddDecode(decodeStart);
                        long encodeStart = Stopwatch.GetTimestamp();
                        string frame = reader.Frame(f => {
                            if (f == null) {
                                return string.Empty;
                            }
                            if (Brightness || Bcgyr) {
                                GrayscaleFilter.Apply(f, Bcgyr);
                            }
                            return renderer.Encode(f);
                        });
                        Stats?.AddEncode(encodeStart);
                        if (string.IsNullOrEmpty(frame)) {
                            Debug.WriteLine("Wait Next");
                            continue;
                        }

                        var target = timeline.Advance(reader.Duration);

                        if (!clock.IsRunning) {
                            clock.Start();
                        }
                        var remaining = target - clock.Elapsed;
                        if (Stats == null && remaining > TimeSpan.Zero) {
                            try {
                                await Task.Delay(remaining, token);
                            }
                            catch {
                            }
                        }
                        if (token.IsCancellationRequested) {
                            break;
                        }
                        long renderStart = Stopwatch.GetTimestamp();
                        renderer.Render(frame, ++index, reader.Duration, stopwatch.ElapsedMilliseconds / reader.FrameRate);
                        Stats?.AddRender(renderStart, renderer.LastFrame.Length);
                        if ((index % 100) == 0) {
                            SaveRecorded(renderer, recordWriter);
                            if (RenderParams.Headless) {
                                var dsp = reader.Duration.ToString(@"hh\:mm\:ss");
                                Console.WriteLine($"index={index} duration={dsp}");
                            }
                        }
                        server?.Broadcast(renderer.LastFrame);
                        stopwatch.Restart();
                    }
                    SaveRecorded(renderer, recordWriter);
                    if (index > 0 && Stats == null) {
                        await Task.Delay(timeline.FrameInterval, token);
                    }
                    token.ThrowIfCancellationRequested();
                }
            } while (infinite);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
        }
        finally {
            Stats?.Stop();
            if (renderMode == RenderMode.Kitty && !RenderParams.Headless) {
                // 代替画面を抜ける前に端末側の画像を解放
                Console.Write(KittyEncoder.DeleteSequence);
            }
        }
    }

    void SaveRecorded(IFrameRenderer render, TextWriter? writer) {
        if (writer == null || render.GetRecorded == 0) {
            return;
        }
        if (!RenderParams.Headless) {
            Console.Write("\e[0;0HSaving ...");
        }
        foreach (var x in render.GetRecord()) {

            switch (RecordingFormat) {
                case RecordingFormat.Base64:
                    WriteBase64Line(writer, render, x);
                    break;
                case RecordingFormat.BSV:
                    WriteBsvLine(writer, render, x);
                    break;
                default:
                    writer.Write("\e[0;0H-- begin frame --\n");
                    writer.Write(x);
                    writer.Write('\n');
                    break;
            }
        }
        writer.Flush();
        if (!RenderParams.Headless) {
            Console.Write("\e[0;0HSaved");
        }
        render.ClearRecord();
    }

    void WriteBase64Line(TextWriter writer, IFrameRenderer render, string text) {
        int byteCount = Encoding.UTF8.GetByteCount(text);
        byte[] bytes = ArrayPool<byte>.Shared.Rent(byteCount);
        char[] chars = ArrayPool<char>.Shared.Rent((byteCount + 2) / 3 * 4);
        try {
            int length = Encoding.UTF8.GetBytes(text, bytes);
            Convert.TryToBase64Chars(bytes.AsSpan(0, length), chars, out int written);
            writer.Write(chars, 0, written);
            writer.Write('\n');
        }
        finally {
            ArrayPool<char>.Shared.Return(chars);
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    void WriteBsvLine(TextWriter writer, IFrameRenderer render, string text)
    {
        writer.Write($"{elaptingGk.ElapsedMilliseconds}{CommonBackspaceEnvironment.Separator}{text.Escape()}");
        writer.Write(CommonBackspaceEnvironment.LineBreak);
    }

    public async ValueTask DisposeAsync()
    {
        if (!RenderParams.Headless) {
            // コンソールリセッシュ
            Console.WriteLine("\x1b[0m");
        }
        if (server != null) {
            await server.DisposeAsync();
        }
    }
}
