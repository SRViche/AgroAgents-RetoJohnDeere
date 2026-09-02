# Implementation Plan: WebSocket Client Adapter

## Overview

Implements `ISimulationConnector` / `ISimulationConnection` / `ISimulationSession` over a real
WebSocket in C#. The work spans two repos: **AgenticModel** (server-side DTO extension first,
since it unblocks the parser's type definitions) and **AgroAgents-RetoJohnDeere** (Unity-side
adapter assembly). Tasks are ordered by dependency: server protocol → assembly skeleton →
Unity-free parser → connector → connection state machine → session tick loop → integration smoke
test.

## Tasks

- [x] 1. Extend AgenticModel server protocol (unblocks everything else)
  - [x] 1.1 Add four new fields to `AgentSnapshot`
    - Open `AgenticModel/src/HarvestingCore.Transport/Dto/AgentSnapshot.cs`
    - Add `[JsonPropertyName("maxLoad")] public int MaxLoad { get; set; }`
    - Add `[JsonPropertyName("pathInvalidatedThisTick")] public bool PathInvalidatedThisTick { get; set; }`
    - Add `[JsonPropertyName("meetingPointX")] public int? MeetingPointX { get; set; }`
    - Add `[JsonPropertyName("meetingPointY")] public int? MeetingPointY { get; set; }`
    - _Requirements: 10.1_

  - [x] 1.2 Populate new fields in `SimulationHostAdapter.GetSnapshot()`
    - Open `AgenticModel/src/HarvestingCore.Host/SimulationHostAdapter.cs`
    - In the `foreach (Agent agent …)` loop, set `MaxLoad`, `PathInvalidatedThisTick`, `MeetingPointX`, `MeetingPointY` from the corresponding `Agent` properties
    - When `Agent.MeetingPoint` is null, both coordinate fields must serialize as JSON `null`
    - _Requirements: 10.2, 10.3_

  - [ ]* 1.3 Write property tests for `AgentSnapshot` round-trip and `SimulationHostAdapter` population
    - Add test file `AgenticModel/tests/HarvestingCore.Transport.Tests/AgentSnapshotExtensionPropertyTests.cs`
    - **Property 19: AgentSnapshot serialization round-trip** — generate random `AgentSnapshot` instances (all fields including the four new ones), serialize with `System.Text.Json`, deserialize, assert field equality
    - **Validates: Requirements 10.6**
    - **Property 20: SimulationHostAdapter populates new fields from Agent** — generate random `Agent` values with arbitrary `MaxLoad`, `PathInvalidatedThisTick`, `MeetingPoint`; call `GetSnapshot()`; assert snapshot fields match
    - **Validates: Requirements 10.2**
    - Use FsCheck.Xunit (already in the `.csproj`); minimum 100 iterations per property

- [x] 2. Create assembly definition and `ServerMessage` discriminated union
  - [x] 2.1 Create `AgroAgents.WebSocketAdapter.asmdef`
    - Create `Assets/Scripts/Adapters/WebSocket/AgroAgents.WebSocketAdapter.asmdef`
    - Set `name = "AgroAgents.WebSocketAdapter"`, `references = ["AgroAgents.SimulationPort"]`, `noEngineReferences = false`
    - _Requirements: 9.1, 9.2, 9.3_

  - [x] 2.2 Create `ServerMessage.cs` with discriminated union and `ServerMessageKind` enum
    - Create `Assets/Scripts/Adapters/WebSocket/ServerMessage.cs`
    - Define `internal enum ServerMessageKind { StateResponse, TickResponse, ErrorResponse, ParseError, UnknownType, Disconnected }`
    - Define `internal sealed class ServerMessage` with read-only properties: `Kind`, `SnapshotData` (`WsSimulationSnapshot?`), `ErrorCode`, `ErrorMessage`, `ParseErrorMessage`, `CloseReason`
    - Add static factory constructors: `ForStateResponse(WsSimulationSnapshot)`, `ForTickResponse(WsSimulationSnapshot)`, `ForError(string code, string msg)`, `ForParseError(string msg)`, `ForUnknownType()`, `ForDisconnected(string reason)`
    - _Requirements: 7.1, 7.2, 7.3_

