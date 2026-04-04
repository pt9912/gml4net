using System.Globalization;
using System.Xml.Linq;
using Gml4Net.Model.Coverage;
using Gml4Net.Parser;

namespace Gml4Net.Generators;

/// <summary>
/// Generates GML 3.2 / gmlcov XML from coverage model objects.
/// The output always uses the <c>gmlcov:</c> namespace for the root element.
/// </summary>
public static class CoverageGenerator
{
    private static readonly XNamespace Gml = GmlNamespaces.Gml32;
    private static readonly XNamespace Gmlcov = GmlNamespaces.Gmlcov;
    private static readonly XNamespace Swe = GmlNamespaces.Swe;

    /// <summary>
    /// Generates a GML 3.2 / gmlcov XML string from a coverage model.
    /// </summary>
    /// <param name="coverage">The coverage model to serialize.</param>
    /// <param name="prettyPrint">Whether to indent the output XML (default true).</param>
    /// <returns>The coverage as a GML/gmlcov XML string.</returns>
    public static string Generate(GmlCoverage coverage, bool prettyPrint = true)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        var element = coverage switch
        {
            GmlRectifiedGridCoverage rgc => GenerateRectifiedGridCoverage(rgc),
            GmlGridCoverage gc => GenerateGridCoverage(gc),
            GmlReferenceableGridCoverage rgc => GenerateReferenceableGridCoverage(rgc),
            GmlMultiPointCoverage mpc => GenerateMultiPointCoverage(mpc),
            _ => throw new NotSupportedException($"Unsupported coverage type: {coverage.GetType().Name}")
        };

