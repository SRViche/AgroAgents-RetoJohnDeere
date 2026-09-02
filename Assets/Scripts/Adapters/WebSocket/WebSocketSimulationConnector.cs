// Feature: websocket-client-adapter
// Connector — carries Inspector-serialized configuration and produces a
// WebSocketSimulationConnection on demand. No socket is opened here.

using System;
using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.WebSocketAdapter
{
    /// <summary>
    /// A <c>[Serializable]</c> implementation of <see cref="ISimulationConnector"/> that can
    /// be assigned inline to <c>WorldBootstrapper</c>'s <c>[SerializeReference]</c> field via
    /// the Unity Inspector without writing any code.
    ///
    /// <para>
    /// Calling <see cref="Connect"/> constructs and returns a new
    /// <see cref="WebSocketSimulationConnection"/>. No socket is opened and no background
    /// task is started until the first <c>Poll()</c> call on the returned connection.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class WebSocketSimulationConnector : ISimulationConnector
    {
        [SerializeField] private string host = "localhost";
        [SerializeField] private int port = 8765;
        [SerializeField] private float connectionTimeoutSeconds = 10f;
        [SerializeField] private bool reconnectOnDrop = false;

        /// <summary>
        /// Constructs a new <see cref="WebSocketSimulationConnection"/> using the current
        /// field values and the supplied <paramref name="request"/>. No socket is opened
        /// and no task is started; that happens on the first <c>Poll()</c> call.
        /// </summary>
        public ISimulationConnection Connect(SessionRequest request)
        {
            return new WebSocketSimulationConnection(
                host,
                port,
                connectionTimeoutSeconds,
                reconnectOnDrop,
                request);
        }
    }
}
