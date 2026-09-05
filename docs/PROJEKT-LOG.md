# Projekt-Log — Rechtschreib-Trainer (Windows)

## 2026-09-05 – Phase 5: Automatisches Lernen aus dem eigenen Korrektur-Log

**Was:** Der ursprüngliche Wunsch des Nutzers, jetzt gebaut. Beim
Programmstart liest `DictionaryLoader` `korrekturen.jsonl` und zählt
(vorher,nachher)-Paare (`DictionaryDistiller`, neue Klasse). Ab **3 gleichen
Treffern** wandert das Paar automatisch in `woerterbuch.txt`, mit
Kommentarzeile und Datum. Eine Tray-Benachrichtigung zeigt beim Start an,
welche Wörter gelernt wurden ("3 neue Wörter gelernt: …").

**Wichtiger Fund beim Gegenprüfen gegen das echte Log** (vor jedem
Produktiveinsatz manuell geprüft, siehe Vorgehen unten): `ich=Ich` wäre mit
gelernt worden. Das ist aber eine reine **Satzanfang**-Positionsregel
(`CorrectionSource.Capitalization`), keine Rechtschreibkorrektur — als
fester Wörterbuch-Eintrag hätte „ich" ab dann **überall im Satz**
großgeschrieben werden können, nicht nur am Anfang. `DictionaryDistiller`
schließt Capitalization-Quellen deshalb grundsätzlich aus, mit eigenem
Regressionstest. Ebenso ausgeschlossen: Wörter aus
`mehrdeutige-substantive.txt` (brauchen weiterhin Satzkontext, siehe letzter
Log-Eintrag) und Wörter, die schon im Wörterbuch oder auf der Nie-Liste
stehen.

**Vorgehen beim Prüfen:** Vor der Verdrahtung ins laufende Programm eine
temporäre Testdatei geschrieben, die `DictionaryDistiller` gegen das echte
`korrekturen.jsonl` des Nutzers laufen ließ (91 Einträge) — Ergebnis vor der
Capitalization-Absicherung: 8 Kandidaten, darunter das fehlerhafte
`ich=Ich`. Nach der Absicherung entfernt sich dieser eine Fall, die
restlichen sieben (`nocg=noch`, `ordner=Ordner`, `cih=ich`, `seperat=separat`,
`computer=Computer`, `montag=Montag`, `datei=Datei`) sind allesamt
unproblematisch (eindeutige Substantive oder feste Vertipper, nicht
positionsabhängig). Testdatei danach wieder gelöscht — sie war nur zur
Verifikation, kein Teil der Suite.

**Entscheidung:** Verdichtung als eigene, pure Core-Klasse
(`DictionaryDistiller`) statt direkt in `DictionaryLoader` — testbar ohne
echte Dateisystem-Pfade. Schwellwert 3 als Konstante in `DictionaryLoader`
(nicht konfigurierbar über eine Datei — bei Bedarf später nachrüstbar).

**Stand danach:** 188 Tests grün (vorher 182). Programm neu gebaut,
`scripts/install.ps1` erneut ausgeführt.

**Offene Punkte / Nächste Schritte:**
- Beobachten, was beim nächsten echten Programmstart tatsächlich gelernt
  wird (die Tray-Benachrichtigung zeigt es an).
- Alltagstest der Phase-1-Ersetzung durch den Nutzer steht weiterhin aus.
- Artikel+Adjektiv+Substantiv-Ketten (`den großen Fluss`) — bräuchte echte
  Wortarten-Erkennung, siehe letzter Log-Eintrag.
- 11 hartnäckige Mehrfach-Vertipper — bräuchten Distanz-2 + Komposita-Schutz.
- Damit sind alle sechs Phasen des Qualitätsplans mindestens einmal
  angefasst (0–3 vollständig, 4 gezielt, 5 gebaut). Phase 6 (dauerhaft
  optimiert halten) läuft bereits mit: die drei Ratschen-Tests fangen jeden
  Rückschritt automatisch ab.

## 2026-09-05 – Phase 4 (Teil): gezielte Großschreibung mit einem Wort Kontext

**Was:** Statt des vollen Bigramm-Systems aus dem ursprünglichen Plan eine
kleinere, risikoärmere Lösung für genau das Problem, das der Fließtext-Test
aufgedeckt hatte.

- **`Determiners.cs`** — Artikel/Possessiv-/Demonstrativpronomen
  (der/die/das/ein/mein/dieser/jeder/…), eine geschlossene Wortklasse.
- **`WordWatcher`** merkt sich jetzt das zuletzt fertig getippte Wort und
  gibt im neuen `WordContext.PrecededByDeterminer` mit, ob direkt davor ein
  Artikel stand. Wird wie der Satzanfang-Zustand bei jedem Kontextbruch
  (Fokuswechsel, Klick, Navigationstaste) verworfen.
- **`mehrdeutige-substantive.txt`** — eine kleine, kuratierte Liste von
  Wörtern, die häufig etwas anderes sind (Verb/Adjektiv/Adverb) und nur
  selten das gleichlautende Substantiv: `fallen`, `falle`, `dusche`,
  `duschen`, `großen`, `dichten`, `klarem`, `schöne`, `überblicken`, `früh`
  (aus dem Fließtext-Fund) sowie `besten`, `gucken`, `schauen`, `bauen`,
  `brauche`, `aktiv`, `drei`, `dies` (die bisherigen 14 „kontextabhängigen"
  Fälle aus `benchmark-faelle.tsv` — alle acht Substantivformen gegen
  `data/substantive.txt` verifiziert, bevor sie aufgenommen wurden).
