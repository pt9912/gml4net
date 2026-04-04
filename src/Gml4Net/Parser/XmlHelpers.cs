using System.Globalization;
using System.Xml.Linq;
using Gml4Net.Model;

namespace Gml4Net.Parser;

/// <summary>
/// Internal helper methods for namespace handling, element lookup and coordinate parsing.
/// </summary>
internal static class XmlHelpers
{
    private static readonly char[] WhitespaceSeparators = [' ', '\t', '\n', '\r'];

    /// <summary>
    /// Returns true if the namespace URI is a known GML namespace.
    /// </summary>
    internal static bool IsGmlNamespace(string? ns) =>
        ns == GmlNamespaces.Gml || ns == GmlNamespaces.Gml32 || ns == GmlNamespaces.Gml33;

    /// <summary>
    /// Returns true if the namespace URI is a known WFS namespace.
    /// </summary>
    internal static bool IsWfsNamespace(string? ns) =>
        ns == GmlNamespaces.Wfs1 || ns == GmlNamespaces.Wfs2;

    /// <summary>
    /// Returns true if the local name is a known GML geometry element.
    /// </summary>
    internal static bool IsGeometryElement(string localName) => localName is
        "Point" or "LineString" or "LinearRing" or "Polygon" or
        "Envelope" or "Box" or "Curve" or "Surface" or
        "MultiPoint" or "MultiLineString" or "MultiPolygon" or
        "MultiCurve" or "MultiSurface" or "MultiGeometry";

    /// <summary>
    /// Finds a direct child element with the given local name in any GML namespace.
    /// </summary>
    internal static XElement? FindGmlChild(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(e =>
            e.Name.LocalName == localName && IsGmlNamespace(e.Name.NamespaceName));
    }

    /// <summary>
    /// Finds all direct child elements with the given local name in any GML namespace.
    /// </summary>
    internal static IEnumerable<XElement> FindGmlChildren(XElement parent, string localName)
    {
        return parent.Elements().Where(e =>
            e.Name.LocalName == localName && IsGmlNamespace(e.Name.NamespaceName));
    }

    /// <summary>
    /// Finds all direct child elements with the given local name in any WFS namespace.
    /// </summary>
    internal static IEnumerable<XElement> FindWfsChildren(XElement parent, string localName)
    {
        return parent.Elements().Where(e =>
            e.Name.LocalName == localName && IsWfsNamespace(e.Name.NamespaceName));
    }

    /// <summary>
    /// Extracts the srsName attribute from an element.
    /// </summary>
    internal static string? GetSrsName(XElement element) =>
        element.Attribute("srsName")?.Value;

    /// <summary>
    /// Extracts the feature ID from gml:id or fid attribute.
    /// </summary>
    internal static string? GetFeatureId(XElement element) =>
        element.Attribute(GmlNamespaces.NsGml32 + "id")?.Value
        ?? element.Attribute(GmlNamespaces.NsGml + "id")?.Value
        ?? element.Attribute("id")?.Value
        ?? element.Attribute("fid")?.Value;

    /// <summary>
    /// Extracts the srsDimension attribute, defaulting to the given value.
    /// </summary>
    internal static int GetSrsDimension(XElement element, int defaultValue = 2)
    {
        var attr = element.Attribute("srsDimension");
        if (attr is not null && int.TryParse(attr.Value, out var dim))
            return dim;
        return defaultValue;
    }

    /// <summary>
    /// Detects the GML version from namespace declarations in the document.
    /// Single-pass traversal with early exit for performance.
    /// </summary>
    internal static GmlVersion DetectVersion(XDocument doc)
    {
        var root = doc.Root;
        if (root is null)
            return GmlVersion.V3_2;

        bool hasGml = false;
        bool hasGml2Indicators = false;

        foreach (var el in root.DescendantsAndSelf())
        {
            var elNs = el.Name.NamespaceName;

            // Check element namespace
            if (elNs == GmlNamespaces.Gml33) return GmlVersion.V3_3;
            if (elNs == GmlNamespaces.Gml32) return GmlVersion.V3_2;
            if (elNs == GmlNamespaces.Gml) hasGml = true;

            // Check namespace declarations on this element
            foreach (var attr in el.Attributes())
            {
                if (!attr.IsNamespaceDeclaration) continue;
                var nsUri = attr.Value;
                if (nsUri == GmlNamespaces.Gml33) return GmlVersion.V3_3;
                if (nsUri == GmlNamespaces.Gml32) return GmlVersion.V3_2;
                if (nsUri == GmlNamespaces.Gml) hasGml = true;
            }

            // Check GML 2 indicators while traversing
            if (!hasGml2Indicators && IsGmlNamespace(elNs))
            {
                var localName = el.Name.LocalName;
                if (localName is "coordinates" or "Box" or "outerBoundaryIs")
                    hasGml2Indicators = true;
            }
        }

        if (hasGml)
            return hasGml2Indicators ? GmlVersion.V2_1_2 : GmlVersion.V3_1;

        return GmlVersion.V3_2;
    }

