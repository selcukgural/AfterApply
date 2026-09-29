using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PromoVideo;

/// <summary>A minimal Chrome DevTools Protocol client over one browser-level WebSocket, with
/// flattened page sessions. Only what the recorder needs: commands with replies, and events.</summary>
internal sealed class Cdp : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly ConcurrentDictionary<string, Action<JsonElement>> _handlers = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private Task _receiveLoop = Task.CompletedTask;
    private int _nextId;

    public static async Task<Cdp> ConnectAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        var cdp = new Cdp();
        // Screencast frames are a few hundred KB of base64 each.
        cdp._socket.Options.SetBuffer(1 << 20, 1 << 16);
        await cdp._socket.ConnectAsync(endpoint, cancellationToken);
        cdp._receiveLoop = cdp.ReceiveAsync();
        return cdp;
    }

    /// <summary>Called for every event with this method name (one handler per name).</summary>
    public void On(string method, Action<JsonElement> handler) => _handlers[method] = handler;

    public async Task<JsonElement> SendAsync(string method, object? parameters = null, string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = reply;

        var message = new JsonObject
        {
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters is null ? new JsonObject() : JsonSerializer.SerializeToNode(parameters, parameters.GetType(), SerializerOptions)
        };
        if (sessionId is not null)
        {
            message["sessionId"] = sessionId;
        }

        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await using var registration = timeout.Token.Register(() => reply.TrySetCanceled());
        return await reply.Task;
    }

    private async Task ReceiveAsync()
    {
        var buffer = new byte[1 << 16];
        using var message = new MemoryStream();
        try
        {
            while (!_stop.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(buffer, _stop.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                message.Write(buffer, 0, result.Count);
                if (!result.EndOfMessage)
                {
                    continue;
                }

                Dispatch(message.ToArray());
                message.SetLength(0);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            // Closing down, or Chrome went away; pending commands fail below.
        }

        foreach (var pending in _pending.Values)
        {
            pending.TrySetException(new InvalidOperationException("The browser connection closed."));
        }
    }

    private void Dispatch(byte[] payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (root.TryGetProperty("id", out var idElement))
        {
            if (!_pending.TryRemove(idElement.GetInt32(), out var reply))
            {
                return;
            }

            if (root.TryGetProperty("error", out var error))
            {
                reply.TrySetException(new InvalidOperationException($"CDP error: {error.GetRawText()}"));
            }
            else
            {
                reply.TrySetResult(root.TryGetProperty("result", out var result) ? result.Clone() : default);
            }

            return;
        }

        if (root.TryGetProperty("method", out var method) && _handlers.TryGetValue(method.GetString()!, out var handler))
        {
            // Handlers must not block: the receive loop is what answers every pending command.
            handler(root.TryGetProperty("params", out var parameters) ? parameters.Clone() : default);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Cancelling the pending receive aborts the socket; no close handshake is needed because
        // Chrome is killed right after.
        await _stop.CancelAsync();
        await _receiveLoop;
        _socket.Dispose();
        _sendLock.Dispose();
        _stop.Dispose();
    }
}
