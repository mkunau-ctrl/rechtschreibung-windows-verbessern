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
   das Programm selbst zuhören. Shift/AltGr/CapsLock liest er per
   `GetAsyncKeyState` (seit Runde 3 — vorher kamen nie Großbuchstaben, `?`
   oder `@` an). Tastenkürzel (Strg+…, Alt+…, Win+…), Steuerzeichen und
   Tottasten melden einen **Kontextbruch** (`KeyInput`). Einzelne Anschläge
   werden **nirgends** protokolliert, auch nicht im Debug-Log.

2. **Wort zusammenbauen.** `WordWatcher` sammelt die Zeichen zum aktuellen
   Wort und merkt sich, ob gerade ein **Satzanfang** ist. Er verwirft das Wort,
   wenn der Zusammenhang verloren geht: Mausklick, Fokuswechsel, Pfeiltasten,
   Pos1/Ende, Tastenkürzel. (Eine **Tipppause** bricht den Kontext seit Runde 3
   nicht mehr — sie verhinderte, dass 12 % aller Wörter geraten wurden.) Nach
   so einem Bruch ist unklar, wo der Cursor steht — das nächste Wort könnte
   ein Bruchstück eines schon vorhandenen Wortes sein. Deshalb wird es mit
   dem Vermerk `AllowSpellGuess = false` gemeldet: exakte Wörterbuch-Treffer
   sind dann noch erlaubt, **Raten nicht**.

   Außerdem setzt der `WordWatcher` diese Vermerke (Runde 3):
   - **`DoNotTouch`** — ganzer Token (bis zum nächsten Leerraum) mit `@ / \ _
     # = < > | ~ :` usw., Wörter mit Ziffern (`s24`), Abkürzungen (`bzw.`).
     E-Mail-Adressen, URLs und Pfade werden so nie angefasst.
   - **`IsSecret`** — die 3 Tokens nach einem Passwort-Auslösewort
     (`SecretTriggers`: passwort, kennwort, pin, tan, login, zugangscode …):
     nicht korrigieren, nicht protokollieren, nicht als Kontext merken.
     Endet bei Enter; ein Satzzeichen *im* Passwort beendet ihn nicht.
   - **Satzanfang** nur, wenn nach `. ! ?` wirklich Leerraum folgt, nicht nach
     Abkürzungen (`Abbreviations`) und Ordinalzahlen (`am 19.`).
   - **Substantiv-Beleg** (`PrecededByDeterminer`, historischer Name):
     Artikel/Possessiv, Kontraktion (`am im zum beim …`), Zahl (`30 sekunden`)
     oder Artikel + dekliniertes Adjektiv (`die ganze zeit`) davor.

3. **Wortgrenze erreicht.** Sobald ein Leerzeichen, Enter oder `, ! ? ;`
   kommt, meldet der `WordWatcher` das fertige Wort samt Grenzzeichen und
   Kontext. Bei `.` und `:` wartet er auf das nächste Zeichen (`gmx.de` oder
   Satzende?) und meldet das Wort dann mit `TypedAfterBoundary`; kommt
   600 ms nichts, meldet `TrayApp` es trotzdem.

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
   2. Steht es im **Wörterbuch** (`falsch=richtig`)? → das nehmen. Das
      Wörterbuch ist die Summe aus `haeufige-fehler.txt` (automatisch aus der
      Wikipedia-Tippfehlerliste erzeugt), `klassische-fehler.txt`,
      `standard-vertipper.txt` und deiner persönlichen `woerterbuch.txt` —
      in dieser Reihenfolge geladen, spätere gewinnen bei einem Konflikt.
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
   7. **Schlussprüfung:** Was geraten/per Regel erzeugt wurde, muss aus
      bekannten Wörtern bestehen, sonst bleibt das Wort stehen.
   8. Zuletzt unabhängig davon: Ist **Satzanfang** und das Wort klein? → groß
      schreiben.
   Vorher: `DoNotTouch`/`IsSecret` → **Finger weg**, nichts wird geloggt.

