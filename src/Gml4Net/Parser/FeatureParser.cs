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
        GmlEnvelope? boundedBy = null;
        var features = new List<GmlFeature>();

        foreach (var child in element.Elements())
        {
            if (XmlHelpers.IsGmlNamespace(child.Name.NamespaceName) && child.Name.LocalName == "boundedBy")
            {
                boundedBy = ParseBoundedBy(child, version, issues);
                continue;
            }

            if (XmlHelpers.IsGmlNamespace(child.Name.NamespaceName) && child.Name.LocalName == "featureMember")
            {
                var feature = ParseFeatureMember(child, version, issues);
                if (feature is not null) features.Add(feature);
                continue;
            }

            if (XmlHelpers.IsWfsNamespace(child.Name.NamespaceName) && child.Name.LocalName == "member")
            {
                var feature = ParseFeatureMember(child, version, issues);
                if (feature is not null) features.Add(feature);
                continue;
            }

            if (XmlHelpers.IsGmlNamespace(child.Name.NamespaceName) && child.Name.LocalName == "featureMembers")
            {
                foreach (var featureEl in child.Elements())
                {
                    var feature = ParseFeature(featureEl, version, issues);
                    if (feature is not null)
                        features.Add(feature);
                }
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
        var properties = new List<GmlPropertyEntry>();

        foreach (var child in element.Elements())
        {
            // Skip boundedBy — it's metadata, not a property
            if (XmlHelpers.IsGmlNamespace(child.Name.NamespaceName) && child.Name.LocalName == "boundedBy")
                continue;

            var propName = child.Name.LocalName;
            var propValue = ParsePropertyValue(child, version, issues);

            if (propValue is not null)
                properties.Add(new GmlPropertyEntry { Name = propName, Value = propValue });
        }

        return new GmlFeature
        {
            Id = id,
            Properties = new GmlPropertyBag(properties)
        };
    }

    /// <summary>
    /// Parses a property value from a feature child element.
    /// </summary>
    private static GmlPropertyValue? ParsePropertyValue(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        // Check if any child is a known geometry element
        var gmlChild = element.Elements().FirstOrDefault(e =>
            XmlHelpers.IsGmlNamespace(e.Name.NamespaceName) && XmlHelpers.IsGeometryElement(e.Name.LocalName));

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

            if (TryParseNumericProperty(text, out var numValue))
                return new GmlNumericProperty { Value = numValue };

            return new GmlStringProperty { Value = text };
        }

        // Has non-geometry child elements → nested or raw XML
        var children = element.Elements().ToList();

        // If children are non-GML elements, treat as nested properties
        if (children.All(c => !XmlHelpers.IsGmlNamespace(c.Name.NamespaceName)))
        {
            var nested = new List<GmlPropertyEntry>();
            foreach (var child in children)
            {
                var childValue = ParsePropertyValue(child, version, issues);
                if (childValue is not null)
                    nested.Add(new GmlPropertyEntry { Name = child.Name.LocalName, Value = childValue });
            }

            if (nested.Count > 0)
                return new GmlNestedProperty { Children = new GmlPropertyBag(nested) };
        }

        // Fallback: raw XML
        return new GmlRawXmlProperty { XmlContent = element.ToString() };
    }

    /// <summary>
    /// Finds the first non-wrapper child element inside a featureMember/member.
    /// </summary>
    private static GmlEnvelope? ParseBoundedBy(XElement boundedByEl, GmlVersion version, List<GmlParseIssue> issues)
    {
        var envEl = XmlHelpers.FindGmlChild(boundedByEl, "Envelope");
        if (envEl is not null)
            return GeometryParser.Parse(envEl, version, issues) as GmlEnvelope;

        var boxEl = XmlHelpers.FindGmlChild(boundedByEl, "Box");
        if (boxEl is null)
            return null;

        var box = GeometryParser.Parse(boxEl, version, issues) as GmlBox;
        if (box is null)
            return null;

        return new GmlEnvelope
        {
            LowerCorner = box.LowerCorner,
            UpperCorner = box.UpperCorner,
            SrsName = box.SrsName
        };
    }

    private static GmlFeature? ParseFeatureMember(XElement memberElement, GmlVersion version, List<GmlParseIssue> issues)
    {
        var featureEl = FindFeatureChild(memberElement);
        if (featureEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Warning,
                Code = "missing_feature_member",
                Message = $"Feature wrapper '{memberElement.Name.LocalName}' does not contain a feature element.",
                Location = memberElement.Name.LocalName
            });
            return null;
        }

        return ParseFeature(featureEl, version, issues);
    }

    private static XElement? FindFeatureChild(XElement memberElement)
    {
        return memberElement.Elements()
            .FirstOrDefault(e => !XmlHelpers.IsGmlNamespace(e.Name.NamespaceName)
                              && !XmlHelpers.IsWfsNamespace(e.Name.NamespaceName));
    }

    private static bool TryParseNumericProperty(string text, out double value)
    {
        value = 0;

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || double.IsNaN(parsed) || double.IsInfinity(parsed))
        {
            return false;
        }

        if (LooksLikeFloatingPoint(text))
        {
            var unsigned = text[0] is '+' or '-' ? text[1..] : text;
            if (unsigned.Length > 1 && unsigned[0] == '0' && unsigned[1] != '.' && unsigned[1] != 'e' && unsigned[1] != 'E')
                return false;

            value = parsed;
            return true;
        }

        return false;
    }

    private static bool LooksLikeFloatingPoint(string text)
    {
        var hasDecimalOrExponent = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiDigit(c))
                continue;

            if (c is '+' or '-')
            {
                if (i == 0)
                    continue;

                var previous = text[i - 1];
                if (previous is 'e' or 'E')
                    continue;

                return false;
            }

            if (c is '.' or 'e' or 'E')
            {
                hasDecimalOrExponent = true;
                continue;
            }

            return false;
        }

        return hasDecimalOrExponent;
    }
}
