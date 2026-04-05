using Gml4Net.Model.Coverage;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Generic builder interface for converting GML model objects to an output format.
/// </summary>
/// <typeparam name="TGeometry">Output type for geometry conversions.</typeparam>
/// <typeparam name="TFeature">Output type for feature conversions.</typeparam>
/// <typeparam name="TCollection">Output type for feature collection conversions.</typeparam>
public interface IBuilder<TGeometry, TFeature, TCollection>
{
    /// <summary>Builds a Point.</summary>
    TGeometry? BuildPoint(GmlPoint point);
    /// <summary>Builds a LineString.</summary>
    TGeometry? BuildLineString(GmlLineString lineString);
    /// <summary>Builds a LinearRing.</summary>
    TGeometry? BuildLinearRing(GmlLinearRing linearRing);
    /// <summary>Builds a Polygon.</summary>
    TGeometry? BuildPolygon(GmlPolygon polygon);
    /// <summary>Builds a MultiPoint.</summary>
    TGeometry? BuildMultiPoint(GmlMultiPoint multiPoint);
    /// <summary>Builds a MultiLineString.</summary>
    TGeometry? BuildMultiLineString(GmlMultiLineString multiLineString);
    /// <summary>Builds a MultiPolygon.</summary>
    TGeometry? BuildMultiPolygon(GmlMultiPolygon multiPolygon);
    /// <summary>Builds an Envelope.</summary>
    TGeometry? BuildEnvelope(GmlEnvelope envelope);
    /// <summary>Builds a Box.</summary>
    TGeometry? BuildBox(GmlBox box);
    /// <summary>Builds a Curve.</summary>
    TGeometry? BuildCurve(GmlCurve curve);
    /// <summary>Builds a Surface.</summary>
    TGeometry? BuildSurface(GmlSurface surface);
    /// <summary>Builds a Feature.</summary>
    TFeature BuildFeature(GmlFeature feature);
    /// <summary>Builds a FeatureCollection.</summary>
    TCollection BuildFeatureCollection(GmlFeatureCollection fc);
    /// <summary>Builds a Coverage.</summary>
    TFeature? BuildCoverage(GmlCoverage coverage);
}
