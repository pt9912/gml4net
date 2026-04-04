# GML4Net - Portierung nach .NET Core / C#

Aktueller Stand: Design Phase. Dieses Dokument ist ein Portierungsentwurf fuer die geplante Implementierung.

## Quellprojekte

| Projekt | Sprache | Pfad | Rolle |
|---------|---------|------|-------|
| **s-gml** | TypeScript | `/Development/s-gml` | Ursprungsbibliothek, Feature-komplett |
| **gml4dart** | Dart | `/Development/flutter/gml4dart` | Dart-Port, aufgeraeumtes Domain-Modell |

Beide Projekte stammen vom selben Autor. `gml4dart` ist eine bereinigte Dart-Portierung von `s-gml` mit staerkerem Typ-System (sealed classes, final classes). Die .NET-Portierung soll das Beste aus beiden Welten vereinen: das saubere Domain-Modell aus Dart mit der Feature-Vollstaendigkeit aus TypeScript.

---

## 1. Projektstruktur

```
GML4Net/
├── src/
│   ├── Gml4Net/                          # Haupt-Library (net8.0)
│   │   ├── Gml4Net.csproj
│   │   ├── Model/                        # Domain-Modell
│   │   │   ├── GmlNode.cs               # Abstrakte Basisklasse
│   │   │   ├── GmlDocument.cs           # Geparstes Dokument
│   │   │   ├── GmlVersion.cs            # Enum: V2_1_2, V3_0, V3_1, V3_2, V3_3
│   │   │   ├── GmlCoordinate.cs         # readonly record struct
│   │   │   ├── Geometry/
│   │   │   │   ├── GmlGeometry.cs       # Abstrakte Basisklasse
│   │   │   │   ├── GmlPoint.cs
│   │   │   │   ├── GmlLineString.cs
│   │   │   │   ├── GmlLinearRing.cs
│   │   │   │   ├── GmlPolygon.cs
│   │   │   │   ├── GmlEnvelope.cs
│   │   │   │   ├── GmlBox.cs
│   │   │   │   ├── GmlCurve.cs
│   │   │   │   ├── GmlSurface.cs
│   │   │   │   ├── GmlMultiPoint.cs
│   │   │   │   ├── GmlMultiLineString.cs
│   │   │   │   └── GmlMultiPolygon.cs
│   │   │   ├── Feature/
│   │   │   │   ├── GmlFeature.cs
│   │   │   │   ├── GmlFeatureCollection.cs
│   │   │   │   └── GmlPropertyValue.cs  # Abstrakt + Subklassen
│   │   │   ├── Coverage/
│   │   │   │   ├── GmlCoverage.cs       # Abstrakte Basisklasse
│   │   │   │   ├── GmlRectifiedGridCoverage.cs
│   │   │   │   ├── GmlGridCoverage.cs
│   │   │   │   ├── GmlReferenceableGridCoverage.cs
│   │   │   │   ├── GmlMultiPointCoverage.cs
│   │   │   │   ├── GmlGrid.cs
│   │   │   │   ├── GmlRectifiedGrid.cs
│   │   │   │   ├── GmlGridEnvelope.cs
│   │   │   │   ├── GmlRangeSet.cs
│   │   │   │   └── GmlRangeType.cs
│   │   │   ├── GmlRootContent.cs        # Marker-Interface IGmlRootContent
│   │   │   ├── GmlParseResult.cs
│   │   │   ├── GmlParseIssue.cs
│   │   │   └── GmlUnsupportedNode.cs
│   │   ├── Parser/                       # Parsing-Logik
│   │   │   ├── GmlParser.cs             # Haupteinstieg (statisch)
│   │   │   ├── GeometryParser.cs
│   │   │   ├── FeatureParser.cs
│   │   │   ├── CoverageParser.cs
│   │   │   ├── XmlHelpers.cs
│   │   │   └── Streaming/
│   │   │       └── GmlFeatureStreamParser.cs
│   │   ├── Interop/                      # Format-Konvertierung
│   │   │   ├── GeoJsonBuilder.cs
│   │   │   └── WktBuilder.cs
│   │   ├── Ows/
│   │   │   └── OwsException.cs
│   │   ├── Wcs/
│   │   │   ├── WcsRequestBuilder.cs
│   │   │   └── WcsCapabilitiesParser.cs
│   │   ├── Generators/
│   │   │   └── CoverageGenerator.cs
│   │   └── Utils/
│   │       └── GeoTiffMetadata.cs
│   │
│   └── Gml4Net.IO/                       # Optionales I/O-Paket
│       ├── Gml4Net.IO.csproj
│       └── GmlIo.cs
│
├── tests/
│   ├── Gml4Net.Tests/
│   │   └── Gml4Net.Tests.csproj
│   └── Gml4Net.IO.Tests/
│       └── Gml4Net.IO.Tests.csproj
│
├── docs/
│   ├── architecture.md                   # Architektur-Dokument
│   ├── roadmap.md                        # Implementierungsplan
│   └── port2net.md                       # Dieses Dokument (Portierungsentwurf)
│
├── GML4Net.sln
├── Directory.Build.props                 # Gemeinsame Build-Einstellungen
└── README.md
```