- [x] 3. Implement `WebSocketMessageParser` (Unity-free)
  - [x] 3.1 Create `WebSocketMessageParser.cs` with private DTOs and `Parse(string json)` entry point
    - Create `Assets/Scripts/Adapters/WebSocket/WebSocketMessageParser.cs`
    - No `using UnityEngine` — this file must compile as `netstandard2.1`
    - Define private DTOs: `WsSimulationSnapshot`, `WsAgentSnapshot` (with four nullable optional fields), `WsCellSnapshot`
    - Implement `internal static ServerMessage Parse(string json)`: use `JsonDocument` to peek `"type"`, then deserialize into the matching DTO; return `ParseError` for `JsonException`; return `UnknownType` for unrecognised type strings; never throw
    - _Requirements: 7.1, 7.2, 7.3, 9.4_

  - [x] 3.2 Create `AgenticModel/tests/HarvestingCore.WebSocketAdapter.Tests/` dotnet test project
    - Create `AgenticModel/tests/HarvestingCore.WebSocketAdapter.Tests/HarvestingCore.WebSocketAdapter.Tests.csproj`
    - Target `net10.0`, reference `xunit`, `FsCheck.Xunit` (same versions as `HarvestingCore.Transport.Tests`), and `Microsoft.NET.Test.Sdk`
    - Add the `WebSocketMessageParser.cs` file as a shared compile item (no Unity project reference) so it can be exercised without a Unity process
    - _Requirements: 9.4_

  - [ ]* 3.3 Write property tests for `WebSocketMessageParser` and string-to-enum mappings
    - Add `AgenticModel/tests/HarvestingCore.WebSocketAdapter.Tests/MessageParserPropertyTests.cs`
    - **Property 15: Parser returns correct kind for known type fields** — generate well-formed JSON objects whose `"type"` is `"state_response"`, `"tick_response"`, or `"error_response"`; assert `Kind` matches
    - **Validates: Requirements 7.1**
    - **Property 16: Parser returns ParseError for malformed JSON** — generate arbitrary non-JSON strings (Gen.Elements of random bytes/chars); assert `Kind == ParseError` and no exception
    - **Validates: Requirements 7.3**
    - **Property 17: String-to-enum mapping is correct and exhaustive** — for all `PortStateId` member names (and case variants), assert exact mapping; for arbitrary strings, assert fallback to `Idle`; for role/cell strings, assert exact mapping
    - **Validates: Requirements 7.4, 7.5, 7.6**
    - **Property 21: Missing optional fields fall back to defaults** — generate JSON `WsAgentSnapshot` objects that omit `maxLoad`, `pathInvalidatedThisTick`, `meetingPointX`, `meetingPointY` in various combinations; assert `MaxLoad = 0`, `PathInvalidatedThisTick = false`, `MeetingPoint = null`
    - **Validates: Requirements 7.7, 7.8, 10.5**

