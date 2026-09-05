# Wie das Programm funktioniert — und was jede Datei tut

**Zweck dieser Datei:** Wer hier weiterarbeitet (Mensch oder KI), soll das
System verstehen, **ohne den Quellcode lesen zu müssen**. Wenn du Code
änderst, halte diese Datei mit aktuell.

---

## Teil 1: Was passiert, wenn du ein Wort tippst?

Der ganze Ablauf in einem Durchgang:

1. **Tastendruck abfangen.** `KeyboardHook` hängt sich systemweit in die
   Tastatur (`WH_KEYBOARD_LL`) und meldet jeden Anschlag als Zeichen,
   Rücktaste, Enter oder Navigationstaste. Eigene, simulierte Anschläge
   erkennt der Hook an einem Marker und **ignoriert sie** — sonst würde sich
   das Programm selbst zuhören.

2. **Wort zusammenbauen.** `WordWatcher` sammelt die Zeichen zum aktuellen
   Wort und merkt sich, ob gerade ein **Satzanfang** ist. Er verwirft das Wort,
   wenn der Zusammenhang verloren geht: Mausklick, Fokuswechsel, Pfeiltasten,
   Pos1/Ende, längere Pause. Nach so einem Bruch ist unklar, wo der Cursor
   steht — das nächste Wort könnte ein Bruchstück eines schon vorhandenen
   Wortes sein. Deshalb wird es mit dem Vermerk `AllowSpellGuess = false`
   gemeldet: exakte Wörterbuch-Treffer sind dann noch erlaubt, **Raten nicht**.

3. **Wortgrenze erreicht.** Sobald ein Leerzeichen, Enter oder `. , ! ? ; :`
   kommt, meldet der `WordWatcher` das fertige Wort samt Grenzzeichen und
   Kontext.

4. **Nachgetipptes mitzählen.** `TrayApp` legt das Wort zur Seite und
   korrigiert beim nächsten Timer-Takt (25 ms). Tippst du in der Zwischenzeit
   weiter, werden diese Zeichen in `_typedSinceWord` gesammelt: Sie stehen auf
   dem Bildschirm zwischen Cursor und Wort und werden bei der Ersetzung
   **mitgelöscht und danach wieder mitgetippt**. Deshalb muss auf keine
   Tipppause gewartet werden — was bei durchgehendem Tippen auch nie
   funktionieren würde.

5. **Korrektur bestimmen.** `OfflineCorrector` geht in dieser festen
   Reihenfolge vor und nimmt das erste Ergebnis:
   1. Steht das Wort in `nie-korrigieren.txt`? → **Finger weg**, Ende.
   2. Steht es im **Wörterbuch** (`falsch=richtig`)? → das nehmen.
   3. Greift eine **Regel** (`scg→sch`, Wortende `cg→ch`, Wortanfang
      `cih→ich`)? → das nehmen.
   4. Löst eine **Ersatzschreibung** (`ReplacementTable`: `ue→ü`, `oe→ö`,
      `ae→ä`, `ss→ß`) das Wort zu einem bekannten Wort auf? → das nehmen.
      Das ist **kein Raten** — wer `fuer` tippt, meint `für`, das steht nicht
      zur Debatte. Läuft auch auf einem möglichen Bruchstück, weil es nicht
      geraten, sondern nur aufgelöst wird. **Absicherung:** Ist das getippte
      Wort selbst schon bekannt, wird nichts angefasst — sonst würde `Masse`
      (korrekt) zu `Maße` (ein anderes Wort) verfälscht.
   5. Löst eine Ersatzschreibung **plus ein weiterer Vertipper** zusammen ein
      bekanntes Wort auf (`zustaedig` → `zustädig` (Ersatzschreibung, noch
      unbekannt) → `zuständig` (Raten))? → das nehmen. Anders als Schritt 4
      ist das ein echtes Raten (zwei Fehlerquellen kombiniert), läuft deshalb
      nur mit `AllowSpellGuess` und respektiert dieselbe „schon richtig"-Absicherung.
   6. Sonst **raten** lassen (`SpellCorrector`, nur wenn `AllowSpellGuess`).
   7. Zuletzt unabhängig davon: Ist **Satzanfang** und das Wort klein? → groß
      schreiben.

