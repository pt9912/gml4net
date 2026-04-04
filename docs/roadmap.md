# Gml4Net: Roadmap

Aktueller Stand: Phase 3 abgeschlossen. Phase 4 (Interop: GeoJSON + WKT) ist als naechstes geplant.

## Uebersicht

Die Implementierung erfolgt in sieben Phasen. Jede Phase ist in sich
abgeschlossen, liefert lauffaehigen Code mit Tests und kann unabhaengig
released werden.

```
Phase 1    Phase 2    Phase 3    Phase 4    Phase 5    Phase 6    Phase 7
Modell +   Feature-   Coverage   Interop    OWS +      Streaming  Erweiterte
Geometrie  Parser     Parser +   GeoJSON    WCS        + I/O      Builder
                      Generator  + WKT
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
   ▲                                                       ▲
   MVP                                               Feature-komplett
```

---

## Phase 1: Core-Modell + Geometrie-Parser

**Status:** Abgeschlossen
**Ziel:** Lauffaehige Basis mit allen Typen und Geometrie-Parsing

### Aufgaben

#### 1.1 Projektstruktur

- [x] Solution `GML4Net.sln` anlegen
- [x] Projekt `Gml4Net` (Class Library, `net10.0`)
- [x] Projekt `Gml4Net.Tests` (xUnit v3)
- [x] `Directory.Build.props` mit gemeinsamen Einstellungen
- [x] `Dockerfile` fuer containerisierten Build-, Test-, Pack- und Release-Workflow
  ```xml
  <LangVersion>14</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  ```

#### 1.2 Domain-Modell

- [x] `GmlNode` -- abstrakte Basisklasse
- [x] `IGmlRootContent` -- Marker-Interface
- [x] `GmlVersion` -- Enum (`V2_1_2`, `V3_0`, `V3_1`, `V3_2`, `V3_3`)
- [x] `GmlCoordinate` -- `readonly record struct`
- [x] `GmlDocument` -- Dokument-Huelle
- [x] `GmlParseResult` + `GmlParseIssue` + `GmlIssueSeverity`
- [x] `GmlUnsupportedNode`
- [x] Geometrie-Hierarchie (11 Typen):
  - [x] `GmlGeometry` (abstract)
  - [x] `GmlPoint`
  - [x] `GmlLineString`
  - [x] `GmlLinearRing`
  - [x] `GmlPolygon`
  - [x] `GmlEnvelope`
  - [x] `GmlBox`
  - [x] `GmlCurve`
  - [x] `GmlSurface`
  - [x] `GmlMultiPoint`
  - [x] `GmlMultiLineString`
  - [x] `GmlMultiPolygon`
- [x] Feature-Modell (Typen ohne Parser):
  - [x] `GmlFeature`
  - [x] `GmlFeatureCollection`
  - [x] `GmlPropertyValue`-Hierarchie (5 Subtypen)
- [x] Coverage-Modell (Typen ohne Parser):
  - [x] `GmlCoverage` (abstract) + 4 Subtypen
  - [x] `GmlGrid`, `GmlRectifiedGrid`, `GmlGridEnvelope`
  - [x] `GmlRangeSet`, `GmlRangeType`, `GmlRangeField`

#### 1.3 Parser-Grundlagen

- [x] `GmlNamespaces` -- Konstanten fuer alle Namespace-URIs
- [x] `XmlHelpers` -- zentrale Hilfsfunktionen:
  - [x] `IsGmlNamespace()`, `IsWfsNamespace()`
  - [x] `FindGmlChild()`, `FindGmlChildren()`, `FindWfsChildren()`
  - [x] `GetSrsName()`, `GetFeatureId()`, `GetSrsDimension()`
  - [x] `DetectVersion()`
  - [x] `ParsePos()`
  - [x] `ParsePosList()`
  - [x] `ParseGml2Coordinates()`
