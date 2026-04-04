using System.Globalization;
using System.Xml.Linq;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Parser;

/// <summary>
/// Internal parser for GML features and feature collections.
/// </summary>
internal static class FeatureParser
{
    /// <summary>
    /// Parses a FeatureCollection element (GML or WFS root).
    /// </summary>
    internal static GmlFeatureCollection? ParseCollection(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        // Extract boundedBy
        GmlEnvelope? boundedBy = null;
        var boundedByEl = XmlHelpers.FindGmlChild(element, "boundedBy");
        if (boundedByEl is not null)
        {
            var envEl = XmlHelpers.FindGmlChild(boundedByEl, "Envelope");
            if (envEl is not null)
            {
                boundedBy = GeometryParser.Parse(envEl, version, issues) as GmlEnvelope;
            }
            else
            {
                var boxEl = XmlHelpers.FindGmlChild(boundedByEl, "Box");
                if (boxEl is not null)
                {
                    var box = GeometryParser.Parse(boxEl, version, issues) as GmlBox;
                    if (box is not null)
                    {
                        boundedBy = new GmlEnvelope
                        {
                            LowerCorner = box.LowerCorner,
                            UpperCorner = box.UpperCorner,
                            SrsName = box.SrsName
                        };
                    }
                }
            }
        }

        var features = new List<GmlFeature>();

        // gml:featureMember (singular, GML 2 / WFS 1.0-1.1)
        foreach (var memberEl in XmlHelpers.FindGmlChildren(element, "featureMember"))
        {
            var featureEl = FindFeatureChild(memberEl);
            if (featureEl is not null)
            {
                var feature = ParseFeature(featureEl, version, issues);
                if (feature is not null)
                    features.Add(feature);
            }
        }

        // wfs:member (WFS 2.0)
        foreach (var memberEl in XmlHelpers.FindWfsChildren(element, "member"))
        {
            var featureEl = FindFeatureChild(memberEl);
            if (featureEl is not null)
            {
                var feature = ParseFeature(featureEl, version, issues);
                if (feature is not null)
                    features.Add(feature);
            }
        }

        // gml:featureMembers (plural, GML 3.1)
        foreach (var membersEl in XmlHelpers.FindGmlChildren(element, "featureMembers"))
        {
            foreach (var child in membersEl.Elements())
            {
                var feature = ParseFeature(child, version, issues);
                if (feature is not null)
                    features.Add(feature);
            }
        }

        return new GmlFeatureCollection
        {
            Features = features,
            BoundedBy = boundedBy
        };
    }

    /// <summary>
    /// Parses a single feature element.
    /// </summary>
    internal static GmlFeature? ParseFeature(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var id = XmlHelpers.GetFeatureId(element);
        var properties = new Dictionary<string, GmlPropertyValue>();

        foreach (var child in element.Elements())
        {
            // Skip boundedBy — it's metadata, not a property
            if (XmlHelpers.IsGmlNamespace(child.Name.NamespaceName) && child.Name.LocalName == "boundedBy")
                continue;

            var propName = child.Name.LocalName;
            var propValue = ParsePropertyValue(child, version, issues);

            if (propValue is not null)
            {
                // Handle duplicate property names by appending index
                if (properties.ContainsKey(propName))
                {
                    var idx = 2;
                    while (properties.ContainsKey($"{propName}_{idx}"))
                        idx++;
                    propName = $"{propName}_{idx}";
                }
                properties[propName] = propValue;
            }
        }

        return new GmlFeature
        {
            Id = id,
            Properties = properties
        };
    }

    /// <summary>
    /// Parses a property value from a feature child element.
    /// </summary>
    private static GmlPropertyValue? ParsePropertyValue(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        // Check if any child is a known geometry element
        var gmlChild = element.Elements().FirstOrDefault(e =>
            XmlHelpers.IsGmlNamespace(e.Name.NamespaceName) && IsGeometryLocalName(e.Name.LocalName));

        if (gmlChild is not null)
        {
            var geometry = GeometryParser.Parse(gmlChild, version, issues);
            if (geometry is not null)
                return new GmlGeometryProperty { Geometry = geometry };
        }

        // No child elements → leaf node
        if (!element.HasElements)
        {
            var text = element.Value.Trim();

            if (string.IsNullOrEmpty(text))
                return new GmlStringProperty { Value = string.Empty };

            // Try numeric
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var numValue))
                return new GmlNumericProperty { Value = numValue };

            return new GmlStringProperty { Value = text };
        }

        // Has non-geometry child elements → nested or raw XML
        var children = element.Elements().ToList();

        // If children are non-GML elements, treat as nested properties
        if (children.All(c => !XmlHelpers.IsGmlNamespace(c.Name.NamespaceName)))
        {
            var nested = new Dictionary<string, GmlPropertyValue>();
            foreach (var child in children)
            {
                var childValue = ParsePropertyValue(child, version, issues);
                if (childValue is not null)
                {
                    var key = child.Name.LocalName;
                    if (nested.ContainsKey(key))
                    {
                        var idx = 2;
                        while (nested.ContainsKey($"{key}_{idx}"))
                            idx++;
                        key = $"{key}_{idx}";
                    }
                    nested[key] = childValue;
                }
            }

            if (nested.Count > 0)
                return new GmlNestedProperty { Children = nested };
        }

        // Fallback: raw XML
        return new GmlRawXmlProperty { XmlContent = element.ToString() };
    }

    /// <summary>
    /// Finds the first non-wrapper child element inside a featureMember/member.
    /// </summary>
    private static XElement? FindFeatureChild(XElement memberElement)
    {
        // The first child element that is not in a GML/WFS namespace,
        // or the first child element overall
        var nonGmlChild = memberElement.Elements()
            .FirstOrDefault(e => !XmlHelpers.IsGmlNamespace(e.Name.NamespaceName)
                              && !XmlHelpers.IsWfsNamespace(e.Name.NamespaceName));

        return nonGmlChild ?? memberElement.Elements().FirstOrDefault();
    }

    private static bool IsGeometryLocalName(string localName) => localName is
        "Point" or "LineString" or "LinearRing" or "Polygon" or
        "Envelope" or "Box" or "Curve" or "Surface" or
        "MultiPoint" or "MultiLineString" or "MultiPolygon" or
        "MultiCurve" or "MultiSurface" or "MultiGeometry";
}
