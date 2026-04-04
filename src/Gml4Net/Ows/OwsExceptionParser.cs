using System.Xml.Linq;
using Gml4Net.Parser;

namespace Gml4Net.Ows;

/// <summary>
/// An individual OWS exception within an exception report.
/// </summary>
public sealed class OwsException
{
    /// <summary>Machine-readable exception code.</summary>
    public required string ExceptionCode { get; init; }

    /// <summary>Optional locator indicating where the error occurred.</summary>
    public string? Locator { get; init; }

    /// <summary>Human-readable exception texts.</summary>
    public IReadOnlyList<string> ExceptionTexts { get; init; } = [];
}

/// <summary>
/// An OGC Web Service exception report containing one or more exceptions.
/// </summary>
public sealed class OwsExceptionReport
{
    /// <summary>OWS version string.</summary>
    public required string Version { get; init; }

    /// <summary>Exceptions in the report.</summary>
    public IReadOnlyList<OwsException> Exceptions { get; init; } = [];

    /// <summary>All exception texts from all exceptions, flattened.</summary>
    public IEnumerable<string> AllMessages =>
        Exceptions.SelectMany(e => e.ExceptionTexts);
}

/// <summary>
/// Detects and parses OGC OWS ExceptionReport XML documents.
/// </summary>
public static class OwsExceptionParser
{
    private static readonly XNamespace NsOws = GmlNamespaces.Ows;

    /// <summary>
    /// Returns true if the XML string appears to be an OWS ExceptionReport.
    /// </summary>
    /// <param name="xml">The XML string to check.</param>
    /// <returns>True if the root element is an ExceptionReport in the OWS namespace.</returns>
    public static bool IsOwsExceptionReport(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Root?.Name.LocalName == "ExceptionReport"
                && doc.Root.Name.NamespaceName == GmlNamespaces.Ows;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses an OWS ExceptionReport XML string.
    /// </summary>
    /// <param name="xml">The XML string to parse.</param>
    /// <returns>The parsed exception report, or null if the XML is not a valid OWS ExceptionReport.</returns>
    public static OwsExceptionReport? Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch
        {
            return null;
        }

        var root = doc.Root;
        if (root is null || root.Name.LocalName != "ExceptionReport" || root.Name.NamespaceName != GmlNamespaces.Ows)
            return null;

        var version = root.Attribute("version")?.Value ?? "unknown";

        var exceptions = new List<OwsException>();
        foreach (var exEl in root.Elements(NsOws + "Exception"))
        {
            var code = exEl.Attribute("exceptionCode")?.Value ?? "unknown";
            var locator = exEl.Attribute("locator")?.Value;
            var texts = exEl.Elements(NsOws + "ExceptionText")
                .Select(t => t.Value.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            exceptions.Add(new OwsException
            {
                ExceptionCode = code,
                Locator = locator,
                ExceptionTexts = texts
            });
        }

        return new OwsExceptionReport
        {
            Version = version,
            Exceptions = exceptions
        };
    }
}