- [x] `GeometryParser.Parse()` -- alle 11 Geometrie-Typen + MultiCurve, MultiSurface, MultiGeometry:
  - [x] Point (GML 2 + 3)
  - [x] LineString (GML 2 + 3)
  - [x] LinearRing
  - [x] Polygon (exterior/interior + outerBoundaryIs/innerBoundaryIs)
  - [x] Envelope (GML 3)
  - [x] Box (GML 2)
  - [x] Curve (Segmente → Koordinaten)
  - [x] Surface (Polygon-Patches)
  - [x] MultiPoint, MultiLineString, MultiPolygon
  - [x] MultiCurve → MultiLineString
  - [x] MultiSurface → MultiPolygon
  - [x] MultiGeometry (best-fit Dispatch)
- [x] `GmlParser.ParseXmlString()` -- Orchestrierung mit Root-Dispatch
- [x] `GmlParser.ParseBytes()` -- Byte-Span-Einstieg
- [x] `GmlParser.ParseStream()` -- Stream-Einstieg

#### 1.4 Tests (41 Tests, alle gruen)

- [x] `GmlCoordinateTests` -- Dimension, Equality, Edge Cases
- [x] `VersionDetectionTests` -- GML 2/3 Namespace-Erkennung
- [x] `GeometryParserTests` -- alle Geometrie-Typen, GML 2 + GML 3 Varianten
- [x] `EdgeCaseTests` -- malformed XML, fehlende Koordinaten, leere Elemente
- [x] Coverage-Gate auf 90% Line Coverage im Docker-Testworkflow festgelegt

**Portierungsquellen:**
- `gml4dart/lib/src/model/` (alle Modell-Dateien)
- `gml4dart/lib/src/parser/geometry_parser.dart`
- `gml4dart/lib/src/parser/xml_helpers.dart`
- `gml4dart/test/gml4dart_test.dart`
- `gml4dart/test/parser_test.dart`

---

## Phase 2: Feature-Parser + FeatureCollection

**Status:** Abgeschlossen
**Voraussetzung:** Phase 1
**Ziel:** WFS-Antworten parsen koennen

### Aufgaben

- [x] `FeatureParser`:
  - [x] `ParseCollection()` -- FeatureCollection mit boundedBy
  - [x] `ParseFeature()` -- einzelnes Feature mit ID und Properties
  - [x] Feature-Member-Varianten: `gml:featureMember`, `wfs:member`,
        `gml:featureMembers` (Plural)
- [x] Property-Value-Parsing:
  - [x] Geometrie-Kinder erkennen → `GmlGeometryProperty`
  - [x] Verschachtelte Elemente → `GmlNestedProperty`
  - [x] Numerische Werte → `GmlNumericProperty`
  - [x] Text-Fallback → `GmlStringProperty`
  - [x] Nicht klassifizierbar → `GmlRawXmlProperty`
- [x] `GmlParser` Root-Dispatch erweitern:
  - [x] FeatureCollection erkennen
  - [x] Einzelnes Feature erkennen (kein featureMember-Wrapper)
- [x] `FeatureParserTests` (12 Tests):
  - [x] einfache FeatureCollection (WFS 2.0)
  - [x] Feature mit gemischten Property-Typen
  - [x] WFS 1.0/1.1 (`gml:featureMember` mit `fid`)
  - [x] GML 3.1 `gml:featureMembers` (Plural)
  - [x] verschachtelte Properties
  - [x] Feature ohne Geometrie
  - [x] boundedBy-Extraktion
  - [x] Standalone Feature (ohne Collection-Wrapper)
  - [x] Leere FeatureCollection
  - [x] Feature mit Polygon-Geometrie
  - [x] Gemischte Member-Varianten in einer Collection
  - [x] Leere Property-Elemente

**Portierungsquellen:**
- `gml4dart/lib/src/parser/feature_parser.dart`
- `gml4dart/test/parser_test.dart` (Feature-Abschnitte)
- `s-gml/test/wfs-integration.test.ts`

