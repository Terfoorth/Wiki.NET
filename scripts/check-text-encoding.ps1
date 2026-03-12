param(
    [string]$RootPath = "."
)

$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path -Path $RootPath).Path

$includeExtensions = @(".cs", ".razor", ".cshtml", ".css", ".js", ".json")
$excludeDirectoryParts = @(
    "\\.git\\",
    "\\bin\\",
    "\\obj\\",
    "\\node_modules\\",
    "\\wwwroot\\js\\devextreme\\",
    "\\wwwroot\\css\\devextreme\\",
    "\\wwwroot\\css\\bootstrap\\",
    "\\wwwroot\\lib\\"
)

$allowedPathRegex = "\\(Components|Services|Data)\\"

$suspiciousChars = @(
    [char]0xFFFD,
    [char]0x00C3,
    [char]0x00C2,
    [char]0x00E2
)

$hits = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -Path $resolvedRoot -Recurse -File | ForEach-Object {
    $file = $_
    if ($includeExtensions -notcontains $file.Extension.ToLowerInvariant()) {
        return
    }

    if ($file.FullName -notmatch $allowedPathRegex) {
        return
    }

    foreach ($excludedPart in $excludeDirectoryParts) {
        if ($file.FullName -match $excludedPart) {
            return
        }
    }

    $lineNumber = 0
    foreach ($line in Get-Content -Path $file.FullName) {
        $lineNumber++
        if ($line.IndexOfAny($suspiciousChars) -ge 0) {
            $hits.Add("$($file.FullName):${lineNumber}: $line")
        }
    }
}

if ($hits.Count -gt 0) {
    Write-Host "Encoding-Check fehlgeschlagen. Verdaechtige Zeichenfolgen gefunden:" -ForegroundColor Red
    $hits | ForEach-Object { Write-Host $_ }
    exit 1
}

Write-Host "Encoding-Check erfolgreich. Keine verdaechtigen Zeichenfolgen gefunden." -ForegroundColor Green
exit 0
