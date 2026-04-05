# Streaming-Pfad fuer grosse GML-Dokumente

Status: Zielbild fuer die oeffentliche Streaming-API.

Hinweis: Ein Low-Level-Streaming-Pfad existiert bereits ueber
`GmlFeatureStreamParser`. Dieses Dokument beschreibt die gewuenschte
oeffentliche API darueber, nicht einen zweiten parallelen Parser-Stack.

Konsequenz: `StreamingGmlParser` ersetzt `GmlFeatureStreamParser` nicht.
`GmlFeatureStreamParser` bleibt der interne Low-Level-Baustein und wird fuer
den neuen Fehlervertrag gezielt erweitert.

Wichtig: Der aktuelle Typ `GmlFeatureStreamParser` ist heute bereits
oeffentlich. Falls er im Zuge dieses Designs auf `internal` umgestellt wird,
ist das ein Breaking Change und muss im Changelog sowie in der
Migrationskommunikation explizit benannt werden. Die neue oeffentliche
Streaming-API ist dann der vorgesehene Ersatzpfad.

## Ausgangslage

`GmlParser<TGeometry, TFeature, TCollection>` ist der Standard-Parser fuer
GML-Dokumente. Er laedt das komplette Dokument in den Speicher und
materialisiert das gesamte Ergebnis:

```csharp
var parser = GmlParser.Create(new GeoJsonBuilder());
var result = parser.Parse(xml);
```

Das ist fuer normale Dokumentgroessen sinnvoll. Fuer sehr grosse
WFS-/GML-Dokumente reicht der DOM-Pfad nicht, weil die komplette
FeatureCollection im Speicher aufgebaut wird.

Fuer diesen Fall gibt es bereits den internen Low-Level-Baustein
`GmlFeatureStreamParser`, der Features einzeln per `XmlReader` aus einem
`Stream` liefert. Was fehlt, ist eine oeffentliche, ergonomische API fuer
denselben Anwendungsfall.

## Ziel

Die oeffentliche Streaming-API soll sich an `s-gml` orientieren:

- eigener `StreamingGmlParser`
- nicht-generische oeffentliche Klasse
- feature-zentrierter Vertrag
- Callback-/Handler-basierte Verarbeitung pro Feature
- Fehlerbehandlung pro Feature
- optionale separate Batch-Convenience
- Rueckgabe eines kleinen Resultats mit Zaehlern

Wichtig: "aehnlich wie s-gml" bezieht sich auf die oeffentliche API und das
Nutzungsmodell, nicht auf die interne Implementierung. Anders als `s-gml`
muss gml4net kein String-Buffering mit Regex verwenden, sondern soll auf dem
vorhandenen `XmlReader`-basierten `GmlFeatureStreamParser` aufbauen.

## Warum nicht-generisch

Der oeffentliche `StreamingGmlParser` soll Features streamen, nicht den
kompletten generischen Builder-Vertrag exponieren.

Ein generischer Typ wie
`StreamingGmlParser<TGeometry, TFeature, TCollection>` waere fuer den
Streaming-Fall unnoetig breit:

- fuer Streaming wird fachlich nur "pro Feature verarbeiten" benoetigt
- `TGeometry` und `TCollection` sind auf der oeffentlichen API unnoetiger Ballast
- die API wuerde zu stark an `IBuilder` gekoppelt
- das wuerde das s-gml-aehnliche Nutzungsmodell verschlechtern

Deshalb ist der Parser selbst nicht-generisch. Falls ein Builder verwendet
werden soll, passiert das ueber separate Convenience-Overloads.

## Oeffentliche API

Namespace-/Ordner-Entscheidung:

- die oeffentlichen Streaming-Typen bleiben bewusst im flachen Namespace
  `Gml4Net.Parser`
- ihre Dateien duerfen trotzdem unter `src/Gml4Net/Parser/Streaming/` liegen,
  um den Streaming-Code physisch zu gruppieren
- die interne Low-Level-Implementierung bleibt dagegen unter
  `Gml4Net.Parser.Streaming`
- die uebliche 1:1-Konvention zwischen Ordner und Namespace wird hier
  absichtlich zugunsten einer flachen oeffentlichen API durchbrochen