---

## Phase 3: Coverage-Parser

**Status:** Abgeschlossen
**Voraussetzung:** Phase 1
**Ziel:** OGC Coverage-Dokumente parsen und erzeugen koennen

### Aufgaben

- [x] `CoverageParser`:
  - [x] `RectifiedGridCoverage` parsen (origin, offsetVectors, limits)
  - [x] `GridCoverage` parsen
  - [x] `ReferenceableGridCoverage` parsen
  - [x] `MultiPointCoverage` parsen
  - [x] boundedBy, rangeSet, rangeType extrahieren
  - [x] Grid-Modelle: dimension, limits (GridEnvelope), axisLabels
  - [x] RectifiedGrid: srsName, origin, offsetVectors (Affine)
  - [x] RangeSet: Inline-Daten und Datei-Referenzen
  - [x] RangeType: SWE DataRecord / RangeField-Definitionen
- [x] `CoverageGenerator`:
  - [x] GML 3.2 XML aus RectifiedGridCoverage erzeugen
  - [x] GML 3.2 XML aus GridCoverage erzeugen
  - [x] GML 3.2 XML aus ReferenceableGridCoverage erzeugen
  - [x] GML 3.2 XML aus MultiPointCoverage erzeugen
  - [x] Namespace-Deklarationen (gml, gmlcov, swe)
- [x] `GeoTiffUtils`:
  - [x] `ExtractMetadata()` -- Width, Height, CRS, Transform aus Coverage
  - [x] `PixelToWorld()` -- Affine Transformation
  - [x] `WorldToPixel()` -- inverse Transformation
- [x] `GmlParser` Root-Dispatch erweitern fuer Coverage-Typen
- [x] Tests (27 neue Tests):
  - [x] `CoverageParserTests` -- alle 4 Coverage-Typen + GMLCOV-Namespace + Edge Cases
  - [x] `CoverageGeneratorTests` -- Roundtrip: Parse → Generate → Parse (alle 4 Typen)
  - [x] `GeoTiffMetadataTests` -- Metadaten-Extraktion, PixelToWorld, WorldToPixel, Roundtrip, degenerierter Transform
  - [x] Edge Cases: fehlende domainSets, fehlende Grids, leere RangeSets, origin ohne Point-Wrapper

**Portierungsquellen:**
- `gml4dart/lib/src/parser/coverage_parser.dart`
- `gml4dart/lib/src/generators/coverage_generator.dart`
- `gml4dart/lib/src/utils/geotiff_metadata.dart`
- `gml4dart/test/coverage_test.dart`
- `gml4dart/test/coverage_gaps_test.dart`
- `s-gml/src/generators/coverage-generator.ts`

---

## Phase 4: Interop (GeoJSON + WKT)

**Status:** Offen
**Voraussetzung:** Phase 2 (Features fuer GeoJSON)
**Ziel:** GML-Daten in gaengige Austauschformate konvertieren

### Aufgaben

- [ ] `GeoJsonBuilder`:
  - [ ] `Geometry()` → `JsonObject` fuer alle Geometrie-Typen:
    - Point, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon
    - Envelope/Box → Polygon-Rechteck
    - Curve → LineString (abgeflacht)
    - Surface → MultiPolygon (Patches)
  - [ ] `Feature()` → GeoJSON Feature mit Properties
  - [ ] `FeatureCollection()` → GeoJSON FeatureCollection
  - [ ] `Document()` → Auto-Dispatch
  - [ ] String-Varianten: `GeometryToJson()`, `FeatureToJson()`, etc.
  - [ ] Property-Konvertierung:
    - String/Numeric → JSON-Werte
    - Geometry → geometry-Feld
    - Nested → verschachteltes JSON-Objekt
    - RawXml → String-Wert
