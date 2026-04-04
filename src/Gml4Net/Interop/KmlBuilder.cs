using System.Globalization;
using System.Xml.Linq;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Converts GML model objects to KML (Keyhole Markup Language) XML.
/// </summary>
public static class KmlBuilder
{
    private static readonly XNamespace Kml = "http://www.opengis.net/kml/2.2";

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
            GmlPoint p => BuildPoint(p),
            GmlLineString ls => BuildLineString(ls),
            GmlLinearRing lr => BuildLinearRing(lr),
            GmlPolygon poly => BuildPolygon(poly),
            GmlEnvelope env => BuildPolygonFromBbox(env.LowerCorner, env.UpperCorner),
            GmlBox box => BuildPolygonFromBbox(box.LowerCorner, box.UpperCorner),
            GmlCurve c => BuildLineString(c.Coordinates),
            GmlSurface s => BuildMultiGeometry(s.Patches.Select(p => BuildPolygon(p)).ToArray()),
            GmlMultiPoint mp => BuildMultiGeometry(mp.Points.Select(p => BuildPoint(p)).ToArray()),
            GmlMultiLineString mls => BuildMultiGeometry(mls.LineStrings.Select(ls => BuildLineString(ls)).ToArray()),
            GmlMultiPolygon mpoly => BuildMultiGeometry(mpoly.Polygons.Select(p => BuildPolygon(p)).ToArray()),
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

        // Build description from properties
        var descParts = new List<string>();
        foreach (var entry in feature.Properties.Entries)
        {
            if (entry.Value is GmlGeometryProperty gp)
            {
                var kmlGeom = Geometry(gp.Geometry);
                if (kmlGeom is not null)
                    placemark.Add(kmlGeom);
            }
            else if (entry.Value is GmlStringProperty sp)
            {
                descParts.Add($"{entry.Name}: {sp.Value}");
            }
            else if (entry.Value is GmlNumericProperty np)
            {
                descParts.Add($"{entry.Name}: {np.Value.ToString(CultureInfo.InvariantCulture)}");
            }
        }

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

        return new XElement(Kml + "kml",
            new XAttribute(XNamespace.Xmlns + "kml", Kml.NamespaceName),
            document);
    }

    /// <summary>
    /// Converts a <see cref="GmlGeometry"/> to a KML XML string.
    /// </summary>
    /// <param name="geometry">The GML geometry to convert.</param>
    /// <returns>A KML XML string, or null if not supported.</returns>
    public static string? GeometryToKml(GmlGeometry geometry) =>
        Geometry(geometry)?.ToString();

    // ---- Private builders ----

    /// <summary>Builds a KML Point element.</summary>
    private static XElement BuildPoint(GmlPoint p) =>
        new(Kml + "Point", new XElement(Kml + "coordinates", FormatCoord(p.Coordinate)));

    /// <summary>Builds a KML LineString element.</summary>
    private static XElement BuildLineString(GmlLineString ls) =>
        BuildLineString(ls.Coordinates);

    /// <summary>Builds a KML LineString element from coordinates.</summary>
    private static XElement BuildLineString(IReadOnlyList<GmlCoordinate> coords) =>
        new(Kml + "LineString", new XElement(Kml + "coordinates", FormatCoords(coords)));

    /// <summary>Builds a KML LineString from a LinearRing.</summary>
    private static XElement BuildLinearRing(GmlLinearRing lr) =>
        new(Kml + "LinearRing", new XElement(Kml + "coordinates", FormatCoords(lr.Coordinates)));

    /// <summary>Builds a KML Polygon element.</summary>
    private static XElement BuildPolygon(GmlPolygon poly)
    {
        var polygonEl = new XElement(Kml + "Polygon",
            new XElement(Kml + "outerBoundaryIs",
                new XElement(Kml + "LinearRing",
                    new XElement(Kml + "coordinates", FormatCoords(poly.Exterior.Coordinates)))));

        foreach (var hole in poly.Interior)
        {
            polygonEl.Add(new XElement(Kml + "innerBoundaryIs",
                new XElement(Kml + "LinearRing",
                    new XElement(Kml + "coordinates", FormatCoords(hole.Coordinates)))));
        }

        return polygonEl;
    }

    /// <summary>Builds a KML Polygon from a bounding box.</summary>
    private static XElement BuildPolygonFromBbox(GmlCoordinate ll, GmlCoordinate ur)
    {
        var ring = new[] { ll, new(ur.X, ll.Y, ll.Z), ur, new(ll.X, ur.Y, ur.Z), ll };
        return new XElement(Kml + "Polygon",
            new XElement(Kml + "outerBoundaryIs",
                new XElement(Kml + "LinearRing",
                    new XElement(Kml + "coordinates", FormatCoords(ring)))));
    }

    /// <summary>Builds a KML MultiGeometry element.</summary>
    private static XElement BuildMultiGeometry(XElement[] members) =>
        new(Kml + "MultiGeometry", members.Cast<object>().ToArray());

    // ---- Formatting ----

    /// <summary>Formats a coordinate as KML "lon,lat[,alt]".</summary>
    private static string FormatCoord(GmlCoordinate c) =>
        c.Z.HasValue
            ? $"{F(c.X)},{F(c.Y)},{F(c.Z.Value)}"
            : $"{F(c.X)},{F(c.Y)}";

    /// <summary>Formats a coordinate list as space-separated KML coordinate tuples.</summary>
    private static string FormatCoords(IEnumerable<GmlCoordinate> coords) =>
        string.Join(" ", coords.Select(FormatCoord));

    /// <summary>Formats a double using invariant culture.</summary>
    private static string F(double d) => d.ToString(CultureInfo.InvariantCulture);
}
