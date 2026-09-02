# Rechtschreib-Trainer (Windows)

Ein Tray-Programm für Windows, das **nur wenn du es per Hotkey aktivierst**
mitschreibt, was du tippst — als Rohmaterial, um später wiederkehrende
Rechtschreibfehler zu finden. Es läuft **nicht** dauerhaft im Hintergrund und
zeichnet nichts auf, solange die Aufnahme nicht aktiv ist.

## Funktionsweise

- `Strg+Alt+R` schaltet die Aufnahme ein/aus (alternativ über das Tray-Icon).
- Bei jedem Umschalten erscheint eine kurze Benachrichtigung ("Aufnahme
  gestartet" / "Aufnahme gestoppt, N Zeichen gespeichert").
- Das Tray-Icon ist **rot, solange aktiv aufgenommen wird**, sonst grau — als
  dauerhaftes visuelles Signal, damit du nie vergisst, dass gerade
  mitgeschrieben wird.
- Getippter Text wird zu lesbarem Text zusammengesetzt (Backspace wird
  verarbeitet, nicht wörtlich mitgeloggt), pro Sitzung mit Zeitstempel an
  `Dokumente\RechtschreibTrainer\keystrokes.log` angehängt.

## Sicherheit — bitte lesen

Die Aufnahme ist bewusst **opt-in per Hotkey**, nicht dauerhaft aktiv:

- **Schalte die Aufnahme aus, bevor du Passwörter, PINs, Bankdaten oder
  ähnlich sensible Dinge eingibst.** Es gibt keinen automatischen Filter
  dafür — die einzige Absicherung ist, dass du die Aufnahme selbst steuerst.
- Die Log-Datei liegt nur lokal in deinem Dokumente-Ordner, wird nirgendwo
  automatisch hochgeladen, und ist über `.gitignore` von diesem Repo
  ausgeschlossen — sie landet nie versehentlich auf GitHub.
- Lösche die Log-Datei, sobald du sie ausgewertet hast, wenn du sie nicht
  dauerhaft aufheben willst.

## Build & Ausführen (auf deinem Windows-PC)

Voraussetzung: [.NET 8 SDK](https://dotnet.microsoft.com/download) unter
Windows (das Programm nutzt Windows-spezifische APIs und läuft nicht unter
Linux/macOS).

```powershell
cd src/RechtschreibTrainer
dotnet run
```

Für eine eigenständige `.exe`:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

## Auswertung mit Claude Code

Wenn du genug Text gesammelt hast:

1. Öffne Claude Code lokal in diesem Repo-Ordner (auf deinem Windows-PC, wo
   auch `Dokumente\RechtschreibTrainer\keystrokes.log` liegt).
2. Bitte darum, die Log-Datei auf wiederkehrende Rechtschreibfehler zu
   untersuchen (z. B. "schau dir keystrokes.log an und fasse meine
   häufigsten Fehler zusammen").
3. Die eigentliche Auto-Korrektur-Logik (z. B. ein persönliches
   Korrekturwörterbuch) ist noch nicht Teil dieses Programms — das ist der
   nächste Schritt, sobald genug Beispieldaten da sind.

## Aktueller Stand

Dies ist die erste Ausbaustufe: Aufnahme per Hotkey + Log-Datei. Es korrigiert
noch nichts automatisch — das kommt, sobald aus den gesammelten Daten ein
persönliches Fehlerprofil abgeleitet wurde.