- [ ] `WktBuilder`:
  - [ ] `Geometry()` → WKT-String fuer alle Geometrie-Typen
  - [ ] 2D und 3D Koordinaten-Ausgabe
  - [ ] Formate: POINT, LINESTRING, POLYGON, MULTI*
- [ ] Tests:
  - [ ] `GeoJsonBuilderTests` -- alle Geometrien, Features, FeatureCollections
  - [ ] `WktBuilderTests` -- alle Geometrien, Dimensionen
  - [ ] Roundtrip-Validierung: Parse → GeoJSON → JSON-String valide

**Portierungsquellen:**
- `gml4dart/lib/src/interop/geojson_builder.dart`
- `gml4dart/lib/src/interop/wkt_builder.dart`
- `gml4dart/test/geojson_test.dart`
- `gml4dart/test/wkt_test.dart`

---

## Phase 5: OWS + WCS

**Status:** Offen
**Voraussetzung:** Phase 3 (Coverage-Modell)
**Ziel:** OGC-Webdienste ansprechen und Fehler verarbeiten koennen

### Aufgaben

- [ ] `OwsExceptionParser`:
  - [ ] `IsOwsExceptionReport()` -- Root-Element pruefen
  - [ ] `Parse()` -- ExceptionReport mit Exceptions extrahieren
  - [ ] Modelle: `OwsException`, `OwsExceptionReport`
- [ ] `WcsRequestBuilder`:
  - [ ] Konstruktor mit baseUrl und WcsVersion
  - [ ] `BuildGetCoverageUrl()` -- URL mit Query-Parametern
    - Versionsabhaengige Parameternamen
    - Subset-Encoding als wiederholte Parameter
  - [ ] `BuildGetCoverageXml()` -- XML-Body fuer WCS 2.0+ POST
  - [ ] Modelle: `WcsVersion`, `WcsSubset`, `WcsGetCoverageOptions`
- [ ] `WcsCapabilitiesParser`:
  - [ ] `Parse()` -- GetCapabilities-XML auswerten
  - [ ] ServiceIdentification (Title, Abstract, Keywords)
  - [ ] Operations (GET/POST URLs)
  - [ ] Coverage-Summaries (ID, Subtype, Bounds)
  - [ ] Supported Formats und CRS
  - [ ] Modelle: `WcsCapabilities`, `WcsServiceIdentification`,
        `WcsOperationMetadata`, `WcsCoverageSummary`
- [ ] Tests:
  - [ ] `OwsExceptionTests` -- Erkennung, Parsing, mehrere Exceptions
  - [ ] `WcsRequestBuilderTests` -- URL + XML, verschiedene Versionen
  - [ ] `WcsCapabilitiesParserTests` -- WCS 1.0, 1.1, 2.0

**Portierungsquellen:**
- `gml4dart/lib/src/ows/ows_exception.dart`
- `gml4dart/lib/src/wcs/request_builder.dart`
- `gml4dart/lib/src/wcs/capabilities_parser.dart`
- `gml4dart/test/ows_wcs_test.dart`
- `s-gml/src/wcs/` (vollstaendigere WCS-Implementierung)

---

## Phase 6: Streaming + I/O

**Status:** Offen
**Voraussetzung:** Phase 2 (Feature-Parser), Phase 5 (OWS fuer HTTP-Error-Detection)
**Ziel:** Grosse Dokumente speichereffizient verarbeiten, Dateien und URLs laden

### Aufgaben

#### 6.1 Streaming-Parser

- [ ] `GmlFeatureStreamParser`:
  - [ ] `ParseAsync(Stream)` → `IAsyncEnumerable<GmlFeature>`
  - [ ] `ProcessFeaturesAsync(Stream, Func<GmlFeature, Task>)` → `Task<int>`
  - [ ] Basiert auf `XmlReader` (forward-only)
  - [ ] Erkennt Feature-Member-Grenzen:
    - `gml:featureMember` (GML 2/WFS 1.0-1.1)
    - `wfs:member` (WFS 2.0)
    - `gml:featureMembers` (Plural, GML 3.1)
  - [ ] Liest Subtree per `XElement.ReadFrom(XmlReader)`
  - [ ] Uebergibt Fragmente an bestehende `FeatureParser`/`GeometryParser`
  - [ ] `CancellationToken`-Support