6. **Raten (nur wenn nötig).** `SpellCorrector` arbeitet gegen die große
   Wortliste und ist bewusst feige:
   - Unter 3 Zeichen nichts. **3–4 Zeichen** (Runde 3): nur Doppelbuchstaben
     (`einn`→`ein`), vertauschte oder vergessene Buchstaben, und nur zu einem
     **sehr häufigen** Wort (≥ 5.000 im Korpus), das den Zweitplatzierten
     **zehnfach** schlägt (`ihc`→`ich`, `mla`→`mal`, `ghet`→`geht`; `mch`
     bleibt stehen, weil mach/mich gleich häufig sind). Bekannte 3-Zeichen-
     Wörter nur, wenn sie bloß ein seltener Name sind.
   - Ist das Wort ein **Eigenname** (`GitHub`, `iPhone`, `Montag`), wird die
     exakte Schreibweise erzwungen — **außer** der Name ist klein ein
     gewöhnliches Wort und im Korpus meist klein (`anders`, `lasse`).
   - Ist das Wort **bekannt**: Ist es extrem selten (≤ 200) und ergibt ein
     vertauschter/vergessener Buchstabe ein ≥ 500× häufigeres Wort, war es ein
     **Echtwort-Fehler** (`genua`→`genau`, `mienen`→`meinen`; nur klein
     getippt). Sonst wird höchstens großgeschrieben (siehe unten).
   - Unbekannt, aber **gültig**: Bindestrich-Wort (`E-Mails`), abgetrenntes
     Stück (`Fleisch-`), Genitiv eines Namens (`Lenas`), zerlegbares
     **Kompositum** (`CompoundSplitter`: `Transportwege`, `Wartebereich`) →
     nicht raten (höchstens großschreiben, wenn der Kopf ein Substantiv ist).
     Ausnahme: ein häufiges Wort (≥ 20) ist nur einen Buchstaben entfernt
     (`mittlerweilen`→`mittlerweile`).
   - **Doppelbuchstaben** (`kannn`, `allles`, `kommmmt`) zuerst.
   - Sonst Kandidaten mit **Damerau-Abstand 1**. Bei genau 5 Zeichen nur die
     harmlosen Varianten (vergessen/vertauscht), ab 6 Zeichen alle. Ein
     vergessener **erster** Buchstabe wird nie ergänzt (`anderern` wurde zu
     `Wanderern`). Ist ein Kandidat einer fast gleich plausiblen Fehlerart
     ≥ 1.000× häufiger, gewinnt er.
   - Nichts mit Abstand 1: **fehlende Leertaste** (`FunctionWords`:
     `damitich`→`damit ich`, `vorallem`→`vor allem`, `demspiel`→`dem Spiel`),
     dann **zwei Fehler** ab 6 Zeichen, nur vertauscht/vergessen/zu viel und
     nicht „zu viel + vergessen" (`vileicht`→`vielleicht`), eindeutig.
   - **Großschreibung** (`WordList.IsCapitalisedOnly`, Runde 3): Der Korpus
     `data/schreibung.txt` sagt, wie oft ein Wort **mitten im Satz** groß
     steht. ≥ 90 % → groß; 20–90 % → nur mit Substantiv-Beleg; darunter →
     nie (`ich würde`, `leider`, `alle drei`). Ohne Korpusbeleg: klein
     gültige Wörter (`klein-gueltig.txt`) nur mit Beleg. Nach `zum`/`beim`/…
     wird ein Infinitiv groß (`zum Testen`). `mehrdeutige-substantive.txt`
     und `klein-schreiben.txt` verschärfen nur (Beleg nötig), erlauben nie
     etwas gegen den Korpus.
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

   **Rücktaste als Ablehnung (Runde 3):** Löschst du direkt nach einer
   Korrektur mit der Rücktaste ins korrigierte Wort hinein (bevor das nächste
   Wort fertig ist), landet ein `Rejected`-Eintrag in `korrekturen.jsonl`. Ab
   **2** Ablehnungen desselben Worts → `nie-korrigieren.txt` (mit
   Benachrichtigung). In den echten Daten wurde Strg+Alt+Z nie benutzt.