### StreamingGmlParser

Siehe `src/Gml4Net/Parser/Streaming/StreamingGmlParser.cs`.

Vertragsregeln fuer `StreamingGmlParser`:

- die Callback-Registrierung folgt einem Setup-then-Run-Modell
- `ParseAsync(...)` ohne registriertes `OnFeature(...)` ist erlaubt; erfolgreiche
  Features werden dann nur gezaehlt, aber nicht weitergereicht
- `OnError(...)`, `OnEnd(...)` und `Progress` bleiben auch ohne
  `OnFeature(...)` aktiv
- `OnEnd(...)` ist fuer Abschlusslogik auf Aufruferseite gedacht, z. B. fuer
  einen letzten synchronen Buffer-Flush oder das Finalisieren lokaler
  Aggregation nach dem letzten Feature
- `OnEnd(...)` wird immer genau einmal aufgerufen, auch bei fatalem Abbruch
  oder Cancellation; der uebergebene `StreamingResult` enthaelt dann die
  Zaehler zum Zeitpunkt des Abbruchs
- `OnEnd(...)` liefert bewusst denselben `StreamingResult` wie der Rueckgabewert
  von `ParseAsync(...)`, damit Abschlusslogik ohne zusaetzlichen Zustand direkt
  auf den Endzaehlern arbeiten kann
- wenn der Abschluss selbst asynchron ist, soll dafuer nicht `OnEnd(...)`
  missbraucht werden; solche Faelle gehoeren in `ParseBatchesAsync(...)` oder
  in einen `IFeatureSink` mit `CompleteAsync(...)`
- eine `StreamingGmlParser`-Instanz ist fuer genau einen Parse-Lauf gedacht
- Mehrfachnutzung derselben Instanz ist nicht Teil des API-Vertrags und soll
  von der Implementierung nicht unterstuetzt werden

### StreamingParserOptions

Siehe `src/Gml4Net/Parser/Streaming/StreamingParserOptions.cs`.

### StreamingResult / StreamingProgress

Siehe `src/Gml4Net/Parser/Streaming/StreamingResult.cs`.

Zaehlersemantik:

- `FeaturesProcessed` zaehlt nur erfolgreich verarbeitete Features
- `FeaturesFailed` zaehlt nur fehlgeschlagene Features
- `FeaturesProcessed + FeaturesFailed` ergibt die Anzahl aller Feature-Ergebnisse,
  fuer die der Streaming-Pfad bereits einen Erfolg oder Fehler festgestellt hat
- `Progress` meldet genau diese kumulativen Zaehler nach jedem Feature-Ergebnis
  neu
- im Batch-Pfad bedeutet das: `Progress` feuert auch nach erfolgreich geparsten
  Features, die vor dem Flush nur als pending gepuffert werden; die gemeldeten
  Zaehler duerfen dabei zwischen zwei Flushes unveraendert bleiben
- ein fehlgeschlagenes Batch mit `N` Features erhoeht `FeaturesFailed` um `N`,
  aber nicht `FeaturesProcessed`
- im Batch-Pfad gelten erfolgreich geparste, aber noch nicht an `onBatch(...)`
  ausgelieferte Features bis zum Flush als pending und zaehlen noch in keinen
  der beiden Result-Zaehler
- wird ein solcher pending Batch spaeter erfolgreich geflusht, erhoehen diese
  Features `FeaturesProcessed`
- wird der Parse vorher durch einen fatalen Fehler oder durch `Stop` beendet,
  werden alle noch pending Features als verloren behandelt und zu
  `FeaturesFailed` geschlagen, weil sie nie ausgeliefert wurden

### StreamingError

Siehe `src/Gml4Net/Parser/Streaming/StreamingError.cs`.

## Builder-Integration

Der bestehende `IBuilder<TGeometry, TFeature, TCollection>` bleibt primaer der
Vertrag fuer den DOM-Pfad. Der Streaming-Parser selbst haengt nicht direkt an
diesem generischen Interface.

Die oeffentliche Convenience-API akzeptiert bestehende `IBuilder`-Implementierungen
direkt und reduziert sie intern auf den Feature-Fall:

