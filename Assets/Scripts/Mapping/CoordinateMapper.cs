using AgroAgents.SimulationPort;
using UnityEngine;

namespace AgroAgents.Presentation.Mapping
{
    /// <summary>
    /// The sole authority for converting between <see cref="PortGridPosition"/> grid
    /// coordinates and Unity world space. Plain, immutable, not a MonoBehaviour: it has
    /// no per-frame work and no lifecycle. Req 6.1, 6.2, 6.5, 6.6.
    /// </summary>
    public sealed class CoordinateMapper
    {
        public Vector3 GridOrigin { get; }
        public float TileSize { get; }
        public int Width { get; }
        public int Height { get; }

        public CoordinateMapper(Vector3 gridOrigin, float tileSize, int width, int height)
        {
            GridOrigin = gridOrigin;
            TileSize = tileSize;
            Width = width;
            Height = height;
        }

        /// <summary>Grid Origin + (X * Tile_Size, 0, Y * Tile_Size). Req 6.1.</summary>
        public Vector3 ToWorld(PortGridPosition p)
        {
            return GridOrigin + new Vector3(p.X * TileSize, 0f, p.Y * TileSize);
        }

        /// <summary>Convenience overload for agent/content Y placement.</summary>
        public Vector3 ToWorld(PortGridPosition p, float height)
        {
            Vector3 world = ToWorld(p);
            world.y = height;
            return world;
        }

        /// <summary>
        /// Rounds the local x/z over Tile_Size to the nearest cell centre. Returns false
        /// without producing a position when the rounded cell falls outside
        /// [0,Width) x [0,Height). Req 6.2, 6.5.
        /// </summary>
        public bool TryToGrid(Vector3 world, out PortGridPosition p)
        {
            Vector3 local = world - GridOrigin;
            int x = Mathf.RoundToInt(local.x / TileSize);
            int y = Mathf.RoundToInt(local.z / TileSize);

            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                p = default;
                return false;
            }

            p = new PortGridPosition(x, y);
            return true;
        }

        public bool InBounds(PortGridPosition p)
        {
            return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        }

        /// <summary>Used by IsometricView.</summary>
        public Vector3 GridCentreWorld =>
            GridOrigin + new Vector3((Width - 1) * TileSize * 0.5f, 0f, (Height - 1) * TileSize * 0.5f);
    }
}