- [ ] 4. Implement `WebSocketSimulationConnector`
  - [ ] 4.1 Create `WebSocketSimulationConnector.cs`
    - Create `Assets/Scripts/Adapters/WebSocket/WebSocketSimulationConnector.cs`
    - Decorate with `[Serializable]`, implement `ISimulationConnector`
    - Add serialized fields: `[SerializeField] string host = "localhost"`, `[SerializeField] int port = 8765`, `[SerializeField] float connectionTimeoutSeconds = 10f`, `[SerializeField] bool reconnectOnDrop = false`
    - Implement `Connect(SessionRequest request)`: construct and return `new WebSocketSimulationConnection(host, port, connectionTimeoutSeconds, reconnectOnDrop, request)` — no socket opened, no task started
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [ ] 5. Implement `WebSocketSimulationConnection` (state machine + receive loop)
  - [ ] 5.1 Create `WebSocketSimulationConnection.cs`
    - Create `Assets/Scripts/Adapters/WebSocket/WebSocketSimulationConnection.cs`
    - Implement `ISimulationConnection` and `IDisposable`
    - Own: `ClientWebSocket _socket`, `ConcurrentQueue<ServerMessage> _queue`, `Task _connectTask`, `Task _receiveLoop`, `CancellationTokenSource _cts`, `float _timeoutAt`, `ConnectionState _state` (Idle/Connecting/Handshaking/Complete/Failed enum)
    - Implement `Poll()` as the four-state machine: Idle→Connecting (fire ConnectAsync), Connecting→Handshaking (on task success: send `state_request`, start ReceiveLoop), Handshaking (drain queue: on `state_response` → construct Session → Complete; on `error_response` or `Disconnected` → Failed; on timeout → Failed), Complete → return immediately
    - Implement `_receiveLoop`: read text frames with `ReceiveAsync`, call `WebSocketMessageParser.Parse()`, enqueue into `_queue`; on close/error enqueue `Disconnected` sentinel
    - Implement `Dispose()` with `Interlocked` guard: cancel `_cts`, await `_receiveLoop` (up to 2 s), call `CloseAsync`, set Failed state if not already Complete
    - Maintain invariants: `Session != null ↔ IsComplete && !Failed`; once `IsComplete = true`, never reset
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 8.4_

  - [ ]* 5.2 Write Unity Edit Mode property tests for connection state machine (Properties 1–7)
    - Add `Assets/Tests/WebSocket/ConnectionStateMachineTests.cs` (Unity Test Framework, Edit Mode assembly)
    - Use a `FakeWebSocket` inner class that implements a `ClientWebSocket`-compatible interface to simulate: successful connect, faulted ConnectAsync, `state_response` frame, `error_response` frame, `Disconnected` sentinel, timeout expiry
    - **Property 1**: valid `state_response` → `IsComplete=true`, `Failed=false`, `Session≠null` — **Validates: Requirements 2.3, 2.5**
    - **Property 2**: `Poll()` idempotent after `IsComplete` — **Validates: Requirements 2.4, 2.6**
    - **Property 3**: `Failed=true` implies `IsComplete=true`; `Session≠null` implies `IsComplete=true && Failed=false` — **Validates: Requirements 2.5, 3.5**
    - **Property 4**: timeout sets `Failed=true`, `IsComplete=true` — **Validates: Requirements 3.1**
    - **Property 5**: `ConnectAsync` exception sets `Failed=true`, `IsComplete=true`, `Error` contains message — **Validates: Requirements 3.2**
    - **Property 6**: `error_response` during handshake sets `Failed=true` — **Validates: Requirements 3.3**
    - **Property 7**: `Disconnected` before `state_response` sets `Failed=true` — **Validates: Requirements 3.4**

  - [ ]* 5.3 Write Unity Edit Mode property test for `Dispose()` idempotence (Property 18)
    - In the same test file as 5.2 (or a companion file)
    - **Property 18: Dispose() is idempotent** — call `Dispose()` two or more times on `WebSocketSimulationConnection` and `WebSocketSimulationSession`; assert no exception and `CloseAsync` called at most once
    - **Validates: Requirements 8.3**

- [ ] 6. Checkpoint — ensure server-side and parser tests pass
  - Ensure all tests pass in `AgenticModel/tests/HarvestingCore.Transport.Tests/` and `AgenticModel/tests/HarvestingCore.WebSocketAdapter.Tests/`. Ask the user if any questions arise.