Siehe `src/Gml4Net/Parser/Streaming/StreamingGml.cs`.

Ein moeglicher interner `FeatureBuilderAdapter` bleibt ein
Implementierungsdetail und muss nicht Teil der typischen Aufrufer-API sein.

Die Convenience-Methoden exponieren bewusst denselben Fehlerkanal wie
`StreamingGmlParser`: Mit `onError` koennen recoverable Einzel-Feature-Fehler
bei `Continue` inspiziert oder geloggt werden, ohne auf den direkten
`StreamingGmlParser` wechseln zu muessen.

Fuer Komponenten, die Features direkt schreiben oder persistieren, soll es
zusaetzlich einen eigenen Sink-Vertrag geben:

Siehe `src/Gml4Net/Interop/IFeatureSink.cs`.

Damit werden zwei Faelle klar getrennt, ohne die oeffentlichen Overloads auf
zwei verschiedene `StreamingGml`-Klassen in unterschiedlichen Namespaces zu
verteilen:

- `IBuilder<TGeometry, TFeature, TCollection>` fuer Transformation mit
  Rueckgabe an `onFeature` oder `onBatch`
- `IFeatureSink` fuer Komponenten, die den Output selbst
  weiterverarbeiten, z. B. DB-Insert oder Datei-Append

Lebenszyklus des Sink-Pfads:

- `WriteFeatureAsync(...)` wird fuer jedes erfolgreich geparste Feature genau
  einmal aufgerufen
- `CompleteAsync(...)` wird genau einmal nach dem letzten erfolgreichen
  Feature aufgerufen
- bei fatalem Abbruch oder Cancellation wird `CompleteAsync(...)` nicht
  aufgerufen
- Ownership von Connection/Transaction/Dispose bleibt beim aufrufenden Code

## Erweiterung von GmlFeatureStreamParser

Damit `StreamingGmlParser` bei `StreamingErrorBehavior.Continue` sauber auf
dem bestehenden Low-Level-Pfad aufbauen kann, muss
`GmlFeatureStreamParser` einen reichhaltigeren internen Vertrag bekommen.

Die bisherige API

```csharp
IAsyncEnumerable<GmlFeature> ParseAsync(Stream stream, CancellationToken ct = default)
```

reicht dafuer nicht aus, weil sie nur erfolgreiche Features liefern kann.
Fehler beim Lesen oder Parsen eines einzelnen Feature-Fragments gehen dabei
verloren oder brechen die Enumeration komplett ab.

Deshalb soll intern ein zweiter Pfad eingefuehrt werden:

Hinweis zur Sichtbarkeit: Die folgende Skizze zeigt das Zielbild mit internem
Low-Level-Vertrag. Gegenueber dem aktuellen Stand waere die Umstellung von
`public` auf `internal` bei `GmlFeatureStreamParser` ein Breaking Change.

Siehe `src/Gml4Net/Parser/Streaming/GmlFeatureStreamParser.cs` und
`src/Gml4Net/Parser/Streaming/FeatureStreamItem.cs`.

`ParseAsync(...)` bleibt als Convenience-API fuer erfolgreiche Features
erhalten. `StreamingGmlParser` verwendet dagegen `ParseItemsAsync(...)`.

### Semantik von FeatureStreamItem

- `Feature != null`: ein Feature wurde erfolgreich geparst
- `Issues.Count > 0` bei gleichzeitigem `Feature != null`: erfolgreiches
  Feature mit nicht-fataler Diagnostik
- `IsSuccess == true` bedeutet deshalb nur, dass ein Feature vorliegt; auch ein
  Feature mit nicht-fataler Diagnostik hat weiter `IsSuccess == true`
- `Feature == null` und (`Issues.Count > 0` oder `Exception != null`) und
  `CanContinue == true`: ein einzelnes Feature ist fehlgeschlagen, der Reader
  steht aber bereits am naechsten moeglichen Element und die Enumeration kann
  fortgesetzt werden
- `Feature == null` und (`Issues.Count > 0` oder `Exception != null`) und
  `CanContinue == false`: fataler Fehler, Enumeration muss beendet werden

### Recoverable vs. fatale Fehler

