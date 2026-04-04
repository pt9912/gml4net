# Gml4Net: Architektur

Aktueller Stand: Design Phase. Dieses Dokument beschreibt die Zielarchitektur, nicht eine bereits implementierte Codebasis.

## Architekturziele

Die Architektur erfuellt fuenf Anforderungen gleichzeitig:

- robuste GML-Verarbeitung in reinem .NET ohne externe Abhaengigkeiten
- saubere Modellierung von Geometrien, Features und Coverage-Typen mit
  modernem C# (sealed classes, records, pattern matching)
- klare Trennung zwischen Core, I/O, Interop und Spezialmodulen
- kontrollierte Erweiterbarkeit fuer WFS-, WCS- und OWS-nahe Anwendungsfaelle
- speichereffiziente Verarbeitung grosser FeatureCollection-Dokumente via
  Streaming

Die zentrale Entscheidung:

Der fachliche Kern bleibt frei von externen Abhaengigkeiten, nutzt
ausschliesslich BCL-APIs (`System.Xml.Linq`, `System.Text.Json`, `System.IO`)
und stellt eine dokumentzentrierte Parse-API auf Basis eines Result-Typs bereit.

Zusaetzliche Architekturentscheidungen:

- die oeffentliche Core-API ist `GmlParser.ParseXmlString()` als primaerer
  Einstiegspunkt
- Parse-Fehler werden als `GmlParseIssue` in `GmlParseResult` modelliert, nicht
  als fachliche Exceptions
- GeoJSON und WKT bleiben als leichte Interop-Builder im Core
- Datei-, HTTP- und andere Transportquellen leben im separaten Paket `Gml4Net.IO`
- Streaming wird als spezialisierter Parserpfad fuer grosse
  WFS-/FeatureCollection-Dokumente bereitgestellt, nicht als generische
  Dokument-API
- Coverage-XML-Erzeugung (Schreiben) wird im Core unterstuetzt
- spaetere Builder (KML, CSV, CoverageJSON) werden ueber ein generisches
  `IGmlBuilder<TGeometry, TFeature, TCollection>` Interface angebunden

---

## System-Architektur

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Gml4Net.IO (optional)                       │
│   GmlIo.ParseFile / ParseUrlAsync / StreamFeaturesFromUrl           │
└──────────────────┬──────────────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                           Input Layer                                │
│   string (XML) · ReadOnlySpan<byte> · Stream                        │
└──────────────────┬──────────────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          Parsing Layer                                │
│                                                                      │
│   GmlParser ─────────┬──────────────┬───────────────┐               │
│   (Orchestrierung)   │              │               │               │
│                      ▼              ▼               ▼               │
│               GeometryParser  FeatureParser  CoverageParser         │
│                      │              │               │               │
│                      └──────┬───────┘               │               │
│                             ▼                       │               │
│                        XmlHelpers ◄─────────────────┘               │
│                    (Namespace, Koordinaten,                          │
│                     Versionserkennung)                               │
│                                                                      │
│   GmlFeatureStreamParser (XmlReader-basiert, IAsyncEnumerable)      │
└──────────────────┬──────────────────────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        Core Domain Model                             │
│                                                                      │
│   GmlDocument ── GmlParseResult ── GmlParseIssue                    │
│        │                                                             │
│        └─► IGmlRootContent                                          │
│               ├── GmlGeometry (sealed hierarchy)                    │
│               │     ├── GmlPoint                                    │
│               │     ├── GmlLineString                               │
│               │     ├── GmlLinearRing                               │
│               │     ├── GmlPolygon                                  │
│               │     ├── GmlEnvelope / GmlBox                        │
│               │     ├── GmlCurve / GmlSurface                       │
│               │     └── GmlMultiPoint / MultiLineString / MultiPoly │
│               ├── GmlFeature ── GmlPropertyValue (sealed hierarchy) │
│               ├── GmlFeatureCollection                              │
│               └── GmlCoverage (sealed hierarchy)                    │
│                     ├── GmlRectifiedGridCoverage                    │
│                     ├── GmlGridCoverage                             │
│                     ├── GmlReferenceableGridCoverage                │
│                     └── GmlMultiPointCoverage                       │
│                                                                      │
│   GmlCoordinate (readonly record struct)                            │
│   GmlVersion (enum)                                                  │
│   GmlUnsupportedNode                                                │
└──────────────────┬──────────────────────────────────────────────────┘
                   │
          ┌────────┼─────────────────────┐
          ▼        ▼                     ▼
