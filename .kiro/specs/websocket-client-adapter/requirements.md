# Requirements Document

## Introduction

This feature adds a **WebSocket client adapter** to the AgroAgents-RetoJohnDeere Unity 6 project.
The adapter implements the three port interfaces (`ISimulationConnector`, `ISimulationConnection`,
`ISimulationSession`) defined in `AgroAgents.SimulationPort`, connecting the Unity presentation
layer to the remote Python `AgenticModel` WebSocket server (`ws://localhost:8765/` by default).

It replaces `InMemorySimulationConnector` as the selectable connector in `WorldBootstrapper`'s
`[SerializeReference]` field, requiring no changes to `WorldBootstrapper` or any presentation code.
A companion server-side change to `AgenticModel` extends the transport DTOs so that
`MaxLoad`, `PathInvalidatedThisTick`, and `MeetingPoint` can eventually be carried over the wire.

---

## Glossary

- **Adapter**: The Unity-side WebSocket client assembly (`AgroAgents.WebSocketAdapter`).
- **AgenticModel**: The remote Python WebSocket simulation server (project root `AgenticModel/`).
- **CellDiffCache**: The adapter's internal flat array of `PortCellState` used to compute `ChangedCells` by diffing successive server snapshots.
- **Connector**: `WebSocketSimulationConnector` — the `[Serializable]` class assigned to `WorldBootstrapper.connector`.
- **Connection**: `WebSocketSimulationConnection` — the in-flight or completed connection handle polled by `WorldBootstrapper.Update()`.
- **MainThreadQueue**: A `ConcurrentQueue<ServerMessage>` enqueued by the WS receive loop (background thread) and drained on `Poll()` (Unity main thread).
- **Port**: The `AgroAgents.SimulationPort` assembly containing only interfaces and pure DTOs.
- **ReceiveLoop**: The background `Task` that reads text frames from the WebSocket and pushes them into `MainThreadQueue`.
- **ServerMessage**: A discriminated union (or tagged wrapper) representing one parsed server frame: `state_response`, `tick_response`, or `error_response`.
- **Session**: `WebSocketSimulationSession` — the live handle returned once the handshake completes.
- **SimulationSnapshot**: The server-side DTO (`HarvestingCore.Transport.Dto.SimulationSnapshot`) carrying the full grid and agent list every tick.
- **WorldBootstrapper**: The Unity MonoBehaviour in `AgroAgents.Presentation` that owns the `[SerializeReference] ISimulationConnector connector` field.
- **WorldUpdate**: The port DTO raised via `UpdateReceived`; its `ChangedCells` is computed client-side from a diff.

---

## Requirements

### Requirement 1: Connector Authoring and Inspector Integration

**User Story:** As a Unity developer, I want to assign a `WebSocketSimulationConnector` to `WorldBootstrapper` in the Inspector without writing any code, so that I can switch from the in-memory adapter to the live server by changing a single field.

#### Acceptance Criteria

1. THE `WebSocketSimulationConnector` SHALL be decorated with `[Serializable]` and implement `ISimulationConnector`, enabling it to be assigned inline to `WorldBootstrapper`'s `[SerializeReference] ISimulationConnector connector` field via the Unity Inspector.
2. THE `WebSocketSimulationConnector` SHALL expose a serialized `string` field `host` (default `"localhost"`) representing the server hostname or IP address.
3. THE `WebSocketSimulationConnector` SHALL expose a serialized `int` field `port` (default `8765`) representing the server TCP port, constrained to the range [1, 65535].
4. THE `WebSocketSimulationConnector` SHALL expose a serialized `float` field `connectionTimeoutSeconds` (default `10.0`) representing the maximum time in seconds to wait for the initial `state_response` before failing.
5. THE `WebSocketSimulationConnector` SHALL expose a serialized `bool` field `reconnectOnDrop` (default `false`). WHERE `reconnectOnDrop` is `false`, THE `Adapter` SHALL treat a mid-session WebSocket disconnection as a fatal session error and not attempt to reconnect.
6. WHEN `Connect(SessionRequest request)` is called, THE `Connector` SHALL construct and return a new `WebSocketSimulationConnection` without opening any socket or starting any background task.

---

### Requirement 2: Connection Lifecycle and Handshake

**User Story:** As the Unity runtime, I want the WebSocket handshake to proceed asynchronously across `Poll()` frames so that the main thread is never blocked during connection setup.

#### Acceptance Criteria