- **`WordList.IsCapitalisedOnly`** großschreibt Wörter aus dieser Liste nur
  noch, wenn `precededByDeterminer` zutrifft — sonst bleibt die häufigere,
  klein geschriebene Lesart bestehen. Alle anderen Substantive (Montag,
  Computer, GitHub …) funktionieren unverändert wie vorher.

**Warum zuerst verworfen, dann so gelöst:** Erster Versuch war, die
Häufigkeitsdaten (`haeufigkeit.txt`) heranzuziehen — Idee: wenn die
Großschreibung im Korpus kaum vorkommt, nicht großschreiben. **Das hätte
alles kaputt gemacht**: `haeufigkeit.txt` ist komplett kleingeschrieben
(Standard-Vorverarbeitung von Frequenzlisten), `montag`/`computer`/`ordner`
zeigen exakt dasselbe Muster wie `früh`/`fallen` — die Methode kann echte
Homographen nicht von bloß klein getippten, aber eindeutigen Substantiven
unterscheiden. Vor dem Bauen mit echten Daten geprüft, verworfen, keine Zeile
Code dafür geschrieben.

**Ergebnis:**

| Test | Vorher | Jetzt |
|---|---|---|
| Isolierter Benchmark: „kontextabhängige Fälle" fälschlich angefasst | 8 von 14 | **0 von 14** |
| Isolierter Benchmark: Präzision/Trefferquote/Fehlalarme | 100 % / 90,4 % / 0 % | unverändert |
| Fließtext-Benchmark: Gesamtquote | 93,3 % | **96,6 %** (230 von 238) |
| Fließtext-Benchmark: Fehlalarme | 9 | **2** (`großen`, `dichten`) |

**Bekannte, bewusst nicht gelöste Grenze:** Steht zwischen Artikel und
Substantiv noch ein dekliniertes Adjektiv (`den großen Fluss`,
`einen dichten Wald`), steht der Artikel trotzdem unmittelbar vor dem
mehrdeutigen Wort — ein einzelnes Vorwort kann das nicht von echter
Nominalisierung (`die Großen`) unterscheiden. Das bräuchte eine echte
Wortarten-Erkennung, die für dieses schlanke, offline arbeitende Programm
nicht angemessen ist.

**Entscheidung:** Bewusst eine kleine, von Hand geprüfte Liste statt eines
allgemeinen Mechanismus — jedes Wort ist gegen die echte 258k-Substantivliste
verifiziert, bevor es aufgenommen wurde. Kein Risiko für die bestehenden,
gut funktionierenden Fälle (Montag, Computer, GitHub …), weil die alte Regel
für alles außerhalb dieser Liste unverändert gilt.

**Stand danach:** 176 Tests grün (vorher 172). Programm neu gebaut und neu
installiert (`scripts/install.ps1`).

**Offene Punkte / Nächste Schritte:**
- Phase 4 ist damit **nicht vollständig** (kein echtes Bigramm-System, siehe
  bekannte Grenze oben), aber der praktisch bedeutsamste Teil des in der
  Fließtext-Messung gefundenen Problems ist behoben.
- Alltagstest der Phase-1-Ersetzung durch den Nutzer steht weiterhin aus.
- 11 hartnäckige Mehrfach-Vertipper — bräuchten Distanz-2 + Komposita-Schutz.
- **Phase 5 (automatisches Lernen aus den eigenen Logs)** — der
  ursprüngliche Wunsch des Nutzers, als Nächstes dran.

## 2026-09-05 – Programm installiert; letzter Fehlalarm behoben (100 % Präzision)

**Was:** Zwei Dinge.

1. **Erstinstallation mit Autostart.** `scripts/install.ps1` ausgeführt — bis
   dahin lief nichts von alledem tatsächlich auf dem Rechner, alles war nur
   Quellcode + Tests. Läuft jetzt unter
   `%LOCALAPPDATA%\RechtschreibTrainer\RechtschreibTrainer.exe`, Autostart-
   Verknüpfung im Windows-Startordner.
2. **`codest → Codes` behoben** (der letzte offene Fehlalarm aus Phase 2/3).
   Ursache: `codest` ("du codest" — Denglisch, 2. Person von "coden") stand
   in keiner Wortliste, fiel deshalb ins Fuzzy-Raten und wurde zu `Codes`
   (Plural von "der Code", Editierdistanz 1) verfälscht — eine Ersetzung
   ohne jeden Bezug zur Absicht des Nutzers. Neue Datei
   `src/RechtschreibTrainer/denglisch-verben.txt`: eingedeutschte
   Tech-Verben (`coden/codest/codet/gecodet`, `committen`, `pushen`,
   `mergen`, `deployen`, `forken`, `clonen`, `debuggen`, `builden`,
   `releasen`, `taggen`, je mit Konjugationen) als **bekannte Wörter** — sie
   werden dadurch nie mehr angefasst, weil `SpellCorrector` ein bereits
   bekanntes Wort grundsätzlich in Ruhe lässt.

