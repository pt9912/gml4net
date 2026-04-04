# Parser Event Stream fuer professionelle Grossdaten-Pipelines

## Ausgangslage

Die aktuelle `gml4net`-Architektur ist modellzentriert:

- XML wird geparst
- daraus wird das interne GML-Modell aufgebaut
- anschliessend arbeiten Builder auf diesem Modell

Das ist fuer API-Klarheit, Tests, Debugging und normale Dokumentgroessen sinnvoll. Fuer professionelle Produktionsumgebungen mit sehr grossen WFS-/GML-Dokumenten reicht dieser Pfad allein aber nicht.

Probleme bei grossen Datenmengen:

- unnoetig hoher Peak-Memory
- viele kurzlebige Allokationen
- hoher GC-Druck
- spaete erste Ausgabe, weil erst materialisiert und dann gebaut wird
- schwache End-to-End-Latenz bei Transformationspipelines

## Was `s-gml` heute macht

`s-gml` koppelt Builder und Parser enger als `gml4net`:

- der Parser haelt eine Builder-Instanz
- das Parser-Ergebnis wird direkt ueber `buildFeature`, `buildFeatureCollection` oder Geometrie-Builder in das Zielformat ueberfuehrt

Wichtig ist aber:

- `s-gml` baut weiterhin interne GML-Objekte pro Geometrie, Feature oder Collection auf
- der Streaming-Pfad verarbeitet Features inkrementell, aber nicht ereignisbasiert bis in den Writer hinein
- der Gewinn liegt dort vor allem darin, nicht die komplette FeatureCollection gleichzeitig zu materialisieren

Das ist besser als ein rein sammelnder Modellpfad, aber noch nicht das Ende der Optimierung.

## Ziel fuer `gml4net`

`gml4net` sollte zwei gleichberechtigte Pfade haben:

- Modellpfad
  Fuer klassische Bibliotheksnutzung, Debugging, zufaelligen Zugriff, Tests und allgemeine Interop
- Event-Stream-Pfad
  Fuer Produktionssysteme, sehr grosse Dokumente, inkrementelle Transformation und minimalen Speicherverbrauch

Der Event-Stream-Pfad ist kein Ersatz fuer das Modell. Er ist eine zweite Architekturspur fuer andere Betriebsanforderungen.

## Verhaeltnis zum bestehenden Streaming-Parser

`GmlFeatureStreamParser` bietet bereits einen Pull-basierten Streaming-Pfad
ueber `IAsyncEnumerable<GmlFeature>`. Der Konsument zieht Features einzeln aus
dem Stream:

```csharp
await foreach (var feature in GmlFeatureStreamParser.ParseAsync(stream, ct))
{
    // feature-weise Verarbeitung
}
```

Der hier beschriebene Event-Stream-Pfad ergaenzt dieses Pull-Modell um ein
Push-Modell (Sink/Writer), das besser geeignet ist, wenn:

- der Writer Start/Ende-Klammern kontrollieren muss (JSON `[`/`]`, KML `<Document>`)
- der Output direkt in einen `Stream`/`PipeWriter` geschrieben werden soll
- der Konsument keinen eigenen Iterationsloop verwalten will

Stufe 1 baut direkt auf `GmlFeatureStreamParser` auf und nutzt dessen
`XmlReader`-basierte Forward-Only-Iteration als Grundlage.

## Architekturidee

Der Parser erzeugt nicht nur Modellobjekte, sondern kann optional Parser-Ereignisse an einen Sink oder Writer liefern.

Zwei Ebenen sind sinnvoll:

### 1. Feature-Sink auf Modellniveau

Dies ist der kleinere Einstieg. Der Parser liefert pro erkanntem Feature ein bereits gebautes `GmlFeature`, aber nie eine komplette Collection.

Beispiel:

```csharp
public interface IGmlFeatureSink
{
    ValueTask OnStartDocumentAsync(CancellationToken ct = default);
    ValueTask OnStartCollectionAsync(CancellationToken ct = default);
    ValueTask OnFeatureAsync(GmlFeature feature, CancellationToken ct = default);
    ValueTask OnEndCollectionAsync(CancellationToken ct = default);
    ValueTask OnEndDocumentAsync(CancellationToken ct = default);
}
```

Vorteil:

- geringe Eingriffstiefe
- Wiederverwendung des bestehenden Feature-Parsers
- klarer Gewinn gegenueber kompletter Collection-Materialisierung

Nachteil:

- pro Feature weiterhin Objektmaterialisierung
- noch nicht optimal fuer maximale Performance