┌──────────────┐ ┌──────────────┐ ┌─────────────────────┐
│ Interop Layer│ │Special Modules│ │ Generators           │
│              │ │               │ │                      │
│ GeoJsonBuilder│ │ OwsException │ │ CoverageGenerator    │
│ WktBuilder   │ │ WcsRequest   │ │                      │
│ IGmlBuilder  │ │ WcsCapabilit.│ │                      │
│ (KML, CSV,..)│ │ GeoTiffMetad.│ │                      │
└──────────────┘ └──────────────┘ └─────────────────────┘
```

---

## Schichten im Detail

### 1. Input Layer

**Verantwortung:**
- XML aus `string`, `ReadOnlySpan<byte>` oder `Stream` einlesen
- Encoding-Probleme abfangen
- Root-Dokument in die Parse-Orchestrierung uebergeben

**API-Einstiegspunkte:**
```csharp
GmlParser.ParseXmlString(string xml)        → GmlParseResult
GmlParser.ParseBytes(ReadOnlySpan<byte>)     → GmlParseResult
GmlParser.ParseStream(Stream)                → GmlParseResult
```

**Fehlerbehandlung:**
- ungueltiges XML: `GmlParseIssue` mit Severity `Error`, `Document` ist `null`
- ungueltiges Encoding: `GmlParseIssue` mit Severity `Error`
- unbekannter Root-Knoten: `GmlParseIssue` mit Severity `Error`

Transportfehler (Datei nicht gefunden, HTTP-Fehler) werden nicht als
`GmlParseIssue` modelliert -- sie gehoeren in `Gml4Net.IO` und werden dort als
eigener Fehlerkanal behandelt.

**Technische Basis:**
- `System.Xml.Linq` (`XDocument.Parse`, `XDocument.Load`) fuer DOM-basiertes Parsen
- `System.Xml.XmlReader` fuer forward-only Streaming
- keine externen Abhaengigkeiten

### 2. Parsing Layer

**Verantwortung:**
- XML in typsichere GML-Modelle transformieren
- Namespaces und Versionsvarianten behandeln
- unbekannte Elemente diagnostizieren, nicht still verlieren
- Root-Typen (Geometrie, FeatureCollection, Coverage) erkennen und dispatchen

**Komponenten:**

| Komponente | Zweck | Verarbeitet |
|---|---|---|
| `GmlParser` | Orchestrierung, oeffentliche API | Root-Dispatch, Version, Result |
| `GeometryParser` | Geometrie-Elemente | Point, LineString, Polygon, Envelope, Curve, Surface, Multi* |
| `FeatureParser` | Feature-Elemente | featureMember, member, FeatureCollection, Properties |
| `CoverageParser` | Coverage-Elemente | RectifiedGridCoverage, GridCoverage, Referenceable, MultiPoint |
| `XmlHelpers` | Namespace- und Knotenlogik | Namespace-Pruefung, Element-Suche, Koordinaten-Parsing |
| `GmlFeatureStreamParser` | Streaming-Pfad | Grosse WFS-Dokumente via XmlReader |

**Alle internen Parser sind `internal static`** -- sie werden nur ueber `GmlParser`
angesteuert und sind nicht Teil der oeffentlichen API.

#### 2.1 DOM-Pfad

Der Standard-Parser arbeitet DOM-basiert ueber `XDocument`:

1. XML per `XDocument.Parse()` einlesen
2. Root-Element identifizieren
3. GML-Version aus Namespace-Deklarationen ableiten
4. Zum passenden Teilparser dispatchen
5. `GmlParseResult` mit Document und Issues zurueckgeben

Geeignet fuer kleine und mittlere Dokumente (< 50 MB).

#### 2.2 Streaming-Pfad

Der `GmlFeatureStreamParser` ist kein generischer Ersatz fuer den DOM-Pfad,
sondern ein spezialisierter Pfad fuer grosse FeatureCollections:

- basiert auf `XmlReader` (forward-only, konstanter Speicher)
- erkennt `featureMember` / `member` / `featureMembers` Grenzen
- liest einzelne Feature-Elemente per `XElement.ReadFrom()` als DOM-Fragment
- uebergibt Fragmente an denselben `GeometryParser` / `FeatureParser`
- gibt Features via `IAsyncEnumerable<GmlFeature>` zurueck

```csharp
await foreach (var feature in GmlFeatureStreamParser.ParseAsync(stream))
{
    // Jedes Feature wird einzeln verarbeitet,
    // Speicher bleibt konstant
}
```

**Wiederverwendung:** Streaming- und DOM-Pfad teilen sich `GeometryParser`,
`FeatureParser` und `XmlHelpers`. Die Teilparser arbeiten auf `XElement`-Ebene,
nicht auf Dokumentebene -- das ermoeglicht die gemeinsame Nutzung.

### 3. Core Domain Model

Das Domain-Modell ist die semantische Mitte der Bibliothek.

**Designprinzipien:**
- keine stringly-typed `type`-Felder -- stattdessen geschlossene Typhierarchien
- immutable Modelle (`{ get; init; }`, `IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`)
- geschlossene Vererbung via `abstract class` mit `internal` Konstruktor +
  `sealed` Ableitungen -- verhindert externe Subklassen und ermoeglicht
  exhaustives Pattern-Matching
- `GmlUnsupportedNode` als Fallback fuer unbekannte XML-Elemente (kein stiller
  Datenverlust)

#### 3.1 Basistypen

```csharp
// Gemeinsame Basisklasse aller GML-Knoten
public abstract class GmlNode
{
    internal GmlNode() { }
}

