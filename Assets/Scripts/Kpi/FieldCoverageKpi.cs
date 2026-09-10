using System.Collections.Generic;
using AgroAgents.SimulationPort;

namespace AgroAgents.Presentation.Kpi
{
    /// <summary>
    /// KPI #4 ("Field coverage"): which zones of the field have already been worked
    /// and how much overall progress exists.
    ///
    /// Intention: let the operator see, at a glance, the spatial distribution of
    /// work and the global completion percentage. The presentation layer is
    /// expected to render <see cref="Cells"/> as a heatmap / grid over the field
    /// and to show <see cref="CoveragePercent"/> as a headline metric.
    ///
    /// "Coverage" is defined against the set of cells that are <i>workable</i> — the
    /// harvestable crop cells — not the whole grid, so that blocked or empty terrain
    /// does not distort the percentage.
    /// </summary>
    public readonly struct FieldCoverageKpi
    {
        /// <summary>Field width in cells (X extent), mirrors the world snapshot.</summary>
        public int Width { get; }

        /// <summary>Field height in cells (Y extent), mirrors the world snapshot.</summary>
        public int Height { get; }

        /// <summary>
        /// Count of workable cells that have been harvested so far. Numerator of
        /// <see cref="CoveragePercent"/>.
        /// </summary>
        public int HarvestedCellCount { get; }

        /// <summary>
        /// Total count of workable (harvestable) cells at simulation start.
        /// Denominator of <see cref="CoveragePercent"/>. A cell that started as
        /// Crop is workable; Empty and Blocked cells are not counted.
        /// </summary>
        public int WorkableCellCount { get; }

        /// <summary>
        /// Overall coverage as a fraction in the range [0, 1] — that is,
        /// <see cref="HarvestedCellCount"/> / <see cref="WorkableCellCount"/>.
        /// Provided pre-computed; the view multiplies by 100 for display. Zero when
        /// there are no workable cells.
        /// </summary>
        public double CoveragePercent { get; }

        /// <summary>
        /// Per-cell state grid used to drive the heatmap. One entry per workable
        /// cell of interest; each carries its position and current state so the
        /// view can colour worked vs. pending zones without consulting any other
        /// data source.
        /// </summary>
        public IReadOnlyList<FieldCoverageCell> Cells { get; }

        public FieldCoverageKpi(
            int width,
            int height,
            int harvestedCellCount,
            int workableCellCount,
            double coveragePercent,
            IReadOnlyList<FieldCoverageCell> cells)
        {
            Width = width;
            Height = height;
            HarvestedCellCount = harvestedCellCount;
            WorkableCellCount = workableCellCount;
            CoveragePercent = coveragePercent;
            Cells = cells;
        }
    }

    /// <summary>
    /// A single cell in the <see cref="FieldCoverageKpi"/> heatmap grid: its
    /// position and whether it has been worked yet.
    /// </summary>
    public readonly struct FieldCoverageCell
    {
        /// <summary>Grid position of this cell within the field.</summary>
        public PortGridPosition Position { get; }

        /// <summary>
        /// Current state of the cell. Consumers treat <see cref="PortCellState.Harvested"/>
        /// as "worked" and <see cref="PortCellState.Crop"/> as "pending"; the raw
        /// state is exposed so the heatmap can distinguish more than two shades if
        /// desired.
        /// </summary>
        public PortCellState State { get; }

        public FieldCoverageCell(PortGridPosition position, PortCellState state)
        {
            Position = position;
            State = state;
        }
    }
}
