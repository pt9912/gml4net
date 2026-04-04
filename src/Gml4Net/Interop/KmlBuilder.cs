using System.Globalization;
using System.Xml.Linq;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Converts GML model objects to KML (Keyhole Markup Language) XML.
/// Implements <see cref="IGmlBuilder{TGeometry,TFeature,TCollection}"/> and also
/// provides static convenience methods via <see cref="Instance"/>.
/// </summary>
public sealed class KmlBuilder : IGmlBuilder<XElement, XElement, XElement>
{
    private static readonly XNamespace Kml = "http://www.opengis.net/kml/2.2";

    /// <summary>Shared default instance.</summary>
    public static KmlBuilder Instance { get; } = new();

    // ---- IGmlBuilder implementation ----

    /// <inheritdoc />
    public XElement? BuildPoint(GmlPoint point) { ArgumentNullException.ThrowIfNull(point); return BuildPointCore(point); }
    /// <inheritdoc />
    public XElement? BuildLineString(GmlLineString lineString) { ArgumentNullException.ThrowIfNull(lineString); return BuildLineStringCore(lineString); }
    /// <inheritdoc />
    public XElement? BuildLinearRing(GmlLinearRing linearRing) { ArgumentNullException.ThrowIfNull(linearRing); return BuildLinearRingCore(linearRing); }
    /// <inheritdoc />
    public XElement? BuildPolygon(GmlPolygon polygon) { ArgumentNullException.ThrowIfNull(polygon); return BuildPolygonCore(polygon); }
    /// <inheritdoc />
    public XElement? BuildMultiPoint(GmlMultiPoint multiPoint) { ArgumentNullException.ThrowIfNull(multiPoint); return BuildMultiGeometry(multiPoint.Points.Select(p => BuildPointCore(p)).ToArray()); }
    /// <inheritdoc />
    public XElement? BuildMultiLineString(GmlMultiLineString multiLineString) { ArgumentNullException.ThrowIfNull(multiLineString); return BuildMultiGeometry(multiLineString.LineStrings.Select(ls => BuildLineStringCore(ls)).ToArray()); }
    /// <inheritdoc />
    public XElement? BuildMultiPolygon(GmlMultiPolygon multiPolygon) { ArgumentNullException.ThrowIfNull(multiPolygon); return BuildMultiGeometry(multiPolygon.Polygons.Select(p => BuildPolygonCore(p)).ToArray()); }
    /// <inheritdoc />
    public XElement? BuildEnvelope(GmlEnvelope envelope) { ArgumentNullException.ThrowIfNull(envelope); return BuildPolygonFromBbox(envelope.LowerCorner, envelope.UpperCorner); }
    /// <inheritdoc />
    public XElement? BuildBox(GmlBox box) { ArgumentNullException.ThrowIfNull(box); return BuildPolygonFromBbox(box.LowerCorner, box.UpperCorner); }
    /// <inheritdoc />
    public XElement? BuildCurve(GmlCurve curve) { ArgumentNullException.ThrowIfNull(curve); return BuildLineStringFromCoords(curve.Coordinates); }
    /// <inheritdoc />
    public XElement? BuildSurface(GmlSurface surface) { ArgumentNullException.ThrowIfNull(surface); return BuildMultiGeometry(surface.Patches.Select(p => BuildPolygonCore(p)).ToArray()); }
    /// <inheritdoc />
    public XElement BuildFeature(GmlFeature feature) => Feature(feature);
    /// <inheritdoc />
    public XElement BuildFeatureCollection(GmlFeatureCollection fc) => FeatureCollection(fc);
    /// <inheritdoc />
    public object? BuildCoverage(GmlCoverage coverage) => null;

    // ---- Static convenience API (backward compatible) ----

    /// <summary>
    /// Converts a <see cref="GmlGeometry"/> to a KML geometry <see cref="XElement"/>.
    /// </summary>
    /// <param name="geometry">The GML geometry to convert.</param>
    /// <returns>A KML geometry element, or null if the type is not supported.</returns>
    public static XElement? Geometry(GmlGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return geometry switch
        {
            GmlPoint p => BuildPointCore(p),
            GmlLineString ls => BuildLineStringCore(ls),
            GmlLinearRing lr => BuildLinearRingCore(lr),
            GmlPolygon poly => BuildPolygonCore(poly),
            GmlEnvelope env => BuildPolygonFromBbox(env.LowerCorner, env.UpperCorner),
            GmlBox box => BuildPolygonFromBbox(box.LowerCorner, box.UpperCorner),
            GmlCurve c => BuildLineStringFromCoords(c.Coordinates),
            GmlSurface s => BuildMultiGeometry(s.Patches.Select(p => BuildPolygonCore(p)).ToArray()),
            GmlMultiPoint mp => BuildMultiGeometry(mp.Points.Select(p => BuildPointCore(p)).ToArray()),
            GmlMultiLineString mls => BuildMultiGeometry(mls.LineStrings.Select(ls => BuildLineStringCore(ls)).ToArray()),
            GmlMultiPolygon mpoly => BuildMultiGeometry(mpoly.Polygons.Select(p => BuildPolygonCore(p)).ToArray()),
            _ => null
        };
    }