Nicht jeder Parse-Fehler ist recoverable. Das Dokument unterscheidet deshalb
bewusst zwei Klassen:

- recoverable Feature-Fehler
  - Fehler nach erfolgreicher Fragment-Isolation, z. B. in
    `FeatureParser.ParseFeature(...)`
  - Fehler im Feature-Handler oder im optionalen Builder-Adapter
  - diese koennen bei `Continue` als `StreamingError` an `OnError(...)`
    gemeldet werden
- fatale Stream-Fehler
  - XML-Fehler beim Lesen eines Feature-Fragments via `XNode.ReadFromAsync(...)`
  - kaputtes XML ausserhalb eines einzelnen Feature-Fragments
  - Reader verliert die Dokumentstruktur
  - Cancellation
  - I/O-Fehler des zugrunde liegenden Streams
  - diese brechen immer ab

### Konkrete Low-Level-Strategie

`GmlFeatureStreamParser` soll pro erkanntem Feature-Wrapper genau eine
isolierte Einheit verarbeiten:

1. Wrapper per `XmlReader` finden (`gml:featureMember`, `wfs:member`,
   `gml:featureMembers`-Kind)
2. einzelnes Feature-Fragment als `XElement` lesen
3. falls das XML-Lesen des Fragments fehlschlaegt: fatal abbrechen
4. `FeatureParser.ParseFeature(...)` auf dieses bereits materialisierte Fragment anwenden
5. Erfolg, Erfolg-mit-Issues oder Fehler als `FeatureStreamItem` zurueckgeben

Wichtig: Der `try/catch` fuer recoverable Fehler muss um die Verarbeitung
eines einzelnen bereits erfolgreich materialisierten Feature-Fragments liegen.
Dann ist der Reader nach diesem Fragment bereits auf dem naechsten Knoten
positioniert und die Enumeration kann fortgesetzt werden.

Fehler, die bereits beim Vorwaertslaufen des Readers oder beim XML-Lesen des
umgebenden Dokuments entstehen, koennen dagegen nicht verlaesslich einem
einzelnen Feature zugeordnet werden. Diese werden als `CanContinue == false`
behandelt oder direkt geworfen.

## Internes Zusammenspiel

```text
Stream (XML)
  |
  v
GmlFeatureStreamParser (bestehend)
  - liest per XmlReader forward-only
  - liefert intern FeatureStreamItem als IAsyncEnumerable
  - exponiert weiterhin ParseAsync(...) fuer Success-only Convenience
  |
  v
StreamingGmlParser (neu, oeffentliche API)
  - verwendet ParseItemsAsync(...)
  - wertet recoverable vs. fatale Fehler aus
  - ruft OnFeature(...) sofort pro Feature auf
  - meldet Fehler ueber `OnError(StreamingError)`
  - fuehrt Zaehler und Progress
  |
  v
StreamingResult

StreamingGml.ParseBatchesAsync(...) (optionale Convenience)
  - verwendet intern denselben Low-Level-Pfad
  - sammelt erfolgreiche Builder-Ergebnisse bis batchSize
  - ruft onBatch(...) pro Batch auf
  - leitet recoverable Fehler optional an `onError(StreamingError)` weiter
  - Progress bleibt pro Feature-Ergebnis definiert, nicht pro Flush
  - fuehrt dieselben Fehler- und Result-Zaehler

StreamingGml.ParseAsync(..., IFeatureSink, ...) (optionale Convenience)
  - verwendet intern denselben Low-Level-Pfad
  - ruft `WriteFeatureAsync(...)` pro erfolgreich geparstem Feature auf
  - leitet recoverable Fehler optional an `onError(StreamingError)` weiter
  - ruft `CompleteAsync(...)` genau einmal am erfolgreichen Ende auf
  - fuehrt dieselben Fehler- und Result-Zaehler
```

## Fehlerverhalten

Das Fehlerverhalten soll sich am s-gml-Nutzungsmodell orientieren:

- Fehler bei einem einzelnen Feature werden an `OnError(...)` gemeldet
- die Convenience-Overloads von `StreamingGml` bieten dafuer denselben Kanal
  ueber einen optionalen `onError`-Parameter
