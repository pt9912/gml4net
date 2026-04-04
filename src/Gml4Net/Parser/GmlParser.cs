using System.Xml.Linq;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Parser;

/// <summary>
/// Main entry point for parsing GML documents.
/// </summary>
public static class GmlParser
{
    /// <summary>
    /// Parses a GML XML string into a <see cref="GmlParseResult"/>.
    /// </summary>
    public static GmlParseResult ParseXmlString(string xml)
    {
        var issues = new List<GmlParseIssue>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_xml",
                Message = $"Invalid XML: {ex.Message}",
                Location = ex.LineNumber > 0 ? $"Line {ex.LineNumber}, Position {ex.LinePosition}" : null
            });
            return new GmlParseResult { Issues = issues };
        }

        return ParseDocument(doc, issues);
    }

    /// <summary>
    /// Parses a GML document from a byte span.
    /// </summary>
    public static GmlParseResult ParseBytes(ReadOnlySpan<byte> bytes)
    {
        var xml = System.Text.Encoding.UTF8.GetString(bytes);
        return ParseXmlString(xml);
    }

    /// <summary>
    /// Parses a GML document from a stream.
    /// </summary>
    public static GmlParseResult ParseStream(Stream stream)
    {
        var issues = new List<GmlParseIssue>();

        XDocument doc;
        try
        {
            doc = XDocument.Load(stream);
        }
        catch (System.Xml.XmlException ex)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_xml",
                Message = $"Invalid XML: {ex.Message}",
                Location = ex.LineNumber > 0 ? $"Line {ex.LineNumber}, Position {ex.LinePosition}" : null
            });
            return new GmlParseResult { Issues = issues };
        }

        return ParseDocument(doc, issues);
    }

    private static GmlParseResult ParseDocument(XDocument doc, List<GmlParseIssue> issues)
    {
        var root = doc.Root;
        if (root is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "empty_document",
                Message = "XML document has no root element"
            });
            return new GmlParseResult { Issues = issues };
        }

        var version = XmlHelpers.DetectVersion(doc);
        var rootContent = DispatchRoot(root, version, issues);

        if (rootContent is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "unknown_root",
                Message = $"Unrecognized root element: {root.Name.LocalName} ({root.Name.NamespaceName})",
                Location = root.Name.LocalName
            });
            return new GmlParseResult { Issues = issues };
        }

        // Extract boundedBy if present
        var boundedByEl = XmlHelpers.FindGmlChild(root, "boundedBy");
        GmlEnvelope? boundedBy = null;
        if (boundedByEl is not null)
        {
            var envEl = XmlHelpers.FindGmlChild(boundedByEl, "Envelope");
            if (envEl is not null)
            {
                boundedBy = GeometryParser.Parse(envEl, version, issues) as GmlEnvelope;
            }
            else
            {
                // GML 2: Box inside boundedBy
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

        var document = new GmlDocument
        {
            Version = version,
            Root = rootContent,
            BoundedBy = boundedBy
        };

        return new GmlParseResult { Document = document, Issues = issues };
    }

    private static IGmlRootContent? DispatchRoot(XElement root, GmlVersion version, List<GmlParseIssue> issues)
    {
        var ns = root.Name.NamespaceName;
        var localName = root.Name.LocalName;

        // Geometry types
        if (XmlHelpers.IsGmlNamespace(ns) && IsGeometryElement(localName))
        {
            return GeometryParser.Parse(root, version, issues);
        }

        // FeatureCollection — will be handled in Phase 2
        // For now, recognize but report as unsupported
        if (localName == "FeatureCollection")
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Info,
                Code = "feature_collection_not_implemented",
                Message = "FeatureCollection parsing is not yet implemented"
            });
            return null;
        }

        // Coverage types — will be handled in Phase 3
        if (IsCoverageElement(localName))
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Info,
                Code = "coverage_not_implemented",
                Message = "Coverage parsing is not yet implemented"
            });
            return null;
        }

        return null;
    }

    private static bool IsGeometryElement(string localName) => localName is
        "Point" or "LineString" or "LinearRing" or "Polygon" or
        "Envelope" or "Box" or "Curve" or "Surface" or
        "MultiPoint" or "MultiLineString" or "MultiPolygon" or
        "MultiCurve" or "MultiSurface" or "MultiGeometry";

    private static bool IsCoverageElement(string localName) => localName is
        "RectifiedGridCoverage" or "GridCoverage" or
        "ReferenceableGridCoverage" or "MultiPointCoverage";
}
