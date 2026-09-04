# Projekt-Log — Rechtschreib-Trainer (Windows)

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
