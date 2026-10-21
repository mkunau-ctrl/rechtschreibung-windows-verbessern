# Plan: Qualitätsrunde 2

**Datum:** 2026-09-08
**Auslöser:** Nutzer fragte „gibt es noch etwas, was du verbessern kannst?".
Nach Rückfrage: Punkte **0, 1, 2, 4** umsetzen. **Punkt 3 (Großschreib-Regel
umkehren) ist ausdrücklich gestrichen** („völlig unnötig"). Punkt 4 wird um
die **beidseitige Wort­grenzen-Korrektur** erweitert (Nutzerwunsch).
**Branch:** `feat/qualitaet-runde-2` von `main`.

**Feste Leitplanke:** Präzision zuerst. Präzision und Fehlalarmrate der
`BenchmarkTests`-Ratsche dürfen **nicht** sinken. Verschlechtert ein Punkt die
Präzision, wird er verworfen und mit Zahlen im Log dokumentiert.

---

## Punkt 0 — alter git-stash weg

`stash@{0}` (`WIP on main: 8b56aff Add opt-in keystroke recorder`) ist aus
einer alten Sitzung, überholt, hat beim letzten Merge den `stash failed`-
Fehler mitverursacht. `git stash show -p stash@{0}` prüfen, dann
`git stash drop stash@{0}`.

---

## Punkt 1 — `naheste → nahesteh`: kein Nicht-Wort mehr raten

`superpowers:systematic-debugging`. Erst ein Test, der den Fall reproduziert,
dann die Ursache belegen (`_words.Knows` wird im Kandidaten-Check schon
aufgerufen — trotzdem kommt `nahesteh` heraus; Weg über `MatchLeadingCase` /
`ReplacementTable`-Kette / `CorrectionRules` prüfen).

**Fix (ursachenunabhängige Minimalinvariante):** Am Ende von
`SpellCorrector.Suggest` eine Schluss­prüfung — der zurückgegebene String muss
selbst ein bekanntes Wort oder Eigenname sein (`IsKnownWord`), sonst `null`.
`naheste → nächste` ist Abstand 2 und wird bewusst **nicht** gelöst; „unberührt
lassen" ist ok.

**Tests:** Reproduktions-Test grün (`null` statt `nahesteh`). Alle Benchmarks
neu — Präzision/Fehlalarme dürfen sich nur verbessern.

---

## Punkt 2 — zweite freie Fehlerquelle: LibreOffice-Autokorrektur

**Quelle:** `extras/source/autocorr/lang/de/DocumentList.xml` aus
`LibreOffice/core` (GitHub-Raw). ~1.200 Einträge
`<block-list:block abbreviated-name="falsch" name="richtig"/>`. Lizenz
**MPL-2.0** (Datei-Header bei der Umsetzung bestätigen), verträglich mit CC
BY-SA 4.0. Attribution in `data/HERKUNFT.md`.

**Umsetzung:** `scripts/fetch-fehlerlisten.ps1` erweitern (kein neues Skript).
- Nach dem Wikipedia-Durchlauf zusätzlich die XML holen, `block`-Elemente per
  Regex lesen, HTML-Entities dekodieren.
- **Exakt derselbe Präzisionsfilter** (falsch kein bekanntes Wort, richtig
  belegt + im Frequenzkorpus, kein Casing/ß-only, nicht von `ReplacementTable`/
  Handlisten abgedeckt, nur Buchstaben/Leer/Bindestrich, ≥ 3 Zeichen). Der
  Pfeil-/Symbol-/Bruch-Kram fällt automatisch durch den Buchstaben-Filter.
  **Mehrwort-Ziele wie `vorallem → vor allem` bleiben erlaubt** (jedes Token
  wird geprüft) — das deckt die „fehlende Leertaste"-Richtung mit ab.
- Quelle pro Paar getrennt zählen (Wikipedia vs. LibreOffice) für das Log.
- Konflikt (gleiches `falsch`, anderes `richtig`) → wie bisher beide raus.
- Holdout `gaengige-tippfehler-holdout.tsv` aus dem kombinierten Rohbestand
  neu ziehen.

**korrekturen.de** bewusst nicht — keine klare Lizenz, öffentliches Repo.

**Tests:** `HaeufigeFehlerListeTests` laufen unverändert (prüfen die Datei).
Eintragszahl-Untergrenze ggf. hoch. Alle Benchmarks neu.

---

## Punkt 4 — Komposita: Schutz vor dem Raten **und** Wortgrenzen-Korrektur

### 4a — `CompoundSplitter` (neu, in `RechtschreibTrainer.Core`)

Eingabe: klein normalisiertes Wort. Zerlegt in 2–3 bekannte Teile mit
optionalem Fugenelement (`s`, `es`, `n`, `en`). Gelingt es → das Wort ist in
Ordnung.

Regeln gegen Fehlauslösung (an Holdout + Benchmarks getunt):
- jeder Teil ≥ 4 Zeichen,
- mindestens ein Teil ist Substantiv (`substantive.txt`),
- im Zweifel gewinnt der Kompositum-Schutz (lieber nicht raten).

Verdrahtung: in `SpellCorrector.Suggest`, nach den „bekannt?"-Prüfungen,
**vor** `Candidates()`: `if (_compound?.CanSplit(lower) == true) return null;`.
`SpellCorrector` bekommt den Splitter optional in den Konstruktor (`RepoFiles`,
`DictionaryLoader`/`Program` mitziehen). `ReplacementTable` bleibt unangetastet.

### 4b — falsch getrennt geschrieben → Leertaste weg (neu)

Nutzerwunsch: „wenn ich zwei Wörter schreibe, die zusammengehören, sollen sie
zusammengeschrieben werden." Beispiel: `Recht schreibung` → `Rechtschreibung`,
`Bein bruch` → `Beinbruch`.

**Live-Mechanik:** Die Korrektur greift, wenn das **zweite** Wort fertig ist
(an seiner Wortgrenze) — dann steht das erste schon da.
- `WordWatcher`: das zuletzt gemeldete Wort **und** ob nur genau ein
  Leerzeichen dazwischen lag, an den Kontext weiterreichen (heute wird das
  Vorwort nur für den — jetzt entfallenden — Determiner-Fall gehalten).
- `OfflineCorrector`: neue Prüfung — ist das aktuelle Wort für sich **kein**
  gültiges Wort, aber `Vorwort + aktuell` (direkt oder über `CompoundSplitter`
  mit Fuge) ein bekanntes Wort → „zusammenfügen".
  **Sicherheitsschranke:** nur, wenn das aktuelle Wort allein ungültig ist
  (`tier` bleibt `tier`, `Haustier` wird nicht erzwungen), nicht über
  Satzgrenzen, nur bei genau einem Leerzeichen.
- `LiveCorrectionController.HandleWord`: bei dieser Quelle zusätzlich das
  Leerzeichen vor dem aktuellen Wort mitlöschen
  (`DeleteCount = Wort.Length + 1 + tail`), Ersatztext ist das (ggf.
  korrigierte) Wortende ohne führendes Leerzeichen. Undo stellt Leerzeichen +
  Originalwort wieder her.
- Neue `CorrectionSource.Wortgrenze` (für Benachrichtigung/Lern-Log).

### 4c — falsch zusammen geschrieben → Leertaste rein

`vorallem → vor allem`, `zuhilfe → zu Hilfe`. Läuft schon heute über die
Wörterbuch-Mechanik (Mehrwort-`richtig`), Punkt 2 liefert viele solche Paare.
Kein neuer Code nötig — nur bestätigen, dass `HandleWord` den Mehrwort-Ersatz
sauber tippt (Leerzeichen mitten im Insert), und einen Test dafür.

**Tests:** `CompoundSplitterTests` (`Kältekreislauf`, `Nutzungslimit`,
`Trainingsdaten`, `Rechtschreibkorrektur` zerlegen; `asdfghjkl`, `zustaedig`
nicht; `desweiteren`, `vorallem` rutschen nicht als Kompositum durch).
`WordWatcher`/`LiveCorrectionController`-Tests für 4b (join + Undo + kein
falscher Join). Test für 4c (Mehrwort-Ersatz). Alle Benchmarks neu — Präzision
und Fehlalarme mindestens gleich.

---

## Reihenfolge

1. Punkt 0 (stash weg), Branch anlegen.
2. Punkt 1 (naheste-Bug) — klein, isoliert, TDD.
3. Punkt 4a (CompoundSplitter).
4. Punkt 4b (Wortgrenze zusammenfügen) — baut auf 4a.
5. Punkt 4c (Wortgrenze trennen) — nur Test + Prüfung.
6. Punkt 2 (LibreOffice-Quelle) — Datenpipeline, Filter + Holdout neu.
7. Voller `dotnet build` + `dotnet test` (alle Benchmarks), Zahlen ins Log.
8. Doku: `PROJEKT-LOG.md` (neuer Kopf-Eintrag), `CLAUDE.md`, `data/HERKUNFT.md`,
   `docs/DATEIEN.md`, `docs/RECHERCHE-KORREKTURSYSTEME.md` (Phase 2b als
   erledigt). Commits mit deutschen Nachrichten.
9. Merge `--no-ff` nach `main`, push, `scripts/install.ps1` — live schalten.
10. Memory (`rechtschreib-trainer-projekt.md`) aktualisieren.

## Was bewusst NICHT gemacht wird

- **Punkt 3 (Großschreib-Regel umkehren)** — vom Nutzer gestrichen.
- korrekturen.de (Lizenz), Leipzig-Bigramme, SymSpell-Distanz-2, Kölner
  Phonetik, Affix-Expansion via `wordforms` — bleiben zurückgestellt.
- Keine KI zur Laufzeit. Neue Quelle nur zur Entwicklungszeit.
- Kein Umschreiben von `KeyboardHook.cs` zur Defender-Umgehung.