- [ ] 7. Implement `WebSocketSimulationSession` (CellDiffCache + tick loop)
  - [ ] 7.1 Create `WebSocketSimulationSession.cs`
    - Create `Assets/Scripts/Adapters/WebSocket/WebSocketSimulationSession.cs`
    - Implement `ISimulationSession` and `IDisposable`
    - Constructor takes `WsSimulationSnapshot initialData`, `ClientWebSocket socket`, `CancellationTokenSource cts`, `ConcurrentQueue<ServerMessage> queue`
    - Build `InitialSnapshot` (immutable `WorldSnapshot`) from `initialData` using the DTO mapping rules (Role→`PortAgentRole`, State→`PortStateId` with `Idle` fallback, MeetingPoint from optional X/Y coords)
    - Initialise `_cellCache` (`PortCellState[]`, length = `width × height`, row-major) from `initialData.Cells`
    - Implement `RequestTick()`: if no tick is in flight, send `{"type":"tick_request","count":1}` immediately and set `_tickInFlight = true`; otherwise increment `_pendingTickCount`
    - Implement `DrainQueue()` (called by `SimulationDriver` each frame): dequeue all pending `ServerMessage` values; on `tick_response` → diff cells via `CellDiffCache`, map agents, raise `UpdateReceived` once, then send next queued tick if `_pendingTickCount > 0`; on `error_response` → log warning, do NOT raise `UpdateReceived`, send next queued tick if pending; on `Disconnected` → log warning
    - Implement `Dispose()` with `Interlocked` guard: cancel `_cts`, await `_receiveLoop` (up to 2 s), call `CloseAsync(Normal, "Session disposed", …)`; idempotent
    - _Requirements: 4.5, 4.6, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 8.1, 8.2, 8.3_

  - [ ]* 7.2 Write property tests for `CellDiffCache` diff logic (Properties 12, 13, 14)
    - Add `AgenticModel/tests/HarvestingCore.WebSocketAdapter.Tests/CellDiffCachePropertyTests.cs`
    - Extract the diff logic from `WebSocketSimulationSession` into a testable static helper (or test via the session directly with fake snapshots)
    - **Property 12: ChangedCells contains exactly the diffed positions** — generate pairs of `PortCellState[]` arrays; assert `ChangedCells` equals the set of indices where values differ
    - **Validates: Requirements 6.2**
    - **Property 13: ChangedCells bounds invariant** — for any snapshot, all positions in `ChangedCells` must satisfy `0 ≤ x < Width` and `0 ≤ y < Height`
    - **Validates: Requirements 6.4**
    - **Property 14: Identical consecutive snapshots produce empty ChangedCells** — apply same snapshot twice; assert second `ChangedCells` is empty
    - **Validates: Requirements 6.5, 6.6**

  - [ ]* 7.3 Write Unity Edit Mode property tests for tick ordering and `UpdateReceived` (Properties 8–11)
    - Add `Assets/Tests/WebSocket/SessionTickTests.cs` (Unity Test Framework, Edit Mode)
    - Use `FakeWebSocket` mock to enqueue `tick_response` frames and drive `DrainQueue()`
    - **Property 8: UpdateReceived fires exactly once per tick_response** — for any valid tick response enqueued, after `DrainQueue()`, `UpdateReceived` raised exactly once with non-null `WorldUpdate` — **Validates: Requirements 5.2, 5.3**
    - **Property 9: Queued tick requests maintain ordering** — call `RequestTick()` twice before first `tick_response`; assert only one `tick_request` sent before first response, second sent only after — **Validates: Requirements 5.4**
    - **Property 10: error_response to tick_request does NOT raise UpdateReceived** — enqueue `error_response` while tick in flight; assert `UpdateReceived` not raised — **Validates: Requirements 5.5**
    - **Property 11: InitialSnapshot is immutable after construction** — call `RequestTick()` N times; assert `InitialSnapshot` reference and value unchanged — **Validates: Requirements 5.6**

- [ ] 8. Checkpoint — ensure all tests pass
  - Ensure all tests pass across both test projects and Unity Edit Mode test assemblies. Ask the user if any questions arise.

- [ ] 9. Integration smoke test
  - [ ] 9.1 Write a Unity Edit Mode integration test that exercises the full handshake → tick → `UpdateReceived` path
    - Add `Assets/Tests/WebSocket/WebSocketAdapterIntegrationTests.cs` (Unity Test Framework, Edit Mode)
    - Wire `WebSocketSimulationConnector` → `Connect()` → `WebSocketSimulationConnection` using a `FakeWebSocket` server stub that responds with a canned `state_response` and a subsequent `tick_response`
    - Drive `Poll()` until `IsComplete`; verify `Session` is non-null and `InitialSnapshot` has expected dimensions
    - Call `RequestTick()` and `DrainQueue()`; verify `UpdateReceived` fires with a non-null `WorldUpdate`
    - _Requirements: 1.1, 2.3, 5.2, 6.2_

- [ ] 10. Final checkpoint — ensure all tests pass
  - Ensure all tests pass. Ask the user if any questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- All property tests use FsCheck with a minimum of 100 iterations, tagged with `// Feature: websocket-client-adapter, Property {N}: …`
- `WebSocketMessageParser` must have zero `UnityEngine` dependencies — it is shared as a compile item with the dotnet test project
- Server-side tasks (1.x) must be completed before parser tests (3.3) since the tests validate the same field names
- `DrainQueue()` must be wired into `SimulationDriver` (in `AgroAgents.Presentation`) after task 7 is complete
