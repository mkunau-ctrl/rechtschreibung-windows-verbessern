<#
.SYNOPSIS
    Holt die deutsche Wikipedia-Tippfehlerliste, filtert sie streng gegen die
    echten Wortlisten des Projekts und schreibt daraus die mitgelieferte Datei
    src/RechtschreibTrainer/haeufige-fehler.txt (falsch=richtig).

.BESCHREIBUNG
    Einmalig beim Entwickeln ausfuehren. Das laufende Programm geht nie ins Netz
    - es liest nur die erzeugte Textdatei.

    Quelle : Wikipedia "Liste von Tippfehlern" (Unterseiten A-Z, 0-9, PQ, XYZ,
             Sonderzeichen), maschinenlesbar ueber ?action=raw als
             {{tippfehler|falsch|richtig}}.
    Lizenz : CC BY-SA 4.0 (Wikipedia-Text). Die abgeleitete Liste steht unter
             derselben Lizenz; Herkunft ist in data/HERKUNFT.md vermerkt.

    Praezisions-Filter (ein Paar kommt NUR rein, wenn alle Punkte zutreffen):
      * 'falsch' ist KEIN bekanntes deutsches Wort (nicht in woerter.txt /
        substantive.txt / namen.txt) - sonst wuerde ein korrektes Wort
        "verschlimmbessert".
      * 'richtig' IST bekannt (bzw. jedes Token bei Mehrwort-Aufloesungen).
      * kein reiner Gross-/Kleinschreib- oder ss/ss-Unterschied (Satzkontext /
        Schweiz-Schreibung).
      * nicht schon von der Ersatzschreibtabelle (ue->ue, oe->oe, ae->ae)
        abgedeckt.
      * steht noch nicht in klassische-fehler.txt / standard-vertipper.txt.
      * 'falsch' enthaelt nur Buchstaben/Bindestrich, mind. 3 Zeichen,
        kein * (Wortform-Platzhalter) und kein + (Mehrwort-Marker).

.PARAMETER DataDir
    Ordner mit woerter.txt, substantive.txt, namen.txt, haeufigkeit.txt.
    Standard: <repo>/data

.PARAMETER OutFile
    Zieldatei. Standard: <repo>/src/RechtschreibTrainer/haeufige-fehler.txt
#>
[CmdletBinding()]
param(
    [string]$DataDir,
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $DataDir) { $DataDir = Join-Path $repo 'data' }
if (-not $OutFile) { $OutFile = Join-Path $repo 'src/RechtschreibTrainer/haeufige-fehler.txt' }

$bundledDir = Join-Path $repo 'src/RechtschreibTrainer'
$holdoutFile = Join-Path $repo 'tests/RechtschreibTrainer.Core.Tests/gaengige-tippfehler-holdout.tsv'

function Read-WordSet {
    param([string[]]$Paths)
    $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($p in $Paths) {
        if (-not (Test-Path $p)) { Write-Warning "fehlt: $p"; continue }
        foreach ($line in [System.IO.File]::ReadLines($p)) {
            $w = $line.Trim()
            if ($w.Length -eq 0 -or $w.StartsWith('#')) { continue }
            # haeufigkeit.txt: "wort anzahl" -> nur das Wort
            $sp = $w.IndexOf(' ')
            if ($sp -gt 0) { $w = $w.Substring(0, $sp) }
            [void]$set.Add($w.ToLowerInvariant())
        }
    }
    return $set
}

Write-Host "Lade Wortlisten aus $DataDir ..."
$known = Read-WordSet @(
    (Join-Path $DataDir 'woerter.txt'),
    (Join-Path $DataDir 'substantive.txt'),
    (Join-Path $DataDir 'namen.txt')
)
Write-Host ("  {0} bekannte Wortformen (klein normalisiert)" -f $known.Count)

# Substantivformen getrennt - fuer die Plausibilitaetspruefung von 'richtig',
# weil viele korrekte Ziele Substantive sind, die im Frequenzkorpus fehlen.
$nouns = Read-WordSet @((Join-Path $DataDir 'substantive.txt'))
Write-Host ("  {0} Substantivformen" -f $nouns.Count)

