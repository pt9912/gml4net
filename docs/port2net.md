# GML4Net - Portierungsnotizen nach .NET / C#

Aktueller Stand: Die Bibliothek ist implementiert. Dieses Dokument ist keine zweite API-Referenz mehr, sondern eine kompakte Portierungsnotiz mit den getroffenen Entscheidungen, den wichtigsten Abweichungen zu den Quellprojekten und den noch offenen Restthemen.

## Quellprojekte

| Projekt | Sprache | Rolle |
|---------|---------|-------|
| `s-gml` | TypeScript | Ursprungsbibliothek, breite Builder- und WCS-Funktionalitaet |
| `gml4dart` | Dart | Sauberes Domain-Modell, parsernahe Portierungsreferenz |

Leitlinie der Portierung:

- Domain-Modell und Result-basierte Fehlerbehandlung orientieren sich primaer an `gml4dart`
- Builder-, Interop- und spaetere Erweiterungspunkte orientieren sich primaer an `s-gml`
- .NET-spezifische Vorteile werden genutzt, aber nur dort, wo sie das Modell nicht verkomplizieren

## Projektstruktur

Die heute relevante Struktur ist:

- `src/Gml4Net`
  Core-Bibliothek mit Modell, Parsern, Interop, OWS/WCS, Coverage-Generatoren und Utilities
- `src/Gml4Net.IO`
  optionales I/O-Paket fuer Datei- und HTTP-Zugriff
- `tests/Gml4Net.Tests`
  Core-Tests
- `tests/Gml4Net.IO.Tests`
  I/O-Tests

Die aktuelle Zielplattform ist `net10.0`. Releases laufen ueber Docker-gestuetzte Build-, Test-, Pack- und Publish-Pfade.

## Uebergeordnete Portierungsentscheidungen

### Domain-Modell

- Geschlossene Hierarchien aus Dart wurden in C# als `abstract`-Basistypen mit `sealed`-Subtypen umgesetzt
- `GmlCoordinate` ist ein `readonly record struct`
- Parse-Fehler werden als `GmlParseIssue` gesammelt, nicht als Parser-Exceptions geworfen
- Feature-Properties werden als geordnete `GmlPropertyBag` modelliert, nicht als flache Dictionary-Approximation

### Parser

- DOM-Parsing basiert auf `System.Xml.Linq`
- Streaming basiert auf `XmlReader`
- Versionsdetektion bleibt heuristisch, weil GML 2.1.2, 3.0 und 3.1 denselben Namespace teilen
- Namespace- und Element-Helfer sind zentral in `XmlHelpers` und `GmlNamespaces` gebuendelt

### Interop und Builder

- GeoJSON nutzt `System.Text.Json`, nicht Newtonsoft
- WKT bleibt statisch und leichtgewichtig
- Erweiterte Builder orientieren sich an `s-gml`, bleiben aber in der .NET-API bewusst klar getrennt statt alles in ein einziges Builder-Objekt zu pressen

### Packaging und Delivery

- Core und I/O sind getrennte NuGet-Pakete
- Build, Tests, Coverage-Gate und Publish laufen ueber das Dockerfile
- GitHub Actions verwenden denselben Docker-Pfad wie lokale Builds

## Phasenrueckblick

## Phase 1: Core-Modell + Geometrie-Parser

Status: Abgeschlossen.

Wichtige Portierungsentscheidungen:

- `GmlCoordinate` wurde als Werttyp modelliert
- Geometrie-Typen sind explizite Klassen statt impliziter Union-Objekte
- Result-basierte Fehlerbehandlung wurde frueh festgezogen, um spaetere Parser nicht auf Exceptions aufzubauen

Wichtige Unterschiede zu den Quellprojekten:

- .NET nutzt `readonly record struct` und `init`-Properties statt Dart-`final` oder TypeScript-Interfaces
- Das Modell ist strikter typisiert als in `s-gml`

Offene Punkte:

- keine fachlichen Restpunkte in dieser Phase

## Phase 2: Feature-Parser + FeatureCollection

Status: Abgeschlossen.

Wichtige Portierungsentscheidungen:

- Feature-Properties behalten Dokumentreihenfolge
- Mehrfach vorkommende Properties werden als echte Mehrfachwerte modelliert, nicht ueber kuenstliche Namenssuffixe
- Schemafreie Leaf-Typisierung bleibt konservativ

Wichtige Unterschiede zu den Quellprojekten:

- Die .NET-Portierung ist bei Property-Semantik strenger als fruehere Entwurfsstaende
- Das Modell trennt besser zwischen erstem Lookup-Wert und vollstaendiger Entry-Liste

Offene Punkte:

- keine akuten Parser-Restpunkte

## Phase 3: Coverage-Parser

Status: Abgeschlossen.

Wichtige Portierungsentscheidungen:

- Coverage-Parsing ist modelltreu und validiert unvollstaendige Georeferenzierung strikt
- Rectified-Grid-Informationen werden so gehalten, dass `GeoTiffUtils` direkt darauf aufbauen kann
- Generator und Parser bilden dieselben Coverage-Typen ab

Wichtige Unterschiede zu den Quellprojekten:

- Validierung ist strenger als in frueheren Entwurfen
- GeoTIFF-Metadatenberechnung ist explizit auf die .NET-Utilities zugeschnitten

Offene Punkte:

- keine akuten Restpunkte fuer die bestehenden Coverage-Typen