// Marker-Interface fuer gueltige Dokument-Root-Typen
public interface IGmlRootContent { }
```

#### 3.2 Typhierarchie

```
GmlNode (abstract)
├── GmlGeometry (abstract, implements IGmlRootContent)
│   ├── GmlPoint
│   ├── GmlLineString
│   ├── GmlLinearRing
│   ├── GmlPolygon
│   ├── GmlEnvelope
│   ├── GmlBox
│   ├── GmlCurve
│   ├── GmlSurface
│   ├── GmlMultiPoint
│   ├── GmlMultiLineString
│   └── GmlMultiPolygon
├── GmlFeature (implements IGmlRootContent)
├── GmlFeatureCollection (implements IGmlRootContent)
├── GmlCoverage (abstract, implements IGmlRootContent)
│   ├── GmlRectifiedGridCoverage
│   ├── GmlGridCoverage
│   ├── GmlReferenceableGridCoverage
│   └── GmlMultiPointCoverage
└── GmlUnsupportedNode
```

#### 3.3 Koordinatenmodell

```csharp
public readonly record struct GmlCoordinate(
    double X, double Y, double? Z = null, double? M = null)
{
    public int Dimension => (Z, M) switch
    {
        (not null, not null) => 4,
        (not null, _) or (_, not null) => 3,
        _ => 2
    };
}
```

Regeln:
- `GmlPoint` enthaelt genau eine `GmlCoordinate`
- `GmlLineString` enthaelt `IReadOnlyList<GmlCoordinate>`
- GML-2 `<coordinates>` und GML-3 `<pos>`/`<posList>` werden beide auf
  `GmlCoordinate` normalisiert
- Dimension wird aus `srsDimension` oder der Werteanzahl abgeleitet

Vorteile des `readonly record struct`:
- Werttyp mit natuerlicher Wert-Semantik (`==`)
- kann in vielen Hot Paths Heap-Allokationen vermeiden
- immutabel durch `readonly`

#### 3.4 Feature-Properties

```csharp
GmlPropertyValue (abstract)
├── GmlStringProperty        { string Value }
├── GmlNumericProperty       { double Value }
├── GmlGeometryProperty      { GmlGeometry Geometry }
├── GmlNestedProperty        { IReadOnlyDictionary<string, GmlPropertyValue> Children }
└── GmlRawXmlProperty        { string XmlContent }
```

`GmlFeature` enthaelt `IReadOnlyDictionary<string, GmlPropertyValue> Properties`.

#### 3.5 Dokument-Root

```csharp
public sealed class GmlDocument
{
    public required GmlVersion Version { get; init; }
    public required IGmlRootContent Root { get; init; }
    public GmlEnvelope? BoundedBy { get; init; }
}
```

`IGmlRootContent` ist ein Marker-Interface, implementiert von `GmlGeometry`,
`GmlFeature`, `GmlFeatureCollection` und `GmlCoverage`. Damit sind alle
Root-Typen sauber abgebildet und per Pattern-Matching unterscheidbar:

```csharp
var description = document.Root switch
{
    GmlFeatureCollection fc => $"{fc.Features.Count} Features",
    GmlFeature f            => $"Feature: {f.Id}",
    GmlGeometry g           => $"Geometrie: {g.GetType().Name}",
    GmlCoverage c           => $"Coverage: {c.Id}",
    _                       => "Unbekannt"
};
```

### 4. Fehlermodell

Das Fehlermodell trennt Parse-Diagnostics von Laufzeit-Exceptions.

```csharp
GmlParseResult
├── GmlDocument? Document      // null bei kritischem Fehler
├── IReadOnlyList<GmlParseIssue> Issues
└── bool HasErrors

