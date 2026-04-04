using System.Globalization;
using System.Xml.Linq;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Parser;

/// <summary>
/// Internal parser for GML/GMLCOV coverage elements.
/// </summary>
internal static class CoverageParser
{
    /// <summary>Parses a GML coverage element and returns the corresponding coverage model.</summary>
    internal static GmlCoverage? Parse(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var localName = element.Name.LocalName;

        return localName switch
        {
            "RectifiedGridCoverage" => ParseRectifiedGridCoverage(element, version, issues),
            "GridCoverage" => ParseGridCoverage(element, version, issues),
            "ReferenceableGridCoverage" => ParseReferenceableGridCoverage(element, version, issues),
            "MultiPointCoverage" => ParseMultiPointCoverage(element, version, issues),
            _ => null
        };
    }

    /// <summary>Parses a RectifiedGridCoverage element into its model representation.</summary>
    private static GmlRectifiedGridCoverage? ParseRectifiedGridCoverage(
        XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var id = XmlHelpers.GetFeatureId(element);
        var boundedBy = ParseBoundedBy(element, version, issues);
        var rangeSet = ParseRangeSet(element);
        var rangeType = ParseRangeType(element);

        var domainSetEl = FindDomainSet(element);
        if (domainSetEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_domain_set",
                Message = "RectifiedGridCoverage has no domainSet",
                Location = "RectifiedGridCoverage"
            });
            return null;
        }

        var rectGridEl = FindGmlOrGmlcovChild(domainSetEl, "RectifiedGrid");
        if (rectGridEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_rectified_grid",
                Message = "domainSet has no RectifiedGrid",
                Location = "RectifiedGridCoverage"
            });
            return null;
        }

        var rectGrid = ParseRectifiedGrid(rectGridEl, issues);
        if (rectGrid is null) return null;

        return new GmlRectifiedGridCoverage
        {
            Id = id,
            BoundedBy = boundedBy,
            DomainSet = rectGrid,
            RangeSet = rangeSet,
            RangeType = rangeType
        };
    }

    /// <summary>Parses a GridCoverage element into its model representation.</summary>
    private static GmlGridCoverage? ParseGridCoverage(
        XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var id = XmlHelpers.GetFeatureId(element);
        var boundedBy = ParseBoundedBy(element, version, issues);
        var rangeSet = ParseRangeSet(element);
        var rangeType = ParseRangeType(element);

        var domainSetEl = FindDomainSet(element);
        if (domainSetEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_domain_set",
                Message = "GridCoverage has no domainSet",
                Location = "GridCoverage"
            });
            return null;
        }

        var gridEl = FindGmlOrGmlcovChild(domainSetEl, "Grid");
        if (gridEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_grid",
                Message = "domainSet has no Grid",
                Location = "GridCoverage"
            });
            return null;
        }

        var grid = ParseGrid(gridEl, issues);
        if (grid is null) return null;

        return new GmlGridCoverage
        {
            Id = id,
            BoundedBy = boundedBy,
            DomainSet = grid,
            RangeSet = rangeSet,
            RangeType = rangeType
        };
    }

    /// <summary>Parses a ReferenceableGridCoverage element into its model representation.</summary>
    private static GmlReferenceableGridCoverage? ParseReferenceableGridCoverage(
        XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var id = XmlHelpers.GetFeatureId(element);
        var boundedBy = ParseBoundedBy(element, version, issues);
        var rangeSet = ParseRangeSet(element);
        var rangeType = ParseRangeType(element);

        var domainSetEl = FindDomainSet(element);
        if (domainSetEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_domain_set",
                Message = "ReferenceableGridCoverage has no domainSet",
                Location = "ReferenceableGridCoverage"
            });
            return null;
        }

        var gridEl = FindGmlOrGmlcovChild(domainSetEl, "ReferenceableGrid")
                  ?? FindGmlOrGmlcovChild(domainSetEl, "Grid");
        if (gridEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_grid",
                Message = "domainSet has no ReferenceableGrid or Grid",
                Location = "ReferenceableGridCoverage"
            });
            return null;
        }

        var grid = ParseGrid(gridEl, issues);
        if (grid is null) return null;

        return new GmlReferenceableGridCoverage
        {
            Id = id,
            BoundedBy = boundedBy,
            DomainSet = grid,
            RangeSet = rangeSet,
            RangeType = rangeType
        };
    }

    /// <summary>Parses a MultiPointCoverage element into its model representation.</summary>
    private static GmlMultiPointCoverage? ParseMultiPointCoverage(
        XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var id = XmlHelpers.GetFeatureId(element);
        var boundedBy = ParseBoundedBy(element, version, issues);
        var rangeSet = ParseRangeSet(element);
        var rangeType = ParseRangeType(element);

        var domainSetEl = FindDomainSet(element);
        List<GmlPoint>? points = null;

        if (domainSetEl is not null)
        {
            var multiPointEl = FindGmlOrGmlcovChild(domainSetEl, "MultiPoint");
            if (multiPointEl is not null)
            {
                var mp = GeometryParser.Parse(multiPointEl, version, issues) as GmlMultiPoint;
                if (mp is not null)
                    points = mp.Points.ToList();
            }
        }

        return new GmlMultiPointCoverage
        {
            Id = id,
            BoundedBy = boundedBy,
            DomainPoints = points,
            RangeSet = rangeSet,
            RangeType = rangeType
        };
    }

    // ---- Grid parsing ----

    /// <summary>Parses a Grid element including its dimension, limits, and axis labels.</summary>
    private static GmlGrid? ParseGrid(XElement element, List<GmlParseIssue> issues)
    {
        var dimAttr = element.Attribute("dimension");
        int dimension = 2;
        if (dimAttr is not null && int.TryParse(dimAttr.Value, out var d))
            dimension = d;

        var limitsEl = XmlHelpers.FindGmlChild(element, "limits");
        if (limitsEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_limits",
                Message = "Grid has no limits element",
                Location = element.Name.LocalName
            });
            return null;
        }

        var gridEnvEl = XmlHelpers.FindGmlChild(limitsEl, "GridEnvelope");
        if (gridEnvEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_grid_envelope",
                Message = "Grid limits has no GridEnvelope",
                Location = element.Name.LocalName
            });
            return null;
        }

        var limits = ParseGridEnvelope(gridEnvEl, issues);
        if (limits is null) return null;

        if (limits.Low.Count != dimension || limits.High.Count != dimension)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_grid_bounds_dimension",
                Message = $"GridEnvelope bounds do not match declared dimension {dimension}",
                Location = element.Name.LocalName
            });
            return null;
        }

        var axisLabelsAttr = element.Attribute("axisLabels");
        var axisLabels = axisLabelsAttr is not null
            ? axisLabelsAttr.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
            : (IReadOnlyList<string>)[];

        return new GmlGrid
        {
            Dimension = dimension,
            Limits = limits,
            AxisLabels = axisLabels
        };
    }

    /// <summary>Parses a RectifiedGrid element including origin and offset vectors.</summary>
    private static GmlRectifiedGrid? ParseRectifiedGrid(XElement element, List<GmlParseIssue> issues)
    {
        var baseGrid = ParseGrid(element, issues);
        if (baseGrid is null) return null;

        var srsName = XmlHelpers.GetSrsName(element);

        var originEl = XmlHelpers.FindGmlChild(element, "origin");
        if (originEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_origin",
                Message = "RectifiedGrid has no origin element",
                Location = element.Name.LocalName
            });
            return null;
        }

        var origin = ParseRequiredOrigin(originEl, issues);
        if (origin is null) return null;

        var offsetVectors = new List<IReadOnlyList<double>>();
        foreach (var ovEl in XmlHelpers.FindGmlChildren(element, "offsetVector"))
        {
            var values = ParseDoubleListStrict(ovEl.Value, "offsetVector", issues);
            if (values is null)
                return null;

            if (values.Count != baseGrid.Dimension)
            {
                issues.Add(new GmlParseIssue
                {
                    Severity = GmlIssueSeverity.Error,
                    Code = "invalid_offset_vector",
                    Message = $"offsetVector length {values.Count} does not match declared dimension {baseGrid.Dimension}",
                    Location = element.Name.LocalName
                });
                return null;
            }

            offsetVectors.Add(values);
        }

        if (offsetVectors.Count < baseGrid.Dimension)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_offset_vectors",
                Message = $"RectifiedGrid requires at least {baseGrid.Dimension} offset vectors",
                Location = element.Name.LocalName
            });
            return null;
        }

        return new GmlRectifiedGrid
        {
            Dimension = baseGrid.Dimension,
            Limits = baseGrid.Limits,
            AxisLabels = baseGrid.AxisLabels,
            SrsName = srsName,
            Origin = origin.Value,
            OffsetVectors = offsetVectors
        };
    }

    /// <summary>Parses a GridEnvelope element with low and high grid bounds.</summary>
    private static GmlGridEnvelope? ParseGridEnvelope(XElement element, List<GmlParseIssue> issues)
    {
        var lowEl = XmlHelpers.FindGmlChild(element, "low");
        var highEl = XmlHelpers.FindGmlChild(element, "high");

        if (lowEl is null || highEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_grid_bounds",
                Message = "GridEnvelope missing low or high",
                Location = "GridEnvelope"
            });
            return null;
        }

        var low = ParseIntListStrict(lowEl.Value, "low", issues);
        var high = ParseIntListStrict(highEl.Value, "high", issues);
        if (low is null || high is null)
            return null;

        if (low.Count != high.Count)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_grid_bounds",
                Message = "GridEnvelope low/high bounds have different dimensions",
                Location = "GridEnvelope"
            });
            return null;
        }

        return new GmlGridEnvelope { Low = low, High = high };
    }

    // ---- Shared helpers ----

    /// <summary>Parses the boundedBy element and returns its envelope, if present.</summary>
    private static GmlEnvelope? ParseBoundedBy(XElement element, GmlVersion version, List<GmlParseIssue> issues)
    {
        var boundedByEl = XmlHelpers.FindGmlChild(element, "boundedBy");
        if (boundedByEl is null) return null;

        var envEl = XmlHelpers.FindGmlChild(boundedByEl, "Envelope");
        if (envEl is not null)
            return GeometryParser.Parse(envEl, version, issues) as GmlEnvelope;

        return null;
    }

    /// <summary>Parses the rangeSet element, extracting data block or file reference content.</summary>
    private static GmlRangeSet? ParseRangeSet(XElement element)
    {
        var rangeSetEl = XmlHelpers.FindGmlChild(element, "rangeSet")
                      ?? FindChildInNamespace(element, GmlNamespaces.Gmlcov, "rangeSet");
        if (rangeSetEl is null) return null;

        // DataBlock/tupleList
        var dataBlockEl = XmlHelpers.FindGmlChild(rangeSetEl, "DataBlock");
        string? dataBlock = null;
        if (dataBlockEl is not null)
        {
            var tupleListEl = XmlHelpers.FindGmlChild(dataBlockEl, "tupleList");
            dataBlock = tupleListEl?.Value.Trim();
        }

        // File reference
        var fileEl = XmlHelpers.FindGmlChild(rangeSetEl, "File");
        string? fileRef = null;
        if (fileEl is not null)
        {
            var rangeParamsEl = XmlHelpers.FindGmlChild(fileEl, "rangeParameters");
            fileRef = rangeParamsEl?.Value.Trim();
            if (string.IsNullOrEmpty(fileRef))
            {
                var fileNameEl = XmlHelpers.FindGmlChild(fileEl, "fileName");
                fileRef = fileNameEl?.Value.Trim();
            }
        }

        return new GmlRangeSet { DataBlock = dataBlock, FileReference = fileRef };
    }

    /// <summary>Parses the rangeType element, extracting SWE DataRecord fields.</summary>
    private static GmlRangeType? ParseRangeType(XElement element)
    {
        var rangeTypeEl = XmlHelpers.FindGmlChild(element, "rangeType")
                       ?? FindChildInNamespace(element, GmlNamespaces.Gmlcov, "rangeType");
        if (rangeTypeEl is null) return null;

        // SWE DataRecord
        var dataRecordEl = FindChildInNamespace(rangeTypeEl, GmlNamespaces.Swe, "DataRecord");
        if (dataRecordEl is null) return new GmlRangeType();

        var fields = new List<GmlRangeField>();
        foreach (var fieldEl in dataRecordEl.Elements()
            .Where(e => e.Name.LocalName == "field" && e.Name.NamespaceName == GmlNamespaces.Swe))
        {
            var name = fieldEl.Attribute("name")?.Value ?? "unknown";
            string? description = null;
            string? uom = null;

            var quantityEl = FindChildInNamespace(fieldEl, GmlNamespaces.Swe, "Quantity");
            if (quantityEl is not null)
            {
                var descEl = FindChildInNamespace(quantityEl, GmlNamespaces.Swe, "description");
                description = descEl?.Value.Trim();
                var uomEl = FindChildInNamespace(quantityEl, GmlNamespaces.Swe, "uom");
                uom = uomEl?.Attribute("code")?.Value;
            }

            fields.Add(new GmlRangeField { Name = name, Description = description, Uom = uom });
        }

        return new GmlRangeType { Fields = fields };
    }

    /// <summary>Finds the domainSet child element in GML or GMLCOV namespace.</summary>
    private static XElement? FindDomainSet(XElement element)
    {
        return XmlHelpers.FindGmlChild(element, "domainSet")
            ?? FindChildInNamespace(element, GmlNamespaces.Gmlcov, "domainSet");
    }

    /// <summary>Finds a child element by local name in either the GML or GMLCOV namespace.</summary>
    private static XElement? FindGmlOrGmlcovChild(XElement parent, string localName)
    {
        return XmlHelpers.FindGmlChild(parent, localName)
            ?? FindChildInNamespace(parent, GmlNamespaces.Gmlcov, localName);
    }

    /// <summary>Finds a child element matching the given local name and namespace.</summary>
    private static XElement? FindChildInNamespace(XElement parent, string ns, string localName)
    {
        return parent.Elements().FirstOrDefault(e =>
            e.Name.LocalName == localName && e.Name.NamespaceName == ns);
    }

    /// <summary>Parses a whitespace-separated string of integers into a list.</summary>
    private static GmlCoordinate? ParseRequiredOrigin(XElement originEl, List<GmlParseIssue> issues)
    {
        var pointEl = XmlHelpers.FindGmlChild(originEl, "Point");
        var posEl = pointEl is not null
            ? XmlHelpers.FindGmlChild(pointEl, "pos")
            : XmlHelpers.FindGmlChild(originEl, "pos");

        if (posEl is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "missing_origin",
                Message = "RectifiedGrid origin has no gml:pos value",
                Location = "origin"
            });
            return null;
        }

        var parts = posEl.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_origin",
                Message = "RectifiedGrid origin must contain at least X and Y ordinates",
                Location = "origin"
            });
            return null;
        }

        var beforeIssueCount = issues.Count;
        var origin = XmlHelpers.ParsePos(posEl.Value, issues: issues);
        return issues.Count > beforeIssueCount ? null : origin;
    }

    private static IReadOnlyList<int>? ParseIntListStrict(string text, string location, List<GmlParseIssue> issues)
    {
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_grid_bounds",
                Message = $"GridEnvelope {location} is empty",
                Location = "GridEnvelope"
            });
            return null;
        }

        var result = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var val))
            {
                issues.Add(new GmlParseIssue
                {
                    Severity = GmlIssueSeverity.Error,
                    Code = "invalid_grid_bounds",
                    Message = $"Cannot parse GridEnvelope {location} value '{part}'",
                    Location = "GridEnvelope"
                });
                return null;
            }

            result.Add(val);
        }
        return result;
    }

    private static IReadOnlyList<double>? ParseDoubleListStrict(string text, string location, List<GmlParseIssue> issues)
    {
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_offset_vector",
                Message = $"{location} is empty",
                Location = location
            });
            return null;
        }

        var result = new List<double>(parts.Length);
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                issues.Add(new GmlParseIssue
                {
                    Severity = GmlIssueSeverity.Error,
                    Code = "invalid_offset_vector",
                    Message = $"Cannot parse {location} value '{part}'",
                    Location = location
                });
                return null;
            }

            result.Add(value);
        }

        return result;
    }
}
