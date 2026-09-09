// Feature: websocket-client-adapter
// Discriminated union representing a single parsed server frame.
// No UnityEngine dependency — safe to reference from the dotnet test project.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgroAgents.WebSocketAdapter
{
    internal enum ServerMessageKind
    {
        StateResponse,
        TickResponse,
        ErrorResponse,
        ParseError,
        UnknownType,
        Disconnected
    }

    // Internal DTO carried by StateResponse / TickResponse messages.
    // Defined here (not as a private nested type) so ServerMessage can reference it
    // and WebSocketMessageParser can populate it without a circular dependency.
    internal sealed class WsSimulationSnapshot
    {
        [JsonPropertyName("tick")]             public int Tick { get; set; }
        [JsonPropertyName("isHalted")]         public bool IsHalted { get; set; }
        [JsonPropertyName("dischargedTotal")]  public int DischargedTotal { get; set; }
        [JsonPropertyName("agents")]           public List<WsAgentSnapshot> Agents { get; set; } = new();
        [JsonPropertyName("cells")]            public List<WsCellSnapshot> Cells { get; set; } = new();
        [JsonPropertyName("width")]            public int Width { get; set; }
        [JsonPropertyName("height")]           public int Height { get; set; }
    }

    internal sealed class WsAgentSnapshot
    {
        [JsonPropertyName("id")]                      public string Id { get; set; } = "";
        [JsonPropertyName("role")]                    public string Role { get; set; } = "";
        [JsonPropertyName("state")]                   public string State { get; set; } = "";
        [JsonPropertyName("x")]                       public int X { get; set; }
        [JsonPropertyName("y")]                       public int Y { get; set; }
        [JsonPropertyName("fuel")]                    public int Fuel { get; set; }
        [JsonPropertyName("load")]                    public int Load { get; set; }
        [JsonPropertyName("maxLoad")]                 public int? MaxLoad { get; set; }
        [JsonPropertyName("pathInvalidatedThisTick")] public bool? PathInvalidatedThisTick { get; set; }
        [JsonPropertyName("meetingPointX")]           public int? MeetingPointX { get; set; }
        [JsonPropertyName("meetingPointY")]           public int? MeetingPointY { get; set; }
    }

    internal sealed class WsCellSnapshot
    {
        [JsonPropertyName("x")]       public int X { get; set; }
        [JsonPropertyName("y")]       public int Y { get; set; }
        [JsonPropertyName("state")]   public string State { get; set; } = "";
        [JsonPropertyName("ownerId")] public string? OwnerId { get; set; }
    }

    internal sealed class ServerMessage
    {
        public ServerMessageKind Kind { get; }

        /// <summary>Non-null for StateResponse and TickResponse.</summary>
        public WsSimulationSnapshot? SnapshotData { get; }

        /// <summary>Non-null for ErrorResponse.</summary>
        public string? ErrorCode { get; }

        /// <summary>Non-null for ErrorResponse.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Non-null for ParseError.</summary>
        public string? ParseErrorMessage { get; }

        /// <summary>Non-null for Disconnected.</summary>
        public string? CloseReason { get; }

        private ServerMessage(
            ServerMessageKind kind,
            WsSimulationSnapshot? snapshotData = null,
            string? errorCode = null,
            string? errorMessage = null,
            string? parseErrorMessage = null,
            string? closeReason = null)
        {
            Kind = kind;
            SnapshotData = snapshotData;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            ParseErrorMessage = parseErrorMessage;
            CloseReason = closeReason;
        }

        public static ServerMessage ForStateResponse(WsSimulationSnapshot snapshot) =>
            new(ServerMessageKind.StateResponse, snapshotData: snapshot);

        public static ServerMessage ForTickResponse(WsSimulationSnapshot snapshot) =>
            new(ServerMessageKind.TickResponse, snapshotData: snapshot);

        public static ServerMessage ForError(string code, string msg) =>
            new(ServerMessageKind.ErrorResponse, errorCode: code, errorMessage: msg);

        public static ServerMessage ForParseError(string msg) =>
            new(ServerMessageKind.ParseError, parseErrorMessage: msg);

        public static ServerMessage ForUnknownType() =>
            new(ServerMessageKind.UnknownType);

        public static ServerMessage ForDisconnected(string reason) =>
            new(ServerMessageKind.Disconnected, closeReason: reason);
    }
}