# Fuer die Plausibilitaetspruefung von 'richtig': das freie Frequenzkorpus
# (OpenSubtitles, MIT) - erlaubt, seltene/erfundene Ziele zu erkennen.
$freq = Read-WordSet @((Join-Path $DataDir 'haeufigkeit.txt'))
Write-Host ("  {0} Frequenzwoerter" -f $freq.Count)

# schon von Hand gepflegte Paare (nicht doppeln)
$existing = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in @('klassische-fehler.txt', 'standard-vertipper.txt', 'denglisch-verben.txt')) {
    $fp = Join-Path $bundledDir $f
    if (-not (Test-Path $fp)) { continue }
    foreach ($line in [System.IO.File]::ReadLines($fp)) {
        $l = $line.Trim()
        if ($l.Length -eq 0 -or $l.StartsWith('#')) { continue }
        $eq = $l.IndexOf('=')
        if ($eq -gt 0) { [void]$existing.Add($l.Substring(0, $eq).Trim()) }
        else { [void]$existing.Add($l) }
    }
}

function Test-Known {
    param([string]$Word)
    return $known.Contains($Word.ToLowerInvariant())
}

# Bewusst herausgehaltene Faelle (Testmaterial fuer die Trefferquote auf
# ungesehenen Tippfehlern) - kommen nie in die ausgelieferte Liste.
$holdout = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
if (Test-Path $holdoutFile) {
    foreach ($line in [System.IO.File]::ReadLines($holdoutFile)) {
        $l = $line.Trim()
        if ($l.Length -eq 0 -or $l.StartsWith('#')) { continue }
        [void]$holdout.Add(($l -split '\t')[0].Trim())
    }
    Write-Host ("  {0} Faelle aus gaengige-tippfehler-holdout.tsv werden ausgespart" -f $holdout.Count)
}

# 'richtig' gilt als belegt, wenn JEDES Token in den deutschen Wortlisten steht.
# Der Frequenzkorpus wird bewusst NICHT als Rueckfall genutzt - er enthaelt
# englische Kontamination (right, ridge) und wuerde erfundene Ziele durchlassen.
# "X oder Y"-Anmerkungen aus Wikipedia werden komplett verworfen.
function Test-RightValid {
    param([string]$Right)
    if ($Right -match '(?i)\boder\b') { return $false }
    $toks = @($Right -split '\s+' | Where-Object { $_.Trim().Length -gt 0 })
    foreach ($tok in $toks) {
        $t = $tok.Trim().ToLowerInvariant()
        if (-not $known.Contains($t)) { return $false }
    }
    # Korpus-Plausibilitaet: das Ziel-Hauptwort muss im freien Frequenzkorpus
    # belegt sein (oder eine bekannte Substantivform) - das siebt seltene und
    # erfundene Ziele aus, die zwar in der 870k-Wortliste stehen, aber real
    # kaum vorkommen. "Praezision zuerst".
    $head = $toks[0].Trim().ToLowerInvariant()
    if (-not ($freq.Contains($head) -or $nouns.Contains($head))) { return $false }
    return $true
}

# ue/oe/ae -> Umlaut (wie ReplacementTable.cs, ss bleibt bewusst aussen vor)
function Resolve-Ersatzschreibung {
    param([string]$S)
    return $S.ToLowerInvariant().Replace('ue', "$([char]0x00FC)").Replace('oe', "$([char]0x00F6)").Replace('ae', "$([char]0x00E4)")
}

$subpages = 'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z','0-9','PQ','XYZ','Sonderzeichen'
$rx = [regex]'\{\{[Tt]ippfehler\|([^|}]+)\|([^|}]+)(?:\|[^}]*)?\}\}'
$onlyLetters = [regex]'^[\p{L}\-]+$'

$pairs = [ordered]@{}
$stats = [ordered]@{ roh = 0; stern = 0; plus = 0; nichtwort = 0; casing = 0; ssvariante = 0; ersatzschreibung = 0; falschIstWort = 0; richtigUnbelegt = 0; schonVorhanden = 0; holdout = 0; konflikt = 0; genommen = 0 }

