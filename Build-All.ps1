# Build-All.ps1 - single deterministic build for the Student Management System.
# Produces build\stage\site, build\stage\AI and dist\StudentManagementSystem-Setup.exe.
$ErrorActionPreference = "Stop"

$ROOT = "D:\4.1C#Project"
$SRC = "$ROOT\StudentManagementSystem"
$SLN = "$ROOT\StudentManagementSystem.sln"
$BUILD_STAGE = "$ROOT\build\stage"
$STAGE_SITE = "$BUILD_STAGE\site"
$DIST = "$ROOT\dist"
$DIST_SITE = "$DIST\StudentManagementSystem"
$BUILD_STAMP_FILE = "$ROOT\BUILD-STAMP.txt"
$INSTALLER_ISS = "$ROOT\Deploy\installer.iss"

Write-Host "=========================================="
Write-Host "BUILD START $(Get-Date)"
Write-Host "=========================================="

$global:BUILD_ID = Get-Date -Format "yyyyMMdd-HHmm"
$global:CSS_HASH = "unknown"
$global:BUILD_STAMP = ""

function Clean-Outputs {
    Write-Host "[1/8] Cleaning outputs..."
    foreach ($p in @("$STAGE_SITE", "$BUILD_STAGE\AI", "$SRC\bin", "$SRC\obj")) {
        if (Test-Path $p) { Remove-Item -Recurse -Force $p -ErrorAction SilentlyContinue }
    }
    if (Test-Path $DIST_SITE) { Remove-Item -Recurse -Force $DIST_SITE -ErrorAction SilentlyContinue }
}

function Find-MSBuild {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    throw "MSBuild not found."
}

function Build-Solution {
    Write-Host "[2/8] Building solution (Release)..."
    $msbuild = Find-MSBuild
    & $msbuild $SLN /t:Build /p:Configuration=Release /m /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "BUILD FAILED" }
}