1. WHEN `Poll()` is called for the first time on a `WebSocketSimulationConnection`, THE `Connection` SHALL initiate a non-blocking WebSocket TCP handshake to `ws://{host}:{port}/` by starting the `ClientWebSocket.ConnectAsync` task without awaiting it on the calling thread.
2. WHEN the underlying `ClientWebSocket` transitions to the `Open` state, THE `Connection` SHALL enqueue a `state_request` JSON frame for transmission and start the `ReceiveLoop` background task.
3. WHEN a `state_response` message is dequeued from `MainThreadQueue` during `Poll()`, THE `Connection` SHALL set `IsComplete = true`, `Failed = false`, and assign `Session` to a new `WebSocketSimulationSession` built from the received `SimulationSnapshot`.
4. IF `Poll()` is called after `IsComplete` is already `true`, THEN THE `Connection` SHALL return immediately without side effects.
5. THE `Connection` SHALL maintain the invariant: `Session` is non-null if and only if `IsComplete == true` and `Failed == false`.
6. THE `Connection` SHALL maintain the invariant: once `IsComplete` is set to `true`, it SHALL NOT be reset to `false`.
7. WHILE the connection is in the `Connecting` state, THE `Connection` SHALL maintain `IsComplete = false` and `Session = null`.

---

### Requirement 3: Timeout and Failure Handling

**User Story:** As a developer, I want clear failure signals when the server is unreachable or returns an error, so that `WorldBootstrapper` can surface a readable error message.

#### Acceptance Criteria

1. WHEN `connectionTimeoutSeconds` elapses after `Connect()` is called without a `state_response` being received, THE `Connection` SHALL set `Failed = true`, `Error = "Connection timed out after {N}s waiting for state_response"`, and `IsComplete = true`.
2. WHEN the `ClientWebSocket.ConnectAsync` task throws any exception (e.g., server not running, DNS failure), THE `Connection` SHALL set `Failed = true`, `Error = "WebSocket connect error: {exceptionMessage}"`, and `IsComplete = true`.
3. WHEN an `error_response` message is received from the server during the handshake phase (before `IsComplete`), THE `Connection` SHALL set `Failed = true`, `Error = "Server error [{code}]: {message}"`, and `IsComplete = true`.
4. WHEN the `ReceiveLoop` encounters an unrecoverable WebSocket error or remote close before the handshake completes, THE `Connection` SHALL set `Failed = true`, `Error = "WebSocket closed unexpectedly: {reason}"`, and `IsComplete = true`.
5. IF `Failed` is `true`, THEN THE `Connection` SHALL maintain `IsComplete = true` (failed is a terminal sub-state of complete).
6. THE `Connection` SHALL expose `IReadOnlyList<string> Warnings` as an empty list unless non-fatal issues are encountered during handshake (e.g., server sends unknown extra fields).

---

### Requirement 4: Thread-Safety — Main-Thread Bridge

**User Story:** As a developer integrating with Unity's single-threaded rendering model, I want all state mutations of `ISimulationConnection` and `ISimulationSession` to occur on the Unity main thread, so that presentation code can safely read them from `Update()`.

#### Acceptance Criteria

1. THE `ReceiveLoop` SHALL run on a background thread and SHALL NOT write to any `Connection` or `Session` field directly; it SHALL only enqueue `ServerMessage` values into `MainThreadQueue`.
2. WHEN `Poll()` is called, THE `Connection` SHALL drain `MainThreadQueue` on the calling thread, processing all pending messages before returning.
3. THE `MainThreadQueue` SHALL be a `System.Collections.Concurrent.ConcurrentQueue<ServerMessage>` to allow lock-free enqueue from the receive thread and single-consumer dequeue from the main thread.
4. THE `Adapter` SHALL NOT use `lock`, `Monitor`, or `Mutex` on the hot path of `Poll()` or `UpdateReceived`; only the `ConcurrentQueue` is permitted as the cross-thread synchronisation primitive.
5. THE `Session` fields (`InitialSnapshot`, pending tick state) SHALL only be mutated from the Unity main thread (inside `Poll()` or `RequestTick()`).
6. IF `Dispose()` is called from any thread, THEN THE `Session` SHALL signal the `ReceiveLoop` to stop via a `CancellationToken` and SHALL await its completion before releasing the WebSocket, tolerating a cancellation wait of up to 2 seconds before forcibly aborting.

---

### Requirement 5: Session — Tick Request and Update Delivery

**User Story:** As `SimulationDriver`, I want to call `RequestTick()` and receive exactly one `UpdateReceived` event in response, so that the presentation layer stays in sync with the simulation.

#### Acceptance Criteria