- [ ] Tests:
  - [ ] Kleines Dokument (Vergleich mit DOM-Ergebnis)
  - [ ] Grosses synthetisches Dokument (10.000+ Features)
  - [ ] Verschiedene Member-Varianten
  - [ ] Cancellation

#### 6.2 I/O-Paket (Gml4Net.IO)

- [ ] Projekt `Gml4Net.IO` anlegen (referenziert `Gml4Net`)
- [ ] Projekt `Gml4Net.IO.Tests` anlegen
- [ ] `GmlIo`:
  - [ ] `ParseFile(string path)` → synchrones File-Parsing
  - [ ] `ParseFileAsync(string path)` → asynchrones File-Parsing
  - [ ] `ParseUrlAsync(Uri, HttpClient?)` → HTTP GET + OWS-Erkennung
  - [ ] `StreamFeaturesFromFile(string path)` → `IAsyncEnumerable<GmlFeature>`
  - [ ] `StreamFeaturesFromUrl(Uri, HttpClient?)` → `IAsyncEnumerable<GmlFeature>`
  - [ ] `GmlIoException` fuer Transportfehler (nicht GmlParseIssue):
    - `file_not_found` -- Datei existiert nicht
    - `file_read_error` -- Datei nicht lesbar
    - `http_error` -- HTTP-Statuscode != 2xx (mit StatusCode Property)
    - `network_error` -- Verbindungsfehler
    - OWS Exceptions → als `GmlParseIssue` im Result (HTTP 200, fachlicher Fehler)
- [ ] Tests:
  - [ ] Datei-Parsing (existierende und nicht-existierende Dateien)
  - [ ] URL-Parsing mit MockHttpMessageHandler
  - [ ] OWS-Exception-Erkennung in HTTP-Antworten
  - [ ] Streaming von Datei und URL

**Portierungsquellen:**
- `gml4dart/lib/src/parser/streaming/gml_feature_stream_parser.dart`
- `gml4dart/lib/src/io/gml_io.dart`
- `gml4dart/test/streaming_test.dart`
- `gml4dart/test/io_test.dart`

---

## Phase 7: Erweiterte Builder

**Status:** Offen
**Voraussetzung:** Phase 4 (Interop-Grundlage)
**Ziel:** Zusaetzliche Ausgabeformate aus s-gml portieren

### Aufgaben

- [ ] `IGmlBuilder<TGeometry, TFeature, TCollection>` Interface finalisieren
- [ ] Bestehende Builder (`GeoJsonBuilder`, `WktBuilder`) auf Interface umstellen
  (optional, Kompatibilitaet mit statischer API beibehalten)
- [ ] Neue Builder:
  - [ ] `KmlBuilder` -- KML-Ausgabe fuer Google Earth
  - [ ] `CsvBuilder` -- CSV mit WKT-Geometrien
  - [ ] `CisJsonBuilder` -- OGC CIS JSON 1.1
  - [ ] `CoverageJsonBuilder` -- OGC CoverageJSON
- [ ] Optional: `Gml4Net.Cli` als `dotnet tool`:
  - [ ] Eingabe: GML-Datei oder URL
  - [ ] Ausgabe: Gewaehltes Format (geojson, wkt, kml, csv)
  - [ ] `--format`, `--output`, `--validate` Optionen
- [ ] Tests fuer alle neuen Builder

**Portierungsquellen:**
- `s-gml/src/builders/kml.ts`
- `s-gml/src/builders/csv.ts`
- `s-gml/src/builders/cis-json.ts`
- `s-gml/src/builders/coveragejson.ts`
- `s-gml/src/cli.ts`