GmlParseIssue
├── GmlIssueSeverity Severity  // Info, Warning, Error
├── string Code                // maschinenlesbar: "missing_coordinates", etc.
├── string Message             // menschenlesbar
└── string? Location           // XPath oder Elementname
```

**Regeln:**
- fehlerhaftes XML → `Error`, `Document == null`
- tolerierbare unbekannte Elemente → `Warning` oder `Info`
- nicht unterstuetzte GML-Konstrukte werden nicht still ignoriert
- OWS Exception Reports sind ein separates fachliches Thema
- Transportfehler (File/HTTP) werden nicht als `GmlParseIssue` modelliert

**Kein Exception-basiertes API:**
```csharp
// Richtig:
var result = GmlParser.ParseXmlString(xml);
if (result.HasErrors) { /* Issues auswerten */ }

// Falsch:
try { GmlParser.Parse(xml); } catch (GmlParseException) { }
```

Exceptions nur fuer echte Programmierfehler (`ArgumentNullException`, etc.).

### 5. Namespace- und Versionsbehandlung

GML tritt in mehreren Versionen und Namespace-Varianten auf. Die Logik ist
zentral in `XmlHelpers` gebundelt:

**Unterstuetzte Namespaces:**

| Konstante | URI | Verwendung |
|---|---|---|
| `Gml` | `http://www.opengis.net/gml` | GML 2.1.2 / 3.0 / 3.1 (gleicher NS, Content-Heuristik) |
| `Gml32` | `http://www.opengis.net/gml/3.2` | GML 3.2 |
| `Gml33` | `http://www.opengis.net/gml/3.3` | GML 3.3 |
| `Wfs1` | `http://www.opengis.net/wfs` | WFS 1.0/1.1 |
| `Wfs2` | `http://www.opengis.net/wfs/2.0` | WFS 2.0 |
| `Swe` | `http://www.opengis.net/swe/2.0` | SWE DataRecord |
| `Gmlcov` | `http://www.opengis.net/gmlcov/1.0` | GMLCOV |
| `Ows` | `http://www.opengis.net/ows/1.1` | OWS Exceptions |
| `Wcs` | `http://www.opengis.net/wcs/2.0` | WCS 2.0 |

