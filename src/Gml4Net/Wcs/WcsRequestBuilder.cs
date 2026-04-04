using System.Xml.Linq;
using Gml4Net.Parser;

namespace Gml4Net.Wcs;

/// <summary>
/// Supported WCS protocol versions.
/// </summary>
public enum WcsVersion
{
    /// <summary>WCS 1.0.0</summary>
    V1_0_0,
    /// <summary>WCS 1.1.0</summary>
    V1_1_0,
    /// <summary>WCS 1.1.1</summary>
    V1_1_1,
    /// <summary>WCS 1.1.2</summary>
    V1_1_2,
    /// <summary>WCS 2.0.0</summary>
    V2_0_0,
    /// <summary>WCS 2.0.1</summary>
    V2_0_1
}

/// <summary>
/// A spatial or temporal subset for a WCS GetCoverage request.
/// </summary>
public sealed class WcsSubset
{
    /// <summary>Axis name (e.g. "Long", "Lat", "time").</summary>
    public required string Axis { get; init; }

    /// <summary>Minimum value for range subsets.</summary>
    public string? Min { get; init; }

    /// <summary>Maximum value for range subsets.</summary>
    public string? Max { get; init; }

    /// <summary>Exact value for point subsets.</summary>
    public string? Value { get; init; }
}

/// <summary>
/// Options for building a WCS GetCoverage request.
/// </summary>
public sealed class WcsGetCoverageOptions
{
    /// <summary>Coverage identifier.</summary>
    public required string CoverageId { get; init; }

    /// <summary>Output format (e.g. "image/tiff").</summary>
    public string? Format { get; init; }

    /// <summary>Spatial/temporal subsets.</summary>
    public IReadOnlyList<WcsSubset> Subsets { get; init; } = [];

    /// <summary>Output CRS identifier.</summary>
    public string? OutputCrs { get; init; }

    /// <summary>Range subset (band selection).</summary>
    public IReadOnlyList<string>? RangeSubset { get; init; }

    /// <summary>Interpolation method.</summary>
    public string? Interpolation { get; init; }
}

/// <summary>
/// Builds WCS GetCoverage requests (URL for GET, XML for POST).
/// </summary>
public sealed class WcsRequestBuilder
{
    private readonly string _baseUrl;
    private readonly WcsVersion _version;

