using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Extension methods for <see cref="IBuilder{TGeometry,TFeature,TCollection}"/>.
/// </summary>
public static class BuilderExtensions
{
    /// <summary>
    /// Dispatches a <see cref="GmlGeometry"/> to the appropriate builder method
    /// based on the concrete geometry type.
    /// </summary>
    /// <returns>The converted geometry, or default if the type is not supported.</returns>
    public static TGeometry? BuildGeometry<TGeometry, TFeature, TCollection>(
        this IBuilder<TGeometry, TFeature, TCollection> builder,
        GmlGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(geometry);

        return geometry switch
        {
            GmlPoint p => builder.BuildPoint(p),
            GmlLineString ls => builder.BuildLineString(ls),
            GmlLinearRing lr => builder.BuildLinearRing(lr),
            GmlPolygon poly => builder.BuildPolygon(poly),
            GmlEnvelope env => builder.BuildEnvelope(env),
            GmlBox box => builder.BuildBox(box),
            GmlCurve c => builder.BuildCurve(c),
            GmlSurface s => builder.BuildSurface(s),
            GmlMultiPoint mp => builder.BuildMultiPoint(mp),
            GmlMultiLineString mls => builder.BuildMultiLineString(mls),
            GmlMultiPolygon mpoly => builder.BuildMultiPolygon(mpoly),
            _ => default
        };
    }
}