1. WHEN `RequestTick()` is called, THE `Session` SHALL send a `tick_request` JSON frame with `{ "type": "tick_request", "count": 1 }` over the open WebSocket.
2. WHEN a `tick_response` message is dequeued during `Poll()` and a tick request is pending, THE `Session` SHALL raise `UpdateReceived` exactly once with a `WorldUpdate` built from the response snapshot.
3. THE `Session` SHALL maintain the invariant: `UpdateReceived` is raised at most once per `RequestTick()` call — never zero times (provided the session is not disposed before the response arrives) and never more than once.
4. WHEN `RequestTick()` is called while a previous tick response is still in flight (pending), THE `Session` SHALL queue the second request and send it only after the first `tick_response` is received and processed, preserving request-response ordering.
5. WHEN an `error_response` is received in reply to a `tick_request`, THE `Session` SHALL NOT raise `UpdateReceived`; it SHALL instead emit a `Debug.LogWarning` with the error code and message.
6. THE `Session` SHALL expose `WorldSnapshot InitialSnapshot` which is immutable after construction and equals the snapshot received from the initial `state_response`.

---

### Requirement 6: ChangedCells Client-Side Diff

**User Story:** As the presentation layer, I want `WorldUpdate.ChangedCells` to contain only the cells whose state changed since the previous tick, so that `GridView` can efficiently update only the cells that need it.

#### Acceptance Criteria

1. THE `Session` SHALL maintain a `CellDiffCache` — a flat `PortCellState[]` array of length `Width × Height` — initialised from the `state_response` snapshot at handshake time.
2. WHEN a `tick_response` is processed, THE `Session` SHALL compare each cell's state in the received snapshot against the corresponding entry in `CellDiffCache` and collect all positions where the state differs into `ChangedCells`.
3. THE `Session` SHALL update `CellDiffCache` for every cell in the received snapshot after computing the diff, so that subsequent diffs are against the latest known state.
4. THE `WorldUpdate.ChangedCells` collection SHALL be a subset of all grid positions (i.e., no position outside `[0, Width) × [0, Height)` SHALL appear in `ChangedCells`).
5. WHEN two consecutive snapshots are identical (no cell state changed), THE `Session` SHALL produce a `WorldUpdate` with an empty `ChangedCells` list.
6. WHEN the same full snapshot is applied twice (idempotence), THE `Session` SHALL produce an empty `ChangedCells` on the second application.

---

### Requirement 7: DTO Mapping — Server ↔ Port

**User Story:** As a developer, I want the adapter to translate server DTOs to port DTOs without leaking server types into the presentation layer, so that the port boundary remains clean.

#### Acceptance Criteria

1. THE `WebSocketMessageParser` SHALL deserialize JSON text frames from the server into one of `StateResponse`, `TickResponse`, or `ErrorResponse` by inspecting the `"type"` field.
2. IF a received frame has an unrecognised `"type"` value, THEN THE `WebSocketMessageParser` SHALL return a null/unknown result and the `ReceiveLoop` SHALL enqueue an error entry that causes a `Debug.LogWarning` on the main thread without crashing.
3. IF a received frame is malformed JSON (cannot be parsed), THEN THE `WebSocketMessageParser` SHALL return a parse error result and the `ReceiveLoop` SHALL enqueue it; on the main thread, `Poll()` SHALL log the error and continue without setting `Failed`.
4. THE `Adapter` SHALL map `AgentSnapshot.Role` string values (`"Harvester"`, `"Tractor"`) to `PortAgentRole` enum values.
5. THE `Adapter` SHALL map `AgentSnapshot.State` string values to `PortStateId` enum values using a case-insensitive lookup; IF a state string has no matching `PortStateId`, THEN THE `Adapter` SHALL use `PortStateId.Idle` as a fallback and log a warning.
6. THE `Adapter` SHALL map `CellSnapshot.State` string values (`"Crop"`, `"Empty"`, `"Blocked"`, `"Harvested"`) to `PortCellState` enum values; `CellSnapshot.OwnerId` SHALL be ignored.
7. THE `Adapter` SHALL set `PortAgentSnapshot.MaxLoad = 0` until the server sends a `maxLoad` field (see Requirement 10).
8. THE `Adapter` SHALL set `PortAgentSnapshot.PathInvalidatedThisTick = false` and `PortAgentSnapshot.MeetingPoint = null` until the server sends those fields (see Requirement 10).
9. THE `WebSocketMessageParser` SHALL have no dependency on any Unity type (`UnityEngine`, `MonoBehaviour`, etc.) and SHALL be testable in a plain `dotnet test` host.

---

### Requirement 8: Session Disposal and WebSocket Shutdown

**User Story:** As the Unity runtime, I want the WebSocket connection to be closed cleanly when the scene is torn down, so that the server is notified and resources are freed.

#### Acceptance Criteria

