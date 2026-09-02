// Feature: websocket-client-adapter
// WebSocketSimulationConnection — ISimulationConnection implementation.
// Drives a four-state machine entirely from Poll() on the Unity main thread.
// The ReceiveLoop runs on a background Task and only touches the ConcurrentQueue.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.WebSocketAdapter
{
    /// <summary>
    /// Internal state of the four-state connection machine.
    /// </summary>
    internal enum ConnectionState
    {
        Idle,
        Connecting,
        Handshaking,
        Complete,
        Failed
    }

    /// <summary>
    /// Asynchronous WebSocket connection handle. All state mutations happen on the
    /// Unity main thread inside <see cref="Poll"/>; the background <see cref="_receiveLoop"/>
    /// only enqueues <see cref="ServerMessage"/> values into the lock-free queue.
    /// </summary>
    internal sealed class WebSocketSimulationConnection : ISimulationConnection, IDisposable
    {
        // ── fields ────────────────────────────────────────────────────────────
        private readonly ClientWebSocket _socket;
        private readonly ConcurrentQueue<ServerMessage> _queue;
        private readonly CancellationTokenSource _cts;
        private readonly string _host;
        private readonly int _port;
        private readonly float _connectionTimeoutSeconds;
        private readonly bool _reconnectOnDrop;
        private readonly SessionRequest _request;

        private Task _connectTask;
        private Task _receiveLoop;
        private ConnectionState _state = ConnectionState.Idle;

        /// <summary>
        /// Wall-clock time (Time.realtimeSinceStartup) at which first Poll() was called,
        /// used together with _connectionTimeoutSeconds to detect a handshake timeout.
        /// Set to -1 until first Poll().
        /// </summary>
        private float _connectStartTime = -1f;

        private int _disposed; // Interlocked guard; 0 = not disposed, 1 = disposed

        private readonly List<string> _warnings = new List<string>();

        // ── ISimulationConnection ─────────────────────────────────────────────
        public bool IsComplete => _state == ConnectionState.Complete || _state == ConnectionState.Failed;
        public bool Failed => _state == ConnectionState.Failed;
        public string Error { get; private set; }
        public IReadOnlyList<string> Warnings => _warnings;
        public ISimulationSession Session { get; private set; }

        // ── constructor ───────────────────────────────────────────────────────
        /// <summary>
        /// Constructs the connection handle. No socket is opened and no task is started
        /// until the first <see cref="Poll"/> call.
        /// </summary>
        public WebSocketSimulationConnection(
            string host,
            int port,
            float connectionTimeoutSeconds,
            bool reconnectOnDrop,
            SessionRequest request)
        {
            _host = host;
            _port = port;
            _connectionTimeoutSeconds = connectionTimeoutSeconds;
            _reconnectOnDrop = reconnectOnDrop;
            _request = request;

            _socket = new ClientWebSocket();
            _queue = new ConcurrentQueue<ServerMessage>();
            _cts = new CancellationTokenSource();
        }

        // ── Poll() state machine ──────────────────────────────────────────────
        /// <summary>
        /// Advances the connection state machine. Called once per frame from the Unity
        /// main thread by <c>WorldBootstrapper</c> until <see cref="IsComplete"/>.
        /// </summary>
        public void Poll()
        {
            switch (_state)
            {
                case ConnectionState.Idle:
                    TransitionToConnecting();
                    break;

                case ConnectionState.Connecting:
                    CheckConnecting();
                    break;

                case ConnectionState.Handshaking:
                    DrainHandshakeQueue();
                    break;

                case ConnectionState.Complete:
                case ConnectionState.Failed:
                    // Idempotent — return immediately.
                    return;
            }
        }

        // ── Idle → Connecting ─────────────────────────────────────────────────
        private void TransitionToConnecting()
        {
            _connectStartTime = Time.realtimeSinceStartup;

            var uri = new Uri($"ws://{_host}:{_port}/");
            // Fire-and-forget: do NOT await here; we store the task and check it next Poll().
            _connectTask = _socket.ConnectAsync(uri, _cts.Token);
            _state = ConnectionState.Connecting;
        }

        // ── Connecting → Handshaking (or Failed) ──────────────────────────────
        private void CheckConnecting()
        {
            // Check timeout even in Connecting state.
            if (IsTimedOut())
            {
                SetFailed($"Connection timed out after {_connectionTimeoutSeconds}s waiting for state_response");
                return;
            }

            if (!_connectTask.IsCompleted)
                return;

            if (_connectTask.IsFaulted)
            {
                var msg = _connectTask.Exception?.GetBaseException().Message ?? "Unknown error";
                SetFailed($"WebSocket connect error: {msg}");
                return;
            }

            if (_connectTask.IsCanceled)
            {
                SetFailed("WebSocket connect error: Connection was cancelled");
                return;
            }

            // ConnectAsync succeeded — send state_request and start receive loop.
            SendStateRequest();
            _receiveLoop = ReceiveLoopAsync();
            _state = ConnectionState.Handshaking;
        }

        // ── Handshaking ───────────────────────────────────────────────────────
        private void DrainHandshakeQueue()
        {
            // Check timeout.
            if (IsTimedOut())
            {
                SetFailed($"Connection timed out after {_connectionTimeoutSeconds}s waiting for state_response");
                return;
            }

            // Drain the queue looking for state_response, error, or disconnect.
            while (_queue.TryDequeue(out ServerMessage msg))
            {
                switch (msg.Kind)
                {
                    case ServerMessageKind.StateResponse:
                        // Handshake complete — construct the session.
                        var snapshot = msg.SnapshotData;
                        Session = new WebSocketSimulationSession(snapshot, _socket, _cts, _queue);
                        _state = ConnectionState.Complete;
                        return;

                    case ServerMessageKind.ErrorResponse:
                        SetFailed($"Server error [{msg.ErrorCode}]: {msg.ErrorMessage}");
                        return;

                    case ServerMessageKind.Disconnected:
                        SetFailed($"WebSocket closed unexpectedly: {msg.CloseReason}");
                        return;

                    case ServerMessageKind.ParseError:
                        // Non-fatal during handshake — log and continue waiting.
                        Debug.LogWarning($"[WebSocketSimulationConnection] Parse error during handshake: {msg.ParseErrorMessage}");
                        break;

                    case ServerMessageKind.UnknownType:
                        Debug.LogWarning("[WebSocketSimulationConnection] Received unknown message type during handshake.");
                        break;

                    // TickResponse before handshake completes is unexpected but non-fatal.
                    default:
                        Debug.LogWarning($"[WebSocketSimulationConnection] Unexpected message kind during handshake: {msg.Kind}");
                        break;
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private bool IsTimedOut()
        {
            return _connectStartTime >= 0f &&
                   Time.realtimeSinceStartup - _connectStartTime >= _connectionTimeoutSeconds;
        }

        private void SetFailed(string error)
        {
            Error = error;
            _state = ConnectionState.Failed;
            // Session must remain null when Failed (invariant).
        }

        private void SendStateRequest()
        {
            // Build and send the state_request JSON frame.
            // We fire-and-forget using ConfigureAwait(false); exceptions are silently swallowed
            // here since the receive loop will detect the broken state and enqueue a Disconnected.
            var json = "{\"type\":\"state_request\"}";
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);
            // SendAsync is safe to call from the main thread; it returns immediately if the
            // socket is in a healthy state. We do not await to avoid blocking Poll().
            _ = _socket.SendAsync(segment, WebSocketMessageType.Text, true, _cts.Token);
        }

        // ── ReceiveLoop ───────────────────────────────────────────────────────
        /// <summary>
        /// Background task: reads text frames from the WebSocket, parses them, and
        /// enqueues the resulting <see cref="ServerMessage"/> for main-thread processing.
        /// Exits on cancellation, WebSocket close, or any exception, in all cases
        /// enqueuing a <see cref="ServerMessageKind.Disconnected"/> sentinel.
        /// </summary>
        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[65536]; // 64 KB receive buffer
            var segment = new ArraySegment<byte>(buffer);

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // Accumulate a complete message (may span multiple ReceiveAsync calls).
                    var accumulatedBytes = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _socket.ReceiveAsync(segment, _cts.Token).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _queue.Enqueue(ServerMessage.ForDisconnected(
                                result.CloseStatusDescription ?? "Server closed the connection"));
                            return;
                        }

                        accumulatedBytes.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    // We only handle Text frames.
                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;

                    var json = Encoding.UTF8.GetString(accumulatedBytes.ToArray());
                    var message = WebSocketMessageParser.Parse(json);
                    _queue.Enqueue(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected on Dispose(); enqueue a sentinel so the
                // main thread can see the socket went away.
                _queue.Enqueue(ServerMessage.ForDisconnected("ReceiveLoop cancelled"));
            }
            catch (WebSocketException ex)
            {
                _queue.Enqueue(ServerMessage.ForDisconnected($"WebSocketException: {ex.Message}"));
            }
            catch (Exception ex)
            {
                _queue.Enqueue(ServerMessage.ForDisconnected($"ReceiveLoop error: {ex.Message}"));
            }
        }

        // ── Dispose ───────────────────────────────────────────────────────────
        /// <summary>
        /// Cancels the receive loop, waits up to 2 seconds for it to exit, then
        /// closes the WebSocket. Idempotent via <see cref="Interlocked"/> guard.
        /// </summary>
        public void Dispose()
        {
            // Interlocked guard — only the first caller does the teardown.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Signal background task to stop.
            _cts.Cancel();

            // If Dispose() is called before the handshake completes, mark as failed.
            if (!IsComplete)
            {
                Error = "Disposed before connection completed";
                _state = ConnectionState.Failed;
            }

            // Await the receive loop with a 2-second timeout.
            if (_receiveLoop != null)
            {
                try
                {
                    // Block synchronously for up to 2 seconds; we're on the main thread
                    // during Unity scene teardown and cannot use await here.
                    _receiveLoop.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                    // Ignore exceptions from the cancelled task.
                }
                catch (Exception)
                {
                    // Ignore.
                }
            }

            // Close the WebSocket gracefully.
            if (_socket.State == WebSocketState.Open ||
                _socket.State == WebSocketState.CloseReceived ||
                _socket.State == WebSocketState.CloseSent)
            {
                try
                {
                    _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection disposed",
                        CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
                }
                catch (Exception)
                {
                    // If close fails, abort.
                    _socket.Abort();
                }
            }

            _socket.Dispose();
            _cts.Dispose();
        }
    }
}
