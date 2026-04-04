using Gml4Net.Model.Geometry;

namespace Gml4Net.Model.Feature;

/// <summary>
/// Abstract base for typed feature property values.
/// </summary>
public abstract class GmlPropertyValue
{
    internal GmlPropertyValue() { }
}

/// <summary>String property value.</summary>
public sealed class GmlStringProperty : GmlPropertyValue
{
    /// <summary>The string value.</summary>
    public required string Value { get; init; }
}

/// <summary>Numeric property value.</summary>
public sealed class GmlNumericProperty : GmlPropertyValue
{
    /// <summary>The numeric value.</summary>
    public required double Value { get; init; }
}

/// <summary>Geometry property value.</summary>
public sealed class GmlGeometryProperty : GmlPropertyValue
{
    /// <summary>The geometry value.</summary>
    public required GmlGeometry Geometry { get; init; }
}

/// <summary>Nested property containing child properties.</summary>
public sealed class GmlNestedProperty : GmlPropertyValue
{
    /// <summary>Child properties in document order.</summary>
    public GmlPropertyBag Children { get; init; } = GmlPropertyBag.Empty;
}

/// <summary>Raw XML property for unclassifiable content.</summary>
public sealed class GmlRawXmlProperty : GmlPropertyValue
{
    /// <summary>The raw XML string.</summary>
    public required string XmlContent { get; init; }
}
