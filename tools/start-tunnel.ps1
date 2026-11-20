# start-tunnel.ps1 - public HTTPS webhook URL without an account (Cloudflare quick tunnel).
# Requires internet on first run; cloudflared is cached under tools\bin.
[CmdletBinding()]
param(
    [int]$Port = 8080,
    [string]$Server = "(LocalDB)\MSSQLLocalDB",
    [string]$Database = "StudentSMS",
    [switch]$NoSave
)

$ErrorActionPreference = "Stop"
$bin = Join-Path $PSScriptRoot "bin"
$exe = Join-Path $bin "cloudflared.exe"
if (-not (Test-Path $bin)) { New-Item -ItemType Directory -Path $bin | Out-Null }
if (-not (Test-Path $exe)) {
    Write-Host "Downloading cloudflared (official GitHub release)..."
    $url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
    Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
}
if (-not (Test-Path $exe)) { throw "cloudflared.exe was not downloaded." }

$log = Join-Path $env:TEMP ("sms-tunnel-" + [Guid]::NewGuid().ToString("N") + ".log")
$proc = Start-Process -FilePath $exe -ArgumentList @("tunnel", "--url", "http://localhost:$Port", "--no-autoupdate") `
    -RedirectStandardError $log -RedirectStandardOutput ($log + ".out") -WindowStyle Hidden -PassThru
Write-Host "Starting quick tunnel to http://localhost:$Port ..."
$public = $null
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Milliseconds 500
    if (Test-Path $log) {
        $text = Get-Content -LiteralPath $log -Raw -ErrorAction SilentlyContinue
        $m = [regex]::Match($text, "https://[a-z0-9-]+\.trycloudflare\.com")
        if ($m.Success) { $public = $m.Value; break }
    }
    if ($proc.HasExited) { break }
}
if (-not $public) {
    Write-Host "Could not read the tunnel URL. See $log" -ForegroundColor Red
    exit 1
}

$baseUrl = $public.TrimEnd('/') + "/"
Write-Host ""
Write-Host "Public base URL : $baseUrl" -ForegroundColor Green
Write-Host "Razorpay webhook: $($baseUrl)Handlers/RazorpayWebhook.ashx" -ForegroundColor Green
Write-Host "Google redirect : $($baseUrl)Account/GoogleCallback" -ForegroundColor Green
Write-Host ""
if (-not $NoSave) {
    try {
        $cs = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;Connect Timeout=5"
        $conn = New-Object System.Data.SqlClient.SqlConnection $cs
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = @"
MERGE dbo.AppSecrets AS t USING (SELECT 'App:BaseUrl' AS K, @V AS V) s ON t.SecretKey=s.K
WHEN MATCHED THEN UPDATE SET SecretValue=s.V, IsEncrypted=0, UpdatedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SecretKey, SecretValue, IsEncrypted, Description) VALUES (s.K, s.V, 0, 'Public base URL (quick tunnel)');
"@
        [void]$cmd.Parameters.AddWithValue("@V", $baseUrl)
        [void]$cmd.ExecuteNonQuery()
        $conn.Dispose()
        Write-Host "Saved as App:BaseUrl in $Database. Reload Settings to confirm." -ForegroundColor DarkGray
    } catch {
        Write-Host "Tunnel is running, but the URL could not be saved automatically. Paste it in Settings > Integrations." -ForegroundColor Yellow
    }
}
Write-Host "Keep this window open. Press Ctrl+C to stop the tunnel." -ForegroundColor DarkGray
try { Wait-Process -Id $proc.Id } finally {
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}
