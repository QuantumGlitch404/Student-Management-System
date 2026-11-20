param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$Port = 8080
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Web.config not found: $Path"
}

$content = Get-Content -Raw -LiteralPath $Path

# App:BaseUrl drives e-mail links, the Google redirect URI and webhook callbacks.
$content = [regex]::Replace(
    $content,
    '(<add key="App:BaseUrl" value=")[^"]*(")',
    ('${1}http://localhost:' + $Port + '/${2}'))

# Production build: no debug symbols in the shipped configuration.
$content = $content -replace '<compilation debug="true"', '<compilation debug="false"'

Set-Content -LiteralPath $Path -Value $content -Encoding UTF8
Write-Host ("Patched {0}: App:BaseUrl = http://localhost:{1}/, debug = false" -f $Path, $Port)
