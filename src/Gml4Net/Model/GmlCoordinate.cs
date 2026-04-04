namespace Gml4Net.Model;

/// <summary>
/// An immutable coordinate with X, Y and optional Z and M components.
/// </summary>
public readonly record struct GmlCoordinate(
    double X,
    double Y,
    double? Z = null,
    double? M = null)
{
    /// <summary>
    /// Number of dimensions (2, 3 or 4).
    /// </summary>
    public int Dimension => (Z, M) switch
    {
        (not null, not null) => 4,
        (not null, _) or (_, not null) => 3,
        _ => 2
    };
}