**Versionserkennung:**
- Root- und Kind-Namespaces durchsuchen
- Namespace-URIs auf `GmlVersion` mappen
- GML-2-Indikatoren: `<coordinates>`, `<Box>`, `<outerBoundaryIs>`
- Element-Suche immer ueber Namespace-URI, nie ueber Praefix

**Unterstuetzte Versionen:**
- GML 2.1.2
- GML 3.0 / 3.1
- GML 3.2
- GML 3.3 (soweit durch Fixtures gedeckt)

### 6. Interop Layer

Der Interop-Layer uebersetzt das Domain-Modell in nutzbare Zielformate.

**Core-Builder (statisch, im Hauptpaket):**

| Builder | Eingabe | Ausgabe |
|---|---|---|
| `GeoJsonBuilder` | Geometry, Feature, FeatureCollection | `JsonObject` / JSON-String |
| `WktBuilder` | Geometry | WKT-String |

**Erweiterbare Builder (ueber Interface):**

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

Geplante Builder-Implementierungen: KML, CSV, CIS JSON, CoverageJSON.

Der Builder-Layer konsumiert das Core-Modell. Er formt nicht die Parse-API und
kennt keine Transportquellen.

### 7. Spezialmodule

#### OWS Exception Module
- OWS Exception Reports erkennen und parsen
- strukturiertes `OwsExceptionReport`-Modell
- nicht mit dem normalen GML-Parse-Ergebnis vermischt

#### WCS Request Builder
- GetCoverage-Requests aus C#-Modellen aufbauen
- versionsabhaengige Parameternamen (`coverage`, `identifier`, `coverageId`)
- URL-Encoding (GET) und XML-Body (POST fuer WCS 2.0+)

#### WCS Capabilities Parser
- GetCapabilities-Dokumente parsen (WCS 1.0 bis 2.0)
- Service-Identification, Operations, Coverage-Summaries extrahieren
- unterstuetzte Formate und CRS auflisten

#### Coverage Generator
- GML 3.2 / gmlcov XML aus Coverage-Modellen erzeugen
- RectifiedGridCoverage, GridCoverage, ReferenceableGridCoverage, MultiPointCoverage

#### GeoTIFF Metadata
- Raster-Metadaten aus Coverage-Modellen ableiten (Width, Height, CRS, Affine)
- Pixel-zu-Welt- und Welt-zu-Pixel-Transformation

### 8. Gml4Net.IO (optionales Paket)

Transportschicht fuer Datei- und HTTP-Zugriff. Lebt ausserhalb des Core, um
die Abhaengigkeitsfreiheit des Hauptpakets zu wahren.

**Fehlermodell:** Transportfehler werden als `GmlIoException` geworfen --
bewusst getrennt vom Result-basierten Parse-Fehlermodell des Core:

```csharp
public class GmlIoException : Exception
{
    public string ErrorCode { get; }     // "file_not_found", "http_error", "network_error"
    public int? HttpStatusCode { get; }  // nur bei HTTP-Fehlern
}
```

Sonderfall OWS: HTTP 200 mit OWS ExceptionReport wird als `GmlParseIssue`
ins `GmlParseResult` uebernommen (kein Transportfehler, sondern fachlicher
Parse-Fehler).

---

## Paketstruktur