---

## Abhaengigkeiten zwischen Phasen

```
Phase 1 ──► Phase 2 ──► Phase 4
   │            │           │
   │            └──► Phase 6 (6.1 Streaming)
   │            │
   │            └──► Phase 6 (6.2 I/O, nach Phase 5)
   │
   └──► Phase 3 ──► Phase 5
                        │
                        └──► Phase 6 (6.2 I/O, OWS)

Phase 4 ──► Phase 7
```

Phase 2 und Phase 3 koennen parallel bearbeitet werden (beide haengen nur
von Phase 1 ab). Phase 4 und Phase 5 koennen ebenfalls teilweise parallel
laufen.

---

## Qualitaetskriterien pro Phase

Jede Phase gilt als abgeschlossen, wenn:

1. **Alle Aufgaben erledigt** -- Checkliste vollstaendig abgehakt
2. **Tests gruen** -- `dotnet test` laeuft fehlerfrei
3. **Null Warnings** -- `TreatWarningsAsErrors` ist aktiviert
4. **Coverage eingehalten** -- mindestens 90% Line Coverage ueber den Testlauf
5. **API dokumentiert** -- XML-Doc-Comments auf allen oeffentlichen Typen und Methoden
6. **Paritaet geprueft** -- Vergleich mit gml4dart/s-gml Testergebnissen
   fuer die jeweilige Funktionalitaet

---

## Meilensteine

| Meilenstein | Phase | Beschreibung |
|---|---|---|
| **MVP** | Phase 1 | Geometrie-Parsing funktioniert, Modell steht -- **erreicht** |
| **WFS-Ready** | Phase 2 | FeatureCollections aus WFS-Antworten parsbar -- **erreicht** |
| **Coverage-Ready** | Phase 3 | OGC Coverages parsbar und erzeugbar -- **erreicht** |
| **Interop-Ready** | Phase 4 | GeoJSON + WKT Export |
| **OGC-komplett** | Phase 5 | OWS + WCS Integration |
| **Production-Ready** | Phase 6 | Streaming + I/O, grosse Dokumente |
| **Feature-komplett** | Phase 7 | Alle Builder, Feature-Paritaet mit s-gml |

---

## Offene Entscheidungen

| Frage | Bereich | Entscheidungszeitpunkt |
|---|---|---|
| Soll `GeoJsonBuilder` auch `IGmlBuilder` implementieren? | Interop | Phase 4 / 7 |
| CLI als eigenes Paket oder im Core? | Packaging | Phase 7 |

## Getroffene Entscheidungen

| Entscheidung | Bereich | Phase |
|---|---|---|
| Target Framework: `net10.0` (LTS), kein Multi-Target | Build | Phase 1 |
| API-Einstieg: `GmlParser.ParseXmlString()` (statische Klasse) | API-Design | Phase 1 |
| Test-Framework: xUnit v3 + FluentAssertions | Test | Phase 1 |
| NuGet-Releases auf `nuget.org` via Docker-gestuetztem `pack`/`push`-Workflow | Release | Design-Phase |

---

## Nicht im Scope (bewusst ausgeklammert)

| Thema | Begruendung |
|---|---|
| XSD-Validierung | Grosse Schema-Dateien, eigenes Paket sinnvoller |
| CRS-Transformation | Eigenes Fachgebiet, Integration mit ProjNET spaeter |
| Shapefile/GeoPackage/FlatGeobuf | Schwere externe Abhaengigkeiten, eigene Pakete |
| Browser/WASM-Build | .NET WASM (Blazor) kann Core-Paket direkt nutzen |
| GML-Schema-Generierung | Write-Seite wird nur fuer Coverage unterstuetzt |
| Performance-Benchmarks | Erst sinnvoll ab Phase 6 mit Streaming |