## Phase 4: Interop (GeoJSON + WKT)

Status: Abgeschlossen.

Wichtige Portierungsentscheidungen:

- GeoJSON und WKT bleiben statische Builder mit klaren Einstiegspunkten
- `M`- und `ZM`-Dimensionen werden explizit mitgetragen
- GeoJSON-Ausgabe behandelt wiederholte Feature-Properties als Arrays

Wichtige Unterschiede zu den Quellprojekten:

- Die .NET-Portierung musste die Dimensionsbehandlung fuer `M` explizit haerten
- Die GeoJSON-Umsetzung folgt den Eigenheiten des .NET-Modells, insbesondere bei Property-Bags

Offene Punkte:

- keine akuten Restpunkte fuer GeoJSON und WKT

## Phase 5: OWS + WCS

Status: Abgeschlossen.

Wichtige Portierungsentscheidungen:

- OWS Exception Reports werden als eigenes Modell und Parser behandelt
- WCS 1.x und 2.x werden nicht ueber dieselbe Pseudo-Semantik verwischt
- Nur sicher unterstuetzte Requests werden erzeugt; unsaubere Cross-Version-Faelle werden nicht stillschweigend gebaut

Wichtige Unterschiede zu den Quellprojekten:

- Die .NET-Portierung grenzt ungueltige oder nicht sauber abgedeckte Requests klarer ab
- OWS 1.1 und 2.0 werden im Parser explizit beruecksichtigt

Offene Punkte:

- bei tieferen WCS-Erweiterungen zuerst reale Interoperabilitaetsziele festlegen, nicht nur Portierungsparitaet

## Phase 6: Streaming + I/O

Status: Abgeschlossen.

Wichtige Portierungsentscheidungen:

- Streaming nutzt `IAsyncEnumerable<GmlFeature>` als nativen .NET-Pfad
- Der Streaming-Parser kann von einem bereits positionierten `XmlReader` weiterarbeiten
- OWS-Fehler im HTTP-Streaming-Pfad werden frueh erkannt und nicht als leere Feature-Streams maskiert
- Transportfehler bleiben im I/O-Paket Exceptions, Parserfehler bleiben Result-Issues

Wichtige Unterschiede zu den Quellprojekten:

- Die .NET-Portierung nutzt Reader-gestuetztes Weiterparsen statt mehrere API-Schichten fuer denselben HTTP-Body
- Response-Disposal und Streaming-Abbruch wurden explizit testbar gemacht

Offene Punkte:

- keine akuten Restpunkte fuer den bestehenden Streaming- und I/O-Scope

## Phase 7: Erweiterte Builder

Status: Abgeschlossen im aktuell definierten Scope.

Wichtige Portierungsentscheidungen:

- `IGmlBuilder<TGeometry, TFeature, TCollection>` wurde als Erweiterungspunkt eingefuehrt
- `KmlBuilder` und `CsvBuilder` wurden umgesetzt
- Bestehende statische Builder wurden nicht zwanghaft auf ein einziges Interface umgebaut

Wichtige Unterschiede zu den Quellprojekten:

- Die .NET-Portierung priorisiert KML und CSV vor weiteren Ausgabeformaten
- Das Builder-Design bleibt pragmatisch und vermeidet eine zu breite gemeinsame Basisklasse im Alltagspfad

Offene Punkte:

- `CisJsonBuilder`
- `CoverageJsonBuilder`
- optionales CLI-Paket

Hier kann spaeter wieder konkreter Entwurfs- oder Beispielcode sinnvoll sein, wenn die Umsetzung dieser Restthemen startet.

## Sprach- und Plattformentscheidungen

### Immutabilitaet

- `sealed class` statt Dart-`final class`
- `init`-Properties statt mutierbarer DTOs
- `IReadOnlyList<T>` oder spezialisierte Value-Objekte statt frei beschreibbarer Collections

### Fehlerbehandlung

- erwartbare Parse-Probleme als `GmlParseIssue`
- I/O- und Transportfehler als `GmlIoException`
- keine parserweiten Kontrollfluesse ueber Exceptions

### Performance

- `readonly record struct` fuer Koordinaten
- `XmlReader` fuer grosses Feature-Streaming
- punktuelle .NET-spezifische Optimierungen nur dort, wo sie die API nicht verschlechtern

## Tests, Build und Release

Aktueller Stand:

- hoher Testabdeckungsgrad mit Coverage-Gate im Docker-Testpfad
- XML-Dokumentationskommentare fuer oeffentliche APIs sind Build-Voraussetzung
- zwei paketbezogene Publish-Workflows fuer `Gml4Net` und `Gml4Net.IO`

Das Detail dazu liegt bewusst nicht mehr hier, sondern in:

- `docs/architecture.md` fuer Architektur und API-Struktur
- `docs/roadmap.md` fuer Phasenstand, Checklisten und Meilensteine
- `README.md` fuer Build-, Docker- und Release-Pfade

## Was dieses Dokument bewusst nicht mehr enthaelt

- keinen ausfuehrlichen API-Pseudocode fuer bereits umgesetzte Phasen
- keine zweite, schnell veraltende Beschreibung des aktuellen Codes
- keine Build- oder Test-Checklisten, die bereits in der Roadmap gepflegt werden

Wenn neue, noch nicht implementierte Restthemen starten, kann hier wieder gezielt Entwurfs- oder Portierungscode fuer genau diese offenen Teile aufgenommen werden.
