using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace ConsolePlayground.Cmd.Services;

/// <summary>
/// TCP
/// </summary>
internal sealed class TelnetServer : IAsyncDisposable {
    private bool disposedValue;
    CancellationTokenSource tokenSource = new();
    TcpListener? listener;
    Task? tasken;

    public int Port { get; set; } = 3128;
    public IPAddress Address { get; set; } = IPAddress.Any;

    const int MaxBuffers = 5;

    /// <summary>
    /// 同時接続数の上限
    /// </summary>
    public int MaxClients { get; set; } = 8;

    private readonly ConcurrentDictionary<Guid, Channel<string>> subscribers = new();

    public TelnetServer() {
    }

    public void Start() {
        listener = new TcpListener(Address, Port);
        listener.Start();
        tasken = StartInternal(listener, tokenSource.Token);
    }

    public void Broadcast(string logMessage) {
        foreach (var channel in subscribers.Values) {
            channel.Writer.TryWrite(logMessage);
        }
    }

    private async Task StartInternal(TcpListener listener, CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested) {
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
                if (subscribers.Count >= MaxClients) {
                    client.Dispose();
                    continue;
                }
                _ = HandleClientSessionAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        finally {
            listener.Stop();
        }
    }

    private async Task HandleClientSessionAsync(TcpClient client, CancellationToken cancellationToken) {
        var sessionId = Guid.NewGuid();
        // クライアントごとに独立したMaxBuffers件のバッファを与える
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(MaxBuffers)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        subscribers[sessionId] = channel;

        using (client)
        using (var stream = client.GetStream())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
            try {
                await foreach (var message in channel.Reader.ReadAllAsync(linkedCts.Token)) {
                    await writer.WriteLineAsync(message.AsMemory(), linkedCts.Token);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException) {
                // 通常の切断
            }
            finally {
                subscribers.TryRemove(sessionId, out _);
                channel.Writer.TryComplete();
            }
        }
    }

    #region IDisposable
    public async ValueTask DisposeAsync() {
        if (disposedValue) {
            return;
        }
        disposedValue = true;
        if (tasken != null) {
            tokenSource.Cancel();
            await tasken;
        }

        listener?.Dispose();
        listener = null;
        tokenSource?.Dispose();
    }
    #endregion
}