---

## 2. Domain-Modell -- Mapping-Strategie

### 2.1 Sealed Classes -> C# Discriminated Unions

Dart verwendet sealed classes fuer exhaustive Pattern-Matching. C# hat kein direktes Aequivalent, aber ab C# 11 / .NET 7+ gibt es gute Alternativen:

**Strategie:** Abstrakte Klasse + geschlossene Vererbung mit internem Konstruktor.

```csharp
// GmlNode.cs -- Gemeinsame Basisklasse aller GML-Knoten
public abstract class GmlNode
{
    internal GmlNode() { }
}

// IGmlRootContent.cs -- Marker-Interface fuer Dokument-Root-Typen
public interface IGmlRootContent { }

// GmlGeometry.cs
public abstract class GmlGeometry : GmlNode, IGmlRootContent
{
    internal GmlGeometry() { } // Verhindert externe Ableitung
    public GmlVersion? Version { get; init; }
    public string? SrsName { get; init; }
}

// GmlPoint.cs
public sealed class GmlPoint : GmlGeometry
{
    public required GmlCoordinate Coordinate { get; init; }
}

// GmlPolygon.cs
public sealed class GmlPolygon : GmlGeometry
{
    public required GmlLinearRing Exterior { get; init; }
    public IReadOnlyList<GmlLinearRing> Interior { get; init; } = [];
}

// GmlEnvelope.cs -- GML 3.x Bounding-Box
public sealed class GmlEnvelope : GmlGeometry
{
    public required GmlCoordinate LowerCorner { get; init; }
    public required GmlCoordinate UpperCorner { get; init; }
}

// GmlBox.cs -- GML 2.1.2 Bounding-Box
public sealed class GmlBox : GmlGeometry
{
    public required GmlCoordinate LowerCorner { get; init; }
    public required GmlCoordinate UpperCorner { get; init; }
}
```

Pattern-Matching in C#:
```csharp
var area = geometry switch
{
    GmlPoint p       => 0,
    GmlPolygon poly  => CalculateArea(poly),
    GmlEnvelope env  => (env.UpperCorner.X - env.LowerCorner.X)
                      * (env.UpperCorner.Y - env.LowerCorner.Y),
    _                => throw new NotSupportedException()
};
```

### 2.2 Coordinate als readonly record struct

```csharp
public readonly record struct GmlCoordinate(
    double X,
    double Y,
    double? Z = null,
    double? M = null)
{
    public int Dimension => (Z, M) switch
    {
        (not null, not null) => 4,
        (not null, _) or (_, not null) => 3,
        _ => 2
    };
}
```

Vorteile gegenueber Dart:
- Werttyp; kann Heap-Allokationen in vielen Faellen reduzieren
- Natuerliche Wert-Semantik mit `==`
- Immutabel durch `readonly`

### 2.3 Property-Value-Hierarchie

```csharp
public abstract class GmlPropertyValue
{
    internal GmlPropertyValue() { }
}

public sealed class GmlStringProperty : GmlPropertyValue
{
    public required string Value { get; init; }
}

public sealed class GmlNumericProperty : GmlPropertyValue
{
    public required double Value { get; init; }
}

public sealed class GmlGeometryProperty : GmlPropertyValue
{
    public required GmlGeometry Geometry { get; init; }
}

public sealed class GmlNestedProperty : GmlPropertyValue
{
    public GmlPropertyBag Children { get; init; } = GmlPropertyBag.Empty;
}

public sealed class GmlRawXmlProperty : GmlPropertyValue
{
    public required string XmlContent { get; init; }
}
```

### 2.4 Coverage-Modell