Nebenbei behoben: `WordList.FromLines` überlas Kommentarzeilen (`#`) in der
Haupt-Wortliste bisher nicht (nur in den Substantiv-/Namenslisten) — hätte
sonst den Kommentarkopf der neuen Datei als „Wort" eingelesen.

**Warum:** Der Nutzer bat um Weiterarbeit ohne konkrete Vorgabe; das war der
letzte konkret benannte, klar abgegrenzte offene Punkt aus der letzten
Zusammenfassung.

**Ergebnis (Benchmark):**

| Kennzahl | Vorher | Jetzt |
|---|---|---|
| Präzision | 99,0 % | **100,0 %** |
| Trefferquote | 90,4 % | 90,4 % (unverändert) |
| Fehlalarme | 1,3 % | **0,0 %** |

**Entscheidung:** Als eigene, bewusst kleine Wortliste angelegt (nicht in
`woerterbuch.txt` oder `standard-vertipper.txt`), weil es **bekannte Wörter**
sind, keine Ersetzungen — das ist ein anderer Mechanismus
(`WordList`/`SpellCorrector.IsKnownWord`, nicht `CorrectionDictionary`).

**Stand danach:** 147 Tests grün (vorher 146). Programm läuft installiert
mit dem neuesten Stand.

**Offene Punkte / Nächste Schritte:**
- Alltagstest der Phase-1-Ersetzung durch den Nutzer steht weiterhin aus —
  jetzt aber am tatsächlich laufenden, installierten Programm möglich.
- Phase 4 (Kontext/Bigramme) — größter verbleibender Hebel laut
  Fließtext-Benchmark.
- 11 hartnäckige Mehrfach-Vertipper — bräuchten Distanz-2 + Komposita-Schutz.
- Automatisches Lernen aus `korrekturen.jsonl`/`keystrokes.log` (Plan-Phase 5,
  der ursprüngliche Wunsch des Nutzers) — noch nicht begonnen.

## 2026-09-05 – Fließtext-Benchmark: Gesamtquote 93,3 %, Phase-4-Lücke bestätigt

**Was:** Neuer Test `PassageBenchmarkTests.cs`, ergänzend zum wort-isolierten
`BenchmarkTests.cs`. Ein zusammenhängender deutscher Absatz (238 Wörter, 14
Sätze, über eine typische Alltagsbeschreibung) wird komplett durch den echten
Korrektor geschickt — mit demselben Satzanfang-Kontext, den auch das
laufende Programm sieht.

Die Vertipper sind **programmgesteuert erzeugt** (Vertauschung, Auslassung,
Tastatur-Nachbar-Ersetzung, ue/oe/ae/ss-Ersatzschreibung, vergessene
Großschreibung an Satzanfängen und bei ausgewählten Substantiven), nicht von
Hand in den Text getippt — damit sich keine Übertragungsfehler einschleichen
und der Originaltext als zuverlässige Ziel-Referenz für den mechanischen
Vergleich dient. 67 von 238 Wörtern bekamen einen Fehler, 171 blieben als
Kontrolle unangetastet.

**Ergebnis:**

| Kennzahl | Wert |
|---|---|
| **Gesamtquote** (Wort am Ende korrekt) | **93,3 %** (222 von 238) |
| Vertipper korrigiert | 89,6 % (60 von 67) |
| Kontrollwörter unangetastet | 94,7 % (162 von 171) |

**Wichtigster Befund: Alle 9 Fehlalarme sind dieselbe, schon dokumentierte
Lücke — nur jetzt mit echten Zahlen belegt.** Stichprobe geprüft
(`früh→Früh`, `fallen→Fallen`, `großen→Großen`, `dichten→Dichten`,
`klarem→Klarem`, `dusche→Dusche`, `falle→Falle`): **alle sieben
Großschreibungen existieren tatsächlich** in der echten 258k-Substantiv-Liste
(`Falle`/`Fallen` = die Falle, `Dusche`, `Großen`/`Klarem`/`Schöne` =
nominalisierte Adjektive, `Morgens` = Genitiv, `Überblicken` = ein
nominalisiertes Verb, das durch den in `data/HERKUNFT.md` beschriebenen
Filter gerutscht ist) — und **keines** steht in `klein-schreiben.txt`. Das
ist echte deutsche Grammatik-Mehrdeutigkeit (dieselben Wörter sind je nach
Satz Adjektiv/Verb/Adverb *oder* Substantiv), keine neue Baustelle. Ein
blindes Eintragen in `klein-schreiben.txt` würde nur den umgekehrten Fehler
einführen (dann bliebe `die Fallen` fälschlich klein). Das ist exakt die
Lücke, die Plan-**Phase 4** (Satzkontext/Bigramme) schließen soll — bisher
nur an einer kleinen, kuratierten Wortliste belegt, jetzt an echtem
Fließtext mit einer konkreten Häufigkeit (9 von 171 Kontrollwörtern, ~5 %).

