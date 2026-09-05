// Feature: websocket-client-adapter
// WebSocketSimulationSession — ISimulationSession implementation.
// Owns the CellDiffCache, tick-request queue, and WorldUpdate delivery.
// All state mutations happen on the Unity main thread (inside RequestTick / DrainQueue).
// The ReceiveLoop is owned by WebSocketSimulationConnection and shares the same
// ConcurrentQueue<ServerMessage>; this class only dequeues from it.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.WebSocketAdapter
{
    /// <summary>
    /// The live session handle returned to <c>WorldBootstrapper</c> once the
    /// WebSocket handshake completes. Implements <see cref="ISimulationSession"/>
    /// and <see cref="IDisposable"/>.
    ///
    /// <para>Thread-safety contract (Requirements 4.5, 4.6):</para>
    /// <list type="bullet">
    ///   <item>All mutable state (<c>_tickInFlight</c>, <c>_pendingTickCount</c>,
    ///   <c>_cellCache</c>) is touched only from the Unity main thread.</item>
    ///   <item>The shared <see cref="ConcurrentQueue{T}"/> is the sole cross-thread
    ///   primitive; the ReceiveLoop (background Task) only enqueues, while
    ///   <see cref="DrainQueue"/> only dequeues — both lock-free.</item>
    /// </list>
    /// </summary>
    internal sealed class WebSocketSimulationSession : ISimulationSession, IDisposable
    {
        // ── owned references ──────────────────────────────────────────────────
        private readonly ClientWebSocket _socket;
        private readonly CancellationTokenSource _cts;
        private readonly ConcurrentQueue<ServerMessage> _queue;

        // ── CellDiffCache (Req 6.1) ───────────────────────────────────────────
        // Flat row-major array: index = y * Width + x
        private readonly PortCellState[] _cellCache;
        private readonly int _width;
        private readonly int _height;

        // ── tick state (main thread only) ─────────────────────────────────────
        private bool _tickInFlight;      // true while a tick_request has been sent and no response yet
        private int _pendingTickCount;   // additional RequestTick() calls queued while in-flight

        // ── dispose guard ─────────────────────────────────────────────────────
        private int _disposed; // 0 = alive, 1 = disposed — written with Interlocked

        // We hold a reference to the receive loop task so we can await it in Dispose().
        // The task is started by WebSocketSimulationConnection and passed in via the
        // constructor (after IsComplete). For construction simplicity, we do NOT start
        // a second receive loop here; the Connection's receive loop keeps running and
        // enqueuing into the shared queue.
        // However, Dispose() still needs to cancel the CTS so the Connection's loop stops.

        // ── ISimulationSession ────────────────────────────────────────────────
        /// <inheritdoc/>
        public WorldSnapshot InitialSnapshot { get; }

        /// <inheritdoc/>
        public event Action<WorldUpdate> UpdateReceived;

        // ── constructor ───────────────────────────────────────────────────────
        /// <summary>
        /// Constructs the session from the initial <c>state_response</c> snapshot.
        /// All DTO mapping and cache initialisation happens here (main thread, once).
        /// </summary>
        /// <param name="initialData">The parsed <c>state_response</c> snapshot.</param>
        /// <param name="socket">The open <see cref="ClientWebSocket"/> (borrowed from Connection).</param>
        /// <param name="cts">The shared <see cref="CancellationTokenSource"/> (shared with ReceiveLoop).</param>
        /// <param name="queue">The <see cref="ConcurrentQueue{T}"/> shared with the ReceiveLoop.</param>
        public WebSocketSimulationSession(
            WsSimulationSnapshot initialData,
            ClientWebSocket socket,
            CancellationTokenSource cts,
            ConcurrentQueue<ServerMessage> queue)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _cts = cts ?? throw new ArgumentNullException(nameof(cts));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));

            // ── Build InitialSnapshot (Req 5.6, 6.1) ─────────────────────────
            _width = initialData.Width;
            _height = initialData.Height;

            var cells = BuildPortCells(initialData.Cells);
            var agents = BuildPortAgents(initialData.Agents);

            // RefuelStations / DumpSites are not carried in the WebSocket snapshot;
            // expose empty lists (the presentation layer reads them from SessionRequest).
            InitialSnapshot = new WorldSnapshot(
                width: _width,
                height: _height,
                cells: cells,
                agents: agents,
                refuelStations: Array.Empty<PortGridPosition>(),
                dumpSites: Array.Empty<PortGridPosition>(),
                tickIndex: initialData.Tick,
                dischargedTotal: initialData.DischargedTotal,
                isHalted: initialData.IsHalted);

            // ── Initialise CellDiffCache from initial snapshot (Req 6.1) ──────
            _cellCache = new PortCellState[_width * _height];
            foreach (var c in initialData.Cells)
            {
                int idx = c.Y * _width + c.X;
                if (idx >= 0 && idx < _cellCache.Length)
                    _cellCache[idx] = MapCellState(c.State);
            }
        }

        // ── ISimulationSession.RequestTick ────────────────────────────────────
        /// <summary>
        /// Sends a <c>tick_request</c> frame if no tick is currently in flight (Req 5.1, 5.4).
        /// If a tick is already in flight, increments the pending queue counter so that
        /// <see cref="DrainQueue"/> sends the next request after the current response arrives.
        /// </summary>
        public void RequestTick()
        {
            if (_tickInFlight)
            {
                // Queue the request; it will be sent after the current response is processed.
                _pendingTickCount++;
                return;
            }

            SendTickRequest();
            _tickInFlight = true;
        }

        // ── DrainQueue (called by SimulationDriver each frame) ────────────────
        /// <summary>
        /// Dequeues all pending <see cref="ServerMessage"/> values and processes them
        /// on the Unity main thread (Req 4.5, 5.2, 5.5, 6.2).
        /// </summary>
        public void DrainQueue()
        {
            while (_queue.TryDequeue(out ServerMessage msg))
            {
                switch (msg.Kind)
                {
                    case ServerMessageKind.TickResponse:
                        ProcessTickResponse(msg.SnapshotData);
                        break;

                    case ServerMessageKind.ErrorResponse:
                        // Req 5.5: log warning, do NOT raise UpdateReceived.
                        Debug.LogWarning(
                            $"[WebSocketSimulationSession] error_response to tick_request" +
                            $" — code: {msg.ErrorCode}, message: {msg.ErrorMessage}");
                        // Clear in-flight flag so that a queued tick can be sent next.
                        _tickInFlight = false;
                        TrySendPendingTick();
                        break;

                    case ServerMessageKind.Disconnected:
                        // Req: log warning on disconnect.
                        Debug.LogWarning(
                            $"[WebSocketSimulationSession] WebSocket disconnected: {msg.CloseReason}");
                        break;

                    case ServerMessageKind.ParseError:
                        Debug.LogWarning(
                            $"[WebSocketSimulationSession] Parse error: {msg.ParseErrorMessage}");
                        break;

                    case ServerMessageKind.UnknownType:
                        Debug.LogWarning("[WebSocketSimulationSession] Received unknown message type.");
                        break;

                    // StateResponse during the session phase is unexpected but harmless.
                    default:
                        Debug.LogWarning($"[WebSocketSimulationSession] Unexpected message kind: {msg.Kind}");
                        break;
                }
            }
        }

        // ── Dispose (Req 8.1, 8.2, 8.3) ──────────────────────────────────────
        /// <summary>
        /// Cancels the shared <see cref="CancellationTokenSource"/>, waits up to 2 seconds
        /// for the ReceiveLoop to exit (via task completion), then sends a WebSocket close
        /// handshake. Idempotent (Req 8.3).
        /// </summary>
        public void Dispose()
        {
            // Interlocked guard — only the first caller performs teardown (Req 8.3).
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Req 8.2: cancel the ReceiveLoop before closing the socket.
            // Cancelling the CTS causes ReceiveAsync to throw OperationCanceledException,
            // which unblocks the receive loop so it can exit cleanly.
            _cts.Cancel();

            // Wait up to 2 seconds for the receive loop to acknowledge cancellation.
            // We block synchronously here because Dispose() is called from Unity's main
            // thread during scene teardown where async/await is not appropriate.
            // The receive loop exits promptly once the CancellationToken is triggered.
            try
            {
                // Spin-wait up to 2 s for the socket to transition out of Open state,
                // which indicates the ReceiveLoop has unwound (or will shortly).
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (DateTime.UtcNow < deadline &&
                       (_socket.State == WebSocketState.Open ||
                        _socket.State == WebSocketState.Connecting))
                {
                    Thread.Sleep(10);
                }
            }
            catch
            {
                // Ignore; proceed to close regardless.
            }

            // Req 8.1: send WebSocket close handshake.
            if (_socket.State == WebSocketState.Open ||
                _socket.State == WebSocketState.CloseReceived ||
                _socket.State == WebSocketState.CloseSent)
            {
                try
                {
                    _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Session disposed",
                        CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
                }
                catch (Exception)
                {
                    // If close fails, abort — never rethrow from Dispose().
                    _socket.Abort();
                }
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Processes a <c>tick_response</c> snapshot: diffs the CellDiffCache,
        /// maps agents, raises <see cref="UpdateReceived"/> exactly once, then sends the
        /// next queued tick if one is pending (Req 5.2, 5.3, 5.4, 6.2, 6.3).
        /// </summary>
        private void ProcessTickResponse(WsSimulationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[WebSocketSimulationSession] tick_response had null snapshot data.");
                _tickInFlight = false;
                TrySendPendingTick();
                return;
            }

            // ── 1. Diff CellDiffCache (Req 6.2, 6.3) ─────────────────────────
            var changedCells = new List<PortCellSnapshot>();
            foreach (var c in snapshot.Cells)
            {
                int idx = c.Y * _width + c.X;
                // Bounds guard (Req 6.4)
                if (idx < 0 || idx >= _cellCache.Length)
                    continue;

                var newState = MapCellState(c.State);
                if (newState != _cellCache[idx])
                {
                    changedCells.Add(new PortCellSnapshot(
                        new PortGridPosition(c.X, c.Y),
                        newState));
                    _cellCache[idx] = newState; // update cache (Req 6.3)
                }
            }

            // ── 2. Map agents ─────────────────────────────────────────────────
            var agents = BuildPortAgents(snapshot.Agents);

            // ── 3. Raise UpdateReceived exactly once (Req 5.2, 5.3) ──────────
            var update = new WorldUpdate(
                tickIndex: snapshot.Tick,
                changedCells: changedCells,
                agents: agents,
                dischargedTotal: snapshot.DischargedTotal,
                isHalted: snapshot.IsHalted);

            _tickInFlight = false;
            UpdateReceived?.Invoke(update);

            // ── 4. Send next queued tick (Req 5.4) ────────────────────────────
            TrySendPendingTick();
        }

        /// <summary>
        /// If there are pending tick requests, sends one and decrements the counter.
        /// </summary>
        private void TrySendPendingTick()
        {
            if (_pendingTickCount > 0)
            {
                _pendingTickCount--;
                SendTickRequest();
                _tickInFlight = true;
            }
        }

        /// <summary>
        /// Fire-and-forget send of <c>{"type":"tick_request","count":1}</c> (Req 5.1).
        /// </summary>
        private void SendTickRequest()
        {
            const string json = "{\"type\":\"tick_request\",\"count\":1}";
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);
            // Fire-and-forget: we do not await to avoid blocking the main thread.
            _ = _socket.SendAsync(segment, WebSocketMessageType.Text, true, _cts.Token);
        }

        // ── DTO mapping ───────────────────────────────────────────────────────

        /// <summary>
        /// Builds an immutable <see cref="IReadOnlyList{T}"/> of <see cref="PortCellSnapshot"/>
        /// from the raw server cell list.
        /// </summary>
        private static IReadOnlyList<PortCellSnapshot> BuildPortCells(IEnumerable<WsCellSnapshot> cells)
        {
            var result = new List<PortCellSnapshot>();
            foreach (var c in cells)
            {
                result.Add(new PortCellSnapshot(
                    new PortGridPosition(c.X, c.Y),
                    MapCellState(c.State)));
            }
            return result;
        }

        /// <summary>
        /// Builds an immutable <see cref="IReadOnlyList{T}"/> of <see cref="PortAgentSnapshot"/>
        /// from the raw server agent list (Req 7.4, 7.5, 7.7, 7.8).
        /// </summary>
        private static IReadOnlyList<PortAgentSnapshot> BuildPortAgents(IEnumerable<WsAgentSnapshot> agents)
        {
            var result = new List<PortAgentSnapshot>();
            foreach (var a in agents)
            {
                result.Add(new PortAgentSnapshot(
                    id: a.Id,
                    role: MapRole(a.Role),
                    position: new PortGridPosition(a.X, a.Y),
                    currentState: MapStateId(a.State),
                    fuel: a.Fuel,
                    load: a.Load,
                    maxLoad: a.MaxLoad ?? 0,                            // Req 7.7, 10.5
                    pathInvalidatedThisTick: a.PathInvalidatedThisTick ?? false, // Req 7.8, 10.5
                    meetingPoint: MapMeetingPoint(a.MeetingPointX, a.MeetingPointY))); // Req 7.8, 10.4
            }
            return result;
        }

        /// <summary>
        /// Maps a server role string to <see cref="PortAgentRole"/> (Req 7.4).
        /// Unknown values fall back to <see cref="PortAgentRole.Harvester"/> with a warning.
        /// </summary>
        private static PortAgentRole MapRole(string role)
        {
            return role switch
            {
                "Harvester" => PortAgentRole.Harvester,
                "Tractor"   => PortAgentRole.Tractor,
                _           => LogAndReturnDefaultRole(role)
            };
        }

        private static PortAgentRole LogAndReturnDefaultRole(string role)
        {
            Debug.LogWarning($"[WebSocketSimulationSession] Unknown agent role '{role}'; defaulting to Harvester.");
            return PortAgentRole.Harvester;
        }

        /// <summary>
        /// Maps a server state string to <see cref="PortStateId"/> using case-insensitive
        /// <see cref="Enum.TryParse{T}"/>; falls back to <see cref="PortStateId.Idle"/> (Req 7.5).
        /// </summary>
        private static PortStateId MapStateId(string state)
        {
            if (Enum.TryParse<PortStateId>(state, ignoreCase: true, out var result))
                return result;

            Debug.LogWarning($"[WebSocketSimulationSession] Unknown agent state '{state}'; defaulting to Idle.");
            return PortStateId.Idle;
        }

        /// <summary>
        /// Maps a server cell state string to <see cref="PortCellState"/> (Req 7.6).
        /// Unknown values fall back to <see cref="PortCellState.Empty"/>.
        /// </summary>
        private static PortCellState MapCellState(string state)
        {
            return state switch
            {
                "Crop"      => PortCellState.Crop,
                "Empty"     => PortCellState.Empty,
                "Blocked"   => PortCellState.Blocked,
                "Harvested" => PortCellState.Harvested,
                _           => PortCellState.Empty
            };
        }

        /// <summary>
        /// Reconstructs a nullable <see cref="PortGridPosition"/> from optional X/Y coords (Req 7.8, 10.4).
        /// Returns <c>null</c> when either coordinate is absent.
        /// </summary>
        private static PortGridPosition? MapMeetingPoint(int? x, int? y)
        {
            if (x.HasValue && y.HasValue)
                return new PortGridPosition(x.Value, y.Value);
            return null;
        }
    }
}