```csharp
public abstract class GmlCoverage : GmlNode, IGmlRootContent
{
    internal GmlCoverage() { }
    public string? Id { get; init; }
    public GmlEnvelope? BoundedBy { get; init; }
    public GmlRangeSet? RangeSet { get; init; }
    public GmlRangeType? RangeType { get; init; }
}

public sealed class GmlRectifiedGridCoverage : GmlCoverage
{
    public required GmlRectifiedGrid DomainSet { get; init; }
}

public sealed class GmlGridCoverage : GmlCoverage
{
    public required GmlGrid DomainSet { get; init; }
}

public sealed class GmlReferenceableGridCoverage : GmlCoverage
{
    public required GmlGrid DomainSet { get; init; }
}

public sealed class GmlMultiPointCoverage : GmlCoverage
{
    public IReadOnlyList<GmlPoint>? DomainPoints { get; init; }
}
```

### 2.5 Parse-Ergebnis

```csharp
public sealed class GmlParseResult
{
    public GmlDocument? Document { get; init; }
    public IReadOnlyList<GmlParseIssue> Issues { get; init; } = [];
    public bool HasErrors => Issues.Any(i => i.Severity == GmlIssueSeverity.Error);
}

public sealed class GmlParseIssue
{
    public required GmlIssueSeverity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Location { get; init; }
}

public enum GmlIssueSeverity { Info, Warning, Error }
```

### 2.6 Vollstaendiges Typ-Mapping

| Dart (gml4dart) | TypeScript (s-gml) | C# (gml4net) |
|---|---|---|
| `sealed class GmlGeometry` | `type GmlGeometry = union` | `abstract class GmlGeometry` |
| `final class GmlPoint` | `interface GmlPoint` | `sealed class GmlPoint` |
| `final class GmlCoordinate` | `number[]` | `readonly record struct GmlCoordinate` |
| `sealed class GmlPropertyValue` | implizit | `abstract class GmlPropertyValue` |
| `sealed class GmlCoverage` | `type GmlCoverage = union` | `abstract class GmlCoverage` |
| `sealed class GmlRootContent` | implizit | `interface IGmlRootContent` |
| `enum GmlVersion` | `type GmlVersion = string` | `enum GmlVersion` |
| `final class GmlParseResult` | `Promise<T>` + throw | `sealed class GmlParseResult` |
| `final class GmlParseIssue` | nicht vorhanden | `sealed class GmlParseIssue` |
| `final class GmlUnsupportedNode` | nicht vorhanden | `sealed class GmlUnsupportedNode` |

---

## 3. Parser-Architektur

### 3.1 XML-Backend

**Entscheidung:** `System.Xml.Linq` (LINQ to XML / XDocument)

Begruendung:
- Teil des .NET SDK, keine externe Abhaengigkeit
- Namespace-aware (wie `package:xml` in Dart)
- Performant fuer DOM-basiertes Parsen
- Gutes API fuer XPath-artige Abfragen
- Fuer Streaming: `XmlReader` (SAX-aequivalent)

```csharp
// Namespace-Konstanten (analog zu xml_helpers.dart)
internal static class GmlNamespaces
{
    // GML 2.1.2, 3.0 und 3.1 teilen sich denselben Namespace.
    // Die Unterscheidung zwischen diesen Versionen erfolgt per Content-Heuristik
    // in DetectVersion() (z.B. <coordinates> und <Box> deuten auf GML 2.1.2 hin).
    internal const string Gml = "http://www.opengis.net/gml";
    internal const string Gml32 = "http://www.opengis.net/gml/3.2";
    internal const string Gml33 = "http://www.opengis.net/gml/3.3";
    internal const string Wfs1 = "http://www.opengis.net/wfs";
    internal const string Wfs2 = "http://www.opengis.net/wfs/2.0";
    internal const string Swe = "http://www.opengis.net/swe/2.0";
    internal const string Gmlcov = "http://www.opengis.net/gmlcov/1.0";
    internal const string Ows = "http://www.opengis.net/ows/1.1";
    internal const string Wcs = "http://www.opengis.net/wcs/2.0";
}
```

### 3.2 Parser-Struktur

```csharp
public static class GmlParser
{
    // Haupteinstiegspunkte (analog zu Dart)
    public static GmlParseResult ParseXmlString(string xml);
    public static GmlParseResult ParseBytes(ReadOnlySpan<byte> bytes);
    public static GmlParseResult ParseStream(Stream stream);
}
```

Interner Ablauf (wie in gml4dart):
1. XML parsen via `XDocument.Parse()` / `XDocument.Load()`
2. GML-Version erkennen aus Namespace-Deklarationen
3. Root-Element dispatchen (FeatureCollection, Coverage, Geometry, Feature)
4. Issues sammeln, nicht werfen
5. `GmlParseResult` zurueckgeben

