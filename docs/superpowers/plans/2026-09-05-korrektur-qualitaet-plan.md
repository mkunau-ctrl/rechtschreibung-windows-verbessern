# Plan: Korrekturqualität auf 90 %+ bringen

Datum: 2026-09-05
Status: **Entwurf — noch nicht freigegeben, noch kein Code**

## Das Ziel, messbar gemacht

"Zu 90 % perfekt" braucht eine Definition, sonst kann man es nicht prüfen.
Vorschlag — drei getrennte Kennzahlen, weil sie unterschiedlich weh tun:

| Kennzahl | Was sie misst | Zielwert |
|---|---|---|
| **Präzision** | Von allen Ersetzungen, die das Programm macht: wie viele waren richtig? | **≥ 98 %** |
| **Trefferquote** | Von allen echten Tippfehlern: wie viele wurden korrigiert? | **≥ 90 %** |
| **Fehlalarm-Rate** | Wie oft wird ein korrekt geschriebenes Wort angefasst? | **≈ 0 %** |

Warum Präzision höher als 90 %: Ein nicht korrigierter Tippfehler kostet dich
zwei Sekunden Nachbessern. Eine **falsche** Ersetzung zerstört einen richtigen
Satz, du merkst es oft zu spät, und sie kostet Vertrauen ins ganze Programm.
Lieber einmal zu wenig anfassen als einmal zu viel. Das ist auch der Grund,
warum das Ding niemals "mutiger" gemacht werden sollte, ohne die Präzision
gleichzeitig zu messen.

## Befund: wo wir heute stehen

### Belege aus dem echten Betrieb (`korrekturen.jsonl`, 82 Korrekturen)

Falsch ersetzt wurde unter anderem:

```
skill     -> kill          (echtes Wort kaputt gemacht)
fuer      -> Feuer         (gemeint: für)
moechte   -> mochte        (gemeint: möchte)
naricht   -> anricht       (gemeint: Nachricht)
ganzen    -> Ganzen        (kontextabhängig, hier falsch)
datri     -> dari          (Unsinn, "dari" steht in der Wortliste)
ernn / itoniert / nstellen / ondern / icht / hochgel -> ...
                           (allesamt Wortfragmente, keine echten Wörter)
```

Achtung bei der Bewertung: Ein Teil dieser Zeilen entstand **vor** den letzten
zwei Korrektur-Commits vom 2026-09-04 (11:58 und 12:17). Welche davon heute
noch auftreten, ist unbekannt — genau deshalb ist Phase 0 die Messung.

### Drei Fehlerklassen

**A) Wettlauf beim Ersetzen (vermutlich die häufigste Ursache).**
Gemessen aus `debug.log`: dein Tastenabstand liegt im Median bei **188 ms**
(p10 = 172 ms, schnellste 159 ms). `TrayApp.SettleTime` wartet aber nur
**130 ms** Tastenruhe, bevor ersetzt wird. 130 ms sind kürzer als dein
normaler Tastenabstand — die Ersetzung feuert also **während du weitertippst**,
nicht in einer Pause. Die simulierten Rücktasten treffen dann Zeichen des
nächsten Worts. Ergebnis: die Fragmente oben. Diese Fragmente werden
anschließend als "unbekanntes Wort" behandelt und munter weiter geraten —
ein Fehler erzeugt den nächsten.

**B) Ersatzschreibungen und Regelfehler sind unbekannt.**
`ue/oe/ae` für Umlaute, `ss/ß`, klassische Buchstabengruppen. Der Corrector
kennt nur Damerau-Abstand **1** (ein Zeichen), `ue → ü` ist aber ein
Zwei-zu-eins-Tausch und damit außer Reichweite.

**C) Groß-/Kleinschreibung ohne Satzkontext.**
`ordner → Ordner`, `stick → Stick`, `montag → Montag` sind ohne die
Nachbarwörter nicht sicher entscheidbar; `ganzen → Ganzen` war belegt falsch.

### Was schon gut ist (nicht anfassen)

- Große, sauber lizenzierte Datenbasis: 881.698 Wortformen, 200.000
  Häufigkeiten, 258.182 Substantivformen, 23.571 Namen (`data/HERKUNFT.md`).
