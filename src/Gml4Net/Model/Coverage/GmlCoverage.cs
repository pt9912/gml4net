using Gml4Net.Model.Geometry;

namespace Gml4Net.Model.Coverage;

/// <summary>
/// Abstract base class for GML coverage types.
/// </summary>
public abstract class GmlCoverage : GmlNode, IGmlRootContent
{
    internal GmlCoverage() { }

    /// <summary>Optional coverage identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Optional bounding box.</summary>
    public GmlEnvelope? BoundedBy { get; init; }

    /// <summary>Range set (data values).</summary>
    public GmlRangeSet? RangeSet { get; init; }

    /// <summary>Range type (band/field definitions).</summary>
    public GmlRangeType? RangeType { get; init; }
}

/// <summary>
/// A rectified grid coverage with georeferenced grid (affine transform).
/// </summary>
public sealed class GmlRectifiedGridCoverage : GmlCoverage
{
    /// <summary>The rectified grid domain.</summary>
    public required GmlRectifiedGrid DomainSet { get; init; }
}

/// <summary>
/// A non-georeferenced grid coverage.
/// </summary>
public sealed class GmlGridCoverage : GmlCoverage
{
    /// <summary>The grid domain.</summary>
    public required GmlGrid DomainSet { get; init; }
}

/// <summary>
/// A referenceable (irregular) grid coverage.
/// </summary>
public sealed class GmlReferenceableGridCoverage : GmlCoverage
{
    /// <summary>The grid domain.</summary>
    public required GmlGrid DomainSet { get; init; }
}

/// <summary>
/// A discrete multi-point coverage.
/// </summary>
public sealed class GmlMultiPointCoverage : GmlCoverage
{
    /// <summary>Domain points.</summary>
    public IReadOnlyList<GmlPoint>? DomainPoints { get; init; }
}
