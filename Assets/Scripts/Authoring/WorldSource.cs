namespace AgroAgents.Presentation.Authoring
{
    /// <summary>
    /// Which construction path <see cref="WorldBootstrapper"/> requests for the grid.
    /// Presentation-only, with no port or core counterpart: <c>Generated</c> leaves
    /// <c>SessionRequest.AuthoredGridText</c> null, <c>AuthoredText</c> fills it with the
    /// parsed authored text. The chosen connector decides what that means — the
    /// in-memory adapter maps <c>Generated</c> to <c>GenerateGrid()</c> and
    /// <c>AuthoredText</c> to <c>WorldModel.Parse</c> (Decision G').
    /// </summary>
    public enum WorldSource
    {
        Generated,
        AuthoredText
    }
}