    /// <summary>
    /// Converts a <see cref="GmlFeature"/> to a KML Placemark <see cref="XElement"/>.
    /// </summary>
    /// <param name="feature">The GML feature to convert.</param>
    /// <returns>A KML Placemark element.</returns>
    public static XElement Feature(GmlFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        var placemark = new XElement(Kml + "Placemark");

        if (feature.Id is not null)
            placemark.Add(new XElement(Kml + "name", feature.Id));

        var geometries = new List<XElement>();
        var descParts = new List<string>();
        foreach (var entry in feature.Properties.Entries)
        {
            if (entry.Value is GmlGeometryProperty gp)
            {
                var kmlGeom = Geometry(gp.Geometry);
                if (kmlGeom is not null)
                    geometries.Add(kmlGeom);
            }
            else if (entry.Value is GmlStringProperty sp)
                descParts.Add($"{entry.Name}: {sp.Value}");
            else if (entry.Value is GmlNumericProperty np)
                descParts.Add($"{entry.Name}: {np.Value.ToString(CultureInfo.InvariantCulture)}");
            else if (entry.Value is GmlNestedProperty)
                descParts.Add($"{entry.Name}: [nested]");
            else if (entry.Value is GmlRawXmlProperty raw)
                descParts.Add($"{entry.Name}: {raw.XmlContent}");
        }

        if (geometries.Count == 1)
            placemark.Add(geometries[0]);
        else if (geometries.Count > 1)
            placemark.Add(BuildMultiGeometry(geometries.ToArray()));

        if (descParts.Count > 0)
            placemark.Add(new XElement(Kml + "description", string.Join("\n", descParts)));

        return placemark;
    }

    /// <summary>
    /// Converts a <see cref="GmlFeatureCollection"/> to a KML Document <see cref="XElement"/>.
    /// </summary>
    /// <param name="fc">The GML feature collection to convert.</param>
    /// <returns>A KML Document element wrapped in a kml root.</returns>
    public static XElement FeatureCollection(GmlFeatureCollection fc)
    {
        ArgumentNullException.ThrowIfNull(fc);

        var document = new XElement(Kml + "Document");
        foreach (var f in fc.Features)
            document.Add(Feature(f));

        return new XElement(Kml + "kml", document);
    }

    /// <summary>Converts a geometry to a KML XML string.</summary>
    /// <param name="geometry">The GML geometry to convert.</param>
    /// <returns>A KML XML string, or null if not supported.</returns>
    public static string? GeometryToKml(GmlGeometry geometry) =>
        Geometry(geometry)?.ToString();

    // ---- Private core builders ----

    private static XElement BuildPointCore(GmlPoint p) =>
        new(Kml + "Point", new XElement(Kml + "coordinates", FormatCoord(p.Coordinate)));

    private static XElement BuildLineStringCore(GmlLineString ls) =>
        BuildLineStringFromCoords(ls.Coordinates);

    private static XElement BuildLineStringFromCoords(IReadOnlyList<GmlCoordinate> coords) =>
        new(Kml + "LineString", new XElement(Kml + "coordinates", FormatCoords(coords)));

    private static XElement BuildLinearRingCore(GmlLinearRing lr) =>
        new(Kml + "LinearRing", new XElement(Kml + "coordinates", FormatCoords(lr.Coordinates)));

    private static XElement BuildPolygonCore(GmlPolygon poly)
    {
        var polygonEl = new XElement(Kml + "Polygon",
            new XElement(Kml + "outerBoundaryIs",
                new XElement(Kml + "LinearRing",
                    new XElement(Kml + "coordinates", FormatCoords(poly.Exterior.Coordinates)))));

        foreach (var hole in poly.Interior)
            polygonEl.Add(new XElement(Kml + "innerBoundaryIs",
                new XElement(Kml + "LinearRing",
                    new XElement(Kml + "coordinates", FormatCoords(hole.Coordinates)))));

        return polygonEl;
    }

    private static XElement BuildPolygonFromBbox(GmlCoordinate ll, GmlCoordinate ur)
    {
        var ring = new[] { ll, new(ur.X, ll.Y, ll.Z), ur, new(ll.X, ur.Y, ur.Z), ll };
        return new XElement(Kml + "Polygon",
            new XElement(Kml + "outerBoundaryIs",
                new XElement(Kml + "LinearRing",
                    new XElement(Kml + "coordinates", FormatCoords(ring)))));
    }

    private static XElement BuildMultiGeometry(XElement[] members) =>
        new(Kml + "MultiGeometry", members.Cast<object>().ToArray());

    // ---- Formatting ----

    /// <summary>Formats a coordinate as KML "lon,lat[,alt]". M-ordinate is not supported by KML 2.2 and is omitted.</summary>
    private static string FormatCoord(GmlCoordinate c) =>
        c.Z.HasValue ? $"{F(c.X)},{F(c.Y)},{F(c.Z.Value)}" : $"{F(c.X)},{F(c.Y)}";

    private static string FormatCoords(IEnumerable<GmlCoordinate> coords) =>
        string.Join(" ", coords.Select(FormatCoord));

    private static string F(double d) => d.ToString(CultureInfo.InvariantCulture);
}