### 2. Echte Event-Writer unterhalb des Modells

Dies ist der eigentliche Produktionspfad. Der Parser sendet strukturierte
Ereignisse direkt an einen Writer. Im Gegensatz zum Feature-Sink liefert
dieser Pfad keine fertigen `GmlGeometry`- oder `GmlFeature`-Objekte, sondern
primitive Daten (Typnamen, Koordinaten, Property-Werte):

Beispiel:

```csharp
public interface IGmlEventSink
{
    ValueTask OnStartDocumentAsync(GmlVersion version, CancellationToken ct = default);
    ValueTask OnStartFeatureCollectionAsync(string? id, CancellationToken ct = default);
    ValueTask OnStartFeatureAsync(string typeName, string? id, CancellationToken ct = default);
    ValueTask OnStartGeometryAsync(string geometryType, string? srsName, CancellationToken ct = default);
    ValueTask OnCoordinatesAsync(ReadOnlyMemory<GmlCoordinate> coordinates, CancellationToken ct = default);
    ValueTask OnEndGeometryAsync(CancellationToken ct = default);
    ValueTask OnPropertyAsync(string name, GmlPropertyValue value, CancellationToken ct = default);
    ValueTask OnEndFeatureAsync(CancellationToken ct = default);
    ValueTask OnEndFeatureCollectionAsync(CancellationToken ct = default);
    ValueTask OnEndDocumentAsync(CancellationToken ct = default);
}
```

Unterschied zum Feature-Sink: Geometrien werden hier nicht als `GmlGeometry`
materialisiert sondern als Typ+Koordinaten-Events geliefert. Property-Werte
verwenden die bestehende `GmlPropertyValue`-Hierarchie fuer Typsicherheit.

## Empfohlene Einfuehrungsstrategie

Nicht alles gleichzeitig umbauen.

### Stufe 1

`GmlFeatureStreamParser` wird um einen Sink-Pfad erweitert. Um
Namenskollisionen mit dem bestehenden `ProcessFeaturesAsync(Stream,
Func<GmlFeature, Task>)` zu vermeiden, erhaelt die Sink-Variante einen
eigenen Methodennamen:

```csharp
public static Task StreamToSinkAsync(
    Stream stream,
    IGmlFeatureSink sink,
    CancellationToken ct = default);
```

Ziel:

- pro Feature verarbeiten
- keine komplette Collection materialisieren
- sofort fuer DB-Writer, CSV-Writer und GeoJSON-FeatureCollection nutzbar

### Stufe 2

Spezielle Streaming-Writer einfuehren:

- `GeoJsonFeatureCollectionWriter`
- `CsvFeatureWriter`
- `KmlFeatureWriter`

Diese Writer implementieren `IGmlFeatureWriter` und schreiben direkt in
einen `Stream`, `PipeWriter` oder `Utf8JsonWriter`.

```csharp
public interface IGmlFeatureWriter
{
    ValueTask WriteStartAsync(CancellationToken ct = default);
    ValueTask WriteFeatureAsync(GmlFeature feature, CancellationToken ct = default);
    ValueTask WriteEndAsync(CancellationToken ct = default);
}
```

Ein Transformer verbindet den Streaming-Parser mit einem Writer:

```csharp
public static class GmlFeatureStreamTransformer
{
    public static async Task TransformAsync(
        Stream input,
        IGmlFeatureWriter writer,
        CancellationToken ct = default);
}
```

Beispielnutzung:

```csharp
await using var input = File.OpenRead("large.gml");
await using var output = File.Create("large.geojson");

var writer = new GeoJsonFeatureCollectionWriter(output);
await GmlFeatureStreamTransformer.TransformAsync(input, writer);
```

### Stufe 3

Falls Messungen zeigen, dass selbst `GmlFeature` noch zu teuer ist:

- Feature- und Property-Ereignisse unterhalb des Modells einfuehren via `IGmlEventSink`
- direkte Writer fuer JSON/XML/CSV auf Parser-Ebene

Das ist die teuerste Ausbaustufe und sollte benchmark-getrieben kommen.

## Geeignete erste Zieltypen

Nicht jedes Zielformat profitiert gleich stark.

Besonders geeignet:

- CSV
  Zeilenweise Ausgabe, nahezu ideal fuer Streaming
- GeoJSON FeatureCollection
  Mit `Utf8JsonWriter` sehr gut inkrementell schreibbar
- KML Document/Placemark
  XML-Ausgabe laesst sich inkrementell schreiben

Weniger dringlich:

- WKT einzelner Geometrien
  dort ist der Modellpfad oft ausreichend
- Coverage-orientierte Builder
  dort haengt der Nutzen staerker vom konkreten Strukturmodell ab

## Designregeln

Damit der Event-Stream-Pfad professionell tragfaehig bleibt, sollten diese Regeln gelten:

- kein Bruch des bestehenden Modellpfads
- keine Vermischung von Modell-API und Streaming-Writer-API in einer ueberladenen Allzweckklasse
- klare Ownership fuer Start/Ende des Outputs
- Writer muessen Abbruch, Exceptions und teilweises Schreiben sauber behandeln
- `CancellationToken` muss durchgaengig unterstuetzt werden
- I/O-Pfade duerfen Ressourcen auch bei fruehem Abbruch sauber freigeben

## Empfohlene .NET-Technik

Fuer einen professionellen Event-Stream-Pfad sind diese Werkzeuge passend:

- `XmlReader` fuer forward-only Parsing (bereits in `GmlFeatureStreamParser` verwendet)
- `IAsyncEnumerable<T>` fuer Pull-basiertes Streaming (bereits implementiert)
- `IGmlFeatureSink` / `IGmlFeatureWriter` fuer Push-basiertes Streaming (neu)
- `Utf8JsonWriter` fuer GeoJSON
- `StreamWriter` oder `IBufferWriter<byte>` fuer CSV/KML
- `PipeWriter` optional fuer hochperformante Server-Pipelines
- `ValueTask` statt `Task` in heissen Writer-Pfaden

## Fehlerbehandlung

Parserfehler und Transportfehler bleiben getrennt:

- Parse-Probleme weiter als `GmlParseIssue` oder writerseitige Fehlerobjekte
- I/O-/Netzwerkfehler weiter als `GmlIoException`
- Writer duerfen bei schwerem Strukturfehler abbrechen, aber nicht stillschweigend Teilausgaben als erfolgreich markieren

Fuer den Event-Stream-Pfad kann zusaetzlich ein konfigurierbares Fehlerverhalten sinnvoll sein:

```csharp
public enum StreamErrorBehavior
{
    StopOnFirstError,
    ContinueWithWarnings,
    SkipMalformedFeature
}
```

## Abgrenzung zum bestehenden `IGmlBuilder`

`IGmlBuilder<TGeometry, TFeature, TCollection>` bleibt sinnvoll fuer den modellzentrierten Pfad.

Aber:

- es ist kein vollwertiger Streaming-Writer-Vertrag
- es kontrolliert Start/Ende und Output-Lebenszyklus nicht sauber
- es ist zu objektzentriert fuer maximale Speicher- und Latenzoptimierung

Darum sollte `IGmlBuilder` nicht ueberladen werden, um Streaming-Probleme zu loesen. Besser ist ein zweiter, expliziter Writer-/Sink-Vertrag.

## Messkriterien

Der Event-Stream-Pfad sollte nicht nur "gefuehlt schneller" sein. Er braucht klare Zielmetriken:

- Peak-Memory gegenueber dem bestehenden Modellpfad
- Allokationen pro 10.000 Features
- End-to-End-Latenz bis zum ersten geschriebenen Output
- Gesamtdurchsatz
- Verhalten bei Abbruch und Fehlern

Empfehlung:

- BenchmarkDotNet fuer synthetische Last
- mindestens ein grosser realer WFS-/GML-Fixture
- Vergleich:
  - Modellpfad (DOM)
  - Pull-Streaming (`IAsyncEnumerable<GmlFeature>`)
  - Push-Streaming (Feature-Sink/Writer)
  - spaeter echter Event-Pfad (Stufe 3)

## Fazit

Wenn `gml4net` in professionellen Umgebungen mit grossen Datenmengen eingesetzt werden soll, braucht es neben dem bestehenden Modellpfad einen expliziten Streaming-Transform-Pfad.

`s-gml` ist dafuer ein sinnvoller Hinweis, aber kein Endzustand:

- dort sitzt der Builder naeher am Parser
- trotzdem werden pro Feature noch Zwischenobjekte materialisiert

Die sinnvolle Weiterentwicklung fuer `gml4net` ist daher:

1. modellzentrierten Pfad behalten
2. inkrementellen Feature-Writer-Pfad einfuehren (`IGmlFeatureWriter` + `GmlFeatureStreamTransformer`)
3. erst benchmark-getrieben entscheiden, ob ein noch tieferer Event-Pfad (`IGmlEventSink`) noetig ist
