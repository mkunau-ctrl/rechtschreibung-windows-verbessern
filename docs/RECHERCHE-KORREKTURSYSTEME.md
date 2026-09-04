# Recherche: Wie andere Korrektursysteme arbeiten

Stand: 2026-09-05. Diese Datei sammelt, was bei anderen Rechtschreib- und
Autokorrektursystemen gelernt werden kann, und was wir davon übernehmen.
Sie ist die Grundlage für
`docs/superpowers/plans/2026-09-05-korrektur-qualitaet-plan.md`.

**Wichtig:** Alle genannten Datenquellen werden **einmalig beim Entwickeln**
heruntergeladen und als Datei mitgeliefert. Das laufende Programm geht nie
ins Netz.

---

## 1. Hunspell / LibreOffice — das deutsche Wörterbuch selbst

Untersucht wurde `de_DE_frami.aff` (die Regel-Datei zum Wörterbuch, das wir
schon als Wortquelle nutzen). Sie enthält mehr, als wir bisher auswerten:

| Direktive | Inhalt | Für uns |
|---|---|---|
| `REP` (28 Einträge) | `ae→ä`, `oe→ö`, `ue→ü`, `ss→ß`, `s↔ss`, `f↔ph`, `d↔t`, `th↔t`, `ch↔k`, `i↔ie`, `a↔ah`, `e↔eh`, `o↔oh`, `r↔rh`, `ee→e` | **Direkt übernehmen.** Erschlägt `fuer→für`, `moechte→möchte` ohne jedes Raten |
| `MAP` (7 Einträge) | `(ss)ß`, `(ue)ü`, `(oe)ö`, `(ae)ä` + Großformen | Umlaut-Äquivalenz: zwei Schreibweisen desselben Zeichens |
| `TRY` | `esijanrtolcdugmphbyfvkwqxz…` | Buchstaben nach Häufigkeit sortiert — Reihenfolge beim Kandidaten-Erzeugen |
| `COMPOUNDBEGIN/MIDDLE/END` | Flags `x`/`y`/`z`, `COMPOUNDMIN 2` | **Deutsch erlaubt beliebige Zusammensetzungen** — siehe Abschnitt 2 |
| `NOSUGGEST` | Flag `n` | Wörter, die existieren, aber nie vorgeschlagen werden sollen |
| `FORBIDDENWORD` | Flag `d` | Formen, die die Regeln erzeugen würden, die es aber nicht gibt |
| `BREAK` | `-`, `.` | Wort an Bindestrich/Punkt zerlegen und Teile einzeln prüfen |

**Kein `KEY`-Eintrag vorhanden.** Das deutsche Wörterbuch liefert also keine
Tastatur-Nachbarschaft mit — die müssen wir selbst bauen (Abschnitt 4).

**Erkenntnis, die am meisten wiegt:** Hunspell wendet die `REP`-Tabelle mit
**höchster Priorität** an, noch vor jeder statistischen Ähnlichkeitssuche.
Feste, sichere Ersetzungen schlagen jede Statistik. Genau diese Reihenfolge
fehlt uns bisher.

