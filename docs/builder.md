# Builder-Parser-Kopplung

## Motivation

Aktuell sind Parser und Builder in gml4net entkoppelt — Parsing und Konvertierung
sind zwei getrennte Schritte:

```csharp
// Schritt 1: Parsen
var result = GmlParser.ParseXmlString(xml);

// Schritt 2: Manuell konvertieren
var json = GeoJsonBuilder.Document(result.Document!);
```

In s-gml (TypeScript) sind Parser und Builder gekoppelt — ein Schritt:

```typescript
const parser = new GmlParser('kml');
const result = await parser.parse(xml);
```

Ziel: gml4net soll dieselbe Ein-Schritt-Erfahrung bieten, ohne die bestehende
entkoppelte API zu brechen.

---

## Breaking Changes

### 1. IGmlBuilder → IBuilder

`IGmlBuilder` suggeriert, dass GML gebaut wird. Tatsaechlich konvertiert das
Interface von GML weg in ein Zielformat. `IBuilder` ist naeher an s-gml (dort
heisst das Interface `Builder`) und der `Gml4Net.Interop`-Namespace liefert
den Kontext bereits.

```csharp
// Vorher
public interface IGmlBuilder<TGeometry, TFeature, TCollection>

// Nachher
public interface IBuilder<TGeometry, TFeature, TCollection>
```

Alle Referenzen im Code werden umbenannt. Die Implementierungen behalten ihre
Namen (`GeoJsonBuilder`, `KmlBuilder`, `WktBuilder`, `CsvBuilder`).

### 2. BuildCoverage: object? → TFeature?

In s-gml geben alle 4 Coverage-Methoden `TFeature` zurueck — ein Coverage wird
wie ein Feature behandelt. gml4net hat eine generische `BuildCoverage()`-Methode
mit `object?`-Rueckgabe, die bei allen Buildern `null` zurueckgibt.

Aenderung zu `TFeature?` macht den Rueckgabetyp konsistent mit s-gml und
ermoeglicht ein voll typisiertes `GmlBuildResult`:

```csharp
// Vorher
object? BuildCoverage(GmlCoverage coverage);

// Nachher
TFeature? BuildCoverage(GmlCoverage coverage);
```

Alle bestehenden Implementierungen geben `null` zurueck — die Aenderung erfordert
nur eine Signaturanpassung, keine Logik-Aenderung.

---

## API-Design

### Neuer generischer GmlParser

Analog zu s-gml's `new GmlParser('kml')` wird ein generischer, nicht-statischer
`GmlParser<TGeometry, TFeature, TCollection>` eingefuehrt. In C# koennen der
bestehende statische `GmlParser` und der neue generische `GmlParser<,,>` im
selben Namespace koexistieren (unterschiedliche generische Aritaet = unterschiedliche
Typen).

```csharp
namespace Gml4Net.Parser;

public class GmlParser<TGeometry, TFeature, TCollection>
{
    private readonly IBuilder<TGeometry, TFeature, TCollection> _builder;

    public GmlParser(IBuilder<TGeometry, TFeature, TCollection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
    }

    public GmlBuildResult<TGeometry, TFeature, TCollection> Parse(string xml);
    public GmlBuildResult<TGeometry, TFeature, TCollection> Parse(ReadOnlySpan<byte> bytes);
    public GmlBuildResult<TGeometry, TFeature, TCollection> Parse(Stream stream);
}
```

### Factory-Methode fuer Typ-Inferenz

Der direkte Konstruktoraufruf erfordert alle drei Typparameter explizit:

```csharp
var parser = new GmlParser<JsonObject, JsonObject, JsonObject>(GeoJsonBuilder.Instance);
```

Eine Factory-Methode auf dem statischen `GmlParser` nutzt C#-Typ-Inferenz —
die Generics werden automatisch vom Builder abgeleitet:

```csharp
// C#-Aequivalent zu: new GmlParser('geojson')
var parser = GmlParser.Create(GeoJsonBuilder.Instance);
```

```csharp
// In der bestehenden statischen Klasse
public static class GmlParser
{
    // ... bestehende Methoden ...

    public static GmlParser<TGeometry, TFeature, TCollection>
        Create<TGeometry, TFeature, TCollection>(
            IBuilder<TGeometry, TFeature, TCollection> builder)
        => new(builder);
}
```

### Bestehende API bleibt unveraendert

```csharp
// Funktioniert weiterhin identisch
var result = GmlParser.ParseXmlString(xml);
var json = GeoJsonBuilder.Document(result.Document!);
```

---

## Neue Typen

### GmlBuildResult<TGeometry, TFeature, TCollection>

Ergebnistyp fuer die gekoppelte Parse+Convert-Operation. Enthaelt das konvertierte
Ergebnis sowie die Parse-Diagnostics.