### 3.3 Interne Parser-Klassen

```csharp
internal static class GeometryParser
{
    internal static GmlGeometry? Parse(XElement element, GmlVersion version,
        List<GmlParseIssue> issues);
}

internal static class FeatureParser
{
    internal static GmlFeatureCollection? ParseCollection(XElement element,
        GmlVersion version, List<GmlParseIssue> issues);
    internal static GmlFeature? ParseFeature(XElement element,
        GmlVersion version, List<GmlParseIssue> issues);
}

internal static class CoverageParser
{
    internal static GmlCoverage? Parse(XElement element, GmlVersion version,
        List<GmlParseIssue> issues);
}
```

### 3.4 XML-Hilfsfunktionen

```csharp
internal static class XmlHelpers
{
    // Namespace-Pruefung
    internal static bool IsGmlNamespace(string? ns);
    internal static bool IsWfsNamespace(string? ns);

    // Element-Suche (namespace-aware)
    internal static XElement? FindGmlChild(XElement parent, string localName);
    internal static IEnumerable<XElement> FindGmlChildren(XElement parent, string localName);
    internal static IEnumerable<XElement> FindWfsChildren(XElement parent, string localName);

    // Attribut-Extraktion
    internal static string? GetSrsName(XElement element);
    internal static string? GetFeatureId(XElement element);
    internal static int GetSrsDimension(XElement element, int defaultValue = 2);

    // Koordinaten-Parsing (intern Span-basiert fuer zero-alloc, string-Overloads als Fassade)
    internal static GmlCoordinate ParsePos(ReadOnlySpan<char> text, int? srsDimension = null);
    internal static GmlCoordinate ParsePos(string text, int? srsDimension = null)
        => ParsePos(text.AsSpan(), srsDimension);
    internal static IReadOnlyList<GmlCoordinate> ParsePosList(ReadOnlySpan<char> text, int srsDimension);
    internal static IReadOnlyList<GmlCoordinate> ParseGml2Coordinates(ReadOnlySpan<char> text);

    // Versions-Erkennung
    internal static GmlVersion DetectVersion(XDocument doc);
}
```

### 3.5 Streaming-Parser

```csharp
public static class GmlFeatureStreamParser
{
    // IAsyncEnumerable fuer native C#-Streaming-Unterstuetzung
    public static async IAsyncEnumerable<GmlFeature> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default);

    // Callback-basiert (analog zu Dart processFeatures)
    public static async Task<int> ProcessFeaturesAsync(
        Stream stream,
        Func<GmlFeature, Task> onFeature,
        CancellationToken ct = default);
}
```

**Implementierung:** Basiert auf `XmlReader` (forward-only, kein DOM):
1. Liest mit `XmlReader` vorwaerts durch das Dokument
2. Erkennt `featureMember` / `member` Elemente
3. Liest Subtree mit `XElement.ReadFrom()` (DOM nur fuer aktuelles Feature)
4. Gibt Feature via `yield return` zurueck
5. Speicher bleibt konstant unabhaengig von Dokumentgroesse

Vorteil gegenueber Dart/TS: `IAsyncEnumerable<T>` ist nativer Bestandteil von C# und erlaubt `await foreach` ohne externe Bibliothek.

---

## 4. Interop-Schicht

### 4.1 GeoJSON-Builder

```csharp
public static class GeoJsonBuilder
{
    // Als System.Text.Json JsonNode (manipulierbar)
    public static JsonObject? Geometry(GmlGeometry geometry);
    public static JsonObject Feature(GmlFeature feature);
    public static JsonObject FeatureCollection(GmlFeatureCollection fc);
    public static JsonObject? Document(GmlDocument document);

    // Als JSON-String
    public static string? GeometryToJson(GmlGeometry geometry);
    public static string FeatureToJson(GmlFeature feature);
    public static string FeatureCollectionToJson(GmlFeatureCollection fc);
}
```

Verwendet `System.Text.Json` (kein Newtonsoft) fuer:
- Zero-Alloc Serialisierung wo moeglich
- Native .NET-Integration
- Keine externe Abhaengigkeit

### 4.2 WKT-Builder

```csharp
public static class WktBuilder
{
    public static string? Geometry(GmlGeometry geometry);
}
```

Ausgabeformat identisch zu Dart:
```
POINT (x y)
LINESTRING (x y, x y, ...)
POLYGON ((x y, ...), (x y, ...))
MULTIPOINT ((x y), ...)
```