Quelle: [de_DE_frami.aff (LibreOffice/dictionaries)](https://github.com/LibreOffice/dictionaries),
Lizenz GPL-2.0 / LGPL-2.1 / MPL-1.1 (Dreifachlizenz).

---

## 2. Deutsche Komposita — das größte unterschätzte Risiko

Deutsch bildet **beliebig viele** zusammengesetzte Wörter: `Kältekreislauf`,
`Nutzungslimit`, `Trainingsdaten`, `Rechtschreibkorrektur`. Die stehen in
keiner Wortliste der Welt vollständig drin.

Für uns heißt das: Ein völlig korrekt geschriebenes Kompositum sieht für das
Programm aus wie ein **unbekanntes Wort** — also wie ein Tippfehler. Solange
wir nur Abstand 1 raten, passiert meist nichts, weil kein Kandidat gefunden
wird. **Sobald Abstand 2 dazukommt (geplante Phase 3), explodiert dieses
Risiko**: Zu einem langen Kompositum findet sich fast immer irgendein
Kandidat in 880.000 Wortformen.

**Konsequenz für den Plan:** Vor jedem Raten muss geprüft werden, ob sich das
Wort in bekannte Teilwörter zerlegen lässt (Zerlegung mit Fugen-`s`, z. B.
`Nutzung|s|limit`). Lässt es sich zerlegen → Wort ist in Ordnung, Finger weg.
Hunspell macht genau das rekursiv (`COMPOUNDMIN 2`).

Das ist eine **neue Aufgabe, die im ursprünglichen Plan fehlte**, und sie ist
Voraussetzung für Phase 3, nicht optional.

Quellen: [Hunspell-Handbuch (COMPOUNDFLAG etc.)](https://manpages.ubuntu.com/manpages/bionic/man5/hunspell.5.html),
[zverok: Rebuilding the spellchecker, pt. 3 — compounds](https://zverok.space/blog/2021-01-14-spellchecker-3.html)

---

## 3. Unsere Wortliste ist unvollständig — Affix-Expansion

Laut `data/HERKUNFT.md` haben wir aus dem LibreOffice-Wörterbuch **nur die
Stämme** übernommen (250.836 Einträge) und die Affix-Regeln der `.aff`-Datei
**nicht ausgewertet**. Damit fehlen uns gebeugte Formen, die das Wörterbuch
eigentlich kennt.

Hunspell bringt dafür ein Werkzeug mit: `unmunch` (veraltet) bzw. dessen
Nachfolger **`wordforms`**, das aus `.dic` + `.aff` alle gültigen Wortformen
erzeugt. Alternativ gibt es eine fertige Implementierung in Apache Lucene
(`WordFormGenerator`).

**Nutzen:** Jedes zusätzliche korrekte Wort in der Liste ist ein Wort, das
nicht mehr fälschlich „korrigiert" wird. Das senkt die Fehlalarm-Rate direkt —
die Kennzahl, die uns am wichtigsten ist.

Quellen: [hunspell/hunspell](https://github.com/hunspell/hunspell),
[Issue #404: unmunch/wordforms](https://github.com/hunspell/hunspell/issues/404),
[Lucene WordFormGenerator](https://lucene.apache.org/core/9_9_1/analysis/common/org/apache/lucene/analysis/hunspell/WordFormGenerator.html)

---

## 4. Tastatur-Distanz — dein Wunsch, und wie die Profis ihn lösen

### Aspell macht es mit einer einfachen Textdatei

GNU Aspell hat eine „Typo-Analyse": Vertipper, die durch **Danebengreifen auf
eine Nachbartaste** entstehen, werden bevorzugt vorgeschlagen. Die
Tastaturkenntnis steckt in einer `.kbd`-Datei mit einem denkbar einfachen
Format:

```
as        # 'a' und 's' liegen nebeneinander
sd
df
```

Die Gegenrichtung (`sa`) ergibt sich automatisch. Akzente/Umlaute werden beim
Bewerten ignoriert (`o` und `ö` gelten als dieselbe Taste).

**Für uns:** Format übernehmen (eine schlichte, von dir editierbare Textdatei
neben der .exe), Inhalt selbst für **QWERTZ** erstellen. Aspell selbst liefert
keine deutsche Tastaturdatei mit — `qwertz.kbd` existiert nur als
Community-Patch in offenen Issues.

### Die Fachliteratur: Noisy-Channel-Modell

Der etablierte Ansatz ist das **Noisy-Channel-Modell**:

> gesuchtes Wort = argmax P(Fehler | gemeintes Wort) × P(gemeintes Wort)

- `P(gemeintes Wort)` ist die Worthäufigkeit — **haben wir schon**
  (`haeufigkeit.txt`).
- `P(Fehler | gemeintes Wort)` ist das **Fehlermodell** — das fehlt uns. Heute
  stehen dort vier feste Zahlen (`WeightSubstitution = 0.45` usw.).

Das Fehlermodell wird üblicherweise als **Confusion-Matrix** dargestellt:
wie oft wird Buchstabe X statt Y getippt. Und genau hier kommt die Tastatur
rein: Die Wahrscheinlichkeit wird mit der **inversen Entfernung der beiden
Tasten** gewichtet — Nachbartaste = wahrscheinlich, andere Tastaturhälfte =
unwahrscheinlich. Brill & Moore (2000) haben das zu einem gelernten
Zeichenketten-Fehlermodell erweitert.

**Für uns:** Genau der von dir gewünschte Mechanismus, in seiner bewährten
Form. Umsetzbar in zwei Stufen — erst QWERTZ-Nachbarschaft (einfach, sofort
wirksam), später eine aus echten Daten gezählte Confusion-Matrix.

Quellen: [Aspell: Notes on Typo-Analysis](http://aspell.net/man-html/Notes-on-Typo_002dAnalysis.html),
[Aspell qwertz.kbd Issue #328](https://github.com/GNUAspell/aspell/issues/328),
[Spelling Correction / Noisy Channel (Vorlesungsskript)](https://www.csd.uwo.ca/~oveksler/Courses/Winter2016/CS4442_9542b/L15-NLP-Spell.pdf),
[Brill & Moore: An Improved Error Model](https://www.researchgate.net/publication/2552471_An_Improved_Error_Model_for_Noisy_Channel_Spelling_Correction)

---

## 5. Kölner Phonetik — für Fehler, die man hört

Ein deutschsprachiges Gegenstück zu Soundex, 1969 von Hans Joachim Postel
veröffentlicht. Jedes Wort bekommt einen Zifferncode (0–8); gleich klingende
Wörter bekommen denselben Code (`Meier`/`Maier`/`Mayer`/`Mayr`). `W` und `V`
werden beide zu `3`; `Wikipedia` ergibt `3412`. Anders als Soundex ist die
Codelänge nicht begrenzt.

Hunspell nutzt phonetische Ähnlichkeit als eigene Vorschlagsquelle neben der
buchstabenbasierten.

**Für uns:** Fängt eine Fehlerklasse, die die Editierdistanz schlecht abdeckt —
`nemlich→nämlich`, `ziehmlich→ziemlich`, `Maschiene→Maschine`,
`Portmonee→Portemonnaie`. Als **zusätzliche Kandidatenquelle mit eigenem,
niedrigerem Gewicht** sinnvoll; niemals allein entscheidend, weil phonetisch
gleiche Wörter oft verschiedene Bedeutungen haben.

Quellen: [Cologne phonetics (Wikipedia)](https://en.wikipedia.org/wiki/Cologne_phonetics),
[Apache Commons Codec: ColognePhonetic](https://commons.apache.org/codec/apidocs/org/apache/commons/codec/language/ColognePhonetic.html)

---

## 6. SymSpell — schnell genug für Abstand 2

Unser `SpellCorrector` erzeugt Kandidaten, indem er das getippte Wort auf alle
möglichen Arten verändert (Norvig-Ansatz). Bei Abstand 1 geht das; bei
Abstand 2 wird es zu langsam.

**SymSpell** dreht das um („Symmetric Delete"): Statt Einfügungen, Ersetzungen
und Vertauschungen zu erzeugen, werden **nur Löschungen** gebildet — sowohl
beim getippten Wort als auch (vorberechnet) bei den Wörterbuchwörtern. Das ist
laut Autor Größenordnungen schneller (Abstand 2 in ~0,000033 s gegenüber dem
Norvig-Ansatz).

Technische Eckdaten:

- C#, NuGet-Paket `SymSpell`, `netstandard2.0` + `net9.0` — passt zu .NET 8.
- Sortiert Ergebnisse nach **Editierdistanz, dann Häufigkeit**.
- `prefixLength` reduziert den Speicher um über 90 %; für große Wörterbücher
  müssen `maxEditDistance`, `prefixLength` und `countThreshold` abgestimmt werden.
- `LookupCompound` korrigiert ganze Wortfolgen und repariert **fehlende oder
  überzählige Leerzeichen** — interessant für Fälle wie `garnicht`.
- `LoadBigramDictionary` für Kontext (siehe Abschnitt 7).
- **Einschränkung: eigene Gewichtungen lassen sich nicht einhängen.**

**Für uns:** SymSpell (oder ein selbst gebauter Index nach demselben Prinzip)
als **Kandidaten-Lieferant**, die Bewertung machen wir mit unserem eigenen
Fehlermodell selbst. So bekommen wir Abstand 2 in erträglicher Zeit, ohne die
Kontrolle über die Entscheidung abzugeben.

Quellen: [wolfgarbe/SymSpell](https://github.com/wolfgarbe/SymSpell),
[NuGet: SymSpell](https://www.nuget.org/packages/symspell),
[1000x Faster Spelling Correction (Wolf Garbe)](https://wolfgarbe.medium.com/1000x-faster-spelling-correction-algorithm-2012-8701fcd87a5f)

---

## 7. Kontext: die Nachbarwörter entscheiden

Ohne Kontext ist `ganzen→Ganzen` nicht entscheidbar (`im ganzen Haus` vs.
`das Ganze`) — ein Fehler, der bei dir belegt aufgetreten ist.

**Wie es andere machen:**

- **Smartphone-Tastaturen** bewerten mit n-Gramm-Modellen die
  Wahrscheinlichkeit eines Wortes *gegeben die vorherigen Wörter*. Genau so
  wird `there`/`their`/`they're` unterschieden.
- **LanguageTool** nutzt **Confusion-Sets** (Paare leicht verwechselbarer
  Wörter) plus große n-Gramm-Daten, um „Real-Word-Fehler" zu finden — also
  Wörter, die korrekt geschrieben, aber falsch sind.

**Datenquellen für deutsche Bigramme:**

| Quelle | Umfang | Lizenz | Bewertung |
|---|---|---|---|
| [orgtre/google-books-ngram-frequency](https://github.com/orgtre/google-books-ngram-frequency) — `ngrams/2grams_german.csv` | **nur 5.000** häufigste Bigramme (1-Gramme: 10.000) | CC BY 3.0 | Sofort per Rohabruf ladbar, aber klein — reicht für die häufigsten Groß-/Kleinschreibfälle, nicht mehr |
| [Leipzig Corpora Collection](https://corpora.wortschatz-leipzig.de/) (`deu_news_2023`: 33 Mio. Sätze, 520 Mio. Token) | groß, mit Nachbarschafts-Statistik | CC BY 3.0 | Beste freie Quelle; Download-Seite ist bot-geschützt → muss **von Hand** heruntergeladen werden |
| Lexical Computing | sehr groß | kommerziell | ausgeschlossen |

**Für uns:** Mit den 5.000 freien Bigrammen anfangen (deckt `im ganzen`,
`das Ganze` und Ähnliches bereits ab), bei Bedarf Leipzig manuell nachladen.
Für die Groß-/Kleinschreibung reicht oft schon eine kleine Regel: **steht ein
Artikel/eine Präposition davor, entscheidet die**.

Quellen: [LanguageTool: Finding errors using n-gram data](https://dev.languagetool.org/finding-errors-using-n-gram-data.html),
[Autokorrektur per N-Gramm (Patent US9779080B2)](https://patents.google.com/patent/US9779080B2/en)

---

## 8. Fertige Ersetzungslisten, die wir übernehmen können

| Quelle | Umfang | Lizenz | Format |
|---|---|---|---|
| [Wikipedia: Liste von Tippfehlern](https://de.wikipedia.org/wiki/Wikipedia:Liste_von_Tippfehlern) | Unterseiten A–Z, P/Q, X/Y/Z, 0-9, Sonderzeichen — mehrere tausend Einträge | CC BY-SA | über `?action=raw` maschinenlesbar: `{{tippfehler|falsch|richtig}}` |
| LibreOffice-Autokorrektur `acor_de-DE.dat` | mehrere tausend | LibreOffice-Lizenz | ZIP mit XML-Ersetzungstabelle |
| Microsoft-Office-Autokorrektur `MSO1031.acl` | mehrere tausend | proprietär | Binärformat im Benutzerprofil — **nur lokal auswertbar, nicht mitliefern** |
| AutoHotkey-Community-Listen (deutsch) | ~400 | frei | Hotstring-Zeilen |

**Achtung bei der Wikipedia-Liste** — sie ist für Menschen mit Urteilsvermögen
gedacht, nicht für blinde Automatik. Beim Einlesen auszusortieren:

- Einträge mit `*` (Wortform-Platzhalter, z. B. `Sateliten*`),
- Einträge mit `+` (Mehrwort-Ersetzungen, z. B. `schief+geht`),
- `ss`/`ß`-Einträge, weil `ss` in der Schweiz korrekt ist,
- alles, was mit `sic`, `schweizbezogen` oder `österreichbezogen` markiert ist.

---

## 9. Windows-Technik: wie ersetzt man Text richtig?

Wir ersetzen heute mit `SendInput`: N Rücktasten simulieren, dann den neuen
Text tippen. Das ist der Grund für den gemessenen Wettlauf (siehe Plan,
Phase 1).

**Der „richtige" Weg wäre das Text Services Framework (TSF).** Ein
TSF-Textdienst kann Text **direkt in den Textspeicher der Anwendung
einfügen**, ohne Tastendrücke zu simulieren. Microsoft nennt Autokorrektur und
Tippvorschläge ausdrücklich als das, wofür TSF gedacht ist, und weist darauf
hin, dass `SendInput` bei komplexen Eingaben falsche Zeichen erzeugen kann.

**Aber:** Ein TSF-Textdienst ist eine COM-Komponente, die systemweit
registriert wird und **im Prozess jeder Zielanwendung** läuft
(`ITfTextInputProcessor`). Das ist in C# ein erheblicher Aufwand, es ist
schwerer zu debuggen, und es würde von Virenscannern noch kritischer beäugt
als der jetzige Tastatur-Hook (vgl. die Defender-Fehlalarme, siehe `CLAUDE.md`).

Die dritte Möglichkeit, **UI Automation `ValuePattern`**, ersetzt immer den
*gesamten* Feldinhalt und taugt deshalb nicht zum Korrigieren einzelner Wörter
beim Tippen.

**Entscheidung fürs Erste:** Bei `SendInput` bleiben und das Timing reparieren
(Plan, Phase 1). TSF bleibt als mögliche spätere Architektur dokumentiert —
mit ehrlichem Preisschild.

Quellen: [Text Services Framework (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/win32/tsf/text-services-framework),
[ITfTextInputProcessor](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nn-msctf-itftextinputprocessor),
[UI Automation TextPattern Overview](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-textpattern-overview)

---

## 10. Sicherheit: Passwortfelder automatisch erkennen

Heute schützt dich nur der Pause-Hotkey `Strg+Alt+P`, den du vor einer
Passworteingabe **selbst** drücken musst. Das ist eine Gedächtnisleistung, die
irgendwann schiefgeht.

Technisch möglich ist mehr: Über `GetGUIThreadInfo` lässt sich das Fenster mit
dem Eingabefokus ermitteln; klassische Passwortfelder tragen den Fensterstil
`ES_PASSWORD`. In solchen Feldern könnte das Programm **automatisch weder
korrigieren noch mitschreiben**.

Einschränkung, die ehrlich dokumentiert gehört: Bei Browsern und modernen
Oberflächen (Electron, WPF, Web-Formularen) gibt es kein klassisches
Fensterhandle je Feld — dort greift die Erkennung nicht. Sie ist also eine
**zusätzliche Absicherung, kein Ersatz** für den Pause-Hotkey.

Quelle: [GetGUIThreadInfo (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo)

---

## 11. Realistische Erwartung an „90 %"

Das Vergleichspapier „An In-Depth Comparison of 14 Spelling Correction Tools
on a Common Benchmark" und die Auswertung des GitHub Typo Corpus zeigen: Auf
**beliebigem Fremdtext** erreichen gängige Korrektoren nur ein F-Maß um **0,5**.

Das heißt nicht, dass 90 % unmöglich sind — es heißt, dass die Messlatte aus
**deinem eigenen Tippverhalten** gebaut sein muss. Auf deinen wiederkehrenden
Fehlern, deinem Wortschatz und deiner Tastatur sind 90 % Trefferquote und
98 % Präzision erreichbar. Auf zufälligem deutschen Text wären sie es nicht.

Als Trainings- und Prüfmaterial nutzbar:

- [GitHub Typo Corpus](https://github.com/mhagiwara/github-typo-corpus) —
  über 350.000 echte Tippfehler-Korrekturen, 65 Mio. Zeichen, 15+ Sprachen
  inkl. Deutsch, JSONL, Direktdownload. Lizenz folgt den Ursprungs-Repos →
  **nur abgeleitete Statistik mitliefern, keinen Rohtext**.
- Wikipedia-Versionsgeschichte (Verfahren wie beim WikEd-Korpus) — aufwendiger,
  aber sehr große Mengen echter deutscher Korrekturen.

---

## 12. Was wir daraus übernehmen — Zusammenfassung

| Erkenntnis | Wohin im Plan | Neu? |
|---|---|---|
| Feste Ersetzungen (`REP`) **vor** jedes Raten | Phase 2 | war drin |
| Wikipedia-Tippfehlerliste, gefiltert | Phase 2 | war drin |
| **Komposita-Zerlegung vor jedem Raten** | **neu: Phase 2b** | **neu, kritisch** |
| **Affix-Expansion der Wortliste (`wordforms`)** | **neu: Phase 2b** | **neu** |
| QWERTZ-Nachbarschaft im Aspell-`.kbd`-Format | Phase 3 | war drin |
| Confusion-Matrix aus GitHub Typo Corpus | Phase 3 | war drin |
| **Kölner Phonetik als zusätzliche Kandidatenquelle** | **Phase 3** | **neu** |
| SymSpell-Prinzip als Kandidaten-Lieferant, eigene Bewertung | Phase 3 | präzisiert |
| Bigramme: erst 5.000 freie, ggf. Leipzig manuell | Phase 4 | präzisiert |
| **Passwortfeld-Erkennung (`ES_PASSWORD`)** | **neu: Phase 1** | **neu** |
| Bei `SendInput` bleiben, TSF nur dokumentieren | Phase 1 | präzisiert |
| Empfindlichkeit (vorsichtig/normal/mutig) als Einstellung | Phase 5 | neu |
