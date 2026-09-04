using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Rhino;
using RhinoMinion.Protocol;

namespace RhinoMinion.Plugin.Bridge;

internal sealed class BridgeClient : IDisposable
{
    private static readonly Uri Endpoint = new("ws://127.0.0.1:8766/ws/rhino");
    private readonly CancellationTokenSource _shutdown = new();
    private readonly RhinoRequestDispatcher _dispatcher;
    private readonly Guid _sessionId = Guid.NewGuid();
    private Task? _runTask;

    public BridgeClient(RhinoRequestDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Start()
    {
        _runTask = Task.Run(() => RunReconnectLoopAsync(_shutdown.Token));
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _runTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Shutdown is best effort; the socket loop observes cancellation.
        }
        _shutdown.Dispose();
    }

    private async Task RunReconnectLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(Endpoint, cancellationToken).ConfigureAwait(false);
                await SendHandshakeAsync(socket, cancellationToken).ConfigureAwait(false);
                RhinoApp.WriteLine("RHINO Minion connected to local backend.");
                await ReceiveLoopAsync(socket, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                RhinoApp.WriteLine("RHINO Minion bridge disconnected: {0}", exception.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SendHandshakeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var secret = Environment.GetEnvironmentVariable("RHINO_MINION_BRIDGE_SECRET") ?? string.Empty;
        var payload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            role = "rhino_plugin",
            plugin_version = "0.1.0",
            rhino_version = RhinoApp.ExeVersion.ToString(),
            secret
        })).RootElement.Clone();
        var request = new BridgeRequest(
            ProtocolConstants.CurrentVersion,
            Guid.NewGuid(),
            _sessionId,
            "handshake",
            payload);
        await SendJsonAsync(socket, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var json = await ReceiveTextAsync(socket, cancellationToken).ConfigureAwait(false);
            if (json is null)
            {
                return;
            }

            BridgeResponse response;
            try
            {
                var request = JsonSerializer.Deserialize<BridgeRequest>(json, JsonDefaults.Options)
                    ?? throw new JsonException("Empty request.");
                response = await _dispatcher.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                response = new BridgeResponse(
                    Guid.Empty,
                    false,
                    Error: new BridgeError(ErrorCode.ProtocolError, exception.Message));
            }

            await SendJsonAsync(socket, response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReceiveTextAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        const int maxMessageBytes = 1024 * 1024;
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidDataException("Only text WebSocket messages are supported.");
            }

            stream.Write(buffer, 0, result.Count);
            if (stream.Length > maxMessageBytes)
            {
                throw new InvalidDataException("WebSocket message exceeds 1 MiB.");
            }
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private static Task SendJsonAsync<T>(
        ClientWebSocket socket,
        T message,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonDefaults.Options));
        return socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }
}
