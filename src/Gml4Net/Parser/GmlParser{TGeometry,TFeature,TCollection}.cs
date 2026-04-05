using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Parser;

/// <summary>
/// A generic GML parser that combines parsing and builder dispatch in a single step.
/// Create instances via <see cref="GmlParser.Create{TGeometry,TFeature,TCollection}"/>
/// for automatic type inference, or use the constructor directly.
/// </summary>
/// <typeparam name="TGeometry">Output type for geometry conversions.</typeparam>
/// <typeparam name="TFeature">Output type for feature conversions.</typeparam>
/// <typeparam name="TCollection">Output type for feature collection conversions.</typeparam>
public class GmlParser<TGeometry, TFeature, TCollection>
{
    private readonly IBuilder<TGeometry, TFeature, TCollection> _builder;

    /// <summary>
    /// Creates a new parser that uses the specified builder for output conversion.
    /// </summary>
    /// <param name="builder">The builder to use for converting parsed GML to the target format.</param>
    public GmlParser(IBuilder<TGeometry, TFeature, TCollection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
    }

    /// <summary>
    /// Parses a GML XML string and converts the result using the builder.
    /// </summary>
    /// <param name="xml">The GML XML string to parse.</param>
    /// <returns>A result containing the converted output and any diagnostic issues.</returns>
    public GmlBuildResult<TGeometry, TFeature, TCollection> Parse(string xml)
    {
        var parseResult = GmlParser.ParseXmlString(xml);
        return BuildResult(parseResult);
    }

    /// <summary>
    /// Parses a GML document from a byte span and converts the result using the builder.
    /// </summary>
    /// <param name="bytes">The raw bytes of the GML XML document.</param>
    /// <returns>A result containing the converted output and any diagnostic issues.</returns>
    public GmlBuildResult<TGeometry, TFeature, TCollection> Parse(ReadOnlySpan<byte> bytes)
    {
        var parseResult = GmlParser.ParseBytes(bytes);
        return BuildResult(parseResult);
    }

    /// <summary>
    /// Parses a GML document from a stream and converts the result using the builder.
    /// </summary>
    /// <param name="stream">A stream containing the GML XML document.</param>
    /// <returns>A result containing the converted output and any diagnostic issues.</returns>
    public GmlBuildResult<TGeometry, TFeature, TCollection> Parse(Stream stream)
    {
        var parseResult = GmlParser.ParseStream(stream);
        return BuildResult(parseResult);
    }

    private GmlBuildResult<TGeometry, TFeature, TCollection> BuildResult(GmlParseResult parseResult)
    {
        if (parseResult.Document is null)
            return new GmlBuildResult<TGeometry, TFeature, TCollection>
            {
                Issues = parseResult.Issues
            };

        var document = parseResult.Document;
        var issues = parseResult.Issues;

        return document.Root switch
        {
            GmlFeatureCollection fc => new GmlBuildResult<TGeometry, TFeature, TCollection>
            {
                Collection = _builder.BuildFeatureCollection(fc),
                Document = document,
                Issues = issues
            },
            GmlFeature f => new GmlBuildResult<TGeometry, TFeature, TCollection>
            {
                Feature = _builder.BuildFeature(f),
                Document = document,
                Issues = issues
            },
            GmlGeometry g => new GmlBuildResult<TGeometry, TFeature, TCollection>
            {
                Geometry = _builder.BuildGeometry(g),
                Document = document,
                Issues = issues
            },
            GmlCoverage c => new GmlBuildResult<TGeometry, TFeature, TCollection>
            {
                Coverage = _builder.BuildCoverage(c),
                Document = document,
                Issues = issues
            },
            _ => new GmlBuildResult<TGeometry, TFeature, TCollection>
            {
                Document = document,
                Issues = issues
            }
        };
    }
}