### 4.3 Weitere Builder (Phase 7, aus s-gml)

Die folgenden Formate existieren in s-gml, aber nicht in gml4dart. Sie werden ueber ein Builder-Interface nachgeruestet:

```csharp
public interface IGmlBuilder<TGeometry, TFeature, TCollection>
{
    TGeometry? BuildPoint(GmlPoint point);
    TGeometry? BuildLineString(GmlLineString lineString);
    TGeometry? BuildLinearRing(GmlLinearRing linearRing);
    TGeometry? BuildPolygon(GmlPolygon polygon);
    TGeometry? BuildMultiPoint(GmlMultiPoint multiPoint);
    TGeometry? BuildMultiLineString(GmlMultiLineString multiLineString);
    TGeometry? BuildMultiPolygon(GmlMultiPolygon multiPolygon);
    TGeometry? BuildEnvelope(GmlEnvelope envelope);
    TGeometry? BuildBox(GmlBox box);
    TGeometry? BuildCurve(GmlCurve curve);
    TGeometry? BuildSurface(GmlSurface surface);
    TFeature BuildFeature(GmlFeature feature);
    TCollection BuildFeatureCollection(GmlFeatureCollection fc);
    object? BuildCoverage(GmlCoverage coverage);
}
```

Geplante Builder (spaetere Phasen):
- **KML** (`KmlBuilder`)
- **CSV** (`CsvBuilder`)
- **CIS JSON** (`CisJsonBuilder`)
- **CoverageJSON** (`CoverageJsonBuilder`)

---

## 5. OWS / WCS Integration

### 5.1 OWS Exception Handling

```csharp
public sealed class OwsException
{
    public required string ExceptionCode { get; init; }
    public string? Locator { get; init; }
    public IReadOnlyList<string> ExceptionTexts { get; init; } = [];
}

public sealed class OwsExceptionReport
{
    public required string Version { get; init; }
    public IReadOnlyList<OwsException> Exceptions { get; init; } = [];
    public IEnumerable<string> AllMessages =>
        Exceptions.SelectMany(e => e.ExceptionTexts);
}

public static class OwsExceptionParser
{
    public static bool IsOwsExceptionReport(string xml);
    public static OwsExceptionReport? Parse(string xml);
}
```

### 5.2 WCS Request Builder

```csharp
public enum WcsVersion { V1_0_0, V1_1_0, V1_1_1, V1_1_2, V2_0_0, V2_0_1 }

public sealed class WcsSubset
{
    public required string Axis { get; init; }
    public string? Min { get; init; }
    public string? Max { get; init; }
    public string? Value { get; init; }
}

public sealed class WcsGetCoverageOptions
{
    public required string CoverageId { get; init; }
    public string? Format { get; init; }
    public IReadOnlyList<WcsSubset> Subsets { get; init; } = [];
    public string? OutputCrs { get; init; }
    public IReadOnlyList<string>? RangeSubset { get; init; }
    public string? Interpolation { get; init; }
}

public sealed class WcsRequestBuilder
{
    public WcsRequestBuilder(string baseUrl, WcsVersion version = WcsVersion.V2_0_1);
    public string BuildGetCoverageUrl(WcsGetCoverageOptions options);
    public string BuildGetCoverageXml(WcsGetCoverageOptions options);
}
```

### 5.3 WCS Capabilities Parser

```csharp
public sealed class WcsCapabilities
{
    public required string Version { get; init; }
    public WcsServiceIdentification? ServiceIdentification { get; init; }
    public IReadOnlyList<WcsOperationMetadata> Operations { get; init; } = [];
    public IReadOnlyList<WcsCoverageSummary> Coverages { get; init; } = [];
    public IReadOnlyList<string> Formats { get; init; } = [];
    public IReadOnlyList<string> Crs { get; init; } = [];
}

public static class WcsCapabilitiesParser
{
    public static WcsCapabilities Parse(string xml);
}
```

---

## 6. Coverage-Generator & Utilities

### 6.1 Coverage Generator

```csharp
public static class CoverageGenerator
{
    public static string Generate(GmlCoverage coverage, bool prettyPrint = true);
}
```

Erzeugt GML 3.2 / gmlcov XML -- identisch zur Dart-Implementierung.

### 6.2 GeoTIFF Metadata