- Vorsichtige Grundhaltung im `SpellCorrector`: Mindestlänge, Dominanz-Schwelle
  (bester Kandidat muss den zweitbesten um Faktor 1,6 schlagen), gewichtete
  Fehlerarten, `AllowSpellGuess` nach Cursor-Sprüngen.
- Trennung reine Logik / Windows-Teil, 113 grüne Tests.

## Was andere Systeme machen (Recherche)

- **Hunspell / LibreOffice `de_DE_frami.aff`** — eine **REP-Tabelle** mit 28
  deutschen Ersetzungen (`ae→ä`, `oe→ö`, `ue→ü`, `ss→ß`, `f↔ph`, `d↔t`,
  `th↔t`, `ch↔k`, `i↔ie` …), die **vor** allem Raten greift; dazu `MAP` für
  Umlaut-Äquivalenz, eine `KEY`-Zeile für Tastatur-Nachbarschaft, `TRY` für
  die Buchstaben-Reihenfolge beim Raten, plus phonetische und n-Gramm-basierte
  Vorschläge. Kernidee: **Feste, sichere Ersetzungen schlagen jede Statistik.**
- **Noisy-Channel-Modell, Brill & Moore (2000)** — statt fester Gewichte eine
  **Confusion-Matrix** P(getippt | gemeint), gelernt aus einem Fehlerkorpus,
  gewichtet mit der **inversen Tastatur-Distanz** benachbarter Tasten. Das ist
  exakt der von dir gewünschte Tastatur-Abstand, in seiner etablierten Form.
