using System.Diagnostics;
using ConsolePlayground.Cmd.Renders;

namespace ConsolePlayground.Cmd.Terminators;

/// <summary>
/// 端末サイズ系
/// </summary>
internal sealed class TerminalSizeProbe : IDisposable
{
    private const int ResponseTimeout = 250;

    // 問い合わせ(16tへ応答しない端末は14tで試す)
    internal static readonly string[] Queries = ["\x1b[16t", "\x1b[14t"];
    private readonly Func<char?> read;
    private readonly Action<string> write;
    private readonly Func<long> milliseconds;
    private readonly Func<TerminalCellSize?> native;
    private readonly Func<(int Columns, int Rows)> viewport;
    private readonly TerminalSizeResponse parser = new();
    private long sentAt;
    private long nextQuery;
    private int query;
    private bool answered;
    private bool disabled;

    public TerminalCellSize? CellSize { get; private set; }
    internal bool Pending { get; private set; }

    public TerminalSizeProbe() : this(
        () => Console.KeyAvailable ? Console.ReadKey(intercept: true).KeyChar : null,
        Console.Write, () => Environment.TickCount64, TerminalWindowSize.CellSize,
        () => (Console.WindowWidth, Console.WindowHeight))
    {
        disabled = Console.IsInputRedirected;
    }

    internal TerminalSizeProbe(Func<char?> read, Action<string> write, Func<long> milliseconds,
        Func<TerminalCellSize?>? native = null, Func<(int Columns, int Rows)>? viewport = null)
    {
        this.read = read;
        this.write = write;
        this.milliseconds = milliseconds;
        this.native = native ?? (() => null);
        this.viewport = viewport ?? (() => (0, 0));
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        Poll();
        while (Pending) {
            await Task.Delay(10, token);
            Poll();
        }
    }

    public void Poll(bool request = true)
    {
        if (!Pending && native() is { } size) {
            CellSize = size;
            return;
        }
        if (disabled) {
            return;
        }
        try {
            for (int count = 0; count < 512 && read() is { } value; count++) {
                if (parser.Push(value) is { } report) {
                    var (columns, rows) = viewport();
                    CellSize = report.ToCellSize(columns, rows) ?? CellSize;
                    Pending = false;
                    answered = true;
                    nextQuery = milliseconds() + 1000;
                }
            }
            long now = milliseconds();
            if (Pending && now - sentAt >= ResponseTimeout) {
                Pending = false;
                if (answered) {
                    nextQuery = now + 5000;
                }
                else {
                    query++;
                    nextQuery = now;
                }
            }
            if (request && !Pending && query < Queries.Length && now >= nextQuery) {
                write(Queries[query]);
                sentAt = now;
                Pending = true;
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException) {
            disabled = true;
            Pending = false;
        }
    }

    public void Dispose()
    {
        var clock = Stopwatch.StartNew();
        do {
            Poll(request: false);
            if (!Pending) {
                break;
            }
            Thread.Sleep(5);
        } while (clock.ElapsedMilliseconds < ResponseTimeout);
        disabled = true;
    }
}