6. **Raten (nur wenn nötig).** `SpellCorrector` arbeitet gegen die große
   Wortliste und ist bewusst feige:
   - Wörter unter 4 Zeichen werden gar nicht angefasst, unter 5 Zeichen wird
     nicht geraten.
   - Ist das Wort ein **Eigenname** (`GitHub`, `iPhone`, `Montag`), wird nur
     die exakte Schreibweise erzwungen.
   - Ist das Wort **bekannt**, passiert nichts — außer es ist ein klein
     getipptes Substantiv, dann wird es großgeschrieben.
   - Sonst werden Kandidaten mit **Damerau-Abstand 1** gebildet (ein
     Buchstabe vergessen, zu viel, vertauscht oder falsch). Bei genau 5
     Zeichen nur die harmlosen Varianten (vergessen/vertauscht), ab 6 Zeichen
     alle.
   - **Großschreibung bei mehrdeutigen Wörtern**: Für eine kleine, kuratierte
     Liste bekannter Mehrdeutigkeiten (`mehrdeutige-substantive.txt` — z. B.
     `fallen`/`Fallen`, `dusche`/`Dusche`, `gucken`/`Gucken`) wird nur dann
     großgeschrieben, wenn direkt davor ein Artikel oder Possessivpronomen
     stand (`Determiners.cs`, von `WordWatcher` als letztes fertiges Wort
     mitverfolgt). Ohne diesen Beleg bleibt die häufigere, klein geschriebene
     Lesart die sicherere Annahme. Bekannte Grenze: Steht zwischen Artikel und
     Substantiv noch ein Adjektiv (`den großen Fluss`), hilft ein einzelnes
     Vorwort nicht — das bräuchte echtes Parsen.
   - Bewertung: Zuerst zählt die **Fehlerart** (vergessen/vertauscht gelten
     als wahrscheinlicher als danebengegriffen), erst danach die
     **Worthäufigkeit**. Ein „falscher Buchstabe" zählt dabei **nicht
     einheitlich**: Liegt die getippte Taste auf der QWERTZ-Tastatur direkt
     neben der richtigen (`KeyboardLayout.AreNeighbours`, z. B. `e`/`r`), wird
     das genauso hoch gewertet wie ein vergessener Buchstabe — eine Taste von
     der anderen Tastaturhälfte dagegen niedriger. Der beste Kandidat muss den
     zweitbesten um den Faktor **1,6** schlagen — sonst wird **nichts** ersetzt.

7. **Ersetzen.** `LiveCorrectionController` rechnet aus, wie viele Zeichen weg
   müssen (Wort + Grenzzeichen + inzwischen Getipptes), und ruft `Replacer`:
   N Rücktasten, dann der neue Text als Unicode-Eingabe (`SendInput`).
   **Nach Enter wird nicht korrigiert** — der Zeilenumbruch stünde zwischen
   Cursor und Wort, und ihn neu zu tippen würde in Chat-Fenstern die Nachricht
   ein zweites Mal abschicken.

   Außerdem: In einem klassischen Windows-Passwortfeld wird **weder gelesen
   noch geschrieben** (`Win32.FocusedFieldIsPassword`). Das greift aber nicht
   in Browsern und modernen Oberflächen — der Pause-Hotkey bleibt die
   eigentliche Absicherung.

8. **Merken.** Die Korrektur wird als eine JSON-Zeile an `korrekturen.jsonl`
   angehängt (nur `vorher → nachher → Quelle`, **kein Fließtext**) und für
   „Rückgängig" im Speicher behalten.

9. **Rückgängig (`Strg+Alt+Z`).** Setzt die letzte Ersetzung zurück **und**
   trägt das Wort in `nie-korrigieren.txt` ein — derselbe Fehlgriff kommt
   damit nie wieder.

