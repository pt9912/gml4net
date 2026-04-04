using System.Globalization;
using System.Text;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Converts GML features to CSV with WKT geometry columns.
/// Line endings use CRLF per RFC 4180.
/// </summary>
public static class CsvBuilder
{
    private const string Crlf = "\r\n";

    /// <summary>
    /// Converts a <see cref="GmlFeatureCollection"/> to a CSV string.
    /// The first geometry property is output as a WKT column named "geometry".
    /// </summary>
    /// <param name="fc">The feature collection to convert.</param>
    /// <param name="separator">Column separator (default comma).</param>
    /// <returns>A CSV string with header row and one row per feature (CRLF line endings).</returns>
    public static string FeatureCollection(GmlFeatureCollection fc, char separator = ',')
    {
        ArgumentNullException.ThrowIfNull(fc);

        if (fc.Features.Count == 0)
            return string.Empty;

        // Collect all unique property names across all features (in order)
        var columns = new List<string>();
        var columnSet = new HashSet<string>(StringComparer.Ordinal);
        var hasGeometry = false;
        foreach (var feature in fc.Features)
        {
            foreach (var entry in feature.Properties.Entries)
            {
                if (entry.Value is GmlGeometryProperty)
                {
                    hasGeometry = true;
                    continue;
                }
                if (columnSet.Add(entry.Name))
                    columns.Add(entry.Name);
            }
        }

        var sb = new StringBuilder();

        // Header (escaped per RFC 4180)
        var headerParts = new List<string>();
        if (hasGeometry) headerParts.Add(CsvEscape("geometry", separator));
        foreach (var col in columns)
            headerParts.Add(CsvEscape(col, separator));
        sb.Append(string.Join(separator, headerParts)).Append(Crlf);

        // Rows
        foreach (var feature in fc.Features)
        {
            var rowParts = new List<string>();

            if (hasGeometry)
            {
                var geomEntry = feature.Properties.Entries
                    .FirstOrDefault(e => e.Value is GmlGeometryProperty);
                var wkt = geomEntry is not null
                    ? WktBuilder.Geometry(((GmlGeometryProperty)geomEntry.Value).Geometry) ?? ""
                    : "";
                rowParts.Add(CsvEscape(wkt, separator));
            }

            foreach (var col in columns)
            {
                if (feature.Properties.TryGetValue(col, out var val))
                {
                    var text = val switch
                    {
                        GmlStringProperty sp => sp.Value,
                        GmlNumericProperty np => np.Value.ToString(CultureInfo.InvariantCulture),
                        GmlNestedProperty => "[nested]",
                        GmlRawXmlProperty raw => raw.XmlContent,
                        _ => ""
                    };
                    rowParts.Add(CsvEscape(text, separator));
                }
                else
                {
                    rowParts.Add("");
                }
            }

            sb.Append(string.Join(separator, rowParts)).Append(Crlf);
        }

        return sb.ToString();
    }

    /// <summary>Escapes a CSV field value per RFC 4180, quoting if necessary.</summary>
    private static string CsvEscape(string value, char separator)
    {
        if (value.Contains(separator) || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
