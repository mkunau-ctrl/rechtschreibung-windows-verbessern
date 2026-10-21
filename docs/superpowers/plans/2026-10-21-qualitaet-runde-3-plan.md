# Plan: Qualitätsrunde 3 — aus echten Tippdaten

**Datum:** 2026-10-21
**Auslöser:** Nutzer: „guck dir die Rechtschreibkorrektur an, ob alles
aufgezeichnet wurde, analysier das und verbessere das System weiter" — nach
dem ersten Analysebericht: „Plan ist gut, ich will wirklich, dass du
ausführlich analysierst und die Autokorrektur sich massiv verbessert."
Außerdem: Passwörter schützen („wenn du erkennst, dass es um ein Passwort
geht, das nächste Wort nicht weiter angucken"), Tastenlog soll erst einmal
aus bleiben, `debug.log` löschen.

**Branch:** `feat/qualitaet-runde-3` (von `hub-anbindung`, damit die noch nicht
gemergte Hub-Anbindung mitkommt).

**Leitplanke wie immer:** Präzision zuerst. Jede Änderung wird gemessen
(alle Benchmarks + neuer Alltags-Benchmark); verschlechtert sie die
Präzision, fliegt sie wieder raus und wird mit Zahlen im Log dokumentiert.

---

## Teil A — Analyse (erledigt, Zahlen zum Nachlesen)

Datengrundlage: `Dokumente\RechtschreibTrainer\debug.log` (seit 2026-09-03,
`debug.on` war die ganze Zeit an): 49.585 Tastenanschläge, 8.318 Wörter,
1.412 Ersetzungen, 20 Programmstarts. `korrekturen.jsonl`: 1.482 Einträge
(Spelling 1.176, Dictionary 169, Capitalization 128, Replacement 9). Die
Auswertungsskripte liefen lokal (Python, Scratchpad) und sind **nicht** im
Repo, weil sie auf dem privaten Text arbeiten.

Funde, nach Schwere:

1. **Shift, AltGr und CapsLock werden nie erkannt.** `GetKeyboardState` im
   Low-Level-Hook liefert den Zustand des *eigenen* Threads, nicht den der
   Tastatur. Belegt: 0 Großbuchstaben und 0 Fragezeichen in 49.585
   Anschlägen. Folgen: `@` (AltGr+Q) kommt als `q` an, `?` (Shift+ß) als `ß`,
   `/` (Shift+7) als `7`, `!` als `1`. Wird so ein Wort ersetzt, tippt das
   Programm die **falschen Zeichen zurück** — 12× wurde die E-Mail-Adresse
   des Nutzers zu `Kunauqgmx.` zerstört (Satzanfang-Großschreibung tippte
   das Wort neu). `?`/`!` beenden nie einen Satz. Ein großgetipptes Wort mit
   Vertipper wird klein „korrigiert".
2. **Großschreibung mitten im Satz ist zu ~15 % falsch.** 767
   Großschreibungen mitten im Satz, davon nach Kontextdurchsicht ~119
   falsch: `ich würde`→`Würde`, `email senden`→`Senden`, `leider`→`Leider`,
   `lokal`, `gerade`, `perfekt`, `testen`, `speichern`, `starten`,
   `ein paar`→`Paar`, `alle drei`→`Drei`, `ich weiß`→`Weiß`, `kriege`.
   Ursache: `WordList.IsCapitalisedOnly` prüft — anders als sein Kommentar
   behauptet — **nicht**, ob das Wort klein ebenfalls gültig ist. Jede
   Substantivform aus `substantive.txt` wird großgezogen.
3. **Automatisches Lernen brennt diese Fehler fest ein.** `woerterbuch.txt`
   hat sich u. a. `würde=Würde`, `weiß=Weiß`, `kriege=Kriege`, `senden=Senden`,
   `plan=Plan`, `test=Test` beigebracht — und `anderern=Wanderern`. Ursache:
   `DictionaryDistiller` schließt nur die Quelle `Capitalization` aus, die
   Substantiv-Großschreibung läuft aber als `Spelling`/`Dictionary`.
4. **12 % aller Wörter werden nie geraten**: das erste Wort nach jeder
   Tipppause > 4 s (`TrayApp.CheckContextStillValid` ruft `DropContext`, das
   setzt „vielleicht Bruchstück"). Belegt an `eigntlich`, `vielleich`,
   `irgenwie`, `zusammenfassun` — alle Abstand 1, alle nach einer Pause.
   Ohne Taste/Klick/Fensterwechsel kann sich der Cursor aber nicht bewegen.
5. **`woerter.txt` ist zu 57.371 Zeilen kaputt kodiert.** Die
   LibreOffice-Stämme (`de_DE_frami.dic`) sind ISO-8859-1 (`SET ISO8859-1`
   in der `.aff`), wurden aber ungewandelt angehängt → `�ppigkeit`. Diese
   Wörter kennt das Programm nicht.
6. **Kurze Vertipper bleiben stehen** (unter 4 Zeichen nie, unter 5 nur exakte
   Listen): `wil`(9×) `ihc`(8×) `mla`(8×) `ghet`(6×) `icht` `mien` `kan` `noh`
   `gukc` `einn` `uund` `auh` `niht` `noc`.
7. **Doppelt getippte Buchstaben** (`wennn`, `kannn`, `allles`, `mmit`,
   `kommmmt`, `zeiit`) und **fehlende Leertaste** (`vorallem`, `damitich`,
   `machweiter`, `meinemlaptop`, `wennmein`) werden nie gelöst.
8. **Vertipper, die zufällig ein seltenes echtes Wort ergeben**: `genua`→
   `Genua` statt `genau`, `mienen`→`Mienen` statt `meinen`, `mehrer`→`Mehrer`.
9. **Erstbuchstaben-Raten**: `anderern`→`Wanderern`, `interseite`→
   `Hinterseite` — ein vergessener *erster* Buchstabe ist beim Tippen selten.
10. **Datenschutz**: Der Tastenlog schreibt *im Hook* mit, also bevor
    `FocusedFieldIsPassword` greift — Passwörter können in `debug.log`
    stehen. Strg+Alt+Z wurde nie benutzt (0 Hotkey-Ereignisse) — die
    „nie korrigieren"-Liste lernt also in der Praxis nichts.
11. **Strg-Kürzel werden als Buchstaben gelesen** (Strg+A hängt `a` ans Wort),
    `ToUnicode` ohne Flag 4 kann Tottasten (`^`, `´`) des Nutzers stören.

Noch offen aus **Qualitätsrunde 2** (Plan vom 2026-09-08, nie umgesetzt,
Nutzer hatte freigegeben): Punkt 1 (`naheste`→`nahesteh`), 4a
(Kompositum-Schutz), 2 (LibreOffice-Autokorrekturliste). Diese Punkte
werden hier mit erledigt. 4b (Wörter zusammenfügen) nur, wenn Zeit bleibt.

---

## Teil B — Umsetzung (Reihenfolge = Priorität)

### B0 Messbasis
- Neuer **Alltags-Benchmark** `tests/.../alltag-faelle.tsv` + `AlltagBenchmarkTests`:
  Fälle **mit Satzkontext** (`vorwörter | getippt | erwartet | kategorie`),
  nachgebaut aus den echten Mustern, aber mit **neutralen Sätzen** (keine
  Namen, Adressen, Privates — das Repo ist öffentlich). Läuft durch den
  echten `WordWatcher`, misst Präzision/Trefferquote/Fehlalarme. Ratsche.
- Ausgangswerte aller vorhandenen Benchmarks festhalten.

### B1 Eingabe richtig lesen (KeyboardHook)
- Modifikatoren per `GetAsyncKeyState` (Shift, Strg, Alt, links/rechts) und
  CapsLock per `GetKeyState` in den Keyboard-State schreiben, dann
  `ToUnicode` mit Flag 4 (verändert den Tottasten-Zustand nicht).
- Neue Core-Klasse `KeyInput.Classify`: Strg/Alt **ohne** AltGr-Zeichen
  (Kürzel wie Strg+A/C/V/Z) → **Kontextbruch**, kein Zeichen.

### B2 Technische Zeichenfolgen und Satzanfang (WordWatcher)
- Ein Token (alles bis zum nächsten Leerraum), das `@ / \ _ # = < > | ~ :`
  enthält oder Ziffern mit Buchstaben mischt, wird **nie** korrigiert —
  weder das Wort davor noch die danach im selben Token (E-Mail, URL, Pfad).
- Satzanfang nur, wenn nach `. ! ?` wirklich Leerraum/Enter kommt (nicht bei
  `gmx.de`), nicht nach Abkürzungen (`bzw.`, `z.B.`, `ca.`, `usw.`, `ggf.`,
  `evtl.`, `inkl.`, `vgl.`, `Nr.` …) und nicht nach Ordinalzahlen (`19.`).

### B3 Großschreibung per Korpus (Truecasing)
- Neue Datei `data/schreibung.txt` (`wort gross klein`): wie oft ein Wort im
  Tatoeba-Korpus (784.113 deutsche Sätze, CC BY 2.0 FR) **mitten im Satz**
  groß bzw. klein steht. Erzeugt von `scripts/fetch-schreibung.ps1`.
- Regel in `WordList.IsCapitalisedOnly` (bei ≥ 5 Belegen): Anteil groß ≥ 90 %
  → groß; 30–90 % → nur mit Substantiv-Beleg; < 30 % → nie. Ohne Korpusbeleg:
  Wörter, die klein gültig sind (`enz`-Liste), nur mit Beleg.
- Substantiv-Beleg erweitert: Artikel/Possessiv direkt davor **oder**
  Kontraktion (`am im zum zur beim vom ins ans aufs …`) **oder** Artikel +
  dekliniertes Adjektiv (`die ganze zeit`) **oder** Zahl davor (`30 sekunden`).
- An den echten Daten vorab simuliert: falsche Großschreibungen 119 → 23,
  richtige 654 → 623 (die Differenz sind fast nur Eigennamen, die einen
  anderen Weg nehmen, also kein echter Verlust).

### B4 Wortliste reparieren
- `woerter.txt` neu: enz + frami-Stämme **aus ISO-8859-1 gewandelt**.
  `data/HERKUNFT.md`-Befehl korrigieren. Benchmarks neu messen.

### B5 Vertipper besser finden
- (a) Pause > 4 s bricht den Kontext nicht mehr (nur Klick/Fokus/Navigation/Kürzel).
- (b) **Doppelbuchstaben**: Mehrfach getippter Buchstabe auf einen reduzieren,
  wenn das ein bekanntes Wort ergibt und das Getippte unbekannt ist.
- (c) **Kurze Wörter** (3–4 Zeichen): nur Vertauschung, Auslassung,
  Doppelbuchstabe — und nur zu einem **sehr häufigen** Wort, das eindeutig
  gewinnt (`ihc`→`ich`, `mla`→`mal`, `wil`→`will`, `ghet`→`geht`).
- (d) Fehler am **ersten Buchstaben** abwerten (`anderern`→`anderen`).
- (e) **Echtwort-Fehler**: getipptes Wort sehr selten, eine Vertauschung
  ergibt ein ≥ 500× häufigeres Wort → das nehmen (`genua`→`genau`).
- (f) **Fehlende Leertaste**: unbekanntes Wort = Funktionswort + bekanntes
  Wort (`vorallem`→`vor allem`, `damitich`→`damit ich`).
- (g) Runde 2 Punkt 1: Ergebnis muss selbst ein bekanntes Wort sein.
- (h) Runde 2 Punkt 4a: Kompositum-Schutz (zerlegbares Wort nicht raten).
- (i) Abstand 2 für lange Wörter (≥ 8 Zeichen), nur eindeutig — wird
  gemessen; kostet es Präzision, fliegt es raus (wie 2026-09-06).

### B6 Lernen reparieren
- `DictionaryDistiller`: reine Groß-/Kleinschreib-Paare nie lernen.
- **Rücktaste als Ablehnung**: Wer direkt nach einer Korrektur mit der
  Rücktaste in das korrigierte Wort hineinlöscht, lehnt sie ab. Ab 2
  Ablehnungen desselben Paares → `nie-korrigieren.txt`; abgelehnte Paare
  werden nie gelernt. (Ersetzt in der Praxis das nie benutzte Strg+Alt+Z.)
- Persönliche `woerterbuch.txt` bereinigen (Sicherung vorher): reine
  Groß-/Klein-Paare und `anderern=Wanderern` raus.

### B7 Datenschutz
- **Passwort-Kontext**: Nach einem Auslösewort (`passwort`, `kennwort`,
  `password`, `pw`, `pin`, `tan`, `puk`, `zugangscode`, `zugangsdaten`, `login`…)
  werden die folgenden Wörter **nicht angesehen, nicht korrigiert, nicht
  geloggt** — bis Enter, Satzende oder höchstens 3 Wörter.
- Hook schreibt **keine Tastenanschläge** mehr ins Debug-Log; Wort-Logs nur
  noch, wenn kein Passwortfeld/-kontext. `debug.on` und `debug.log` löschen
  (Nutzerwunsch).

### B8 LibreOffice-Autokorrektur (Runde 2 Punkt 2)
- Wie im Runde-2-Plan: `fetch-fehlerlisten.ps1` erweitern, gleicher Filter.

### B9 Abschluss
- Volles `dotnet build` + `dotnet test`, Ratschen hochsetzen, Zahlen ins Log.
- Doku: `PROJEKT-LOG.md`, `CLAUDE.md`, `docs/DATEIEN.md`, `data/HERKUNFT.md`.
- Merge nach `main`, Push, `scripts/install.ps1`, Handtest-Liste für den Nutzer.

## Was bewusst NICHT gemacht wird
- Keine KI zur Laufzeit, kein Netz zur Laufzeit (Korpora nur zur Entwicklungszeit).
- Keine privaten Texte im Repo — Benchmarks nur mit neutralen Nachbauten.
- Großschreibregel nicht pauschal umkehren (Runde 2 Punkt 3, vom Nutzer
  gestrichen) — B3 betrifft nur Wörter, die klein *auch* gültig sind.