10. **Lernen beim nächsten Programmstart.** `DictionaryLoader` liest beim
    Start `korrekturen.jsonl` und zählt, wie oft welches Wort gleich
    korrigiert wurde (`DictionaryDistiller`). Ab **3 gleichen Treffern**
    wandert das Paar automatisch in `woerterbuch.txt` — mit Kommentarzeile
    und Datum, damit sichtbar bleibt, was automatisch dazukam. Eine
    Tray-Benachrichtigung zeigt an, welche Wörter gelernt wurden.
    **Ausgeschlossen sind absichtlich:** Wörter, die schon im Wörterbuch oder
    auf der Nie-Liste stehen; Satzanfang-Großschreibungen (`ich→Ich` ist eine
    reine Positionsregel, kein feststehender Rechtschreibfehler — als fester
    Eintrag würde „ich" plötzlich überall im Satz großgeschrieben); und die
    mehrdeutigen Substantive aus `mehrdeutige-substantive.txt` (brauchen
    weiterhin Satzkontext, dürfen nicht zur festen Regel werden).

---

## Teil 2: Die Projektstruktur

```
rechtschreibung-windows-verbessern/
├── CLAUDE.md                  Einstieg für die nächste Session
├── README.md                  Nutzersicht: was das Programm tut
├── RechtschreibTrainer.sln    Visual-Studio-Projektmappe
├── data/                      große Wortlisten (NICHT in Git, s. u.)
├── docs/                      diese Dokumentation
├── scripts/install.ps1        Installation + Autostart
├── src/RechtschreibTrainer.Core/    reine Logik, testbar
├── src/RechtschreibTrainer/         Windows-Programm (Tray, Hooks)
└── tests/                     xUnit-Tests
```

### `src/RechtschreibTrainer.Core/` — die reine Logik

Kein Windows, keine Dateizugriffe im Kern, alles unit-testbar.

| Datei | Was sie tut |
|---|---|
| `WordWatcher.cs` | Baut aus einzelnen Tastenereignissen das aktuelle Wort, verfolgt den Satzanfang, verwirft bei Kontextverlust. Liefert `WordCompleted`. |
| `OfflineCorrector.cs` | Die Entscheidungskette (Nie-Liste → Wörterbuch → Regeln → Raten → Satzanfang). Definiert auch `WordContext`, `CorrectionResult` und `CorrectionSource`. |
| `CorrectionDictionary.cs` | Feste Ersetzungen im Format `falsch=richtig`. Späterer Eintrag gewinnt. Kennt den Groß-/Kleinschreibungs-Sonderfall am Satzanfang. |
| `CorrectionRules.cs` | Drei fest verdrahtete Muster-Regeln: `scg→sch` überall, `cg→ch` am Wortende, `cih→ich` am Wortanfang. |
| `ReplacementTable.cs` | Feste Ersatzschreibungen nach Hunspell-`REP`-Vorbild: `ue→ü`, `oe→ö`, `ae→ä`, `ss→ß`. Liefert Kandidaten; `OfflineCorrector` prüft, ob einer davon ein bekanntes Wort ergibt — oder speist ihn zusätzlich ins Raten ein, wenn noch ein zweiter Fehler dazukommt. |
| `KeyboardLayout.cs` | QWERTZ-Tastatur-Nachbarschaft (nach Aspell-`.kbd`-Vorbild, nur Nachbarn in derselben Reihe). Sagt `SpellCorrector`, ob ein falscher Buchstabe ein plausibler Danebengriff war. |
| `Determiners.cs` | Artikel/Possessiv-/Demonstrativpronomen (der/die/das/ein/mein/…). Signalisiert, dass das nächste Wort vermutlich ein Substantiv ist. |
| `SpellCorrector.cs` | Das Raten gegen die große Wortliste (Damerau-Abstand 1, Gewichte, Dominanz-Schwelle). `SpellSettings` hält die Stellschrauben. |
| `WordList.cs` | Die geladenen Wortlisten im Speicher: kennt Wörter, Häufigkeiten, Substantive, Eigennamen und die „bleibt klein"-Ausnahmen. |
| `LiveCorrectionController.cs` | Bindeglied: nimmt fertige Wörter, holt die Korrektur, stößt Ersetzung + Protokoll an, verwaltet Rückgängig. Kennt kein Windows — Ersetzen und Loggen sind Rückrufe. |
| `LearnStore.cs` | Hängt eine Korrektur als JSON-Zeile an `korrekturen.jsonl` an; liest sie beim Start auch wieder ein (`ReadAll`). |
| `DictionaryDistiller.cs` | Zählt (vorher,nachher)-Paare aus dem Korrektur-Log; ab einem Schwellwert ein Vorschlag fürs Wörterbuch. Schließt Satzanfang-Großschreibung grundsätzlich aus (siehe Ablauf, Schritt 10). |
| `HotkeySpec.cs` | Wandelt Text wie `Strg+Alt+R` in Windows-Tastencodes. |

### `src/RechtschreibTrainer/` — das Windows-Programm

| Datei | Was sie tut |
|---|---|
| `Program.cs` | Einstiegspunkt. Startet `TrayApp`, schreibt unbehandelte Fehler ins Debug-Log. |
| `TrayApp.cs` | **Die Schaltzentrale.** Tray-Icon, Menü, Hotkeys, beide Hooks, die zwei Timer (Ersetz-Verzögerung 40 ms Takt, Fokusprüfung jede Sekunde), Pause und Mitschreib-Modus. |
| `KeyboardHook.cs` | Systemweiter Tastatur-Hook. Meldet Zeichen, Rücktaste, Enter, Navigationstasten. Ignoriert eigene simulierte Eingaben per Marker. ⚠️ Wird von Windows Defender gern fälschlich als Virus gemeldet. |
| `MouseHook.cs` | Meldet Mausklicks, damit der Wort-Kontext verworfen wird. |
| `Replacer.cs` | Führt die Ersetzung per `SendInput` aus (Rücktasten + Unicode-Text). |
| `DictionaryLoader.cs` | Lädt die Wörterbücher in der richtigen Reihenfolge und die große Wortliste. Fehlen die Datendateien, läuft das Programm ohne Rechtschreibprüfung weiter. |
| `HotkeySettings.cs` | Liest `tasten.txt`, legt sie beim ersten Start an. |
| `NeverCorrectStore.cs` | Verwaltet `nie-korrigieren.txt`; neue Einträge wirken sofort. |
| `AppPaths.cs` | **Alle Dateipfade an einem Ort.** Erste Anlaufstelle bei der Frage „wo liegt was?". |
| `IconFactory.cs` | Zeichnet die Tray-Icons zur Laufzeit: grün = bereit, blau = korrigiert gerade, grau = pausiert, roter Punkt = Mitschreiben läuft. |
| `DebugLog.cs` | Diagnose-Protokoll. **Nur aktiv, wenn `debug.log` oder `debug.on` im Datenordner liegt** — sonst wird nichts geschrieben. |
| `HotkeyForm.cs` | Unsichtbares Fenster; empfängt Hotkey-Nachrichten und dient als Ziel, um Hook-Ereignisse auf den UI-Thread zu holen. |
| `Win32.cs` | Die Windows-Funktionsaufrufe (`RegisterHotKey`, `GetForegroundWindow` …). |
| `standard-vertipper.txt` | Mitgelieferte Vertipper-Liste (deine ersten Mitschnitte + häufige deutsche Dreher). |
| `klassische-fehler.txt` | Mitgelieferte Liste klassischer Rechtschreibfehler (`seperat=separat`, `Standart=Standard` …). Nur eindeutige Fälle, nichts Kontextabhängiges. |
| `denglisch-verben.txt` | Eingedeutschte englische Tech-Verben (`coden`, `committen`, `pushen`, `mergen`, `deployen` …) als **bekannte Wörter** (keine Ersetzung) — verhindert, dass z. B. `codest` fälschlich zu `Codes` geraten wird. |
| `mehrdeutige-substantive.txt` | Wörter, die oft etwas anderes sind (Verb/Adjektiv) und nur mit vorangehendem Artikel großgeschrieben werden (`fallen`, `dusche`, `gucken`, `aktiv`, `drei` …). |

### `tests/`

| Ordner | Inhalt |
|---|---|
| `RechtschreibTrainer.Core.Tests/` | Unit-Tests der reinen Logik plus zwei Qualitäts-Benchmarks gegen die **echten** Wortlisten: `BenchmarkTests.cs` (206 isolierte, aus den echten Logs beschriftete Einzelwörter — Präzision/Trefferquote/Fehlalarme) und `PassageBenchmarkTests.cs` (ein zusammenhängender 238-Wörter-Fließtext mit programmgesteuert erzeugten Vertippern — Gesamtquote im Lesefluss). Beide laufen als **Ratsche**: Konstanten im Testcode hochsetzen, wenn sich etwas verbessert; ein Rückschritt lässt die Suite sofort rot werden. |
| `RechtschreibTrainer.Tests/` | 4 Tests der Windows-nahen Teile (Icon, Replacer-Struktur). |

---

## Teil 3: Die Dateien auf der Festplatte

### Mitgeliefert (neben der `.exe`, kommen aus `data/` im Repo)

| Datei | Inhalt | Größe |
|---|---|---|
| `data/woerter.txt` | deutsche Wortformen | 881.698 Zeilen, 11 MB |
| `data/haeufigkeit.txt` | Wort + Häufigkeit | 200.000 Zeilen, 2,6 MB |
| `data/substantive.txt` | großgeschriebene Substantivformen | 258.182 Zeilen, 3,3 MB |
| `data/namen.txt` | Eigennamen in exakter Schreibweise | 23.571 Zeilen |
| `data/klein-schreiben.txt` | Wörter, die klein bleiben müssen | ~1.500 Zeilen |

⚠️ **Diese Dateien liegen NICHT in Git** (`.gitignore` schließt `data/` aus).
Nach einem frischen `git clone` fehlen sie, und das Programm läuft dann ohne
Rechtschreibprüfung. Neu erzeugen: die `curl`-Befehle in **`data/HERKUNFT.md`**
(dort stehen auch Quellen und Lizenzen aller Listen).

### Deine Daten (`Dokumente\RechtschreibTrainer\`)

| Datei | Inhalt | Wer schreibt |
|---|---|---|
| `woerterbuch.txt` | deine eigenen Vertipper, **Vorrang vor allem** | du |
| `nie-korrigieren.txt` | Wörter, die nie angefasst werden | wächst bei jedem Rückgängig |
| `eigene-namen.txt` | deine Eigennamen in exakter Schreibweise | du |
| `tasten.txt` | Tastenbelegung | beim ersten Start angelegt |
| `korrekturen.jsonl` | jede Korrektur als `vorher → nachher → Quelle` | das Programm |
| `keystrokes.log` | mitgeschriebener Text — **nur im Mitschreib-Modus** | das Programm |
| `debug.log` | Diagnose — nur wenn die Datei existiert | das Programm |

**Datenschutz:** Nichts davon verlässt je den Rechner; das Programm hat keinen
Netzzugriff. Der Mitschreib-Modus ist ausdrücklich opt-in und protokolliert
alles Getippte — **vor Passworteingaben ausschalten**. Im normalen Betrieb
wird nur `vorher → nachher` echter Korrekturen gespeichert, kein Fließtext.

---

## Teil 4: Bedienung

| Taste | Wirkung |
|---|---|
| `Strg+Alt+P` | Live-Korrektur an/aus (Icon grün ↔ grau) |
| `Strg+Alt+Z` | letzte Korrektur rückgängig + Wort auf die Nie-Liste |
| `Strg+Alt+R` | Mitschreib-Modus an/aus (roter Punkt im Icon) |

Alle drei sind über `tasten.txt` änderbar (Neustart nötig). Dasselbe geht über
das Tray-Menü, das auch dann funktioniert, wenn eine Taste sich nicht
registrieren ließ.

**Starten:** `dotnet run` in `src/RechtschreibTrainer`.
**Installieren mit Autostart:** `powershell -ExecutionPolicy Bypass -File scripts\install.ps1`
(kopiert nach `%LOCALAPPDATA%\RechtschreibTrainer`, legt eine Verknüpfung im
Autostart an). **Entfernen:** derselbe Befehl mit `-Uninstall` — deine Daten in
`Dokumente\RechtschreibTrainer` bleiben dabei erhalten.

---

## Teil 5: Bekannte Probleme und Fallstricke

**Diese Punkte sind teuer wiederzuentdecken — bitte hier nachlesen, bevor du
irgendwas debuggst.**

1. **Auf eine Tipppause zu warten ist der falsche Ansatz** (behoben am
   2026-09-05, aber merken, damit es nicht zurückkommt). Früher wartete
   `TrayApp` auf 130 ms Tastenruhe. Das war doppelt falsch: Der gemessene
   Tastenabstand des Nutzers liegt im Median bei **188 ms** — 130 ms Ruhe gibt
   es also ständig *während* des Tippens, die Ersetzung feuerte mittendrin und
   die Rücktasten fraßen das nächste Wort an. Und ein *längerer* Wert hätte es
   schlimmer gemacht: Bei durchgehendem Tippen gäbe es nie eine Pause, in der
   korrigiert werden dürfte, und die Korrekturen fielen ganz aus. Die Lösung
   ist, **nicht zu warten**, sondern die inzwischen getippten Zeichen
   mitzuzählen und mitzuersetzen.

2. **Windows Defender hält `KeyboardHook.cs` für einen Virus.** Fehlalarm,
   weil ein systemweiter Tastatur-Hook technisch wie ein Keylogger aussieht.
   Symptome: Build bricht mit „enthält einen Virus" ab, **oder git-Befehle
   scheitern an dieser einen Datei**. Behebung: Windows-Sicherheit →
   Schutzverlauf → zulassen, oder Projektordner als Ausnahme. **Den Code
   nicht umschreiben, um der Erkennung zu entgehen.**

3. **`SendInput` verwirft alles kommentarlos, wenn `cbSize` nicht stimmt.**
   Die `INPUT`-Struktur muss das vollständige Union mit `MOUSEINPUT`
   enthalten, sonst ist sie zu klein und **jede** Ersetzung schlägt still
   fehl. Das hat schon einmal einen halben Tag gekostet (Commit `42ac52d`).

4. **Der Hook muss seine eigenen Eingaben erkennen.** Simulierte Anschläge
   tragen einen Marker; ohne ihn hört sich das Programm selbst zu und löst
   Endlosschleifen aus.

5. **Zusammengesetzte Wörter sind in keiner Wortliste.** `Kältekreislauf`,
   `Nutzungslimit` — Deutsch bildet beliebig viele davon. Sie sehen für das
   Programm wie Tippfehler aus. Solange nur Abstand 1 geraten wird, passiert
   meist nichts; **bei Abstand 2 wäre das gefährlich** (siehe Plan, Phase 2b).

6. **Nach einem Cursor-Sprung wird nicht geraten.** `AllowSpellGuess = false`
   ist Absicht, kein Fehler: Der Cursor könnte mitten in einem bestehenden
   Wort stehen, und das „fertige" Wort wäre nur ein Bruchstück.

7. **Tests brauchen die `data/`-Dateien.** `RealDataSpellingTests` sucht
   `data/woerter.txt` in den übergeordneten Ordnern. Fehlen die Dateien,
   scheitert dieser Test — nicht der Code ist kaputt, die Daten fehlen.

---

## Weiterlesen

- **`docs/PROJEKT-LOG.md`** — was wann warum gemacht wurde, aktueller Stand
- **`docs/superpowers/plans/2026-09-05-korrektur-qualitaet-plan.md`** — der Plan zur Qualitätssteigerung
- **`docs/RECHERCHE-KORREKTURSYSTEME.md`** — wie andere Systeme das lösen, mit Quellen
- **`docs/superpowers/specs/2026-09-03-live-korrektur-offline-design.md`** — das ursprüngliche Design
- **`data/HERKUNFT.md`** — Herkunft und Lizenz aller Wortlisten
