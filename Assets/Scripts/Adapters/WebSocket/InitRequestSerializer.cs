// Feature: websocket-client-adapter
// Serializes the client-authored SessionRequest into the server's init_request
// wire shape. The client is the sole author of the world: this payload is sent
// once, immediately after the WebSocket handshake, and the server builds its
// SimulationWorld from it before replying with a state_response.
//
// Unity-free (System.Text.Json only) so it can be exercised via dotnet test.

using System.IO;
using System.Text;
using System.Text.Json;
using AgroAgents.SimulationPort;

namespace AgroAgents.WebSocketAdapter
{
    internal static class InitRequestSerializer
    {
        internal static string Serialize(SessionRequest request)
        {
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteString("type", "init_request");
                w.WriteNumber("width", request.Width);
                w.WriteNumber("height", request.Height);
                w.WriteNumber("seed", request.Seed);
                w.WriteNumber("cropDensity", request.CropDensity);
                w.WriteNumber("blockedDensity", request.BlockedDensity);

                if (request.AuthoredGridText == null)
                    w.WriteNull("authoredGridText");
                else
                    w.WriteString("authoredGridText", request.AuthoredGridText);

                WritePositions(w, "refuelStations", request.RefuelStations);
                WritePositions(w, "dumpSites", request.DumpSites);

                w.WritePropertyName("agents");
                w.WriteStartArray();
                if (request.Agents != null)
                {
                    for (int i = 0; i < request.Agents.Count; i++)
                    {
                        var a = request.Agents[i];
                        w.WriteStartObject();
                        w.WriteString("id", a.Id);
                        w.WriteString("role", a.Role == PortAgentRole.Tractor ? "Tractor" : "Harvester");
                        w.WriteNumber("x", a.Start.X);
                        w.WriteNumber("y", a.Start.Y);
                        WriteNullableInt(w, "maxLoad", a.MaxLoad);
                        WriteNullableInt(w, "maxFuel", a.MaxFuel);
                        WriteNullableInt(w, "fuelConsumption", a.FuelConsumption);
                        w.WriteEndObject();
                    }
                }
                w.WriteEndArray();

                w.WriteNumber("cropCost", request.CropCost);
                w.WriteNumber("emptyCost", request.EmptyCost);
                w.WriteNumber("harvestedCost", request.HarvestedCost);
                w.WriteNumber("heuristicKind", request.HeuristicKind);
                w.WriteNumber("defaultMaxLoad", request.DefaultMaxLoad);
                w.WriteNumber("defaultMaxFuel", request.DefaultMaxFuel);
                w.WriteNumber("defaultFuelConsumption", request.DefaultFuelConsumption);
                w.WriteNumber("dumpPreferenceFactor", request.DumpPreferenceFactor);
                w.WriteNumber("capacityFactor", request.CapacityFactor);
                w.WriteNumber("harvesterFuelReserveMultiplier", request.HarvesterFuelReserveMultiplier);
                w.WriteNumber("tractorFuelReserveMultiplier", request.TractorFuelReserveMultiplier);

                w.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WritePositions(
            Utf8JsonWriter w, string name, System.Collections.Generic.IReadOnlyList<PortGridPosition> positions)
        {
            w.WritePropertyName(name);
            w.WriteStartArray();
            if (positions != null)
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    w.WriteStartObject();
                    w.WriteNumber("x", positions[i].X);
                    w.WriteNumber("y", positions[i].Y);
                    w.WriteEndObject();
                }
            }
            w.WriteEndArray();
        }

        private static void WriteNullableInt(Utf8JsonWriter w, string name, int? value)
        {
            if (value.HasValue)
                w.WriteNumber(name, value.Value);
            else
                w.WriteNull(name);
        }
    }
}