function Publish-Site {
    Write-Host "[3/8] Publishing site to build/stage..."
    New-Item -ItemType Directory -Force -Path $STAGE_SITE | Out-Null

    Copy-Item "$SRC\*.aspx" $STAGE_SITE -Recurse -Force
    Copy-Item "$SRC\*.master" $STAGE_SITE -Recurse -Force
    Copy-Item "$SRC\Web.config" $STAGE_SITE -Force
    & "$ROOT\tools\Patch-WebConfig.ps1" -Path "$STAGE_SITE\Web.config" -Port 8080
    Copy-Item "$SRC\Global.asax" $STAGE_SITE -Force
    Copy-Item "$SRC\StyleGuide.html" $STAGE_SITE -Force -ErrorAction SilentlyContinue

    foreach ($dir in @("Account","Admin","Faculty","Student","Chatbot","Handlers","Help","Notifications","Timetable","Calendar")) {
        $dest = Join-Path $STAGE_SITE $dir
        New-Item -ItemType Directory -Force -Path $dest | Out-Null
        Copy-Item "$SRC\$dir\*.aspx" $dest -Recurse -Force
        Copy-Item "$SRC\$dir\*.master" $dest -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item "$SRC\$dir\*.ashx" $dest -Recurse -Force -ErrorAction SilentlyContinue
    }

    # RECURSIVE: css\pages, js, fonts, icons, vendor must all arrive together.
    Copy-Item "$SRC\Content" "$STAGE_SITE\Content" -Recurse -Force
    Copy-Item "$SRC\Scripts" "$STAGE_SITE\Scripts" -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$SRC\Database" "$STAGE_SITE\Database" -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$SRC\Reports" "$STAGE_SITE\Reports" -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item "$SRC\App_Data" "$STAGE_SITE\App_Data" -Recurse -Force -ErrorAction SilentlyContinue
    # Never ship logs (or anything the runtime may hold open); the app recreates them.
    if (Test-Path "$STAGE_SITE\App_Data\logs") { Remove-Item "$STAGE_SITE\App_Data\logs\*" -Recurse -Force -ErrorAction SilentlyContinue }

    New-Item -ItemType Directory -Force -Path "$STAGE_SITE\bin" | Out-Null
    $dll = "$SRC\bin\StudentManagementSystem.dll"
    if (-not (Test-Path $dll)) { throw "Compiled DLL missing: $dll" }
    Copy-Item "$SRC\bin\*.dll" "$STAGE_SITE\bin\" -Force
    Copy-Item "$SRC\bin\*.pdb" "$STAGE_SITE\bin\" -Force -ErrorAction SilentlyContinue

    # The AI component lives next to the site: {app}\AI. The installer copies it there.
    New-Item -ItemType Directory -Force -Path "$BUILD_STAGE\AI\model" | Out-Null
    New-Item -ItemType Directory -Force -Path "$BUILD_STAGE\AI\runtime" | Out-Null
    Copy-Item "$ROOT\AI\model\*" "$BUILD_STAGE\AI\model\" -Force
    Copy-Item "$ROOT\AI\runtime\*" "$BUILD_STAGE\AI\runtime\" -Recurse -Force

    # Verify the exact files that were previously missing.
    foreach ($required in @(
        "Content\css\pages\auth.css", "Content\css\pages\dashboard.css",
        "Content\css\pages\chat.css", "Content\css\pages\settings.css",
        "Content\css\pages\misc.css", "Content\css\base.css",
        "Content\js\ai.js", "Content\js\ai-quick.js",
        "AI\model\Ektome-Qwen1.5-0.5B-Chat-PristinelyUncensored.Q2_K.gguf",
        "AI\runtime\llama-server.exe")) {
        $probe = if ($required.StartsWith("AI\")) { Join-Path $BUILD_STAGE $required } else { Join-Path $STAGE_SITE $required }
        if (-not (Test-Path $probe)) { throw "Publish verification failed: missing $probe" }
    }

    Write-Host "Published to build/stage (Content recursive + AI)."
}

function Compute-BuildStamp {
    Write-Host "[4/8] Computing build stamp..."
    $hash = (Get-FileHash "$STAGE_SITE\Content\css\base.css" -Algorithm SHA256).Hash
    $global:CSS_HASH = $hash.Substring(0, 8)
    $global:BUILD_STAMP = "BUILD $global:BUILD_ID / $global:CSS_HASH"
    Set-Content -Path $BUILD_STAMP_FILE -Value $global:BUILD_STAMP -Encoding ASCII
    Set-Content -Path "$STAGE_SITE\BUILD-STAMP.txt" -Value $global:BUILD_STAMP -Encoding ASCII
    Write-Host "Build stamp: $global:BUILD_STAMP"
}

function Version-AssetLinks {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return }
    $c = Get-Content $Path -Raw
    $ver = "$global:BUILD_ID-$global:CSS_HASH"
    $c = [regex]::Replace($c, '((?:href|src)=")(/Content/[^"?]+\.(?:css|js))(?:\?v=[^"]*)?(")', {
        param($m)
        return $m.Groups[1].Value + $m.Groups[2].Value + "?v=" + $ver + $m.Groups[3].Value
    })
    Set-Content -Path $Path -Value $c -Encoding UTF8
}

function Inject-BuildStamp {
    Write-Host "[5/8] Versioning assets and injecting the shared footer..."
    # BOTH masters: authenticated and auth pages.
    Version-AssetLinks "$STAGE_SITE\Site.Master"
    Version-AssetLinks "$STAGE_SITE\Account\Account.master"
    Version-AssetLinks "$STAGE_SITE\StyleGuide.html"

    $footer = 'Made with <span class="footer-heart" aria-hidden="true">&#9829;</span> by <strong>QuantumGlitch404</strong>'

    $master = "$STAGE_SITE\Site.Master"
    if (Test-Path $master) {
        $c = Get-Content $master -Raw
        $replacement = '<span class="footer-made">' + $footer + '</span>'
        $regex = [System.Text.RegularExpressions.Regex]::new('(?s)(<footer class="app-footer">).*?(</footer>)')
        $c = $regex.Replace($c, '$1' + $replacement + '$2')
        Set-Content -Path $master -Value $c -Encoding UTF8
    }

    $masterAuth = "$STAGE_SITE\Account\Account.master"
    if (Test-Path $masterAuth) {
        $c = Get-Content $masterAuth -Raw
        $replacement = '<p class="auth__footer auth-footnote">' + $footer + '</p>'
        $regex = [System.Text.RegularExpressions.Regex]::new('(?s)(<p class="auth__footer auth-footnote">).*?(</p>)')
        $c = $regex.Replace($c, $replacement)
        Set-Content -Path $masterAuth -Value $c -Encoding UTF8
    }

    # Standalone pages that do not inherit a master still get versioned links.
    Get-ChildItem $STAGE_SITE -Recurse -Include *.aspx,*.master | ForEach-Object { Version-AssetLinks $_.FullName }
}

function Copy-ToDist {
    Write-Host "[6/8] Copying to dist..."
    New-Item -ItemType Directory -Force -Path $DIST | Out-Null
    Copy-Item "$STAGE_SITE" $DIST_SITE -Recurse -Force
    Set-Content -Path "$DIST_SITE\BUILD-STAMP.txt" -Value $global:BUILD_STAMP -Encoding ASCII
}

function Find-ISCC {
    $candidates = @(
        "E:\SMS-Build\InnoSetup\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

function Build-SetupHelper {
    Write-Host "[6b/8] Building database helper (SmsSetup.exe)..."
    $tools = "$BUILD_STAGE\tools"
    New-Item -ItemType Directory -Force -Path $tools | Out-Null
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    if (-not (Test-Path $csc)) { throw "csc.exe not found for SmsSetup." }
    & $csc /nologo /target:exe /out:"$tools\SmsSetup.exe" "$ROOT\Deploy\SetupHelper\SmsSetup.cs"
    if ($LASTEXITCODE -ne 0) { throw "SmsSetup build failed" }
}

function Build-Installer {
    Write-Host "[7/8] Building installer..."
    $iscc = Find-ISCC
    if (-not $iscc) { throw "Inno Setup (ISCC.exe) not found. Install it or set E:\SMS-Build\InnoSetup\ISCC.exe." }
    & $iscc $INSTALLER_ISS
    if ($LASTEXITCODE -ne 0) { throw "INSTALLER BUILD FAILED" }
    $exe = "$DIST\StudentManagementSystem-Setup.exe"
    if (-not (Test-Path $exe)) { throw "Installer output missing: $exe" }
    $size = (Get-Item $exe).Length
    Write-Host "[8/8] Installer: $exe ($([math]::Round($size/1MB,1)) MB)"
}

try {
    Clean-Outputs
    Build-Solution
    Publish-Site
    Build-SetupHelper
    Compute-BuildStamp
    Inject-BuildStamp
    Copy-ToDist
    Build-Installer
    Write-Host "Done."
    Write-Host "Build stamp: $global:BUILD_STAMP"
    Write-Host "=========================================="
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}