        return element.ToString(prettyPrint ? SaveOptions.None : SaveOptions.DisableFormatting);
    }

    /// <summary>Generates a gmlcov:RectifiedGridCoverage element from the given model.</summary>
    private static XElement GenerateRectifiedGridCoverage(GmlRectifiedGridCoverage coverage)
    {
        var root = CreateCoverageRoot("RectifiedGridCoverage", coverage);

        var domainSet = new XElement(Gml + "domainSet",
            GenerateRectifiedGrid(coverage.DomainSet));
        root.Add(domainSet);

        AddRangeSet(root, coverage);
        AddRangeType(root, coverage);

        return root;
    }

    /// <summary>Generates a gmlcov:GridCoverage element from the given model.</summary>
    private static XElement GenerateGridCoverage(GmlGridCoverage coverage)
    {
        var root = CreateCoverageRoot("GridCoverage", coverage);

        var domainSet = new XElement(Gml + "domainSet",
            GenerateGrid(coverage.DomainSet));
        root.Add(domainSet);

        AddRangeSet(root, coverage);
        AddRangeType(root, coverage);

        return root;
    }

    /// <summary>Generates a gmlcov:ReferenceableGridCoverage element from the given model.</summary>
    private static XElement GenerateReferenceableGridCoverage(GmlReferenceableGridCoverage coverage)
    {
        var root = CreateCoverageRoot("ReferenceableGridCoverage", coverage);

        var domainSet = new XElement(Gml + "domainSet",
            GenerateGrid(coverage.DomainSet));
        root.Add(domainSet);

        AddRangeSet(root, coverage);
        AddRangeType(root, coverage);

        return root;
    }

    /// <summary>Generates a gmlcov:MultiPointCoverage element from the given model.</summary>
    private static XElement GenerateMultiPointCoverage(GmlMultiPointCoverage coverage)
    {
        var root = CreateCoverageRoot("MultiPointCoverage", coverage);

        if (coverage.DomainPoints is { Count: > 0 })
        {
            var domainSet = new XElement(Gml + "domainSet",
                new XElement(Gml + "MultiPoint",
                    coverage.DomainPoints.Select(p =>
                        new XElement(Gml + "pointMember",
                            new XElement(Gml + "Point",
                                new XElement(Gml + "pos", FormatCoord(p.Coordinate)))))));
            root.Add(domainSet);
        }

        AddRangeSet(root, coverage);
        AddRangeType(root, coverage);

        return root;
    }

    // ---- Helpers ----

    /// <summary>Creates the root gmlcov element with standard namespace declarations and optional bounded-by envelope.</summary>
    private static XElement CreateCoverageRoot(string localName, GmlCoverage coverage)
    {
        var root = new XElement(Gmlcov + localName,
            new XAttribute(XNamespace.Xmlns + "gml", Gml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "gmlcov", Gmlcov.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "swe", Swe.NamespaceName));

        if (coverage.Id is not null)
            root.Add(new XAttribute(Gml + "id", coverage.Id));

        if (coverage.BoundedBy is not null)
        {
            var env = new XElement(Gml + "Envelope",
                new XElement(Gml + "lowerCorner", FormatCoord(coverage.BoundedBy.LowerCorner)),
                new XElement(Gml + "upperCorner", FormatCoord(coverage.BoundedBy.UpperCorner)));

            if (coverage.BoundedBy.SrsName is not null)
                env.Add(new XAttribute("srsName", coverage.BoundedBy.SrsName));

            root.Add(new XElement(Gml + "boundedBy", env));
        }

        return root;
    }

    /// <summary>Generates a gml:Grid element with limits and optional axis labels.</summary>
    private static XElement GenerateGrid(GmlGrid grid)
    {
        var gridEl = new XElement(Gml + "Grid",
            new XAttribute("dimension", grid.Dimension));

        if (grid.AxisLabels.Count > 0)
            gridEl.Add(new XAttribute("axisLabels", string.Join(" ", grid.AxisLabels)));

        gridEl.Add(new XElement(Gml + "limits",
            new XElement(Gml + "GridEnvelope",
                new XElement(Gml + "low", string.Join(" ", grid.Limits.Low)),
                new XElement(Gml + "high", string.Join(" ", grid.Limits.High)))));

        return gridEl;
    }

    /// <summary>Generates a gml:RectifiedGrid element with limits, origin, and offset vectors.</summary>
    private static XElement GenerateRectifiedGrid(GmlRectifiedGrid grid)
    {
        var gridEl = new XElement(Gml + "RectifiedGrid",
            new XAttribute("dimension", grid.Dimension));

        if (grid.SrsName is not null)
            gridEl.Add(new XAttribute("srsName", grid.SrsName));

        if (grid.AxisLabels.Count > 0)
            gridEl.Add(new XAttribute("axisLabels", string.Join(" ", grid.AxisLabels)));

        gridEl.Add(new XElement(Gml + "limits",
            new XElement(Gml + "GridEnvelope",
                new XElement(Gml + "low", string.Join(" ", grid.Limits.Low)),
                new XElement(Gml + "high", string.Join(" ", grid.Limits.High)))));

        gridEl.Add(new XElement(Gml + "origin",
            new XElement(Gml + "Point",
                new XElement(Gml + "pos", FormatCoord(grid.Origin)))));

        foreach (var ov in grid.OffsetVectors)
        {
            gridEl.Add(new XElement(Gml + "offsetVector",
                string.Join(" ", ov.Select(v => v.ToString(CultureInfo.InvariantCulture)))));
        }

        return gridEl;
    }

    /// <summary>Appends a gml:rangeSet element containing either a DataBlock or File reference.</summary>
    private static void AddRangeSet(XElement root, GmlCoverage coverage)
    {
        if (coverage.RangeSet is null) return;

        var rangeSetEl = new XElement(Gml + "rangeSet");

        if (coverage.RangeSet.DataBlock is not null)
        {
            rangeSetEl.Add(new XElement(Gml + "DataBlock",
                new XElement(Gml + "tupleList", coverage.RangeSet.DataBlock)));
        }
        else if (coverage.RangeSet.FileReference is not null)
        {
            rangeSetEl.Add(new XElement(Gml + "File",
                new XElement(Gml + "fileName", coverage.RangeSet.FileReference)));
        }

        root.Add(rangeSetEl);
    }

    /// <summary>Appends a gmlcov:rangeType element with swe:DataRecord fields describing the coverage bands.</summary>
    private static void AddRangeType(XElement root, GmlCoverage coverage)
    {
        if (coverage.RangeType is null || coverage.RangeType.Fields.Count == 0) return;

        var dataRecord = new XElement(Swe + "DataRecord");

        foreach (var field in coverage.RangeType.Fields)
        {
            var quantity = new XElement(Swe + "Quantity");

            if (field.Description is not null)
                quantity.Add(new XElement(Swe + "description", field.Description));

            if (field.Uom is not null)
                quantity.Add(new XElement(Swe + "uom", new XAttribute("code", field.Uom)));

            dataRecord.Add(new XElement(Swe + "field",
                new XAttribute("name", field.Name), quantity));
        }

        root.Add(new XElement(Gmlcov + "rangeType", dataRecord));
    }

    /// <summary>Formats a coordinate as a space-separated string using invariant culture.</summary>
    private static string FormatCoord(Model.GmlCoordinate c)
    {
        var result = $"{c.X.ToString(CultureInfo.InvariantCulture)} {c.Y.ToString(CultureInfo.InvariantCulture)}";
        if (c.Z.HasValue)
            result += $" {c.Z.Value.ToString(CultureInfo.InvariantCulture)}";
        return result;
    }
}
