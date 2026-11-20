# fetch-model.ps1
# Downloads the GGUF model for the AI assistant and verifies SHA256.

param(
    [string]$ModelUrl = "https://huggingface.co/TheBloke/Llama-2-7B-Chat-GGUF/resolve/main/llama-2-7b-chat.Q4_K_M.gguf",
    [string]$OutputPath = "AI\models\llama-2-7b-chat.Q4_K_M.gguf",
    [string]$ExpectedSha256 = "REPLACE_WITH_ACTUAL_SHA256"
)

if (Test-Path $OutputPath) {
    Write-Host "Model already exists at $OutputPath"
    exit 0
}

Write-Host "Downloading model from $ModelUrl ..."
try {
    Invoke-WebRequest -Uri $ModelUrl -OutFile $OutputPath -UseBasicParsing
} catch {
    Write-Error "Download failed: $_"
    exit 1
}

$actual = Get-FileHash -Algorithm SHA256 -Path $OutputPath | Select-Object -ExpandProperty Hash
if ($actual -ne $ExpectedSha256) {
    Write-Error "SHA256 mismatch! Expected $ExpectedSha256, got $actual"
    Remove-Item $OutputPath -Force
    exit 1
}

Write-Host "Model downloaded and verified."
exit 0
