using System;
using System.Collections.Generic;

namespace AgroAgents.SimulationPort
{
    /// <summary>
    /// The live handle to a running simulation. <see cref="RequestTick"/> is the
    /// presentation assembly's only mutating call (Req 2.4); everything else is a
    /// read of <see cref="InitialSnapshot"/> or of the most recent <see cref="WorldUpdate"/>.
    /// </summary>
    public interface ISimulationSession : IDisposable
    {
        WorldSnapshot InitialSnapshot { get; }

        /// <summary>
        /// In-memory: synchronous — calls SimulationWorld.Tick() and raises
        /// UpdateReceived before this call returns. A remote session is free to
        /// return immediately and raise UpdateReceived on a later frame; callers
        /// must not assume synchronous delivery.
        /// </summary>
        void RequestTick();

        event Action<WorldUpdate> UpdateReceived;
    }

    /// <summary>
    /// Opens a session from a <see cref="SessionRequest"/>. The in-memory adapter
    /// is the sole implementation today; the seam a future WebSocket adapter fills.
    /// </summary>
    public interface ISimulationConnector
    {
        ISimulationConnection Connect(SessionRequest request);
    }

    /// <summary>
    /// A handle to an in-flight or completed connection attempt. Poll() is called
    /// once per frame by WorldBootstrapper until IsComplete; the in-memory adapter
    /// completes on its first Poll(), so this release resolves within one Awake.
    /// </summary>
    public interface ISimulationConnection
    {
        bool IsComplete { get; }
        bool Failed { get; }

        /// <summary>Valid once <see cref="Failed"/> is true.</summary>
        string Error { get; }

        /// <summary>Valid once <see cref="IsComplete"/> is true; non-fatal issues.</summary>
        IReadOnlyList<string> Warnings { get; }

        /// <summary>Valid once <see cref="IsComplete"/> is true and <see cref="Failed"/> is false.</summary>
        ISimulationSession Session { get; }

        void Poll();
    }
}