```csharp
namespace Gml4Net.Model;

public sealed class GmlBuildResult<TGeometry, TFeature, TCollection>
{
    /// <summary>Gesetzt, wenn Root eine Geometrie ist.</summary>
    public TGeometry? Geometry { get; init; }

    /// <summary>Gesetzt, wenn Root ein Feature ist.</summary>
    public TFeature? Feature { get; init; }

    /// <summary>Gesetzt, wenn Root eine FeatureCollection ist.</summary>
    public TCollection? Collection { get; init; }

    /// <summary>Gesetzt, wenn Root ein Coverage ist.</summary>
    public TFeature? Coverage { get; init; }

    /// <summary>Das geparste Dokument (fuer Metadaten wie Version, BoundedBy).</summary>
    public GmlDocument? Document { get; init; }

    /// <summary>Diagnostische Issues aus dem Parsing.</summary>
    public IReadOnlyList<GmlParseIssue> Issues { get; init; } = [];

    /// <summary>True wenn ein Error-Issue vorliegt.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == GmlIssueSeverity.Error);
}
```

**Warum separate Properties statt einem einzelnen `object? Root`?**

Bei Buildern wie `GeoJsonBuilder` sind alle drei Typparameter `JsonObject`.
Ein einzelnes `Root`-Property wuerde nicht verraten, ob es sich um eine Geometrie,
ein Feature oder eine FeatureCollection handelt. Die separaten Properties machen
den Root-Typ ueber die Property-Belegung eindeutig:

```csharp
var result = parser.Parse(xml);

if (result.Collection is { } fc)
    Console.WriteLine($"FeatureCollection: {fc}");
else if (result.Feature is { } f)
    Console.WriteLine($"Feature: {f}");
else if (result.Geometry is { } g)
    Console.WriteLine($"Geometry: {g}");
```

### BuilderExtensions

Die Dispatch-Logik (welche Builder-Methode fuer welchen Geometrietyp?) ist als
generische Extension-Method `BuildGeometry()` auf `IBuilder` zentralisiert.
Siehe `src/Gml4Net/Interop/BuilderExtensions.cs`.

---

## Nutzungsbeispiele

### FeatureCollection parsen und direkt als GeoJSON erhalten

```csharp
var parser = GmlParser.Create(GeoJsonBuilder.Instance);
var result = parser.Parse(wfsXml);

if (result.HasErrors)
{
    foreach (var issue in result.Issues)
        Console.WriteLine($"{issue.Severity}: {issue.Message}");
    return;
}

JsonObject featureCollection = result.Collection!;
Console.WriteLine(featureCollection.ToJsonString());
```

### Geometrie parsen und als WKT erhalten

```csharp
var parser = GmlParser.Create(WktBuilder.Instance);
var result = parser.Parse(gmlPoint);
string wkt = result.Geometry!;
// "POINT (8.5 47.3)"
```

### Direkt als KML konvertieren

```csharp
var parser = GmlParser.Create(KmlBuilder.Instance);
var result = parser.Parse(gmlFeature);
XElement kmlPlacemark = result.Feature!;
```

### Parser wiederverwenden

Der generische Parser ist zustandslos und kann beliebig oft wiederverwendet werden:

```csharp
var parser = GmlParser.Create(GeoJsonBuilder.Instance);

foreach (var xml in gmlDocuments)
{
    var result = parser.Parse(xml);
    // ...
}
```

### Extension-Method eigenstaendig nutzen

Die `BuildGeometry`-Extension ist auch ohne den generischen Parser nutzbar:

```csharp
var point = new GmlPoint { Coordinate = new GmlCoordinate(8.5, 47.3) };
IBuilder<JsonObject, JsonObject, JsonObject> builder = GeoJsonBuilder.Instance;
JsonObject? json = builder.BuildGeometry(point);
```

### Entkoppelter Pfad (unveraendert)

```csharp
var parseResult = GmlParser.ParseXmlString(xml);
var document = parseResult.Document!;

// Einmal parsen, mehrfach konvertieren
var json = GeoJsonBuilder.Document(document);
var kml = KmlBuilder.Geometry((GmlGeometry)document.Root);
```

---

## Vergleich s-gml ↔ gml4net (nachher)

| Aspekt | s-gml | gml4net |
|---|---|---|
| Parser erzeugen | `new GmlParser('kml')` | `GmlParser.Create(KmlBuilder.Instance)` |
| Parsen | `await parser.parse(xml)` | `parser.Parse(xml)` |
| Interface | `Builder<TGeometry, TFeature, TCollection>` | `IBuilder<TGeometry, TFeature, TCollection>` |
| Entkoppelter Pfad | nicht vorhanden | `GmlParser.ParseXmlString()` + Builder separat |
| Parser-Wiederverwendung | ja | ja |

---

## Dateien

Siehe `src/Gml4Net/Interop/` fuer `IBuilder`, `BuilderExtensions` und die
Builder-Implementierungen. Siehe `src/Gml4Net/Parser/GmlParser{TGeometry,TFeature,TCollection}.cs`
fuer den generischen Parser und `src/Gml4Net/Model/GmlBuildResult.cs` fuer den Ergebnistyp.
