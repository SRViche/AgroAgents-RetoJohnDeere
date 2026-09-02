// Feature: websocket-client-adapter
// Unity-free static parser — no UnityEngine dependency.
// Compiles as netstandard2.1 and can be exercised via dotnet test.

using System;
using System.Text.Json;

namespace AgroAgents.WebSocketAdapter
{
    internal static class WebSocketMessageParser
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Parses a raw JSON text frame from the server into a <see cref="ServerMessage"/>.
        /// Never throws — all exceptions are caught and returned as ParseError or UnknownType.
        /// </summary>
        internal static ServerMessage Parse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp))
                    return ServerMessage.ForParseError("Missing 'type' field in JSON frame.");

                var type = typeProp.GetString();

                return type switch
                {
                    "state_response" => ParseSnapshot(json, snapshot =>
                        ServerMessage.ForStateResponse(snapshot)),

                    "tick_response" => ParseSnapshot(json, snapshot =>
                        ServerMessage.ForTickResponse(snapshot)),

                    "error_response" => ParseError(root),

                    _ => ServerMessage.ForUnknownType()
                };
            }
            catch (JsonException ex)
            {
                return ServerMessage.ForParseError(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerMessage.ForParseError($"Unexpected parse error: {ex.Message}");
            }
        }

        private static ServerMessage ParseSnapshot(
            string json,
            Func<WsSimulationSnapshot, ServerMessage> factory)
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<WsSimulationSnapshot>(json, _options);
                if (snapshot == null)
                    return ServerMessage.ForParseError("Deserialized snapshot was null.");
                return factory(snapshot);
            }
            catch (JsonException ex)
            {
                return ServerMessage.ForParseError(ex.Message);
            }
        }

        private static ServerMessage ParseError(JsonElement root)
        {
            var code = root.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
            var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return ServerMessage.ForError(code, message);
        }
    }
}
