using System.Xml.Linq;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Parser;

/// <summary>
/// Internal parser for GML geometry elements.
/// </summary>
internal static class GeometryParser
{
    /// <summary>
    /// Parses an XElement into a GmlGeometry, or null if parsing fails.
    /// </summary>
    internal static GmlGeometry? Parse(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var localName = element.Name.LocalName;

        GmlGeometry? result = localName switch
        {
            "Point" => ParsePoint(element, version, issues),
            "LineString" => ParseLineString(element, version, issues),
            "LinearRing" => ParseLinearRing(element, version, issues),
            "Polygon" => ParsePolygon(element, version, issues),
            "Envelope" => ParseEnvelope(element, version, issues),
            "Box" => ParseBox(element, version, issues),
            "Curve" => ParseCurve(element, version, issues),
            "Surface" => ParseSurface(element, version, issues),
            "MultiPoint" => ParseMultiPoint(element, version, issues),
            "MultiLineString" => ParseMultiLineString(element, version, issues),
            "MultiPolygon" => ParseMultiPolygon(element, version, issues),
            "MultiCurve" => ParseMultiCurve(element, version, issues),
            "MultiSurface" => ParseMultiSurface(element, version, issues),
            "MultiGeometry" => ParseMultiGeometry(element, version, issues),
            _ => null
        };

        if (result is null && XmlHelpers.IsGmlNamespace(element.Name.NamespaceName))
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Warning,
                Code = "unsupported_geometry",
                Message = $"Unsupported geometry type: {localName}",
                Location = localName
            });
        }

        return result;
    }

    private static GmlPoint? ParsePoint(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var srsDim = XmlHelpers.GetSrsDimension(element);

        // GML 3: <pos>
        var posEl = XmlHelpers.FindGmlChild(element, "pos");
        if (posEl is not null)
        {
            var coord = XmlHelpers.ParsePos(posEl.Value, srsDim, issues);
            return new GmlPoint { Coordinate = coord, SrsName = srsName, Version = version };
        }

        // GML 2: <coordinates>
        var coordsEl = XmlHelpers.FindGmlChild(element, "coordinates");
        if (coordsEl is not null)
        {
            var coords = XmlHelpers.ParseGml2Coordinates(coordsEl.Value, issues);
            if (coords.Count > 0)
                return new GmlPoint { Coordinate = coords[0], SrsName = srsName, Version = version };
        }

        // GML 2: <coord>
        var coordEl = XmlHelpers.FindGmlChild(element, "coord");
        if (coordEl is not null)
        {
            var coord = ParseCoordElement(coordEl, issues);
            return new GmlPoint { Coordinate = coord, SrsName = srsName, Version = version };
        }

        issues.Add(new GmlParseIssue
        {
            Severity = GmlIssueSeverity.Error,
            Code = "missing_coordinates",
            Message = "Point element has no pos, coordinates, or coord child",
            Location = "Point"
        });
        return null;
    }

    private static GmlLineString? ParseLineString(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var coords = ParseCoordinateList(element, issues);

        if (coords is null || coords.Count == 0)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_coordinates",
                Message = "LineString has no coordinates",
                Location = "LineString"
            });
            return null;
        }

        return new GmlLineString { Coordinates = coords, SrsName = srsName, Version = version };
    }

    private static GmlLinearRing? ParseLinearRing(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var coords = ParseCoordinateList(element, issues);

        if (coords is null || coords.Count == 0)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_coordinates",
                Message = "LinearRing has no coordinates",
                Location = "LinearRing"
            });
            return null;
        }

        return new GmlLinearRing { Coordinates = coords, SrsName = srsName, Version = version };
    }

    private static GmlPolygon? ParsePolygon(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);

        // GML 3: exterior/interior
        var exteriorEl = XmlHelpers.FindGmlChild(element, "exterior");
        // GML 2: outerBoundaryIs/innerBoundaryIs
        exteriorEl ??= XmlHelpers.FindGmlChild(element, "outerBoundaryIs");

        if (exteriorEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_exterior",
                Message = "Polygon has no exterior/outerBoundaryIs",
                Location = "Polygon"
            });
            return null;
        }

        var ringEl = XmlHelpers.FindGmlChild(exteriorEl, "LinearRing");
        if (ringEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_linear_ring",
                Message = "Polygon exterior has no LinearRing",
                Location = "Polygon"
            });
            return null;
        }

        var exterior = ParseLinearRing(ringEl, version, issues);
        if (exterior is null) return null;

        // Parse interior rings
        var interiorEls = XmlHelpers.FindGmlChildren(element, "interior")
            .Concat(XmlHelpers.FindGmlChildren(element, "innerBoundaryIs"));

        var interiorRings = new List<GmlLinearRing>();
        foreach (var intEl in interiorEls)
        {
            var intRingEl = XmlHelpers.FindGmlChild(intEl, "LinearRing");
            if (intRingEl is not null)
            {
                var ring = ParseLinearRing(intRingEl, version, issues);
                if (ring is not null)
                    interiorRings.Add(ring);
            }
        }

        return new GmlPolygon
        {
            Exterior = exterior,
            Interior = interiorRings,
            SrsName = srsName,
            Version = version
        };
    }

    private static GmlEnvelope? ParseEnvelope(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var srsDim = XmlHelpers.GetSrsDimension(element);

        var lowerEl = XmlHelpers.FindGmlChild(element, "lowerCorner");
        var upperEl = XmlHelpers.FindGmlChild(element, "upperCorner");

        if (lowerEl is null || upperEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_corners",
                Message = "Envelope missing lowerCorner or upperCorner",
                Location = "Envelope"
            });
            return null;
        }

        return new GmlEnvelope
        {
            LowerCorner = XmlHelpers.ParsePos(lowerEl.Value, srsDim, issues),
            UpperCorner = XmlHelpers.ParsePos(upperEl.Value, srsDim, issues),
            SrsName = srsName,
            Version = version
        };
    }

    private static GmlBox? ParseBox(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);

        var coordsEl = XmlHelpers.FindGmlChild(element, "coordinates");
        if (coordsEl is not null)
        {
            var coords = XmlHelpers.ParseGml2Coordinates(coordsEl.Value, issues);
            if (coords.Count >= 2)
            {
                return new GmlBox
                {
                    LowerCorner = coords[0],
                    UpperCorner = coords[1],
                    SrsName = srsName,
                    Version = version
                };
            }
        }

        // Try <coord> elements
        var coordEls = XmlHelpers.FindGmlChildren(element, "coord").ToList();
        if (coordEls.Count >= 2)
        {
            return new GmlBox
            {
                LowerCorner = ParseCoordElement(coordEls[0], issues),
                UpperCorner = ParseCoordElement(coordEls[1], issues),
                SrsName = srsName,
                Version = version
            };
        }

        issues.Add(new GmlParseIssue
        {
            Severity = GmlIssueSeverity.Error,
            Code = "missing_coordinates",
            Message = "Box has no coordinates or coord children",
            Location = "Box"
        });
        return null;
    }

    private static GmlCurve? ParseCurve(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var segmentsEl = XmlHelpers.FindGmlChild(element, "segments");

        if (segmentsEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Warning,
                Code = "missing_segments",
                Message = "Curve has no segments element",
                Location = "Curve"
            });
            return null;
        }

        var allCoords = new List<GmlCoordinate>();
        foreach (var seg in segmentsEl.Elements())
        {
            if (!XmlHelpers.IsGmlNamespace(seg.Name.NamespaceName)) continue;

            var segCoords = ParseCoordinateList(seg, issues);
            if (segCoords is not null)
            {
                // Avoid duplicate points at segment boundaries
                if (allCoords.Count > 0 && segCoords.Count > 0 && allCoords[^1] == segCoords[0])
                    allCoords.AddRange(segCoords.Skip(1));
                else
                    allCoords.AddRange(segCoords);
            }
        }

        return new GmlCurve { Coordinates = allCoords, SrsName = srsName, Version = version };
    }

    private static GmlSurface? ParseSurface(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var patchesEl = XmlHelpers.FindGmlChild(element, "patches")
                        ?? XmlHelpers.FindGmlChild(element, "polygonPatches");

        if (patchesEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Warning,
                Code = "missing_patches",
                Message = "Surface has no patches element",
                Location = "Surface"
            });
            return null;
        }

        var patches = new List<GmlPolygon>();
        foreach (var patchEl in XmlHelpers.FindGmlChildren(patchesEl, "PolygonPatch"))
        {
            var poly = ParsePolygon(patchEl, version, issues);
            if (poly is not null)
                patches.Add(poly);
        }

        return new GmlSurface { Patches = patches, SrsName = srsName, Version = version };
    }

    private static GmlMultiPoint? ParseMultiPoint(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var points = new List<GmlPoint>();

        foreach (var memberEl in XmlHelpers.FindGmlChildren(element, "pointMember"))
        {
            var pointEl = XmlHelpers.FindGmlChild(memberEl, "Point");
            if (pointEl is not null)
            {
                var pt = ParsePoint(pointEl, version, issues);
                if (pt is not null)
                    points.Add(pt);
            }
        }

        var membersEl = XmlHelpers.FindGmlChild(element, "pointMembers");
        if (membersEl is not null)
        {
            foreach (var pointEl in XmlHelpers.FindGmlChildren(membersEl, "Point"))
            {
                var pt = ParsePoint(pointEl, version, issues);
                if (pt is not null)
                    points.Add(pt);
            }
        }

        return new GmlMultiPoint { Points = points, SrsName = srsName, Version = version };
    }

    private static GmlMultiLineString? ParseMultiLineString(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var lineStrings = new List<GmlLineString>();

        foreach (var memberEl in XmlHelpers.FindGmlChildren(element, "lineStringMember"))
        {
            var lsEl = XmlHelpers.FindGmlChild(memberEl, "LineString");
            if (lsEl is not null)
            {
                var ls = ParseLineString(lsEl, version, issues);
                if (ls is not null)
                    lineStrings.Add(ls);
            }
        }

        return new GmlMultiLineString { LineStrings = lineStrings, SrsName = srsName, Version = version };
    }

    private static GmlMultiPolygon? ParseMultiPolygon(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var polygons = new List<GmlPolygon>();

        foreach (var memberEl in XmlHelpers.FindGmlChildren(element, "polygonMember"))
        {
            var polyEl = XmlHelpers.FindGmlChild(memberEl, "Polygon");
            if (polyEl is not null)
            {
                var poly = ParsePolygon(polyEl, version, issues);
                if (poly is not null)
                    polygons.Add(poly);
            }
        }

        return new GmlMultiPolygon { Polygons = polygons, SrsName = srsName, Version = version };
    }

    private static GmlMultiLineString? ParseMultiCurve(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var lineStrings = new List<GmlLineString>();

        foreach (var memberEl in XmlHelpers.FindGmlChildren(element, "curveMember"))
        {
            var child = memberEl.Elements().FirstOrDefault(e => XmlHelpers.IsGmlNamespace(e.Name.NamespaceName));
            if (child is null) continue;

            if (child.Name.LocalName == "LineString")
            {
                var ls = ParseLineString(child, version, issues);
                if (ls is not null)
                    lineStrings.Add(ls);
            }
            else if (child.Name.LocalName == "Curve")
            {
                var curve = ParseCurve(child, version, issues);
                if (curve is not null)
                    lineStrings.Add(new GmlLineString
                    {
                        Coordinates = curve.Coordinates,
                        SrsName = curve.SrsName,
                        Version = version
                    });
            }
        }

        return new GmlMultiLineString { LineStrings = lineStrings, SrsName = srsName, Version = version };
    }

    private static GmlMultiPolygon? ParseMultiSurface(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var polygons = new List<GmlPolygon>();

        foreach (var memberEl in XmlHelpers.FindGmlChildren(element, "surfaceMember"))
        {
            var child = memberEl.Elements().FirstOrDefault(e => XmlHelpers.IsGmlNamespace(e.Name.NamespaceName));
            if (child is null) continue;

            if (child.Name.LocalName == "Polygon")
            {
                var poly = ParsePolygon(child, version, issues);
                if (poly is not null)
                    polygons.Add(poly);
            }
            else if (child.Name.LocalName == "Surface")
            {
                var surface = ParseSurface(child, version, issues);
                if (surface is not null)
                    polygons.AddRange(surface.Patches);
            }
        }

        return new GmlMultiPolygon { Polygons = polygons, SrsName = srsName, Version = version };
    }

    /// <summary>
    /// Parses MultiGeometry by aggregating all member geometries into the best-fit
    /// multi type (MultiPoint, MultiLineString, MultiPolygon) based on actual content.
    /// </summary>
    private static GmlGeometry? ParseMultiGeometry(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var srsName = XmlHelpers.GetSrsName(element);
        var geometries = new List<GmlGeometry>();

        // geometryMember (singular) — child is wrapped
        foreach (var memberEl in XmlHelpers.FindGmlChildren(element, "geometryMember"))
        {
            var child = memberEl.Elements().FirstOrDefault(e => XmlHelpers.IsGmlNamespace(e.Name.NamespaceName));
            if (child is null) continue;

            var geom = Parse(child, version, issues);
            if (geom is not null)
                geometries.Add(geom);
        }

        // geometryMembers (plural) — children are geometry elements directly
        foreach (var membersEl in XmlHelpers.FindGmlChildren(element, "geometryMembers"))
        {
            foreach (var child in membersEl.Elements())
            {
                if (!XmlHelpers.IsGmlNamespace(child.Name.NamespaceName)) continue;

                var geom = Parse(child, version, issues);
                if (geom is not null)
                    geometries.Add(geom);
            }
        }

        if (geometries.Count == 0) return null;
        if (geometries.Count == 1) return geometries[0];

        // Aggregate homogeneous collections
        if (geometries.All(g => g is GmlPoint))
            return new GmlMultiPoint
            {
                Points = geometries.Cast<GmlPoint>().ToList(),
                SrsName = srsName,
                Version = version
            };

        if (geometries.All(g => g is GmlLineString))
            return new GmlMultiLineString
            {
                LineStrings = geometries.Cast<GmlLineString>().ToList(),
                SrsName = srsName,
                Version = version
            };

        if (geometries.All(g => g is GmlPolygon))
            return new GmlMultiPolygon
            {
                Polygons = geometries.Cast<GmlPolygon>().ToList(),
                SrsName = srsName,
                Version = version
            };

        // Heterogeneous: keep only geometries matching the dominant (most frequent) type
        var dominantType = geometries.GroupBy(g => g.GetType())
            .OrderByDescending(g => g.Count())
            .First();

        issues.Add(new GmlParseIssue
        {
            Severity = GmlIssueSeverity.Warning,
            Code = "heterogeneous_multi_geometry",
            Message = $"MultiGeometry contains {geometries.Count} mixed geometry types; " +
                      $"keeping {dominantType.Count()} {dominantType.Key.Name} elements, " +
                      $"dropping {geometries.Count - dominantType.Count()} others",
            Location = "MultiGeometry"
        });

        var firstType = dominantType.Key;
        var sameType = dominantType.ToList();

        if (firstType == typeof(GmlPoint))
            return new GmlMultiPoint { Points = sameType.Cast<GmlPoint>().ToList(), SrsName = srsName, Version = version };
        if (firstType == typeof(GmlLineString))
            return new GmlMultiLineString { LineStrings = sameType.Cast<GmlLineString>().ToList(), SrsName = srsName, Version = version };
        if (firstType == typeof(GmlPolygon))
            return new GmlMultiPolygon { Polygons = sameType.Cast<GmlPolygon>().ToList(), SrsName = srsName, Version = version };

        // Ultimate fallback: return first geometry
        return geometries[0];
    }

    // ---- Coordinate extraction helpers ----

    private static IReadOnlyList<GmlCoordinate>? ParseCoordinateList(XElement element, List<GmlParseIssue> issues)
    {
        var srsDim = XmlHelpers.GetSrsDimension(element);

        // GML 3: <posList>
        var posListEl = XmlHelpers.FindGmlChild(element, "posList");
        if (posListEl is not null)
        {
            var dim = XmlHelpers.GetSrsDimension(posListEl, srsDim);
            return XmlHelpers.ParsePosList(posListEl.Value, dim, issues);
        }

        // GML 3: multiple <pos> elements
        var posEls = XmlHelpers.FindGmlChildren(element, "pos").ToList();
        if (posEls.Count > 0)
        {
            var coords = new List<GmlCoordinate>(posEls.Count);
            foreach (var posEl in posEls)
            {
                coords.Add(XmlHelpers.ParsePos(posEl.Value, srsDim, issues));
            }
            return coords;
        }

        // GML 2: <coordinates>
        var coordsEl = XmlHelpers.FindGmlChild(element, "coordinates");
        if (coordsEl is not null)
        {
            return XmlHelpers.ParseGml2Coordinates(coordsEl.Value, issues);
        }

        return null;
    }

    /// <summary>
    /// Parses a GML 2 coord element with X, Y, Z children.
    /// </summary>
    private static GmlCoordinate ParseCoordElement(XElement coordEl, List<GmlParseIssue> issues)
    {
        var xEl = XmlHelpers.FindGmlChild(coordEl, "X");
        var yEl = XmlHelpers.FindGmlChild(coordEl, "Y");
        var zEl = XmlHelpers.FindGmlChild(coordEl, "Z");

        if (!TryParseCoordChild(xEl, out double x) || !TryParseCoordChild(yEl, out double y))
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_coordinate",
                Message = "Cannot parse coord X/Y values",
                Location = "coord"
            });
            return new GmlCoordinate(0, 0);
        }

        double? z = zEl is not null && TryParseCoordChild(zEl, out var zVal) ? zVal : null;
        return new GmlCoordinate(x, y, z);
    }

    private static bool TryParseCoordChild(XElement? el, out double value)
    {
        value = 0;
        return el is not null
            && double.TryParse(el.Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