1. WHEN `Dispose()` is called on a `WebSocketSimulationSession`, THE `Session` SHALL initiate a WebSocket close handshake by calling `ClientWebSocket.CloseAsync(Normal, "Session disposed", ...)`.
2. WHEN `Dispose()` is called while the `ReceiveLoop` task is running, THE `Session` SHALL cancel the `ReceiveLoop`'s `CancellationToken` before calling `CloseAsync`.
3. THE `Session` SHALL be idempotent with respect to `Dispose()`: calling it more than once SHALL have no additional effect and SHALL NOT throw.
4. WHEN `Dispose()` is called before the handshake completes (`IsComplete == false`), THE `Connection` SHALL cancel the pending connect task and set `Failed = true`, `Error = "Disposed before connection completed"`, `IsComplete = true`.

---

### Requirement 9: Assembly Definition and Dependency Isolation

**User Story:** As a developer, I want the WebSocket adapter in its own assembly definition so that dependency boundaries are enforced by the Unity build system.

#### Acceptance Criteria

1. THE `Adapter` SHALL have an `.asmdef` file at `Assets/Scripts/Adapters/WebSocket/AgroAgents.WebSocketAdapter.asmdef` with `name = "AgroAgents.WebSocketAdapter"`.
2. THE `Adapter` assembly SHALL reference `AgroAgents.SimulationPort` and SHALL NOT reference `AgroAgents.InMemoryAdapter`, `HarvestingCore`, or any `AgenticModel` assembly.
3. THE `Adapter` assembly SHALL NOT set `noEngineReferences = true`; Unity engine types (`Debug.Log`, etc.) are permitted in non-parser files.
4. THE `WebSocketMessageParser` class SHALL be compilable as a standalone `netstandard2.1` class library (no Unity engine types), enabling it to be tested with `dotnet test`.

---

### Requirement 10: AgenticModel Server Protocol Extension

**User Story:** As a developer, I want the server's `AgentSnapshot` DTO to carry `maxLoad`, `pathInvalidatedThisTick`, and `meetingPoint` so that the adapter can expose accurate values to the presentation layer.

#### Acceptance Criteria

1. THE `AgentSnapshot` class at `AgenticModel/src/HarvestingCore.Transport/Dto/AgentSnapshot.cs` SHALL be extended with:
   - `int MaxLoad` serialized as `"maxLoad"`
   - `bool PathInvalidatedThisTick` serialized as `"pathInvalidatedThisTick"`
   - `int? MeetingPointX` serialized as `"meetingPointX"` (nullable)
   - `int? MeetingPointY` serialized as `"meetingPointY"` (nullable)
2. THE `SimulationHostAdapter.GetSnapshot()` method SHALL populate the new fields from `Agent.MaxLoad`, `Agent.PathInvalidatedThisTick`, and `Agent.MeetingPoint` respectively.
3. WHEN `MeetingPoint` is null on the `Agent`, THE `SimulationHostAdapter` SHALL serialize `meetingPointX` and `meetingPointY` as JSON `null`.
4. WHEN the new fields are present in a server response, THE `Adapter` SHALL use `maxLoad` instead of the default `0`, `pathInvalidatedThisTick` directly, and reconstruct `PortGridPosition?` from `meetingPointX` / `meetingPointY`.
5. WHEN the new fields are absent (legacy server or pre-extension server), THE `Adapter` SHALL gracefully fall back to `MaxLoad = 0`, `PathInvalidatedThisTick = false`, `MeetingPoint = null` without throwing.
6. FOR ALL valid `AgentSnapshot` objects, serializing then deserializing SHALL produce an object with equal field values (round-trip property).

---

### Requirement 11: Non-Goals

**User Story:** As a project lead, I want the scope of this adapter bounded so that complexity is contained and future features are separate decisions.

#### Acceptance Criteria

1. THE `Adapter` SHALL NOT implement automatic reconnection logic beyond the single `reconnectOnDrop` flag that governs whether a mid-session disconnect is treated as fatal.
2. THE `Adapter` SHALL NOT implement multi-tick batching: `RequestTick()` always sends `count: 1`, one request at a time.
3. THE `Adapter` SHALL NOT modify any class in `HarvestingCore` (the simulation logic layer); only `HarvestingCore.Transport.Dto.AgentSnapshot` and `HarvestingCore.Host.SimulationHostAdapter` are permitted to change.
4. THE `Adapter` SHALL NOT add any retry loop, exponential back-off, or reconnect delay; a failed connection is terminal for the lifetime of the `ISimulationConnection` object.
5. THE `Adapter` SHALL NOT support sending `tick_request` with `count > 1`.