- bei `StreamingErrorBehavior.Continue` laeuft die Verarbeitung weiter
- bei `StreamingErrorBehavior.Stop` wird der Parse abgebrochen
- Fehler im Low-Level-Parser und Fehler im Feature-Handler laufen ueber
  denselben Mechanismus

`OnError(...)` erhaelt dabei strukturierte Diagnostik:

- `Issues` fuer parsernahe, nicht notwendig exceptionale Probleme
- `Exception` fuer Handler-, I/O- oder sonstige Laufzeitfehler
- `CanContinue` fuer die Fortsetzbarkeit
- optional `FeatureId`, falls bekannt

Wichtig: "Continue" gilt nur fuer recoverable Feature-Fehler. Fatale
Stream-/XML-Fehler brechen immer ab, auch wenn `ErrorBehavior == Continue`
gesetzt ist.

Fuer `ParseBatchesAsync(...)` gilt zusaetzlich:

- wirft `onBatch(...)` fuer einen Batch mit `N` Features eine Exception, gelten
  diese `N` Features als fehlgeschlagen
- diese `N` Features erhoehen `FeaturesFailed`, nicht `FeaturesProcessed`
- erfolgreiche Features werden ueber recoverable Einzel-Feature-Fehler hinweg
  weiter fuer den naechsten Batch gesammelt; ein Fehler flusht den aktuellen
  Batch nicht vorzeitig
- `Progress` bleibt auch hier pro Feature-Parse-Ergebnis definiert, nicht pro
  Flush; bei pending Erfolgen kann derselbe Zaehlerstand deshalb mehrfach
  hintereinander gemeldet werden
- bei `Continue` wird der Batch als verloren gezaehlt und mit dem naechsten
  Batch fortgesetzt
- bei `Stop` wird nach diesem fehlgeschlagenen Batch abgebrochen
- ein fehlgeschlagener Batch wird nicht automatisch wiederholt
- ein letzter partieller Batch mit `Count < batchSize` wird am erfolgreichen
  Ende immer genau einmal geflusht
- dieser End-Flush gilt auch dann, wenn zuvor einzelne Features fehlgeschlagen
  sind und `FeaturesFailed > 0` ist, solange kein fataler Abbruch oder `Stop`
  dazwischenliegt
- liegt bei fatalem Abbruch oder bei `Stop` noch ein ungeflushter Batch vor,
  wird er nicht mehr an `onBatch(...)` ausgeliefert; seine bereits gepufferten
  Features werden stattdessen als fehlgeschlagen gezaehlt

## Batching

Batching soll unterstuetzt werden, aber nicht ueber die Semantik von
`StreamingGmlParser.OnFeature(...)` versteckt werden.

Deshalb gilt:

- `StreamingGmlParser.OnFeature(...)` wird immer sofort pro Feature aufgerufen
- `StreamingParserOptions` enthaelt kein `BatchSize`
- Batching ist eine separate Convenience-API

Vorgeschlagener Batch-Pfad:

```csharp
await StreamingGml.ParseBatchesAsync(
    input,
    new GeoJsonBuilder(),
    batch => SaveBatchAsync(batch),
    batchSize: 100);
```

Der Zweck ist derselbe:

- weniger Callback-Overhead
- bessere Integration fuer DB-Inserts und Bulk-Writes
- regelmaessige Progress-Meldungen

Der Vorteil dieser Trennung ist ein klarer API-Vertrag:

- `OnFeature(...)` bedeutet geringe Latenz und eindeutiges Fehler-Timing
- `ParseBatchesAsync(...)` bedeutet bewusst gepufferte Verarbeitung
- bei 150 erfolgreichen Features und `batchSize: 100` wird `onBatch(...)`
  deterministisch zweimal aufgerufen: einmal mit 100 und einmal mit 50
- recoverable Fehler zwischen erfolgreichen Features veraendern diese
  Flush-Regel nicht; sie erhoehen nur die Fehlerzaehler und werden ueber
  `onError` gemeldet

Unterschied zu `s-gml`: Es gibt weiterhin keine oeffentliche
`maxBufferSize`-Option. Dieses Detail stammt dort aus der
String-/Chunk-Implementierung. gml4net soll intern `XmlReader` und
`IAsyncEnumerable<GmlFeature>` nutzen; dafuer ist eine solche Option auf
API-Ebene nicht noetig.