    /// <summary>
    /// Parses a GML 3 pos element value (e.g. "10.0 20.0") into a coordinate.
    /// Returns false via issues list if values are not valid numbers.
    /// </summary>
    internal static GmlCoordinate ParsePos(string text, int? srsDimension = null, List<GmlParseIssue>? issues = null)
    {
        var parts = text.Trim().Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return new GmlCoordinate(0, 0);

        if (!TryParseDouble(parts[0], out double x) || !TryParseDouble(parts[1], out double y))
        {
            issues?.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_coordinate",
                Message = $"Cannot parse coordinate values: '{text.Trim()}'",
                Location = "pos"
            });
            return new GmlCoordinate(0, 0);
        }

        double? z = parts.Length > 2 && TryParseDouble(parts[2], out var zVal) ? zVal : null;
        double? m = parts.Length > 3 && TryParseDouble(parts[3], out var mVal) ? mVal : null;

        if (srsDimension == 2)
            return new GmlCoordinate(x, y);

        return new GmlCoordinate(x, y, z, m);
    }

    /// <summary>
    /// Parses a GML 3 posList element value into a list of coordinates.
    /// </summary>
    internal static IReadOnlyList<GmlCoordinate> ParsePosList(string text, int srsDimension, List<GmlParseIssue>? issues = null)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return [];

        var parts = text.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
        var values = new List<double>(parts.Length);
        foreach (var part in parts)
        {
            if (TryParseDouble(part, out var val))
            {
                values.Add(val);
            }
            else
            {
                issues?.Add(new GmlParseIssue
                {
                    Severity = GmlIssueSeverity.Error,
                    Code = "invalid_coordinate",
                    Message = $"Cannot parse coordinate value: '{part}'",
                    Location = "posList"
                });
                return [];
            }
        }

        if (srsDimension < 2) srsDimension = 2;

        var coords = new List<GmlCoordinate>(values.Count / srsDimension);
        for (int i = 0; i + srsDimension <= values.Count; i += srsDimension)
        {
            double x = values[i];
            double y = values[i + 1];
            double? z = srsDimension >= 3 && i + 2 < values.Count ? values[i + 2] : null;
            double? m2 = srsDimension >= 4 && i + 3 < values.Count ? values[i + 3] : null;
            coords.Add(new GmlCoordinate(x, y, z, m2));
        }

        return coords;
    }

    /// <summary>
    /// Parses a GML 2 coordinates element value (e.g. "10.0,20.0 30.0,40.0").
    /// </summary>
    internal static IReadOnlyList<GmlCoordinate> ParseGml2Coordinates(string text, List<GmlParseIssue>? issues = null)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return [];

        var tuples = text.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
        var coords = new List<GmlCoordinate>(tuples.Length);

        foreach (var tuple in tuples)
        {
            var parts = tuple.Split(',');
            if (parts.Length < 2) continue;

            if (!TryParseDouble(parts[0].Trim(), out var x) || !TryParseDouble(parts[1].Trim(), out var y))
            {
                issues?.Add(new GmlParseIssue
                {
                    Severity = GmlIssueSeverity.Error,
                    Code = "invalid_coordinate",
                    Message = $"Cannot parse coordinate tuple: '{tuple}'",
                    Location = "coordinates"
                });
                continue;
            }

            double? z = parts.Length > 2 && TryParseDouble(parts[2].Trim(), out var zVal) ? zVal : null;
            coords.Add(new GmlCoordinate(x, y, z));
        }

        return coords;
    }

    private static bool TryParseDouble(string s, out double result) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
}