Ein zusätzlicher Einzelfall (`Tanen→Tanzen` statt `Tannen`) zeigt die
Kehrseite der Häufigkeits-Entscheidung ohne Kontext: „tanzen" ist als Verb
weitaus häufiger als das Substantiv „Tannen", weshalb die Umgebung
(„alten Eichen und Tannen") ohne Satzkontext nicht einfließen kann.

**Entscheidung:** Kein Code geändert, um diese Homographen-Fälle zu
kaschieren — jeder Versuch würde Präzision an anderer Stelle kosten. Als
Ratsche festgehalten (93,2 % als Mindeststand), damit künftige Änderungen
nicht unbemerkt schlechter werden. Der Test dient als **zweite, unabhängige
Nachweisquelle** für den Bedarf an Phase 4, ergänzend zu den Einzelwort-Fällen
in `benchmark-faelle.tsv`.

**Stand danach:** 146 Tests grün (vorher 145).

**Offene Punkte / Nächste Schritte:**
- Phase 4 (Kontext/Bigramme) ist jetzt durch zwei unabhängige Messungen
  begründet, nicht nur eine Vermutung.
- Alltagstest der Phase-1-Ersetzung durch den Nutzer steht weiterhin aus.

## 2026-09-05 – Phase 3: Tastatur-Distanz + Verkettung — beide Zielwerte erreicht

**Was:** Zwei neue Bausteine in der Korrektur-Logik.

1. **`KeyboardLayout.cs`** — QWERTZ-Tastatur-Nachbarschaft nach dem Vorbild
   der Aspell-`.kbd`-Dateien: eine Liste, welche Tasten in derselben Reihe
   nebeneinander liegen (inkl. `ü/ö/ä`). `SpellCorrector` gewichtet einen
   „falschen Buchstaben" jetzt höher (`0.9` statt `0.45`), wenn die getippte
   Taste neben der richtigen liegt — ein Danebengriff auf die Nachbartaste
   ist genauso plausibel wie ein vergessener Buchstabe.
2. **Verkettung Ersatzschreibung→Raten** (`OfflineCorrector.TryReplacementThenSpelling`)
   — für Wörter mit **zwei Fehlern auf einmal**: Erst die Ersatzschreibung
   auflösen (auch wenn das Ergebnis noch kein bekanntes Wort ist), dann den
   Rest vom Fuzzy-Raten erledigen lassen. Fängt `zustaedig→zustädig→zuständig`
   und `ausfuerhren→ausführhren→ausführen`.

**Ergebnis (Benchmark):**

| Kennzahl | Vorher (Phase 2) | Jetzt (Phase 3) | Ziel |
|---|---|---|---|
| Präzision | 99,0 % | 99,0 % (unverändert) | ≥ 98 % ✅ |
| Trefferquote | 86,8 % | **90,4 %** | ≥ 90 % ✅ |
| Fehlalarme | 1,3 % | 1,3 % (unverändert) | ≈ 0 % |

**Beide Zielwerte des Qualitätsplans sind damit erreicht.**

**Ein Fehlgriff beim Bauen entdeckt und sofort behoben:** Die neue Verkettung
hätte `besser` (korrekt) über die Ersatzschreibung `beßer` zu einem seltenen,
aber existierenden Wort (`Beißer`) verfälscht — die „schon richtig, nicht
anfassen"-Absicherung aus Phase 2 fehlte in der Verkettung. Ergänzt, mit
eigenem Regressionstest (`VerkettungRuehrtEinBereitsKorrektesWortNichtAn`).
Genau das Risiko, vor dem im Recherche-Dokument bei `ss→ß` gewarnt wurde.

**Entscheidungen:**
- **Komposita-Zerlegung, volle Editierdistanz-2-Suche und Kölner Phonetik
  wurden bewusst zurückgestellt**, weil beide Zielwerte bereits ohne sie
  erreicht waren. Die verbleibenden elf übersehenen Fälle
  (`korogirt`, `personlaissierter`, `iennfach`, `shcnlell` …) sind mehrfache
  Vertipper, die eine echte Distanz-2-Suche bräuchten — die wäre ohne
  Komposita-Schutz riskant (zerstört echte Wortzusammensetzungen) und ohne
  SymSpell-artigen Index spürbar langsam. Aufwand und Risiko stehen für den
  verbleibenden Nutzen nicht mehr im Verhältnis. Ausführliche Begründung im
  Plan-Nachtrag.
- Tastatur-Nachbarschaft bewusst **nur Nachbarn in derselben Reihe**
  (keine Diagonalen) — einfach, defensiv, deckt den wichtigen Teil der Fälle.

**Stand danach:** 145 Tests grün (vorher 131).

**Offene Punkte / Nächste Schritte:**
- Alltagstest der Phase-1-Ersetzung durch den Nutzer steht weiterhin aus.
- Der eine verbliebene Fehlalarm `codest→Codes` (Fuzzy-Raten) — noch nicht
  untersucht, gehört zu keinem der bisherigen Bausteine.
- Komposita-Zerlegung/Distanz-2/Kölner Phonetik: zurückgestellt, siehe oben —
  erst bei konkretem neuem Bedarf wieder aufgreifen.
- **Als Nächstes (auf Wunsch des Nutzers):** ein Test mit einem
  zusammenhängenden ~200-Wörter-Fließtext, um die Korrekturquote im
  realistischen Lesefluss zu prüfen — anders als der bestehende Benchmark,
  der einzelne Wörter isoliert testet.

## 2026-09-05 – Phase 2: Ersatzschreibung (ue/oe/ae/ss) — Präzisionsziel erreicht

**Was:** Neue Klasse `ReplacementTable.cs` mit den deutschen Ersatzschreibungen
nach Hunspell-`REP`-Vorbild (`ue→ü`, `oe→ö`, `ae→ä`, `ss→ß`, jeweils auch
großgeschrieben). `OfflineCorrector` wendet sie **vor** dem Fuzzy-Raten an,
als neue Quelle `CorrectionSource.Replacement`.

**Absicherung (wichtigster Teil der Änderung):** Eine Auflösung wird nur
übernommen, wenn (a) das getippte Wort selbst **unbekannt** ist und (b) genau
eine Auflösung ein **bekanntes** Wort ergibt. Ohne diese Prüfung würde z. B.
`Masse` (korrekt) zu `Maße` (ein anderes Wort) verfälscht — beide sind
gültige deutsche Wörter, nur eine reine `ss→ß`-Ersetzung ohne Gegenprüfung
könnte das nicht unterscheiden.

**Warum:** Die Benchmark-Messung aus Phase 0 zeigte, dass **alle vier**
protokollierten Falschersetzungen Umlaut-Ersatzschreibung waren
(`moechte→mochte`, `waehrend→wahrend`, `gruen→grauen`, `schoen→Schonen`) —
der mit Abstand größte Hebel für Präzision.

**Ergebnis (Benchmark vorher/nachher):**

| Kennzahl | Phase 0 | Phase 2 | Ziel |
|---|---|---|---|
| Präzision | 94,9 % | **99,0 %** | ≥ 98 % ✅ erreicht |
| Trefferquote | 82,5 % | 86,8 % | ≥ 90 % |
| Fehlalarme | 1,3 % | 1,3 % (unverändert) | ≈ 0 % |

Alle vier Falschersetzungen aus Phase 0 sind weg. Der verbleibende eine
Fehlalarm (`codest→Codes`) stammt aus dem Fuzzy-Raten, nicht aus der
Ersatzschreibung, und ist nicht Teil dieser Änderung.

**Entscheidungen:**
- Nur die **eindeutigen** Tastatur-Ersatzformen übernommen (`ue/oe/ae/ss`),
  nicht die übrigen Hunspell-`REP`-Paare (`f/ph`, `d/t`, `ch/k` …) — die
  betreffen Rechtschreibunsicherheit bei ganzen Wortstämmen, nicht das Tippen
  von Umlauten, und hätten hier nur Fehlgriffe produziert.
- Die Ersatzschreibung läuft **auch auf möglichen Wortbruchstücken**
  (`AllowSpellGuess: false`) — anders als das Fuzzy-Raten, weil sie nicht
  rät, sondern nur eine feststehende Auflösung prüft.
- **Ratsche hochgesetzt** (`BenchmarkTests.cs`): Präzision 94,9 % → 99,0 %,
  Trefferquote 82,5 % → 86,8 %. Ein künftiger Rückschritt unter diese Werte
  lässt die Testsuite sofort rot werden.

**Stand danach:** 131 Tests grün (vorher 118).

**Offene Punkte / Nächste Schritte:**
- Trefferquote (86,8 %) liegt noch unter dem Ziel von 90 % — die 15
  verbleibenden übersehenen Fälle sind größtenteils Distanz-2-Vertipper
  (`korogirt`, `personlaissierter`, `ausfuerhren`) → Plan-Phase 3.
- Der eine verbliebene Fehlalarm `codest→Codes` — separat zu untersuchen,
  gehört nicht zur Ersatzschreibung.
- Alltagstest der Phase-1-Ersetzung durch den Nutzer steht weiterhin aus.

## 2026-09-05 – Phase 1: Wettlauf beim Ersetzen behoben, Passwortfelder ausgespart

**Was:** Drei Änderungen am Ersetz-Verhalten.

1. **Nachgetippte Zeichen werden mitgezählt.** `LiveCorrectionController.HandleWord`
   nimmt jetzt einen zweiten Parameter `typedSince`: die Zeichen, die der
   Nutzer seit der Wortgrenze schon getippt hat. Sie stehen auf dem Bildschirm
   zwischen Cursor und zu korrigierendem Wort und werden mitgelöscht und
   danach wieder mitgetippt. `TrayApp` sammelt sie in `_typedSinceWord`.
2. **Nach Enter wird nicht mehr korrigiert.** Bisher wurden `word.Length`
   Zeichen gelöscht, obwohl der Zeilenumbruch dazwischenstand — das löschte
   den Umbruch plus einen Teil des Wortes und schrieb Müll. Ihn stattdessen
   mitzulöschen und neu zu tippen ist auch keine Option: In Chat-Fenstern
   verschickt das die Nachricht ein zweites Mal.
3. **Passwortfelder werden erkannt** (`Win32.FocusedFieldIsPassword`): In
   einem Edit-Steuerelement mit dem Stil `ES_PASSWORD` wird weder korrigiert
   noch mitgeschrieben.

**Warum:** Die Messung in `debug.log` hatte gezeigt, dass die alte
Ersetz-Verzögerung von 130 ms unter dem realen Tastenabstand des Nutzers
(Median 188 ms) lag.

**Entscheidungen:**
- **Nicht einfach länger warten.** Das war der ursprüngliche Plan, ist aber
  falsch: Bei durchgehendem Tippen gibt es überhaupt keine Pause. Ein höherer
  Schwellwert hätte dazu geführt, dass Korrekturen ganz ausbleiben oder erst
  Wörter später kommen. Stattdessen wird jetzt **gar nicht** gewartet (Takt
  25 ms statt 40 ms) und der Versatz sauber verrechnet. Das ist gleichzeitig
  schneller *und* sicherer.
- **Nach Enter lieber nichts tun** als etwas kaputt zu machen — entspricht dem
  Grundsatz „Präzision vor Trefferquote".
- **Passwortfeld-Erkennung wird bewusst als schwache Zusatzsicherung
  dokumentiert**, nicht als Lösung: In Browsern, Electron- und WPF-Oberflächen
  gibt es kein eigenes Fensterhandle je Feld, dort greift sie nicht. Der
  Pause-Hotkey bleibt die eigentliche Absicherung. Alles andere wäre eine
  falsche Sicherheit, die zum Leichtsinn verleitet.

**Stand danach:** 118 Tests grün (vorher 115). Die Benchmark-Zahlen ändern
sich durch Phase 1 **nicht** — der Fehler steckte im Ersetzen im echten
Textfeld, nicht in der Korrekturlogik.

⚠️ **Noch nicht im Alltag verifiziert.** Die neue Ersetz-Logik ist durch
Tests abgedeckt (`BeruecksichtigtWasSeitDerWortgrenzeGetipptWurde`,
`UndoStelltAuchDasInzwischenGetippteWiederHer`, `KorrigiertNachEnterGarNicht`),
aber ob der Wettlauf im echten Betrieb wirklich weg ist, lässt sich nur durch
Tippen prüfen. **Nächste Aufgabe für den Nutzer:** Programm neu bauen,
schnell einen Absatz tippen und schauen, ob noch Wort-Bruchstücke entstehen.

**Offene Punkte / Nächste Schritte:**
- Alltagstest der Ersetzung durch den Nutzer (siehe oben).
- **Phase 2:** Umlaut-Ersetzungstabelle — laut Messung der größte Hebel.

## 2026-09-05 – Phase 0 erledigt: die Messlatte steht, Ausgangszahlen gemessen

**Was:** Der Benchmark aus Plan-Phase 0 ist gebaut und liefert zum ersten Mal
harte Zahlen.

- `tests/RechtschreibTrainer.Core.Tests/benchmark-faelle.tsv` — **206
  beschriftete Fälle** aus `keystrokes.log` und `korrekturen.jsonl`, in vier
  Kategorien: `tippfehler` (muss korrigiert werden), `gross` (klein getipptes
  Substantiv/Eigenname), `korrekt` (darf nicht angefasst werden) und `kontext`
  (nur mit Satzzusammenhang entscheidbar, wird getrennt ausgewiesen).
- `BenchmarkTests.cs` — rechnet Präzision, Trefferquote und Fehlalarmrate aus
  und listet jeden Fehlgriff namentlich auf.
- `RepoFiles.cs` — lädt die echten Wortlisten und Vertipper-Dateien genau in
  der Reihenfolge, in der das laufende Programm sie lädt. Die persönliche
  `woerterbuch.txt` bleibt bewusst außen vor: gemessen wird der
  Auslieferungszustand, nicht ein einzelner PC.

**Die Ausgangsmessung (2026-09-05, vor jeder Verbesserung):**

| Kennzahl | Stand | Ziel |
|---|---|---|
| Präzision (von allem, was angefasst wird: wie viel war richtig) | **94,9 %** (94 von 99) | ≥ 98 % |
| Trefferquote (von allen nötigen Änderungen) | **82,5 %** (94 von 114) | ≥ 90 % |
| Fehlalarme (korrektes Wort angefasst) | **1,3 %** (1 von 78) | ≈ 0 % |
| Kontextabhängige Fälle fälschlich angefasst | 8 von 14 | Phase 4 |

**Der wichtigste Befund: Alle vier Falschersetzungen sind Umlaut-Fälle.**

```
moechte  -> mochte    (erwartet: möchte)
waehrend -> wahrend   (erwartet: während)
gruen    -> grauen    (erwartet: grün)
schoen   -> Schonen   (erwartet: schön)
```

Dazu kommen sechs weitere übersehene Wörter aus demselben Grund (`fuer`,
`ueberhauot`, `naemlih`, `zustaedig`, `ausfuerhren`, `auffaekt`). Die
Ersatzschreibung `ue/oe/ae` ist damit **die mit Abstand größte einzelne
Fehlerquelle** — und genau das, was die Hunspell-`REP`-Tabelle aus Phase 2
ohne jedes Raten löst. `gruen → grauen` zeigt auch, warum: Weil `ue → ü`
unbekannt ist, rät der Korrektor stattdessen wild in der Wortliste herum.

**Weitere Erkenntnis:** Ein Teil der im Log protokollierten Fehlgriffe ist
durch die Fixes vom 2026-09-04 **bereits behoben**. `skill→kill`,
`fuer→Feuer`, `datri→dari`, `ganzen→Ganzen`, `icht→nicht`, `kkann→kann`,
`ernn→renn` und `obne→oben` passieren heute nicht mehr — das Programm lässt
diese Wörter jetzt in Ruhe. Die Ausgangslage ist also besser als der rohe Log
vermuten ließ.

**Verbleibender Fehlalarm:** `codest → Codes` — hier zerstört der Rateschritt
ein korrektes Wort.

**Entscheidungen:**
- Der Benchmark-Test arbeitet als **Ratsche**: Geprüft wird gegen den zuletzt
  erreichten Stand, nicht gegen das Endziel. Damit ist die Testsuite grün,
  schlägt aber sofort an, wenn eine Änderung die Qualität verschlechtert.
  Wird etwas besser, werden die Konstanten `StandPraezision` /
  `StandTrefferquote` / `StandFehlalarme` in `BenchmarkTests.cs` hochgesetzt.
  Ein dauerhaft roter Test wäre wertlos — den schaut nach zwei Wochen niemand
  mehr an.
- Kontextabhängige Fälle werden **getrennt** ausgewiesen und nicht in die
  Hauptkennzahlen eingerechnet. Sie sind vor Phase 4 prinzipiell nicht
  lösbar; sie in die Zahlen zu mischen, würde den Fortschritt verschleiern.

**Stand danach:** 115 Tests grün (vorher 113). Kein Produktivcode geändert —
nur Messung.

**Offene Punkte / Nächste Schritte:**
- **Phase 1:** Ersetz-Verzögerung (`SettleTime` 130 ms bei 188 ms echtem
  Tastenabstand) und automatische Passwortfeld-Erkennung. Achtung: Dieser
  Fehler ist im Benchmark **nicht sichtbar**, weil er beim Ersetzen im echten
  Textfeld entsteht, nicht in der Logik. Er braucht einen eigenen Test bzw.
  eine manuelle Prüfung.
- **Phase 2:** Umlaut-Ersetzungstabelle nach Hunspell-Vorbild — laut Messung
  der größte Hebel: behebt 4 von 5 Falschersetzungen und 6 von 16 übersehenen
  Fällen.

## 2026-09-05 – Qualitätsplan, Recherche und vollständige Systemdokumentation

**Was:** Kein Code geändert — drei neue Dokumente und eine überarbeitete
`CLAUDE.md`:

- **`docs/DATEIEN.md`** (neu) — erklärt den kompletten Ablauf beim Tippen
  Schritt für Schritt, was jede einzelne Datei tut, wo welche Daten liegen und
  welche Fallstricke bekannt sind. Ziel: Eine neue Session (Mensch oder KI)
  versteht das System, **ohne den Quellcode lesen zu müssen**.
- **`docs/superpowers/plans/2026-09-05-korrektur-qualitaet-plan.md`** (neu) —
  Plan, um die Korrekturqualität messbar auf ≥ 98 % Präzision und ≥ 90 %
  Trefferquote zu bringen. Sechs Phasen, jede mit einer Messgröße.
- **`docs/RECHERCHE-KORREKTURSYSTEME.md`** (neu) — was Hunspell, Aspell,
  SymSpell, LanguageTool, Kölner Phonetik und die Fachliteratur zum Thema
  beitragen, mit Quellen, Lizenzen und der Bewertung, was wir übernehmen.

**Warum:** Der Nutzer will eine Korrektur, die „zu 90 % perfekt" läuft und
sich weiter verbessert — und er will nicht, dass jede neue Session erst den
ganzen Code lesen muss, um zu verstehen, was das Programm ist.

**Die wichtigsten Befunde:**

1. **Ein Timing-Fehler erzeugt vermutlich die Hälfte aller Fehlgriffe.** Aus
   `debug.log` gemessen: Tastenabstand im Median **188 ms** (p10 = 172 ms).
   `TrayApp.SettleTime` wartet aber nur **130 ms** — die Ersetzung feuert also
   mitten im Weitertippen, die simulierten Rücktasten treffen das nächste
   Wort. Im Korrektur-Log stehen entsprechend viele Bruchstücke (`ernn`,
   `itoniert`, `nstellen`, `ondern`, `icht`, `hochgel`), die dann auch noch
   „korrigiert" wurden.
2. **Umlaut-Ersatzschreibung fehlt**: `fuer→Feuer` statt `für`,
   `moechte→mochte`. Hunspell löst das mit einer festen Ersetzungstabelle
   (28 deutsche Einträge), die **vor** jedem Raten greift.
3. **Groß-/Kleinschreibung ohne Satzkontext** ist prinzipiell nicht sicher:
   `ganzen→Ganzen` war belegt falsch.
4. **Deutsche Komposita sind ein unterschätztes Risiko** (`Kältekreislauf`,
   `Nutzungslimit`): Sie stehen in keiner Wortliste und sehen wie Tippfehler
   aus. Bei Abstand 1 harmlos, bei Abstand 2 gefährlich → Zerlegungsprüfung
   wurde als neue Phase 2b in den Plan aufgenommen.
5. **Unsere Wortliste ist unvollständig**: Vom LibreOffice-Wörterbuch wurden
   nur die Stämme übernommen, die Affix-Regeln nie ausgewertet.

**Entscheidungen:**
- Zielwerte getrennt nach Präzision (≥ 98 %), Trefferquote (≥ 90 %) und
  Fehlalarmen (≈ 0 %). Präzision zählt mehr: Eine falsche Ersetzung wiegt
  schwerer als ein übersehener Tippfehler.
- Reihenfolge **0 → 1 → 2** (erst messen, dann Timing, dann sichere
  Ersetzungen), abgestimmt mit dem Nutzer.
- Speicherbudget bis ~500 MB, Start darf 2–3 Sekunden dauern.
- Phase 4 (Kontext/Bigramme) ist gesetzt, mit der Auflage, dass das
  Wortfenster nur im Arbeitsspeicher lebt und nie in eine Datei gelangt.
- **Bei `SendInput` bleiben**, Timing reparieren. Das Text Services Framework
  wäre technisch sauberer (kein Wettlauf bauartbedingt), ist aber als
  COM-Textdienst im Prozess jeder Fremdanwendung zu aufwendig und für
  Virenscanner noch verdächtiger. Als Option dokumentiert.
- Neu aufgenommen: automatische Passwortfeld-Erkennung (`ES_PASSWORD`) als
  **zusätzliche** Absicherung — sie greift nicht in Browsern und modernen
  Oberflächen und ersetzt den Pause-Hotkey deshalb nicht.

**Stand danach:** Plan freigegeben, noch keine Zeile Code geändert. Build und
Tests unverändert grün (113 Tests).

**Offene Punkte / Nächste Schritte:**
- **Phase 0, erste Aufgabe:** Alle 82 Korrekturen aus `korrekturen.jsonl`
  gegen den heutigen Stand nachrechnen — ein Teil der protokollierten
  Fehlgriffe stammt aus der Zeit **vor** den Fixes vom 2026-09-04 (11:58 und
  12:17) und ist womöglich schon erledigt. Ergebnis ist die ehrliche
  Ausgangszahl.
- Danach Phase 1 (Timing + Passwortfelder), dann Phase 2 (sichere
  Ersetzungen: Hunspell-REP-Tabelle + gefilterte Wikipedia-Tippfehlerliste).

## 2026-09-05 – Feature-Branch nach main gemergt, Projekt-Doku nachgezogen

**Was:** Der Branch `feat/live-korrektur-offline` (9 Commits, seit
2026-09-03 auf GitHub gepusht, aber noch nicht in `main`) wurde per
`git merge --no-ff` nach `main` gemergt und gepusht (Merge-Commit
`ccde2ce`). Danach `CLAUDE.md` und dieses Log neu angelegt, da die
bisherigen Sessions das noch nicht getan hatten.

**Warum:** Sitzung wurde mit "weiter machen am GitHub-Projekt Autokorrektur"
begonnen. Vor dem Weiterbauen sollte der Branch-Stand konsolidiert und die
Doku auf den aktuellen Stand gebracht werden (Skill `projekt-workflow`).

**Stolperstein unterwegs:** Beim Merge blockierte Windows Defender aktiv den
Lese-/Schreibzugriff auf `src/RechtschreibTrainer/KeyboardHook.cs`
("enthält einen Virus oder möglicherweise unerwünschte Software") —
vermutlich ein Fehlalarm, da ein `WH_KEYBOARD_LL`-Hook technisch wie ein
Keylogger aussieht. Das brachte `git merge`/`git stash` kurz in einen
unfertigen Zustand (nichts verloren, beide Branches lagen vollständig auf
GitHub). Nutzer hat die Erkennung in Windows-Sicherheit zugelassen, danach
lief Merge, Build und Test sauber durch. **Der Code wurde bewusst nicht
verändert, um die Erkennung zu umgehen** — nur die Windows-Einstellung war
nötig.

**Entscheidungen:**
- Merge statt Rebase, damit die Historie des Feature-Branches sichtbar bleibt.
- Keine inhaltlichen Code-Änderungen in diesem Schritt, nur Konsolidierung + Doku.

**Stand danach:**
- `main` und `feat/live-korrektur-offline` sind identisch, beide auf GitHub aktuell.
- `dotnet build RechtschreibTrainer.sln` und `dotnet test RechtschreibTrainer.sln`
  laufen fehlerfrei durch: 113 Tests grün (109 Core + 4 Tray-nah).
- Live-Korrektur v1 ist vollständig umgesetzt: Wörterbuch- und Regel-basierte
  Korrektur, Satzanfang- und Substantiv-Großschreibung, Namen-Ausnahmeliste
  (23k Namen, exakte Schreibweise), Undo-Hotkey, konfigurierbare Hotkeys,
  Typing-Debounce (~130 ms) gegen Race mit Weitertippen.
- `CLAUDE.md` und `docs/PROJEKT-LOG.md` existieren jetzt und sind aktuell.

**Offene Punkte / Nächste Schritte:**
- `DictionaryDistiller` (laut Spec als v1.1 vorgesehen): `korrekturen.jsonl`
  automatisch zu neuen Wörterbuch-Kandidaten verdichten — noch nicht gebaut.
- v2 laut Spec (bewusst nicht in v1): KI-Ebene über `claude -p` für ganze
  Sätze, kontextabhängige Substantiv-Großschreibung, Umformulieren im
  Verkäufer-/Unternehmer-Stil. Dafür vorher: Passwortfeld-Erkennung, bevor
  Text das Gerät verlassen könnte (siehe Spec, Abschnitt „Sicherheit").
- Manuelle Checkliste für `WordWatcher`/`Replacer`/`TrayApp`/Hook (laut Spec
  nur manuell testbar) ist noch nicht als Dokument festgehalten.