10. **Lernen beim nächsten Programmstart.** `DictionaryLoader` liest beim
    Start `korrekturen.jsonl` und zählt, wie oft welches Wort gleich
    korrigiert wurde (`DictionaryDistiller`). Ab **3 gleichen Treffern**
    wandert das Paar automatisch in `woerterbuch.txt` — mit Kommentarzeile
    und Datum, damit sichtbar bleibt, was automatisch dazukam. Eine
    Tray-Benachrichtigung zeigt an, welche Wörter gelernt wurden.
    **Ausgeschlossen sind absichtlich:** Wörter, die schon im Wörterbuch oder
    auf der Nie-Liste stehen; Satzanfang-Großschreibungen (`ich→Ich` ist eine
    reine Positionsregel, kein feststehender Rechtschreibfehler — als fester
    Eintrag würde „ich" plötzlich überall im Satz großgeschrieben); seit
    Runde 3 **jede reine Groß-/Kleinschreib-Korrektur** (so waren `würde=Würde`,
    `weiß=Weiß`, `kriege=Kriege` fest eingebrannt worden — die Großschreibung
    entscheidet live der Satzkontext); abgelehnte Paare; und die
    mehrdeutigen Substantive aus `mehrdeutige-substantive.txt`.

---

## Teil 2: Die Projektstruktur

```
rechtschreibung-windows-verbessern/
├── CLAUDE.md                  Einstieg für die nächste Session
├── README.md                  Nutzersicht: was das Programm tut
├── RechtschreibTrainer.sln    Visual-Studio-Projektmappe
├── data/                      große Wortlisten (NICHT in Git, s. u.)
├── docs/                      diese Dokumentation
├── scripts/install.ps1              Installation + Autostart
├── scripts/fetch-fehlerlisten.ps1   erzeugt haeufige-fehler.txt neu (Entwicklung)
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
| `LetterConfusions.cs` | Buchstabenpaare, die trotz fehlender Tastatur-Nachbarschaft oft verwechselt werden (bisher nur i/e). Wird wie ein Tastatur-Nachbar gewichtet (`WeightConfusable`). |
| `Determiners.cs` | Artikel/Possessiv-/Demonstrativpronomen (der/die/das/ein/mein/…) und Kontraktionen (am/im/zum/beim/…). Signalisiert, dass das nächste Wort vermutlich ein Substantiv ist. |
| `Abbreviations.cs` | Abkürzungen mit Punkt (`bzw ca usw ggf …`): danach kein Satzanfang, selbst nie korrigiert. |
| `SecretTriggers.cs` | Passwort-Auslösewörter. Bewusst ohne „code" (der Nutzer schreibt oft „Claude Code"). |
| `FunctionWords.cs` | Geschlossene Wortklassen für die fehlende Leertaste; Präpositionen getrennt (die sind übliche Kompositum-Anfänge). |
| `CompoundSplitter.cs` | Zerlegt unbekannte Wörter in 2–3 bekannte Teile (je ≥ 4 Zeichen, Fugen s/es/n/en/e, Kopf kein Funktionswort) → Kompositum, nicht raten. |
| `KeyInput.cs` | Entscheidet pro Tastendruck: Text, Kontextbruch (Kürzel, Steuerzeichen, Tottaste) oder ignorieren. Testbar ohne Windows. |
| `SpellCorrector.cs` | Das Raten gegen die große Wortliste (Damerau-Abstand 1 und 2, Kurzwörter, Doppelbuchstaben, Echtwort-Fehler, fehlende Leertaste, Kompositum-Schutz, Gewichte, Dominanz). `SpellSettings` hält die Stellschrauben, die übrigen Schwellen sind Konstanten mit Kommentar. |
| `WordList.cs` | Die geladenen Wortlisten im Speicher: kennt Wörter, Häufigkeiten, Substantive, Eigennamen, Korpus-Großschreibung (`schreibung.txt`), klein gültige Wörter und die „bleibt klein"-Ausnahmen. Entscheidet die Großschreibung (`IsCapitalisedOnly`) und ob ein Name gemeint ist (`ProperNounReading`). |
| `LiveCorrectionController.cs` | Bindeglied: nimmt fertige Wörter, holt die Korrektur, stößt Ersetzung + Protokoll an, verwaltet Rückgängig und erkennt die Ablehnung per Rücktaste. Fasst `DoNotTouch`/`IsSecret`-Wörter nie an. Kennt kein Windows — Ersetzen und Loggen sind Rückrufe. |
| `LearnStore.cs` | Hängt eine Korrektur als JSON-Zeile an `korrekturen.jsonl` an; liest sie beim Start auch wieder ein (`ReadAll`). |
| `StatusTracker.cs` | Zählt angewandte Korrekturen (heute/gesamt, Pause, Laufzeit) und baut die `status.json` für den PC-Hub. Enthält nur Zähler und Zustand, nie getippten Text. |
| `StatusFileWriter.cs` | Schreibt die `status.json` atomar (Temp-Datei + Ersetzen), UTF-8 ohne BOM, wirft nie (best effort). |
| `DictionaryDistiller.cs` | Zählt (vorher,nachher)-Paare aus dem Korrektur-Log; ab einem Schwellwert ein Vorschlag fürs Wörterbuch. Schließt jede reine Groß-/Kleinschreibung und abgelehnte Paare aus (siehe Ablauf, Schritt 10). `RejectedOften` liefert die per Rücktaste oft abgelehnten Wörter. |
| `HotkeySpec.cs` | Wandelt Text wie `Strg+Alt+R` in Windows-Tastencodes. |

### `src/RechtschreibTrainer/` — das Windows-Programm

| Datei | Was sie tut |
|---|---|
| `Program.cs` | Einstiegspunkt. Startet `TrayApp`, schreibt unbehandelte Fehler ins Debug-Log. |
| `TrayApp.cs` | **Die Schaltzentrale.** Tray-Icon, Menü, Hotkeys, beide Hooks, die zwei Timer (Ersetz-Verzögerung 40 ms Takt, Fokusprüfung jede Sekunde), Pause und Mitschreib-Modus. |
| `KeyboardHook.cs` | Systemweiter Tastatur-Hook. Meldet Zeichen, Rücktaste, Enter, Navigationstasten und Kontextbrüche (Kürzel). Liest Zusatztasten per `GetAsyncKeyState`, ruft `ToUnicode` mit Flag 4 (Tottasten unberührt). Ignoriert eigene simulierte Eingaben per Marker. Protokolliert keine Anschläge. ⚠️ Wird von Windows Defender gern fälschlich als Virus gemeldet. |
| `MouseHook.cs` | Meldet Mausklicks, damit der Wort-Kontext verworfen wird. |
| `Replacer.cs` | Führt die Ersetzung per `SendInput` aus (Rücktasten + Unicode-Text). |
| `DictionaryLoader.cs` | Lädt die Wörterbücher in der Reihenfolge `haeufige-fehler.txt` → `klassische-fehler.txt` → `standard-vertipper.txt` → Benutzerdatei und die große Wortliste. Fehlen die Datendateien, läuft das Programm ohne Rechtschreibprüfung weiter. |
| `HotkeySettings.cs` | Liest `tasten.txt`, legt sie beim ersten Start an. |
| `NeverCorrectStore.cs` | Verwaltet `nie-korrigieren.txt`; neue Einträge wirken sofort. |
| `AppPaths.cs` | **Alle Dateipfade an einem Ort.** Erste Anlaufstelle bei der Frage „wo liegt was?". Enthält auch `StatusPath` (`%APPDATA%\RechtschreibTrainer\status.json`, Umgebungsvariable `RECHTSCHREIBTRAINER_STATUS` überschreibt) für den PC-Hub. |
| `IconFactory.cs` | Zeichnet die Tray-Icons zur Laufzeit: grün = bereit, blau = korrigiert gerade, grau = pausiert, roter Punkt = Mitschreiben läuft. |
| `DebugLog.cs` | Diagnose-Protokoll. **Nur aktiv, wenn `debug.log` oder `debug.on` im Datenordner liegt** — sonst wird nichts geschrieben. |
| `HotkeyForm.cs` | Unsichtbares Fenster; empfängt Hotkey-Nachrichten und dient als Ziel, um Hook-Ereignisse auf den UI-Thread zu holen. |
| `Win32.cs` | Die Windows-Funktionsaufrufe (`RegisterHotKey`, `GetForegroundWindow` …). |
| `standard-vertipper.txt` | Mitgelieferte Vertipper-Liste (deine ersten Mitschnitte + häufige deutsche Dreher). |
| `klassische-fehler.txt` | Mitgelieferte Liste klassischer Rechtschreibfehler (`seperat=separat`, `Standart=Standard` …). Nur eindeutige Fälle, nichts Kontextabhängiges. Von Hand gepflegt. |
| `haeufige-fehler.txt` | **Automatisch erzeugt** von `scripts/fetch-fehlerlisten.ps1` aus der Wikipedia-„Liste von Tippfehlern" (CC BY-SA 4.0) und der LibreOffice-Autokorrektur Deutsch (MPL-2.0, siehe `data/HERKUNFT.md`). ~1.000 streng gefilterte `falsch=richtig`-Paare: `falsch` ist nachweislich kein echtes Wort, `richtig` ist belegt, keine reinen Groß-/Klein- oder ss/ß-Unterschiede. **Nicht von Hand pflegen** — beim nächsten Lauf überschrieben. |
| `denglisch-verben.txt` | Eingedeutschte englische Tech-Verben (`coden`, `committen`, `pushen`, `mergen`, `deployen` …) als **bekannte Wörter** (keine Ersetzung) — verhindert, dass z. B. `codest` fälschlich zu `Codes` geraten wird. |
| `mehrdeutige-substantive.txt` | Wörter, die oft etwas anderes sind (Verb/Adjektiv) und nur mit vorangehendem Artikel großgeschrieben werden (`fallen`, `dusche`, `gucken`, `aktiv`, `drei` …). |

### `tests/`

| Ordner | Inhalt |
|---|---|
| `RechtschreibTrainer.Core.Tests/` | Unit-Tests der reinen Logik plus Qualitäts-Benchmarks gegen die **echten** Wortlisten: **`AlltagBenchmarkTests.cs`** (seit Runde 3, 250 Fälle **mit Satzkontext** in `alltag-faelle.tsv`, nach den Mustern der echten Tippdaten mit neutralen Sätzen nachgebaut; läuft durch `WordWatcher` + `LiveCorrectionController` auf einen nachgebauten Bildschirm; Ratsche — die wichtigste Messlatte), `BenchmarkTests.cs` (206 isolierte, aus den echten Logs beschriftete Einzelwörter — Präzision/Trefferquote/Fehlalarme) und `PassageBenchmarkTests.cs` (238-Wörter-Fließtext) laufen als **Ratsche**. Dazu zwei Prüfungen für die Auto-Fehlerliste: `HaeufigeFehlerListeTests.cs` (kein Schlüssel ist in Wahrheit ein echtes Wort, jeder Eintrag greift im Betrieb wie erwartet, keine Überschneidung mit den Handlisten) und `GaengigeTippfehlerBenchmarkTests.cs` (misst an `gaengige-tippfehler-holdout.tsv` — 76 bewusst *nicht* ausgelieferten Tippfehlern — wie viel die restliche Kette von allein fängt; **keine Ratsche**, nur Analyse + grober Einbruchschutz). |
| `RechtschreibTrainer.Tests/` | 4 Tests der Windows-nahen Teile (Icon, Replacer-Struktur). |

---

## Teil 3: Die Dateien auf der Festplatte

### Mitgeliefert (neben der `.exe`, kommen aus `data/` im Repo)

| Datei | Inhalt | Größe |
|---|---|---|
| `data/woerter.txt` | deutsche Wortformen | ~788.000 Zeilen, 11 MB |
| `data/haeufigkeit.txt` | Wort + Häufigkeit | 200.000 Zeilen, 2,6 MB |
| `data/substantive.txt` | großgeschriebene Substantivformen | 258.182 Zeilen, 3,3 MB |
| `data/namen.txt` | Eigennamen in exakter Schreibweise | 23.571 Zeilen |
| `data/klein-schreiben.txt` | Wörter, die klein bleiben müssen | ~1.500 Zeilen |
| `data/schreibung.txt` | `wort gross klein` — mitten im Satz groß/klein im Tatoeba-Korpus | 44.058 Zeilen |
| `data/klein-gueltig.txt` | klein gültig und groß ein Substantiv/Name (`testen`, `anders`) | 7.904 Zeilen |

⚠️ **Diese Dateien liegen NICHT in Git** (`.gitignore` schließt `data/` aus).
Nach einem frischen `git clone` fehlen sie, und das Programm läuft dann ohne
Rechtschreibprüfung. Neu erzeugen: **`python scripts/erzeuge-wortdaten.py`**
für `woerter.txt`, `schreibung.txt`, `klein-gueltig.txt`; die übrigen per
`curl` laut **`data/HERKUNFT.md`** (dort stehen auch Quellen und Lizenzen).

### Deine Daten (`Dokumente\RechtschreibTrainer\`)

| Datei | Inhalt | Wer schreibt |
|---|---|---|
| `woerterbuch.txt` | deine eigenen Vertipper, **Vorrang vor allem** | du |
| `nie-korrigieren.txt` | Wörter, die nie angefasst werden | wächst bei jedem Rückgängig |
| `eigene-namen.txt` | deine Eigennamen in exakter Schreibweise | du |
| `tasten.txt` | Tastenbelegung | beim ersten Start angelegt |
| `korrekturen.jsonl` | jede Korrektur als `vorher → nachher → Quelle`, dazu `Rejected`-Einträge für Ablehnungen per Rücktaste | das Programm |
| `keystrokes.log` | mitgeschriebener Text — **nur im Mitschreib-Modus** | das Programm |
| `debug.log` | Diagnose — nur wenn `debug.log` oder `debug.on` existiert. Seit Runde 3 nur noch fertige Wörter (nie technische/geheime), keine einzelnen Anschläge. **Standard: aus** (Nutzerwunsch 2026-10-21). | das Programm |

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
   `Nutzungslimit` — Deutsch bildet beliebig viele davon. Seit Runde 3 schützt
   `CompoundSplitter` sie vor dem Raten; das war die Voraussetzung, um Abstand 2
   überhaupt zu erlauben. Der Schutz ist bewusst zu großzügig und gibt deshalb
   nach, wenn ein **häufiges** Wort nur einen Buchstaben entfernt ist — sonst
   galten `russ|sich` und `mittler|weilen` als „Kompositum".

6. **Nach einem Cursor-Sprung wird nicht geraten.** `AllowSpellGuess = false`
   ist Absicht, kein Fehler: Der Cursor könnte mitten in einem bestehenden
   Wort stehen, und das „fertige" Wort wäre nur ein Bruchstück.

7. **Tests brauchen die `data/`-Dateien.** `RealDataSpellingTests` sucht
   `data/woerter.txt` in den übergeordneten Ordnern. Fehlen die Dateien,
   scheitert dieser Test — nicht der Code ist kaputt, die Daten fehlen.

8. **(Gelöst in Runde 3 durch `data/schreibung.txt`.)** Der fehlende Baustein
   war ein Korpus, in dem die Groß-/Kleinschreibung erhalten ist, gezählt nur
   **mitten im Satz** (Tatoeba): „trug" steht dort fast nur klein, „Montag"
   fast nur groß. Ursprünglicher Befund zum Nachlesen: Jeder größere,
   vielfältigere Text (siehe `GrosserTextBenchmarkTests.cs`, 2026-09-06)
   findet neue Wörter, deren Großform zufällig in der 258k-Substantivliste
   steht, obwohl klein gemeint (Zahlwörter, Adjektive, Verb-Präteritumformen:
   "acht", "stolz", "trug", "bestand", …). Weder Groß-/Kleinschreibung im
   Rohkorpus noch Häufigkeit trennen zuverlässig ("trug" und "montag" haben
   fast identische Häufigkeit, brauchen aber entgegengesetzte Behandlung) —
   nur einzelnes Kuratieren in `mehrdeutige-substantive.txt` wirkt, aber nie
   vollständig. Grundsätzliche Abwägung (Artikel-Beleg standardmäßig für
   *alle* Substantive verlangen, statt nur für die Ausnahmeliste) ist eine
   offene, dem Nutzer vorzulegende Entscheidung, siehe `docs/PROJEKT-LOG.md`.

9. **Satzanfang-Erkennung kennt keine wörtliche Rede.** `WordWatcher` hielt
   bis zum 2026-09-06 jedes `.`/`!`/`?` für ein Satzende — auch mitten im
   Satz nach einer zitierten Frage (`„Wie war dein Tag?“, fragte er`).
   Behoben über eine Komma-Ausnahme (siehe Code-Kommentar in
   `WordWatcher.cs`), aber die Erkennung bleibt ein einfacher
   Zeichen-Check, kein echtes Satzverständnis — weitere Sonderfälle
   (Doppelpunkt vor Zitat, verschachtelte Anführungszeichen) sind denkbar.

10. **`GetKeyboardState` taugt im Low-Level-Hook nicht** (behoben 2026-10-21).
    Es liefert den Tastenzustand des *eigenen* Threads, also immer „keine
    Zusatztaste". Folge: 0 Großbuchstaben und 0 Fragezeichen in 49.585 echten
    Anschlägen, aus `@` wurde `q` — und beim Neutippen eines Worts wurde z. B.
    die E-Mail-Adresse des Nutzers zu `Kunauqgmx.` zerstört. Zusatztasten
    immer per `GetAsyncKeyState` lesen.

11. **Die LibreOffice-Wortliste ist ISO-8859-1 und hat Kennzeichen.**
    `de_DE_frami.dic` muss als Latin-1 gelesen werden (sonst 57.371 kaputte
    Zeilen), und die Kennzeichen zählen: `d` = **verbotene Fehlschreibung**
    (`Wiederspruch`, `Rückgrad` — galten bis Runde 3 als richtig!), `o` = nur
    in Zusammensetzungen (`auto`, `laptop` klein), `h` = nur mit Endung.
    `scripts/erzeuge-wortdaten.py` macht das richtig — nie wieder per
    `cut -d/ -f1` importieren.

12. **Dateien nie mit PowerShell `Get-Content`/`Set-Content` umschreiben.**
    PowerShell 5.1 liest UTF-8 ohne BOM als ANSI und schreibt es doppelt
    kodiert zurück — alle Umlaute kaputt (2026-10-21 einmal passiert und
    repariert). Edit-Werkzeug oder Python benutzen.

---

## Weiterlesen

- **`docs/PROJEKT-LOG.md`** — was wann warum gemacht wurde, aktueller Stand
- **`docs/superpowers/plans/2026-09-05-korrektur-qualitaet-plan.md`** — der Plan zur Qualitätssteigerung
- **`docs/RECHERCHE-KORREKTURSYSTEME.md`** — wie andere Systeme das lösen, mit Quellen
- **`docs/superpowers/specs/2026-09-03-live-korrektur-offline-design.md`** — das ursprüngliche Design
- **`data/HERKUNFT.md`** — Herkunft und Lizenz aller Wortlisten