```csharp
public sealed class GeoTiffMetadata
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double[]? Bbox { get; init; }    // [minX, minY, maxX, maxY]
    public string? Crs { get; init; }
    public double[]? Transform { get; init; } // [a, b, c, d, e, f]
    public double[]? Resolution { get; init; }
    public double[]? Origin { get; init; }
    public int? Bands { get; init; }
    public IReadOnlyList<GmlRangeField>? BandInfo { get; init; }
}

public static class GeoTiffUtils
{
    public static GeoTiffMetadata? ExtractMetadata(GmlCoverage coverage);
    public static (double X, double Y)? PixelToWorld(double col, double row,
        GeoTiffMetadata metadata);
    public static (double Col, double Row)? WorldToPixel(double x, double y,
        GeoTiffMetadata metadata);
}
```

---

## 7. I/O-Modul (separates NuGet-Paket)

### Fehlerbehandlung im I/O-Modul

Transportfehler werden als Exceptions geworfen, nicht als `GmlParseIssue`.
Das I/O-Modul liegt ausserhalb des Result-basierten Parse-Fehlermodells:

```csharp
// Exceptions fuer Transportfehler
public class GmlIoException : Exception
{
    public string ErrorCode { get; }  // "file_not_found", "http_error", "network_error"
    public int? HttpStatusCode { get; }
}
```

OWS Exception Reports sind der Sonderfall: sie kommen als HTTP 200 zurueck,
werden aber als `GmlParseIssue` mit dem OWS-ExceptionCode ins `GmlParseResult`
uebernommen.

```csharp
// Gml4Net.IO -- separates Paket, um HTTP-Abhaengigkeit optional zu halten
public static class GmlIo
{
    // Datei
    public static GmlParseResult ParseFile(string path);
    public static async Task<GmlParseResult> ParseFileAsync(string path,
        CancellationToken ct = default);

    // URL (benoetigt HttpClient)
    public static async Task<GmlParseResult> ParseUrlAsync(Uri url,
        HttpClient? client = null, CancellationToken ct = default);

    // Streaming
    public static IAsyncEnumerable<GmlFeature> StreamFeaturesFromFile(string path,
        CancellationToken ct = default);
    public static IAsyncEnumerable<GmlFeature> StreamFeaturesFromUrl(Uri url,
        HttpClient? client = null, CancellationToken ct = default);
}
```

---

## 8. Abhaengigkeiten

### Gml4Net (Core)

| Abhaengigkeit | Zweck |
|---|---|
| *keine* | `System.Xml.Linq`, `System.Text.Json` sind Teil des SDK |

**Null externe Abhaengigkeiten** fuer das Core-Paket. Das ist ein wesentlicher Vorteil gegenueber s-gml (7 Produktions-Deps) und gml4dart (2 Deps).

### Gml4Net.IO

| Abhaengigkeit | Zweck |
|---|---|
| *keine* | `HttpClient`, `File`, `Stream` sind Teil des SDK |

### Tests

| Abhaengigkeit | Zweck |
|---|---|
| `xunit` | Test-Framework |
| `FluentAssertions` | Assertions |
| `Microsoft.NET.Test.Sdk` | Test-Runner |

---

## 9. Sprachspezifische Entscheidungen

### 9.1 Immutabilitaet

| Konzept | Dart | C# Umsetzung |
|---|---|---|
| `final class` | Verhindert Vererbung | `sealed class` |
| `final` Felder | Zur Laufzeit immutabel | `{ get; init; }` Properties |
| `const` Konstruktor | Compile-Time-Konstante | `readonly record struct` wo passend |
| `List<T>` (immutabel) | `UnmodifiableListView` | `IReadOnlyList<T>` |
| `Map<K,V>` (immutabel) | `UnmodifiableMapView` | `IReadOnlyDictionary<K,V>` |

### 9.2 Fehlerbehandlung

Wie in gml4dart: **Result-Typ statt Exceptions** fuer erwartbare Parse-Fehler.

```csharp
// Korrekt:
var result = GmlParser.ParseXmlString(xml);
if (result.HasErrors) { /* Issues auswerten */ }

// NICHT:
try { var doc = GmlParser.Parse(xml); }
catch (GmlParseException ex) { ... }
```

Exceptions nur fuer echte Programmierfehler (null-Argumente, etc.).

### 9.3 Nullable Reference Types

Das Projekt verwendet `<Nullable>enable</Nullable>`. Alle optionalen Felder sind explizit `T?`:

```csharp
public sealed class GmlFeature : GmlNode, IGmlRootContent
{
    public string? Id { get; init; }  // Optional wie in Dart
    public GmlPropertyBag Properties { get; init; } = GmlPropertyBag.Empty;
}
```