    /// <summary>
    /// Creates a new WCS request builder.
    /// </summary>
    /// <param name="baseUrl">The base URL of the WCS service endpoint.</param>
    /// <param name="version">The WCS protocol version to use.</param>
    public WcsRequestBuilder(string baseUrl, WcsVersion version = WcsVersion.V2_0_1)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        _baseUrl = baseUrl.TrimEnd('&');
        _version = version;
    }

    /// <summary>
    /// Builds a WCS GetCoverage URL with query parameters.
    /// </summary>
    /// <param name="options">The coverage request options.</param>
    /// <returns>A complete GetCoverage URL string.</returns>
    public string BuildGetCoverageUrl(WcsGetCoverageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!SupportsAdvancedKvpOptions(_version)
            && (options.Subsets.Count > 0
                || options.OutputCrs is not null
                || options.RangeSubset is { Count: > 0 }
                || options.Interpolation is not null))
        {
            throw new NotSupportedException(
                $"Advanced GetCoverage KVP options are only supported for WCS 2.0+ by this builder. Version: {FormatVersion(_version)}");
        }

        var parts = new List<string>
        {
            "service=WCS",
            "request=GetCoverage",
            $"version={FormatVersion(_version)}"
        };

        // Coverage ID parameter name varies by version
        parts.Add(_version switch
        {
            WcsVersion.V1_0_0 => $"coverage={Uri.EscapeDataString(options.CoverageId)}",
            WcsVersion.V1_1_0 or WcsVersion.V1_1_1 or WcsVersion.V1_1_2
                => $"identifier={Uri.EscapeDataString(options.CoverageId)}",
            _ => $"CoverageId={Uri.EscapeDataString(options.CoverageId)}"
        });

        if (options.Format is not null)
            parts.Add($"format={Uri.EscapeDataString(options.Format)}");

        foreach (var subset in options.Subsets)
        {
            parts.Add($"subset={Uri.EscapeDataString(FormatSubset(subset))}");
        }

        if (options.OutputCrs is not null)
            parts.Add($"outputCrs={Uri.EscapeDataString(options.OutputCrs)}");

        if (options.RangeSubset is { Count: > 0 })
            parts.Add($"rangesubset={Uri.EscapeDataString(string.Join(",", options.RangeSubset))}");

        if (options.Interpolation is not null)
            parts.Add($"interpolation={Uri.EscapeDataString(options.Interpolation)}");

        var separator = _baseUrl.Contains('?') ? "&" : "?";
        return $"{_baseUrl}{separator}{string.Join("&", parts)}";
    }

    /// <summary>
    /// Builds a WCS 2.0+ GetCoverage XML request body for HTTP POST.
    /// </summary>
    /// <param name="options">The coverage request options.</param>
    /// <returns>An XML string for the POST body.</returns>
    public string BuildGetCoverageXml(WcsGetCoverageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!IsWcs20Plus(_version))
        {
            throw new NotSupportedException(
                $"POST XML GetCoverage requests are only supported for WCS 2.0+. Version: {FormatVersion(_version)}");
        }

        if (options.OutputCrs is not null || options.RangeSubset is { Count: > 0 } || options.Interpolation is not null)
        {
            throw new NotSupportedException(
                "OutputCrs, RangeSubset and Interpolation are not yet supported for WCS POST XML requests.");
        }

        XNamespace wcs = GmlNamespaces.Wcs;
        var root = new XElement(wcs + "GetCoverage",
            new XAttribute(XNamespace.Xmlns + "wcs", wcs.NamespaceName),
            new XAttribute("service", "WCS"),
            new XAttribute("version", FormatVersion(_version)),
            new XElement(wcs + "CoverageId", options.CoverageId));

        foreach (var subset in options.Subsets)
        {
            XElement dimEl;

            if (subset.Value is not null)
            {
                dimEl = new XElement(wcs + "DimensionSlice",
                    new XElement(wcs + "Dimension", subset.Axis));
                dimEl.Add(new XElement(wcs + "SlicePoint", subset.Value));
            }
            else
            {
                dimEl = new XElement(wcs + "DimensionTrim",
                    new XElement(wcs + "Dimension", subset.Axis));
                if (subset.Min is not null)
                    dimEl.Add(new XElement(wcs + "TrimLow", subset.Min));
                if (subset.Max is not null)
                    dimEl.Add(new XElement(wcs + "TrimHigh", subset.Max));
            }

            root.Add(dimEl);
        }

        if (options.Format is not null)
            root.Add(new XElement(wcs + "format", options.Format));

        return root.ToString();
    }

    /// <summary>Formats a WcsVersion enum to a version string.</summary>
    private static string FormatVersion(WcsVersion v) => v switch
    {
        WcsVersion.V1_0_0 => "1.0.0",
        WcsVersion.V1_1_0 => "1.1.0",
        WcsVersion.V1_1_1 => "1.1.1",
        WcsVersion.V1_1_2 => "1.1.2",
        WcsVersion.V2_0_0 => "2.0.0",
        WcsVersion.V2_0_1 => "2.0.1",
        _ => "2.0.1"
    };

    /// <summary>Formats a subset as a KVP string (e.g. "Long(10,20)" or "time(2020-01-01)").</summary>
    private static string FormatSubset(WcsSubset subset)
    {
        if (subset.Value is not null)
            return $"{subset.Axis}({subset.Value})";
        return $"{subset.Axis}({subset.Min},{subset.Max})";
    }

    private static bool IsWcs20Plus(WcsVersion version) =>
        version is WcsVersion.V2_0_0 or WcsVersion.V2_0_1;

    private static bool SupportsAdvancedKvpOptions(WcsVersion version) => IsWcs20Plus(version);
}