foreach ($sub in $subpages) {
    $url = "https://de.wikipedia.org/wiki/Wikipedia:Liste_von_Tippfehlern/$([uri]::EscapeDataString($sub))?action=raw"
    Write-Host "  hole $sub ..."
    try { $raw = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60 | Select-Object -ExpandProperty Content }
    catch { Write-Warning "  $sub uebersprungen: $($_.Exception.Message)"; continue }

    foreach ($m in $rx.Matches($raw)) {
        $stats.roh++
        $wrong = $m.Groups[1].Value.Trim()
        $right = $m.Groups[2].Value.Trim()

        if ($wrong -like '*`**' -or $right -like '*`**') { $stats.stern++; continue }
        if ($wrong.Contains('+') -or $right.Contains('+')) { $stats.plus++; continue }
        if ($wrong.Length -lt 3) { $stats.nichtwort++; continue }
        if (-not $onlyLetters.IsMatch($wrong)) { $stats.nichtwort++; continue }
        if ($right.Length -lt 2) { $stats.nichtwort++; continue }
        # 'richtig' darf nur Buchstaben, Leerzeichen, Bindestrich enthalten -
        # sonst ist eine Wikipedia-Anmerkung in den Parameter gerutscht
        # (z. B. "sodass, so dass").
        if ($right -notmatch '^[\p{L}\- ]+$') { $stats.nichtwort++; continue }

        $wl = $wrong.ToLowerInvariant()
        $rl = $right.ToLowerInvariant()

        if ($wl -eq $rl) { $stats.casing++; continue }
        $sz = [string][char]0x00DF
        if ($wl.Replace($sz, 'ss') -eq $rl.Replace($sz, 'ss')) { $stats.ssvariante++; continue }
        if ((Resolve-Ersatzschreibung $wrong) -eq $rl) { $stats.ersatzschreibung++; continue }

        if (Test-Known $wrong) { $stats.falschIstWort++; continue }
        if (-not (Test-RightValid $right)) { $stats.richtigUnbelegt++; continue }
        if ($existing.Contains($wrong)) { $stats.schonVorhanden++; continue }
        if ($holdout.Contains($wrong)) { $stats.holdout++; continue }

        # Schluessel klein normalisiert (die CorrectionDictionary-Kleinschreib-
        # Rueckfallregel deckt damit auch den Satzanfang ab). Wert behaelt die
        # Wikipedia-Schreibung (Substantive gross).
        if ($pairs.Contains($wl)) {
            if ($pairs[$wl] -ne $right) { $pairs[$wl] = $null; $stats.konflikt++ }
            continue
        }
        $pairs[$wl] = $right
    }
}

$final = $pairs.GetEnumerator() | Where-Object { $_.Value } | Sort-Object Key
$stats.genommen = @($final).Count

$header = @"
# Haeufige deutsche Tippfehler (falsch=richtig) - automatisch erzeugt.
#
# Quelle : Wikipedia "Liste von Tippfehlern" (de.wikipedia.org), CC BY-SA 4.0.
# Abruf  : $(Get-Date -Format 'yyyy-MM-dd') durch scripts/fetch-fehlerlisten.ps1
# Filter : 'falsch' ist kein bekanntes Wort, 'richtig' ist belegt, kein reiner
#          Gross-/Klein- oder ss/ss-Unterschied, nicht von der Ersatzschreib-
#          tabelle abgedeckt, nicht schon in klassische-fehler.txt.
#
# NICHT von Hand pflegen - Aenderungen gehen beim naechsten Lauf verloren.
# Eigene Eintraege: Dokumente\RechtschreibTrainer\woerterbuch.txt (hat Vorrang).

"@

$sb = [System.Text.StringBuilder]::new()
[void]$sb.Append($header)
foreach ($e in $final) { [void]$sb.AppendLine("$($e.Key)=$($e.Value)") }
[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

Write-Host ""
Write-Host "Statistik:"
$stats.GetEnumerator() | ForEach-Object { Write-Host ("  {0,-18} {1}" -f $_.Key, $_.Value) }
Write-Host ""
Write-Host "geschrieben: $OutFile  ($($stats.genommen) Paare)"