```
GML4Net/
├── src/
│   ├── Gml4Net/                              # Core-Library
│   │   ├── Gml4Net.csproj                   # net8.0, keine externen Deps
│   │   ├── Model/
│   │   │   ├── GmlNode.cs
│   │   │   ├── GmlDocument.cs
│   │   │   ├── GmlVersion.cs
│   │   │   ├── GmlCoordinate.cs
│   │   │   ├── GmlRootContent.cs            # IGmlRootContent
│   │   │   ├── GmlParseResult.cs
│   │   │   ├── GmlParseIssue.cs
│   │   │   ├── GmlUnsupportedNode.cs
│   │   │   ├── Geometry/
│   │   │   │   ├── GmlGeometry.cs           # abstract + alle sealed Subtypen
│   │   │   │   └── ... (11 Dateien)
│   │   │   ├── Feature/
│   │   │   │   ├── GmlFeature.cs
│   │   │   │   ├── GmlFeatureCollection.cs
│   │   │   │   └── GmlPropertyValue.cs      # abstract + 5 sealed Subtypen
│   │   │   └── Coverage/
│   │   │       ├── GmlCoverage.cs           # abstract + 4 sealed Subtypen
│   │   │       ├── GmlGrid.cs
│   │   │       ├── GmlRectifiedGrid.cs
│   │   │       ├── GmlGridEnvelope.cs
│   │   │       ├── GmlRangeSet.cs
│   │   │       └── GmlRangeType.cs
│   │   ├── Parser/
│   │   │   ├── GmlParser.cs                 # public static, Haupteinstieg
│   │   │   ├── GeometryParser.cs            # internal static
│   │   │   ├── FeatureParser.cs             # internal static
│   │   │   ├── CoverageParser.cs            # internal static
│   │   │   ├── XmlHelpers.cs                # internal static
│   │   │   └── Streaming/
│   │   │       └── GmlFeatureStreamParser.cs
│   │   ├── Interop/
│   │   │   ├── GeoJsonBuilder.cs            # public static
│   │   │   ├── WktBuilder.cs                # public static
│   │   │   └── IGmlBuilder.cs               # public interface
│   │   ├── Ows/
│   │   │   └── OwsException.cs              # Modelle + Parser
│   │   ├── Wcs/
│   │   │   ├── WcsRequestBuilder.cs
│   │   │   └── WcsCapabilitiesParser.cs
│   │   ├── Generators/
│   │   │   └── CoverageGenerator.cs
│   │   └── Utils/
│   │       └── GeoTiffMetadata.cs
│   │
│   └── Gml4Net.IO/                          # Optionales I/O-Paket
│       ├── Gml4Net.IO.csproj               # Referenziert Gml4Net
│       └── GmlIo.cs                         # File, URL, Streaming
│
├── tests/
│   ├── Gml4Net.Tests/                       # Unit + Integration
│   └── Gml4Net.IO.Tests/                   # I/O-spezifische Tests
│
├── GML4Net.sln
├── Directory.Build.props
└── docs/
    ├── architecture.md                      # Dieses Dokument
    ├── roadmap.md                           # Implementierungsplan
    └── port2net.md                          # Portierungsentwurf
```

---

## .NET-spezifische Architekturentscheidungen

### XML-Backend: System.Xml.Linq

`XDocument` / `XElement` als DOM-Backend, `XmlReader` fuer Streaming.

Begruendung:
- Teil des .NET SDK, keine externe Abhaengigkeit
- namespace-aware (wie `package:xml` in Dart, besser als `fast-xml-parser` in TS)
- `XDocument` fuer komfortable Abfragen (LINQ)
- `XmlReader` fuer forward-only Streaming mit konstantem Speicher
- `XElement.ReadFrom(XmlReader)` als Bruecke: Streaming + DOM fuer Fragmente

### Immutabilitaet via init-only Properties

```csharp
public sealed class GmlPolygon : GmlGeometry
{
    public required GmlLinearRing Exterior { get; init; }
    public IReadOnlyList<GmlLinearRing> Interior { get; init; } = [];
}
```

- `{ get; init; }` statt `{ get; set; }` -- einmal gesetzt, dann unveraenderlich
- `IReadOnlyList<T>` und `IReadOnlyDictionary<K,V>` fuer Collections
- `required` fuer Pflichtfelder -- Compiler erzwingt Initialisierung

### Performance-Architektur