### 9.4 Performance-Optimierungen

Vorteile gegenueber Dart/TypeScript die C# bietet:

| Feature | Anwendung |
|---|---|
| `readonly record struct` | `GmlCoordinate` -- Werttyp, kann Heap-Allokationen reduzieren |
| `Span<T>` / `ReadOnlySpan<T>` | Koordinaten-Parsing ohne String-Allokation |
| `XmlReader` | Forward-only Streaming ohne DOM |
| `IAsyncEnumerable<T>` | Nativer Streaming-Support |
| `ArrayPool<T>` | Wiederverwendbare Puffer fuer Koordinaten |
| `StringPool` / String-Interning | Namespace-Strings cachen |

Koordinaten-Parsing mit Span (zero-alloc):
```csharp
internal static GmlCoordinate ParsePos(ReadOnlySpan<char> text, int? srsDimension = null)
{
    // Whitespace trimmen
    text = text.Trim();
    // Werte ohne Substring-Allokation extrahieren
    Span<Range> ranges = stackalloc Range[4];
    int count = text.Split(ranges, ' ', StringSplitOptions.RemoveEmptyEntries);
    double x = double.Parse(text[ranges[0]], CultureInfo.InvariantCulture);
    double y = double.Parse(text[ranges[1]], CultureInfo.InvariantCulture);
    double? z = count > 2 ? double.Parse(text[ranges[2]], CultureInfo.InvariantCulture) : null;
    double? m = count > 3 ? double.Parse(text[ranges[3]], CultureInfo.InvariantCulture) : null;
    return new GmlCoordinate(x, y, z, m);
}
```

---

## 10. Implementierungsphasen

### Phase 1: Core-Modell + Geometrie-Parser (Grundlage)

**Umfang:**
- Projektstruktur anlegen (Solution, Projekte, Directory.Build.props)
- Gesamtes Domain-Modell (Geometry, Feature, Coverage, ParseResult)
- `GmlParser.ParseXmlString()` mit Geometry-Support
- `XmlHelpers` mit Namespace-Handling und Koordinaten-Parsing
- GML-Versionserkennung
- Unit-Tests fuer alle Geometrie-Typen (GML 2 + GML 3)

**Quellen:** Primaer `gml4dart/lib/src/model/` und `gml4dart/lib/src/parser/geometry_parser.dart`

### Phase 2: Feature-Parser + FeatureCollection

**Umfang:**
- `FeatureParser` (Feature + FeatureCollection)
- PropertyValue-Parsing (String, Numeric, Geometry, Nested, RawXml)
- WFS-Unterstuetzung (featureMember, member, featureMembers)
- BoundedBy-Extraktion
- Tests mit realen WFS-Antworten

**Quellen:** `gml4dart/lib/src/parser/feature_parser.dart`

### Phase 3: Coverage-Parser

**Umfang:**
- `CoverageParser` (RectifiedGrid, Grid, Referenceable, MultiPoint)
- Grid/RectifiedGrid/GridEnvelope Modelle
- RangeSet + RangeType Parsing
- GeoTIFF-Metadata-Extraktion
- Coverage-Generator (GML 3.2 XML Ausgabe)

**Quellen:** `gml4dart/lib/src/parser/coverage_parser.dart`, `s-gml/src/generators/coverage-generator.ts`

### Phase 4: Interop (GeoJSON + WKT)

**Umfang:**
- `GeoJsonBuilder` -- alle Geometrie-Typen + Features
- `WktBuilder` -- alle Geometrie-Typen
- JSON-Serialisierung via `System.Text.Json`

**Quellen:** `gml4dart/lib/src/interop/`

### Phase 5: OWS + WCS

**Umfang:**
- `OwsExceptionParser`
- `WcsRequestBuilder` (URL + XML)
- `WcsCapabilitiesParser`

**Quellen:** `gml4dart/lib/src/ows/`, `gml4dart/lib/src/wcs/`, `s-gml/src/wcs/`

### Phase 6: Streaming + I/O

**Umfang:**
- `GmlFeatureStreamParser` mit `IAsyncEnumerable<GmlFeature>`
- `Gml4Net.IO` Paket (File, URL, Streaming)
- Integration mit OWS-Exception-Erkennung in HTTP-Antworten

**Quellen:** `gml4dart/lib/src/parser/streaming/`, `gml4dart/lib/src/io/`

### Phase 7: Erweiterte Builder (aus s-gml)