Das Speicherprofil bleibt dabei "konstant pro Feature", nicht "konstant pro
beliebig grossem XML-Fragment". Ein einzelnes sehr grosses Feature kann weiter
mehr Speicher benoetigen, weil es vor dem Parsen als Fragment materialisiert
wird.

## Nutzung

### Rohes Feature-Streaming

```csharp
var parser = new StreamingGmlParser();

parser.OnFeature(feature =>
{
    Console.WriteLine(feature.Id);
    return ValueTask.CompletedTask;
});

parser.OnError(error =>
{
    Console.Error.WriteLine(error.Exception?.Message ?? "Parse issue");
});

var result = await parser.ParseAsync(input);
// result.FeaturesProcessed == 42000
```

### Mit Builder

```csharp
var result = await StreamingGml.ParseAsync(
    input,
    new GeoJsonBuilder(),
    feature =>
    {
        Console.WriteLine(feature["id"]);
        return ValueTask.CompletedTask;
    },
    onError: error => Log.Warning(error.Exception, "Feature skipped"));
```

### Batch-Verarbeitung

```csharp
var result = await StreamingGml.ParseBatchesAsync(
    input,
    new GeoJsonBuilder(),
    batch =>
    {
        Console.WriteLine($"Batch mit {batch.Count} Features");
        return ValueTask.CompletedTask;
    },
    batchSize: 100,
    onError: error => Log.Warning(error.Exception, "Batch input contained invalid feature"));
```

### Mit schreibendem Sink

```csharp
await using var connection = await dataSource.OpenConnectionAsync(ct);

var result = await StreamingGml.ParseAsync(
    input,
    new PostGisSink(connection),
    onError: error => Log.Warning(error.Exception, "Feature skipped before sink write"),
    options: new StreamingParserOptions
    {
        ErrorBehavior = StreamingErrorBehavior.Continue
    },
    ct: ct);
```

In diesem Fall entscheidet der konkrete Sink selbst, wie ein Feature
weiterverarbeitet wird, z. B. per Insert oder Upsert in PostGIS. Es gibt dann
kein zusaetzliches `onFeature`-Callback auf der API.

### Mit Fehlertoleranz

```csharp
var parser = new StreamingGmlParser(
    new StreamingParserOptions
    {
        ErrorBehavior = StreamingErrorBehavior.Continue
    });

parser.OnFeature(feature => InsertIntoDatabaseAsync(feature));
parser.OnError(error => Log.Warning(error, "Feature skipped"));

var result = await parser.ParseAsync(input);
// result.FeaturesProcessed == 41998
// result.FeaturesFailed == 2
```

## Vergleich

| Aspekt | GmlParser<,,> | StreamingGmlParser |
|---|---|---|
| API-Form | generisch | nicht-generisch |
| Eingabe | string, byte[], Stream | Stream |
| Verarbeitung | DOM, alles im Speicher | forward-only, pro Feature |
| Rueckgabe | `GmlBuildResult` | `StreamingResult` |
| Builder/Sink | direkter Teil des Parser-Typs | optional ueber Convenience-Overloads oder `IFeatureSink` |
| Root-Typen | Geometry, Feature, Collection, Coverage | FeatureCollection |
| Fehler | Issues im Result | Error-Callback plus Result-Zaehler |
| Fortschritt | - | `IProgress<StreamingProgress>` |
| Basis | `GmlParser.Parse...()` | `GmlFeatureStreamParser.ParseItemsAsync()` intern, `ParseAsync()` als Success-only Convenience |

## Ziel-Dateien

Hinweis zur Ablage: Die folgenden Pfade unter `Parser/Streaming/` bedeuten
nicht, dass die oeffentlichen Typen auch im Namespace
`Gml4Net.Parser.Streaming` liegen sollen. Oeffentliche Typen bleiben in
`Gml4Net.Parser`; nur die internen Low-Level-Typen liegen in
`Gml4Net.Parser.Streaming`.