| Technik | Anwendung | Vorteil ggue. Dart/TS |
|---|---|---|
| `readonly record struct` | `GmlCoordinate` | Stack-Allokation, kein GC |
| `ReadOnlySpan<char>` | Koordinaten-Parsing | Zero-Alloc String-Verarbeitung |
| `stackalloc` | Temporaere Puffer | Kein Heap fuer kleine Arrays |
| `XmlReader` | Streaming | Forward-only, O(1) Speicher |
| `IAsyncEnumerable<T>` | Feature-Streaming | Native Sprachunterstuetzung |
| `ArrayPool<T>` | Grosse Koordinaten-Listen | Wiederverwendbare Puffer |
| `CultureInfo.InvariantCulture` | double.Parse | Korrekt unabhaengig von Locale |

### Nullable Reference Types

`<Nullable>enable</Nullable>` im gesamten Projekt. Alle optionalen Felder sind
explizit `T?`. Der Compiler prueft alle Zugriffe auf `null`-Sicherheit.

### Target Framework

- Primaer: `net8.0` (LTS bis November 2026)
- Alle modernen C#-Features verfuegbar: sealed hierarchy, pattern matching,
  `IAsyncEnumerable`, `Span<T>`, `required`, `init`

---

## Teststrategie

### Testpyramide

```
          ┌──────────┐
          │Integration│  I/O-Tests, HTTP-Mock, OWS-Detection
          ├──────────┤
          │  Builder  │  GeoJSON, WKT, Coverage-Generator
          ├──────────┤
          │  Parser   │  Geometrie, Feature, Coverage, Streaming
          ├──────────┤
          │  Modell   │  Coordinate, Dimension, Equality
          └──────────┘
```

### Teststruktur

```
tests/Gml4Net.Tests/
├── Model/
│   └── GmlCoordinateTests.cs
├── Parser/
│   ├── GeometryParserTests.cs
│   ├── FeatureParserTests.cs
│   ├── CoverageParserTests.cs
│   ├── VersionDetectionTests.cs
│   └── EdgeCaseTests.cs
├── Interop/
│   ├── GeoJsonBuilderTests.cs
│   └── WktBuilderTests.cs
├── Streaming/
│   └── StreamParserTests.cs
├── Ows/
│   └── OwsExceptionTests.cs
├── Wcs/
│   ├── WcsRequestBuilderTests.cs
│   └── WcsCapabilitiesParserTests.cs
└── Generators/
    └── CoverageGeneratorTests.cs
```

### Testdaten

- Inline GML-Strings aus `gml4dart/test/` und `s-gml/test/` uebernehmen
- gleiche XML-Fixtures, gleiche erwartete Ergebnisse
- GML 2.1.2 und GML 3.x Varianten
- Edge Cases: malformed XML, fehlende Elemente, unbekannte Namespaces

### Namenskonvention

`MethodUnderTest_Scenario_ExpectedResult`:

```csharp
[Fact]
public void ParseXmlString_WithGml32Point_ReturnsGmlPoint()
```

### Test-Abhaengigkeiten

- `xunit` (Test-Framework)
- `FluentAssertions` (lesbare Assertions)
- `Microsoft.NET.Test.Sdk` (Runner)

---

## Abgrenzung: Was nicht in den Core gehoert

| Thema | Grund | Alternative |
|---|---|---|
| XSD-Validierung | Schwere Abhaengigkeit, nicht fuer alle Nutzer relevant | Spaeter via `XmlSchemaSet` als optionales Paket |
| Shapefile/GeoPackage/FlatGeobuf | Grosse externe Dependencies | Separate NuGet-Pakete |
| Browser-Kompatibilitaet | .NET ist kein Browser-Runtime | Blazor WASM kann Core-Paket direkt nutzen |
| CLI-Tool | Separate Verteilung als dotnet tool | `Gml4Net.Cli` als eigenes Projekt |
| Koordinatentransformation (CRS) | Eigenstaendiges Fachgebiet | Integration mit ProjNET o.ae. |
