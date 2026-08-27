namespace AgroAgents.Presentation.Authoring
{
    /// <summary>
    /// Which kind of site a <see cref="SiteMarker"/> represents. Presentation-only:
    /// neither <c>HarvestingCore</c> nor the port has a counterpart to duplicate: a
    /// resolved marker is forwarded as a plain <c>PortGridPosition</c> into
    /// <c>SessionRequest.RefuelStations</c>/<c>DumpSites</c> (Decision H).
    /// </summary>
    public enum SiteKind
    {
        Refuel,
        Dump
    }
}
