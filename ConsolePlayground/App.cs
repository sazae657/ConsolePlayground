using System.CommandLine;
using System.Globalization;
using ConsolePlayground.Cmd.Common;
using ConsolePlayground.Cmd.Renders;
using ConsolePlayground.Cmd.Terminators;

namespace ConsolePlayground.Cmd;

/// <summary>
/// ゆぷぷ
/// </summary>
internal class App
{
    private static async Task<int> Main(string[] args) {
#if WINDOWS
        WinRT.ComWrappersSupport.InitializeComWrappers();
#endif
        return await CreateCommand().Parse(args).InvokeAsync();
    }

    internal static RootCommand CreateCommand()
    {
        var movies = new Option<string[]>("--movie", ["-m"]) {
            Description = "動画またはウインドウまたはモニター"
        };
        var characters = new Option<string>("--characters", ["-c"]) {
            DefaultValueFactory = _ => " `.:-=;*?+#%@",
            Description = "ASCII表示に使用する文字"
        };
        var color = new Option<bool>("--color") {
            Description = "ASCII文字を着色する"
        };
        var fill = new Option<bool>("--fill") {
            Description = "ASCII表示を色付きブロックで埋める"
        };
        var fullWidth = new Option<bool>("--full-width") {
            Description = "全角文字でASCII表示する"
        };
        var boxDrawing = new Option<bool>("--box") {
            Description = "輪郭を罫線素片で描く"
        };
        var boxRound = new Option<bool>("--box-round") {
            Description = "罫線の角を角丸にする(--box用)"
        };
        var boxFill = new Option<bool>("--box-fill") {
            Description = "線の無い所を --characters の文字で明るさに応じて塗る(--box用)"
        };
        var boxThin = new Option<bool>("--box-thin") {
            Description = "太線を使わない(--box用)"
        };
        var boxNoDiagonal = new Option<bool>("--box-no-diagonal") {
            Description = "斜線 ╱╲ を使わない(--box用)"
        };
        var sixel = new Option<bool>("--sixel") {
            Description = "sixelで表示(--renderer sixelと等価)"
        };
        var kitty = new Option<bool>("--kitty") {
            Description = "Kitty Graphics Protocolで表示(--renderer kittyと等価)"
        };
        var brightness = new Option<bool>("--brightness") {
            Description = "輝度を使ってグレースケールで表示する"
        };
        var brightnessBcgyr = new Option<bool>("--brightness-bcgyr")
        {
            Description = "輝度を使ってBCGYRで表示する"
        };

        var infinite = new Option<bool>("--infinite")
        {
            Description = "無限"
        };

        var ffmpegPath = new Option<string>("--ffmpeg", ["-f"]) {
            Description = "ffmpegのパス"
        };
        var renderer = new Option<string>("--renderer") {
            DefaultValueFactory = _ => "ascii",
            Description = "描画方式: ascii か sixel か kitty"
        };
        var width = new Option<int?>("--image-width", ["--sixel-width"]) {
            Description = $"画像の最大幅（1～{SixelEncoder.MaximumDimension}ピクセル、省略時は端末に合わせる）"
        };
        var height = new Option<int?>("--image-height", ["--sixel-height"]) {
            Description = $"画像の最大高さ（1～{SixelEncoder.MaximumDimension}ピクセル、省略時は端末に合わせる）"
        };
        var colorReduction = new Option<int?>("--color-reduction") {
            Arity = ArgumentArity.ZeroOrOne,
            Description = $"まあまあな品質の減色を使う。色数は{SixelEncoder.MinimumColors}～{SixelEncoder.MaximumColors}"
                + $"(省略時{SixelEncoder.MaximumColors}色、死ぬほど重い)",
            CustomParser = result => {
                // 値なしで指定されたら最大色数
                if (result.Tokens.Count == 0) {
                    return SixelEncoder.MaximumColors;
                }
                if (int.TryParse(result.Tokens[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && value >= SixelEncoder.MinimumColors && value <= SixelEncoder.MaximumColors) {
                    return value;
                }
                result.AddError($"--color-reduction は{SixelEncoder.MinimumColors}～{SixelEncoder.MaximumColors}で指定");
                return null;
            }
        };
        var record = new Option<string?>("--record")
        {
            Description = "テキストに保存",
            DefaultValueFactory = _ => null
        };

        var recordFormat = new Option<RecordingFormat>("--record-format")
        {
            Description = "保存形式",
            DefaultValueFactory = _ => RecordingFormat.BSV
        };

        var showInfo = new Option<bool>("--info") {
            Description = "情報表示",
            DefaultValueFactory = _ => false
        };

        var headless = new Option<bool>("--headless")
        {
            Description = "レンダリングするだけ(表示しない)",
            DefaultValueFactory = _ => false
        };

        var headlessWidth = new Option<int>("--headless-width") {
            Description = "レンダリングする幅(文字数)",
        };
        var headlessHeight = new Option<int>("--headless-height") {
            Description = "レンダリングする高さ(文字数)",
        };

        var listen = new Option<string?>("--listen")
        {
            Description = "TCP待ち受け(IP:Port)",
            DefaultValueFactory = _ => null
        };
        var listenRemote = new Option<bool>("--listen-remote")
        {
            Description = "ループバック以外での待ち受けを許可する(プレーンテキストでダダ漏れ注意)",
            DefaultValueFactory = _ => false
        };

        var benchmark = new Option<bool>("--benchmark") {
            Description = "全フレーム全速処理して、終了時に処理時間の内訳を表示",
            DefaultValueFactory = _ => false
        };

#if WINDOWS
        var noCaptureBorder = new Option<bool>("--no-capture-border")
        {
            Description = "黄色い枠線を出さない(薄気味悪い)",
            DefaultValueFactory = _ => false
        };
        var noWindowFrame = new Option<bool>("--no-window-frame")
        {
            Description = "クライアント領域のみキャプチャー",
            DefaultValueFactory = _ => false
        };
        var windowList = new Option<bool>("--list-window")
        {
            Description = "ウインドウリスト表示",
            DefaultValueFactory = _ => false
        };
        var monitorList = new Option<bool>("--list-monitor") {
            Description = "モニターリスト表示",
            DefaultValueFactory = _ => false
        };
#endif

        var root = new RootCommand(nameof(ConsolePlayground.Cmd)) {
            movies,
            renderer,
            characters,
            color,
            fill,
            fullWidth,
            boxDrawing, boxRound, boxFill, boxThin, boxNoDiagonal,
            sixel,
            kitty,
            brightness,
            brightnessBcgyr,
            infinite,
            ffmpegPath,
            width,
            height,
            record,
            recordFormat,
            showInfo,
            colorReduction,
            listen,
            listenRemote,
            headless, headlessWidth, headlessHeight,
            benchmark,
#if WINDOWS
            noCaptureBorder,noWindowFrame,
            windowList, monitorList
#endif
        };
        root.Validators.Add(result => {
            bool listMode = false;
#if WINDOWS
            listMode = result.GetValue(windowList) || result.GetValue(monitorList);
#endif
            if (!listMode && (result.GetValue(movies)?.Length ?? 0) == 0) {
                result.AddError("--movieの指定がない");
            }

            if (result.GetValue(sixel) && result.GetValue(kitty)) {
                result.AddError("--sixel と --kitty は同時に指定できない");
            }
            string? rendererName = result.GetResult(renderer)?.Tokens.FirstOrDefault()?.Value;
            var parsedMode = ParseRenderMode(rendererName, result.GetValue(sixel), result.GetValue(kitty));
            if (parsedMode == null) {
                result.AddError("--renderer は ascii、sixel、kitty のいずれかを指定");
            }
            var mode = parsedMode ?? RenderMode.Ascii;
            bool isSixel = mode == RenderMode.Sixel;
            bool isImage = mode != RenderMode.Ascii;
            if (isImage) {
                foreach (var option in new Option[] { characters, color, fill, fullWidth, boxDrawing, boxRound, boxFill, boxThin, boxNoDiagonal }) {
                    if (result.GetResult(option) is { Implicit: false }) {
                        result.AddError($"{option.Name} はASCII専用なので画像表示と併用できない");
                    }
                }
            }
            else if (result.GetResult(width) is { Implicit: false } || result.GetResult(height) is { Implicit: false }) {
                result.AddError("--image-width と --image-height には --renderer sixel または kitty が必要");
            }
            // ASCIIは着色するときだけ減色の意味がある
            bool reducible = isSixel || (!isImage && (result.GetValue(color) || result.GetValue(fill)));
            if (!reducible && result.GetResult(colorReduction) is { Implicit: false }) {
                result.AddError("--color-reduction には --sixel か、ASCIIの --color / --fill が必要");
            }
            foreach (var option in new[] { width, height }) {
                string? text = result.GetResult(option)?.Tokens.FirstOrDefault()?.Value;
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && (value < 1 || value > SixelEncoder.MaximumDimension)) {
                    result.AddError($"{option.Name} は1～{SixelEncoder.MaximumDimension}で指定");
                }
            }
            if (!isImage && result.GetResult(characters) is { Tokens.Count: 1 } characterResult
                && characterResult.Tokens[0].Value.Length == 0) {
                result.AddError("--characters が空");
            }
            if (result.GetResult(listenRemote) is { Implicit: false } && result.GetResult(listen) is not { Implicit: false }) {
                result.AddError("--listen-remote には --listen が必要");
            }
            if (result.GetValue(boxDrawing) && (result.GetValue(fill) || result.GetValue(fullWidth))) {
                // 罫線素片は半角幅で並べないと線がつながらない
                result.AddError("--box は --fill や --full-width と併用できない");
            }
            if (!result.GetValue(boxDrawing)) {
                foreach (var option in new Option[] { boxRound, boxFill, boxThin, boxNoDiagonal }) {
                    if (result.GetResult(option) is { Implicit: false }) {
                        result.AddError($"{option.Name} には --box が必要");
                    }
                }
            }
        });


        root.SetAction(async (result, cancellationToken) => {
            try {
#if WINDOWS
                if (result.GetValue(windowList) || result.GetValue(monitorList)) {
                    if (result.GetValue(windowList)) {
                        ShowWindowList();
                    }
                    if (result.GetValue(monitorList)) {
                        ShowMonitorList();
                    }
                    return 0;
                }
#endif

                if (!result.GetValue(headless) && Console.IsOutputRedirected) {
                    throw new InvalidOperationException("標準出力へのリダイレクトは使用できない");
                }
                var mode = ParseRenderMode(result.GetValue(renderer), result.GetValue(sixel), result.GetValue(kitty))
                    ?? RenderMode.Ascii;
                var rec = result.GetValue(record);

                await using var player = new Jussen(
                    result.GetValue(characters)!, result.GetValue(ffmpegPath),
                    result.GetValue(color), result.GetValue(fill), result.GetValue(infinite),
                    result.GetValue(movies)!,
                    mode, result.GetValue(width), result.GetValue(height),
                    result.GetValue(fullWidth), result.GetValue(colorReduction),
                    result.GetValue(boxDrawing) ? new BoxDrawingConverter {
                        RoundCorners = result.GetValue(boxRound),
                        FillCharacters = result.GetValue(boxFill) ? result.GetValue(characters) : null,
                        HeavyThreshold = result.GetValue(boxThin) ? double.PositiveInfinity : 0.5,
                        Diagonals = !result.GetValue(boxNoDiagonal),
                    } : null)
                {
                    RecordFile = !string.IsNullOrEmpty(rec) ? new FileInfo(rec) : null,
                    RecordingFormat = result.GetValue(recordFormat),
                    Listen = result.GetValue(listen),
                    ListenRemote = result.GetValue(listenRemote),
                    Brightness = result.GetValue(brightness),
                    Bcgyr = result.GetValue(brightnessBcgyr),
                };
                player.RenderParams.ShowInfo = result.GetValue(showInfo);
                player.RenderParams.Headless = result.GetValue(headless);
                player.RenderParams.HeadlessWidth = result.GetValue(headlessWidth);
                player.RenderParams.HeadlessHeight = result.GetValue(headlessHeight);
                if (result.GetValue(benchmark)) {
                    player.Stats = new PlaybackStats();
                }
#if WINDOWS
                player.WindowCaptureOptions = new Imoging.Platform.WindowCaptureOptions(
                    Borderless: result.GetValue(noCaptureBorder), NoFrame:result.GetValue(noWindowFrame)
                );
#endif

                await player.Run(cancellationToken);
                if (player.Stats != null) {
                    // 端末を元の画面に戻した後に出す
                    Console.WriteLine(player.Stats);
                }
                return 0;
            }
            catch (Exception error) {
                Console.Error.WriteLine($"エラー: {error.Message}\n{error.StackTrace}");
                return 9;
            }
        });
        return root;
    }


    /// <summary>
    /// --renderer と別名オプションから描画方式を決める。不明な名前なら null
    /// </summary>
    /// <param name="name"></param>
    /// <param name="sixel"></param>
    /// <param name="kitty"></param>
    /// <returns></returns>
    internal static RenderMode? ParseRenderMode(string? name, bool sixel, bool kitty)
    {
        if (sixel) {
            return RenderMode.Sixel;
        }
        if (kitty) {
            return RenderMode.Kitty;
        }
        return (name ?? "ascii").ToLowerInvariant() switch {
            "ascii" => RenderMode.Ascii,
            "sixel" => RenderMode.Sixel,
            "kitty" => RenderMode.Kitty,
            _ => null
        };
    }

#if WINDOWS
    static string AsPrefixed(string prefix, string? n) {
        if (string.IsNullOrEmpty(n)) {
            return string.Empty;
        }
        if (n.Contains(' ')) {
            return '"' + prefix + n + '"';
        }
        return prefix + n;
    }

    static void ShowWindowList() {
        var table = new  List<List<string>>();
        foreach (var x in Imoging.Platform.WindowEnumerator.GetCapturableWindows().OrderBy(m => m.Title)) {
            table.Add([
                AsPrefixed(Jussen.WindowPrefix, $"0x{x.Handle.ToInt64():X8}"),
                AsPrefixed(Jussen.WindowPrefix, x.ClassName),
                x.Title ?? string.Empty
            ]);
        }
        Console.WriteLine(TableFormatter.FormatAsTable(table));
    }

    static void ShowMonitorList()
    {
        const string pfx = @"\\.\";
        var table = new List<List<string>>();
        foreach (var x in Imoging.Platform.MonitorEnumerator.GetMonitors()) {
            table.Add([
                AsPrefixed(Jussen.MonitorPrefix,x.DeviceName.Substring(pfx.Length)),
                $"IsPrimary:{x.IsPrimary}",
                $"X:{x.WorkArea.Left}-{x.WorkArea.Right}",
                $"Y:{x.WorkArea.Top}-{x.WorkArea.Bottom}",
                $"0x{x.HMonitor.ToInt64():X8}"
            ]);
        }
        Console.WriteLine(TableFormatter.FormatAsTable(table));
    }
#endif
}