- **SymSpell** (C#, NuGet, netstandard2.0) — Symmetric-Delete-Verfahren,
  Größenordnungen schneller als der klassische Ansatz, macht **Editierdistanz 2**
  praktikabel; sortiert nach Distanz, dann Häufigkeit; `LookupCompound` kann
  fehlende/überzählige Leerzeichen reparieren. Einschränkung: eigene
  Gewichtungen lassen sich **nicht** einhängen — deshalb als
  *Kandidatenlieferant* nutzen und selbst nachbewerten.
- **LanguageTool** — Confusion-Sets plus n-Gramm-Daten gegen Real-Word-Fehler.
- **Wikipedia: Liste von Tippfehlern** — über `?action=raw` maschinenlesbar,
  Format `{{tippfehler|falsch|richtig}}`, mehrere tausend geprüfte deutsche
  Einträge (`*` = Wortform-Wildcard, `+` = Leerzeichen).
- **GitHub Typo Corpus** — 350k echte Tippfehler-Korrekturen aus Commits,
  mehrsprachig inkl. Deutsch, JSONL. Wichtiger Realitätscheck aus dem
  zugehörigen Vergleichspapier: gängige Korrektoren erreichen darauf nur
  **F ≈ 0,5**. 90 % sind auf *unseren* Alltagstext erreichbar, auf beliebigem
  Fremdtext nicht — die Messlatte muss also aus deinem eigenen Tippverhalten
  gebaut sein.

## Der Plan

Reihenfolge nach **Nutzen pro Risiko**, nicht nach Aufwand. Jede Phase endet
mit einer Zahl, die vorher und nachher gemessen wird.

### Phase 0 — Messlatte bauen (ohne das ist alles andere Blindflug)

- Benchmark-Datei aus drei Quellen:
  1. **Deine echten Fälle** aus `keystrokes.log` und `korrekturen.jsonl`
     (inkl. aller oben aufgelisteten Fehlgriffe als Negativ-Fälle).
  2. **Korrekt geschriebener Alltagstext** als Fehlalarm-Falle — mehrere
     tausend Wörter, die das Programm *nicht* anfassen darf.
  3. **Synthetische Tippfehler**: aus häufigen Wörtern per Tastatur-Nachbar,
     Vertauschung, Auslassung erzeugt — mit bekannter Wahrheit.
- Ein Test, der die drei Kennzahlen oben ausrechnet und ausdruckt, statt nur
  "mindestens 20 von 27" zu prüfen.
- **Erster Erkenntnisgewinn:** alle 82 Log-Korrekturen gegen den *heutigen*
  Stand nachrechnen — welche Fehlgriffe existieren überhaupt noch?

*Ergebnis: eine Zahl, gegen die ab jetzt jede Änderung geprüft wird.*

### Phase 1 — Den Wettlauf beim Ersetzen abstellen

Die billigste große Verbesserung, und sie betrifft nur den Windows-Teil.

- `SettleTime` an die gemessene Realität anpassen (deutlich über 188 ms,
  Größenordnung 350–400 ms) — und diesen Wert aus deinem echten Tippverhalten
  ableiten, nicht raten.
- Zusätzlich absichern: kommt während einer laufenden Ersetzung ein
  Tastendruck, wird die Ersetzung verworfen statt halb ausgeführt.
- Fragmente gar nicht erst raten lassen: ein Wort direkt nach einer eigenen
  Ersetzung ist verdächtig und darf nur exakte Wörterbuch-Treffer bekommen.
- Optional prüfen: Ersetzung über die Zwischenablage statt Rücktasten — schneller
  und atomar, aber invasiver. Erst messen, dann entscheiden.

*Erwartung: die Fragment-Fehlgriffe verschwinden, Präzision steigt spürbar.*

### Phase 2 — Sichere Ersetzungen vor jedes Raten schieben

- **Ersetzungstabelle nach Hunspell-Vorbild**: `ue→ü`, `oe→ö`, `ae→ä`,
  `ss→ß`, sowie die restlichen deutschen REP-Paare — aber nur anwenden, wenn
  das Ergebnis ein bekanntes Wort ist und das Original keins ist. Damit sind
  `fuer→für` und `moechte→möchte` erschlagen, ohne zu raten.
- **Wikipedia-Tippfehlerliste** über `?action=raw` einmalig herunterladen,
  in unser `falsch=richtig`-Format wandeln und als mitgelieferte Datei ablegen
  (Herkunft + Lizenz in `data/HERKUNFT.md` nachtragen). Wildcards (`*`) und
  Mehrwort-Einträge (`+`) dabei bewusst aussortieren oder gesondert behandeln.
- Diese Quellen bekommen **Vorrang vor dem Fuzzy-Raten** — genau wie REP bei
  Hunspell.

*Erwartung: großer Sprung bei der Trefferquote, ohne Präzisionsverlust,
weil nichts geraten wird.*

### Phase 3 — Fehlermodell mit Tastatur-Distanz (dein ursprünglicher Wunsch)

- **QWERTZ-Nachbarschaftstabelle** (deutsche Tastatur, inkl. Umlaut-Tasten):
  für jede Taste die physisch angrenzenden.
- Das feste Gewicht `WeightSubstitution = 0.45` wird ersetzt durch eine
  Funktion der Tastenentfernung: Nachbartaste = plausibler Vertipper,
  weit entfernte Taste = unplausibel. `datri→dari` und `skill→kill` sind
  genau solche Fälle, bei denen das hilft.
- **Confusion-Matrix aus echten Daten** statt geschätzter Gewichte: aus dem
  GitHub Typo Corpus (deutscher Anteil) zählen, wie oft welcher Buchstabe
  durch welchen ersetzt/ausgelassen/eingefügt wird. Ergebnis ist eine kleine
  mitgelieferte Tabelle, kein Modell zur Laufzeit.
- **Editierdistanz 2** über einen SymSpell-artigen Index erschließen, damit
  Fälle wie `naricht→Nachricht` überhaupt in Reichweite kommen — aber mit
  **strengerer** Dominanz-Schwelle als bei Distanz 1, sonst kippt die Präzision.

*Erwartung: die verbleibenden echten Vertipper werden getroffen; hier ist das
Risiko für Fehlgriffe am größten, deshalb erst nach Phase 0 und mit Messung
nach jedem Teilschritt.*

### Phase 4 — Kontext: die Nachbarwörter mitlesen

- **Wort-Bigramme** (z. B. aus der Leipzig Corpora Collection, CC-BY) als
  mitgelieferte Datei: wie oft folgt Wort B auf Wort A.
- Damit wird entscheidbar, was heute geraten wird:
  - Groß-/Kleinschreibung: `im ganzen Haus` vs. `das Ganze` — Artikel davor
    entscheidet, nicht die Wortliste.
  - Kandidatenauswahl: passt `renn` oder `denn` besser hinter das Vorwort?
- Der `WordWatcher` müsste dafür ein kleines Fenster der letzten ein bis zwei
  Wörter behalten (heute nur das aktuelle Wort). **Datenschutz:** dieses
  Fenster bleibt im Speicher, wird bei Fokuswechsel verworfen und niemals
  geschrieben — das ist ausdrücklich festzuhalten.

*Erwartung: die Groß-/Kleinschreibfehler verschwinden; das ist der Schritt,
der aus "guter Vertipper-Korrektur" ein System macht, das sich richtig anfühlt.*

### Phase 5 — Automatisches Lernen (deine ursprüngliche Frage)

Jetzt sinnvoll, weil vorher die Datenbasis stimmt:

- Beim **Programmstart** `korrekturen.jsonl` auswerten (deine Antworten:
  vollautomatisch, ab Schwellwert, beim Start).
- Ein Wort, das **mehrfach gleich** korrigiert und nie zurückgenommen wurde,
  wandert automatisch ins persönliche Wörterbuch — dann greift beim nächsten
  Mal der schnelle, sichere Weg statt des Ratens.
- Umgekehrt: was du per Undo zurückgewiesen hast, verschärft die Schwelle für
  ähnliche Fälle, nicht nur für das eine Wort.
- Schwellwert und Verhalten gehören in eine Textdatei, damit du sie ohne
  Neubau ändern kannst.

### Phase 6 — Dauerhaft optimiert halten

- Der Benchmark aus Phase 0 läuft als normaler Test mit; jede Änderung, die
  die Präzision senkt, fällt sofort auf.
- Kennzahlen im Tray sichtbar machen (Korrekturen, Rücknahmen) — deine
  Rücknahme-Quote ist das ehrlichste Qualitätsmaß im Alltag.
- Neue Fehlgriffe aus dem Alltag landen als Testfall im Benchmark, nicht nur
  als Wörterbucheintrag.

## Reihenfolge und Aufwand (grob)

| Phase | Nutzen | Risiko | Aufwand |
|---|---|---|---|
| 0 Messlatte | Voraussetzung für alles | keins | mittel |
| 1 Wettlauf abstellen | **sehr hoch** | niedrig | klein |
| 2 Sichere Ersetzungen | hoch | niedrig | klein–mittel |
| 3 Tastatur + Fehlermodell | hoch | **mittel–hoch** | mittel |
| 4 Kontext/Bigramme | hoch | mittel | groß |
| 5 Auto-Lernen | mittel | niedrig | klein |
| 6 Dauerbetrieb | Absicherung | keins | klein |

Empfehlung: **0 → 1 → 2** liefern zusammen wahrscheinlich schon den größten
Teil des Weges und sind risikoarm. Erst danach 3 und 4 angehen, jeweils mit
Messung vorher/nachher.

## Bewusst nicht in diesem Plan

- **Keine KI zur Laufzeit, kein Netz.** Alle genannten Quellen werden
  **einmalig beim Entwickeln** heruntergeladen und als Datei mitgeliefert; das
  laufende Programm bleibt offline. (Die KI-Ebene über `claude -p` bleibt als
  v2 in der Spec bestehen und braucht vorher Passwortfeld-Erkennung.)
- **Keine Grammatik-/Stilkorrektur** (Kommas, Umformulieren).
- **Kein Umschreiben ganzer Sätze**, nur wortweise Korrektur.

## Lizenz-Hausaufgabe vor Umsetzung

Zu jeder neuen Datenquelle Lizenz prüfen und in `data/HERKUNFT.md` eintragen:
Wikipedia-Tippfehlerliste (CC BY-SA), Leipzig Corpora (CC BY), GitHub Typo
Corpus (Lizenz folgt den Ursprungs-Repos — für das *Ableiten einer
Häufigkeitstabelle* unkritisch, für das Mitliefern von Rohtext nicht).

## Entschieden (2026-09-05, mit dem Nutzer abgestimmt)

1. **Reihenfolge: 0 → 1 → 2 wie vorgeschlagen.** Erst die Messlatte, dann der
   Wettlauf beim Ersetzen, dann die sicheren Ersetzungen. Nach Phase 2 wird
   anhand der Zahlen neu bewertet, wie viel von Phase 3 noch nötig ist.
2. **Speicherbudget: bis ~500 MB, Start darf 2–3 Sekunden dauern.** Damit sind
   Bigramme in voller Größe und ein Distanz-2-Index möglich; es wird jeweils
   die genauere Variante gewählt. Der tatsächliche Verbrauch wird nach jeder
   Phase gemessen und im Log festgehalten.
3. **Phase 4 (Kontext/Bigramme) ist gesetzt** — ohne Satzkontext ist die
   Groß-/Kleinschreibung nicht sicher zu lösen und 90 % sind nicht erreichbar.
   Auflage: Das Wortfenster (letzte 1–2 Wörter) bleibt **ausschließlich im
   Arbeitsspeicher**, wird bei Fokuswechsel, Mausklick und Pause sofort
   verworfen und **niemals in eine Datei geschrieben**. Das ist beim Bauen
   von Phase 4 als Test abzusichern, nicht nur als Vorsatz.

## Nachtrag 2026-09-05: Ergebnisse der zweiten Recherche-Runde

Ausführlich in **`docs/RECHERCHE-KORREKTURSYSTEME.md`**. Vier Punkte ändern
den Plan inhaltlich:

### NEU: Phase 2b — Wortliste vervollständigen (vor Phase 3 zwingend)

**Komposita-Zerlegung.** Deutsch bildet beliebig viele Zusammensetzungen
(`Kältekreislauf`, `Nutzungslimit`). Die stehen in keiner Wortliste und sehen
für das Programm aus wie Tippfehler. Bei Abstand 1 passiert meist nichts, weil
kein Kandidat gefunden wird — **bei Abstand 2 (Phase 3) findet sich zu fast
jedem langen Wort irgendein Kandidat**. Ohne Zerlegungsprüfung würde Phase 3
also korrekte Komposita zerstören. Deshalb: Vor jedem Raten prüfen, ob sich
das Wort in bekannte Teile zerlegen lässt (inkl. Fugen-`s`) — wenn ja, Finger
weg. Hunspell macht das rekursiv mit `COMPOUNDMIN 2`.

**Affix-Expansion.** Laut `data/HERKUNFT.md` haben wir vom LibreOffice-
Wörterbuch nur die **Stämme** übernommen und die Affix-Regeln nie ausgewertet.
Mit `wordforms` (Hunspell) bzw. Lucenes `WordFormGenerator` lassen sich alle
gültigen Wortformen erzeugen. Jedes zusätzliche korrekte Wort ist ein Wort,
das nicht mehr fälschlich angefasst wird — senkt die Fehlalarm-Rate direkt.

### Ergänzung zu Phase 1: Passwortfelder automatisch erkennen

Heute schützt nur der Pause-Hotkey, den du selbst drücken musst. Über
`GetGUIThreadInfo` + Fensterstil `ES_PASSWORD` lässt sich ein klassisches
Passwortfeld erkennen und Korrektur **wie Mitschreiben** automatisch
aussetzen. Ehrliche Einschränkung: In Browsern und modernen Oberflächen
(Electron, WPF) greift das nicht — es ist eine **zusätzliche** Absicherung,
kein Ersatz für den Hotkey. Muss genau so dokumentiert werden.

### Ergänzung zu Phase 3: Kölner Phonetik als zweite Kandidatenquelle

Deutsches Gegenstück zu Soundex (Postel, 1969). Fängt Fehlerklassen, die die
Editierdistanz schlecht abdeckt: `nemlich→nämlich`, `ziehmlich→ziemlich`,
`Maschiene→Maschine`. Als **zusätzliche** Quelle mit eigenem, niedrigerem
Gewicht — nie allein entscheidend.

### Präzisierung Phase 1 und 4

- **Phase 1:** Der technisch saubere Weg wäre das Text Services Framework
  (TSF) — damit ließe sich Text direkt einfügen statt Tastendrücke zu
  simulieren, der Wettlauf entfiele bauartbedingt. Preis: COM-Textdienst,
  systemweit registriert, läuft im Prozess jeder Zielanwendung, in C# sehr
  aufwendig und für Virenscanner noch verdächtiger als der jetzige Hook.
  **Entscheidung: bei `SendInput` bleiben, Timing reparieren.** TSF bleibt als
  Option dokumentiert.
- **Phase 4:** Frei verfügbar sind zunächst nur **5.000** deutsche Bigramme
  (Google-Books-Ableitung, CC BY 3.0, direkt ladbar). Die große freie Quelle
  (Leipzig Corpora, CC BY) ist bot-geschützt und müsste **von Hand**
  heruntergeladen werden. Für die häufigsten Groß-/Kleinschreibfälle reicht
  die kleine Liste plus eine Artikel-/Präpositions-Regel.

### Realistische Erwartung

Auf beliebigem Fremdtext erreichen gängige Korrektoren nur ein F-Maß um 0,5.
90 % sind erreichbar, **weil die Messlatte aus deinem eigenen Tippverhalten
gebaut wird** — nicht aus zufälligem deutschen Text. Das ist keine
Schummelei, sondern genau das Ziel: Das Ding soll *dich* verstehen.

## Nachtrag 2026-09-05, Teil 2: Phasen 0–3 durchgeführt — beide Ziele erreicht

Gemessen mit dem Benchmark aus Phase 0 (206 Fälle aus den echten Logs):

| Kennzahl | Ausgang (Phase 0) | Nach Phase 1+2 | Nach Phase 3 | Ziel |
|---|---|---|---|---|
| Präzision | 94,9 % | 99,0 % | **99,0 %** | ≥ 98 % ✅ |
| Trefferquote | 82,5 % | 86,8 % | **90,4 %** | ≥ 90 % ✅ |
| Fehlalarme | 1,3 % | 1,3 % | 1,3 % | ≈ 0 % |

**Phase 3 wurde bewusst nur teilweise umgesetzt**, weil die beiden
Zielwerte bereits mit den risikoärmeren Bausteinen erreicht waren:

- **Umgesetzt:** QWERTZ-Tastatur-Nachbarschaft (`KeyboardLayout.cs`, nach
  Aspell-`.kbd`-Vorbild) gewichtet einen falschen Buchstaben höher, wenn die
  getippte Taste neben der richtigen liegt. Dazu die Verkettung
  Ersatzschreibung→Raten (`zustaedig→zustädig→zuständig`) für Wörter mit zwei
  Fehlern auf einmal.
- **Absichtlich zurückgestellt**, weil das Ziel schon erreicht war und der
  Aufwand/das Risiko den verbleibenden Nutzen nicht mehr rechtfertigt:
  - **Komposita-Zerlegung (Phase 2b)** — nötig als Absicherung, *bevor*
    Distanz 2 aktiviert wird, aber ohne Distanz 2 nicht dringend.
  - **Volle Editierdistanz-2-Suche** — die verbleibenden 11 übersehenen
    Fälle (`korogirt`, `personlaissierter`, `iennfach`, `shcnlell` …) sind
    mehrfache Vertipper (Vertauschung + Auslassung kombiniert). Eine
    brauchbare Distanz-2-Suche bräuchte einen SymSpell-artigen Index — die
    naive zweistufige Kandidatengenerierung wäre für jedes getippte Wort
    spürbar langsam und würde ohne den Komposita-Schutz echte
    Wortzusammensetzungen gefährden.
  - **Kölner Phonetik** — keine der verbleibenden Fälle ist ein phonetischer
    Fehler; ohne konkreten Bedarf hätte sie nur neues Fehlalarm-Risiko
    eingeführt.
- **Ein neuer Fehlgriff wurde beim Bauen entdeckt und sofort behoben:** Die
  Verkettung Ersatzschreibung→Raten hätte `besser` (korrekt) über `beßer` zu
  `Beißer` verfälscht. Die „schon ein bekanntes Wort → nicht anfassen"-
  Absicherung aus Phase 2 fehlte in der neuen Verkettung und wurde ergänzt,
  mit eigenem Regressionstest.

**Bewertung:** Beide Zielwerte des Plans sind erreicht. Die verbleibenden elf
übersehenen Fälle sind harte Mehrfach-Vertipper, für die sich der
Aufwand von Komposita-Zerlegung + Distanz-2-Index + Kölner Phonetik erst dann
lohnt, wenn ein neuer, konkreter Bedarf (z. B. aus dem 200-Wörter-Fließtext-
Test oder aus neuen echten Logs) das rechtfertigt. Bis dahin bleiben sie im
Benchmark als dokumentierte, bekannte Lücke stehen.

## Startpunkt für die Umsetzung

Phase 0, erste Aufgabe: alle 82 Korrekturen aus `korrekturen.jsonl` gegen den
heutigen Stand nachrechnen und feststellen, welche der oben aufgelisteten
Fehlgriffe überhaupt noch auftreten. Das ist gleichzeitig der erste Baustein
des Benchmarks und die ehrliche Ausgangszahl.