**Umfang:**
- `IGmlBuilder<TGeometry, TFeature, TCollection>` Interface
- KML, CSV, CIS JSON, CoverageJSON Builder
- CLI-Tool (optional)

**Quellen:** `s-gml/src/builders/`

---

## 11. Test-Strategie

### Testdaten

Die Testdaten aus `gml4dart/test/` und `s-gml/test/` (inline GML-Strings) werden direkt uebernommen. Gleiche XML-Fixtures, gleiche erwartete Ergebnisse.

### Test-Kategorien

```
tests/
└── Gml4Net.Tests/
    ├── Model/
    │   └── GmlCoordinateTests.cs        # Dimension, Equality
    ├── Parser/
    │   ├── GeometryParserTests.cs       # Alle Geometrie-Typen
    │   ├── FeatureParserTests.cs        # Features + Collections
    │   ├── CoverageParserTests.cs       # Coverage-Typen
    │   ├── VersionDetectionTests.cs     # GML 2/3 Erkennung
    │   └── EdgeCaseTests.cs             # Malformed XML, fehlende Elemente
    ├── Interop/
    │   ├── GeoJsonBuilderTests.cs
    │   └── WktBuilderTests.cs
    ├── Ows/
    │   └── OwsExceptionTests.cs
    ├── Wcs/
    │   ├── WcsRequestBuilderTests.cs
    │   └── WcsCapabilitiesParserTests.cs
    ├── Streaming/
    │   └── StreamParserTests.cs
    └── Generators/
        └── CoverageGeneratorTests.cs
```

### Namenskonventionen

```csharp
[Fact]
public void ParseXmlString_WithGml32Point_ReturnsGmlPoint()
{
    // Arrange
    var xml = "<gml:Point xmlns:gml=\"http://www.opengis.net/gml/3.2\">" +
              "<gml:pos>10.0 20.0</gml:pos></gml:Point>";

    // Act
    var result = GmlParser.ParseXmlString(xml);

    // Assert
    result.HasErrors.Should().BeFalse();
    result.Document!.Root.Should().BeOfType<GmlPoint>()
        .Which.Coordinate.Should().Be(new GmlCoordinate(10.0, 20.0));
}
```

---

## 12. Build & Packaging

### Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### NuGet-Pakete

| Paket | Beschreibung |
|---|---|
| `Gml4Net` | Core: Modell, Parser, Interop, OWS, WCS |
| `Gml4Net.IO` | File- und HTTP-I/O, Streaming |

### Target Frameworks

- **Primaer:** `net8.0` (LTS)
- **Optional:** `netstandard2.1` fuer breitere Kompatibilitaet
  - Einschraenkung: separate Bewertung noetig, weil neuere Sprach- und API-Komfortfeatures im Design aktuell auf `net8.0` ausgerichtet sind
  - Entscheidung wird in Phase 1 getroffen

---

## 13. Abgrenzung zu Quellprojekten

### Was gml4net von gml4dart uebernimmt
- Sauberes sealed/final Domain-Modell
- Result-basierte Fehlerbehandlung (GmlParseResult + GmlParseIssue)
- GmlUnsupportedNode als Fallback
- Klare Trennung Core / I/O
- Null externe Abhaengigkeiten im Core (gml4dart hat 2)

### Was gml4net von s-gml uebernimmt
- Builder-Interface-Architektur (IGmlBuilder)
- Zusaetzliche Ausgabeformate (KML, CSV, CIS JSON, CoverageJSON)
- Performance-Optimierungen (String-Interning, Pooling)
- WCS Capabilities Parsing (vollstaendiger als in gml4dart)
- CLI-Konzept (optional als `dotnet tool`)

### Was gml4net NICHT uebernimmt
- Browser-Build (s-gml) -- nicht relevant fuer .NET
- `fast-xml-parser` (s-gml) -- `System.Xml.Linq` ist ueberlegen
- XSD-Validierung (s-gml) -- kann spaeter via `XmlSchemaSet` ergaenzt werden
- Docker-CLI (s-gml) -- .NET hat eigene Publish-Mechanismen
- Shapefile/GeoPackage/FlatGeobuf Builder (s-gml) -- externe Deps, spaetere Phase

---

## Verwandte Dokumente

- **[architecture.md](architecture.md)** -- Detaillierte .NET-Architektur,
  Schichtenmodell, Systemdiagramm, Teststrategie
- **[roadmap.md](roadmap.md)** -- Phasenplan mit Aufgaben-Checklisten,
  Abhaengigkeitsgraph, Meilensteine und offene Entscheidungen
