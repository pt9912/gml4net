namespace Gml4Net.Model.Coverage;

/// <summary>
/// A rectified grid with origin and offset vectors for affine transformation.
/// </summary>
public sealed class GmlRectifiedGrid : GmlGrid
{
    /// <summary>Spatial reference system name.</summary>
    public string? SrsName { get; init; }

    /// <summary>Grid origin coordinate.</summary>
    public required GmlCoordinate Origin { get; init; }

    /// <summary>Offset vectors defining the affine transform.</summary>
    public required IReadOnlyList<IReadOnlyList<double>> OffsetVectors { get; init; }
}