| Datei | Rolle |
|---|---|
| `src/Gml4Net/Parser/Streaming/GmlFeatureStreamParser.cs` | erweiteter Low-Level-Streaming-Pfad mit Item-basiertem Fehlervertrag |
| `src/Gml4Net/Parser/Streaming/StreamingGmlParser.cs` | oeffentliche Streaming-API |
| `src/Gml4Net/Parser/Streaming/StreamingParserOptions.cs` | Optionen und Enums |
| `src/Gml4Net/Parser/Streaming/StreamingError.cs` | strukturierter Fehlertransport fuer Callbacks |
| `src/Gml4Net/Parser/Streaming/StreamingResult.cs` | Zaehler und Progress-Typen |
| `src/Gml4Net/Interop/IFeatureSink.cs` | Vertrag fuer schreibende Streaming-Sinks |
| `tests/Gml4Net.Tests/Streaming/StreamingGmlParserTests.cs` | API- und Fehlerfall-Tests |

## Testfaelle

Mindestens diese Faelle muessen abgedeckt sein:

- streamt `wfs:member`-Features einzeln
- streamt `gml:featureMember`-Features einzeln
- streamt `gml:featureMembers`
- ruft `OnFeature` in stabiler Reihenfolge auf
- ruft `OnFeature` ohne versteckte Batch-Verzoegerung auf
- `ParseAsync(...)` ohne registriertes `OnFeature(...)` zaehlt erfolgreiche
  Features, ohne sie weiterzureichen
- `OnEnd(...)` wird immer genau einmal aufgerufen, auch bei fatalem Abbruch
  oder Cancellation
- `OnEnd(...)` erhaelt denselben `StreamingResult` wie der Rueckgabewert von
  `ParseAsync(...)`
- `IProgress<StreamingProgress>` meldet nach jedem Feature-Ergebnis die
  kumulativen Zaehler
- `IProgress<StreamingProgress>` darf im Batch-Pfad zwischen zwei Flushes
  mehrfach denselben Zaehlerstand melden
- `ParseBatchesAsync(...)` flusht deterministisch bei `batchSize`
- `ParseBatchesAsync(...)` flusht am erfolgreichen Ende auch einen letzten
  partiellen Batch mit `Count < batchSize`
- `ParseBatchesAsync(...)` behaelt erfolgreiche Features ueber recoverable
  Einzel-Feature-Fehler hinweg im laufenden Batch
- `ParseBatchesAsync(...)` zaehlt einen ungeflushter Rest-Batch bei fatalem
  Abbruch oder `Stop` nicht still weg, sondern als fehlgeschlagen
- `ParseBatchesAsync(...)` zaehlt bei fatalem Abbruch zuvor erfolgreich
  gepufferte, aber noch nicht geflushte Features als fehlgeschlagen
- liefert recoverable Parse-Fehler als `FeatureStreamItem` mit Issues und `CanContinue == true`
- transportiert parse-nahe Warnungen verlustfrei ueber `StreamingError`
- liefert fatale Stream-Fehler nicht faelschlich als recoverable Feature-Fehler
- meldet Handler-Fehler ueber `OnError`
- meldet recoverable Fehler in `StreamingGml.ParseAsync(...)` ueber den
  optionalen `onError`-Parameter
- meldet recoverable Fehler in `StreamingGml.ParseBatchesAsync(...)` ueber den
  optionalen `onError`-Parameter
- meldet recoverable Fehler in `StreamingGml.ParseAsync(..., IFeatureSink, ...)`
  ueber den optionalen `onError`-Parameter
- kann bei `Continue` nach einem Feature-Fehler weitermachen
- bricht bei `Stop` deterministisch ab
- liefert korrekte `FeaturesProcessed`-/`FeaturesFailed`-Zaehler
- respektiert Cancellation
- `StreamingGml.ParseAsync(...)` und `ParseBatchesAsync(...)` leiten `BuildFeature(...)` korrekt weiter
- `StreamingGml.ParseAsync(..., IFeatureSink, ...)` ruft `WriteFeatureAsync(...)` pro Feature auf
- `StreamingGml.ParseAsync(..., IFeatureSink, ...)` ruft `CompleteAsync(...)` genau einmal am erfolgreichen Ende auf
