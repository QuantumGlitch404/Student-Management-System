# provision-secrets.ps1
# Stores integration credentials in dbo.AppSecrets using DPAPI (LocalMachine).
# Secrets are never written to Web.config, source, logs or temp files.
# Run on the same machine (and, for a non-default IIS identity, under an account
# that can decrypt machine-scope DPAPI) as the SMS application.
#
# Example (values are prompted when omitted):
#   powershell -ExecutionPolicy Bypass -File tools\provision-secrets.ps1
#
[CmdletBinding()]
param(
    [string]$Server = "(LocalDB)\MSSQLLocalDB",
    [string]$Database = "StudentSMS",
    [string]$GmailAddress,
    [string]$GmailAppPassword,
    [string]$GoogleClientId,
    [string]$GoogleClientSecret,
    [string]$RazorpayKeyId,
    [string]$RazorpayKeySecret,
    [string]$WebhookSecret,
    [string]$BaseUrl = "http://localhost:8080/",
    [string]$FromName = "Student Management System",
    [switch]$SkipMailAccount,
    [switch]$NoPrompt
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Security
Add-Type -AssemblyName System.Data

function Read-Secret([string]$Prompt) {
    if ($NoPrompt) { return "" }
    $secure = Read-Host -AsSecureString $Prompt
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Protect-Secret([string]$Plain) {
    if ([string]::IsNullOrWhiteSpace($Plain)) { return $null }
    $bytes = [Text.Encoding]::UTF8.GetBytes($Plain)
    $enc = [Security.Cryptography.ProtectedData]::Protect($bytes, $null, [Security.Cryptography.DataProtectionScope]::LocalMachine)
    return "enc:v1:" + [Convert]::ToBase64String($enc)
}

function New-WebhookSecret {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($bytes).Replace("+", "-").Replace("/", "_").Replace("=", "")
}

if ([string]::IsNullOrWhiteSpace($GmailAddress)) { $GmailAddress = $env:SMS_GMAIL_ADDRESS }
if ([string]::IsNullOrWhiteSpace($GmailAppPassword)) { $GmailAppPassword = $env:SMS_GMAIL_APP_PASSWORD }
if ([string]::IsNullOrWhiteSpace($GoogleClientId)) { $GoogleClientId = $env:SMS_GOOGLE_CLIENT_ID }
if ([string]::IsNullOrWhiteSpace($GoogleClientSecret)) { $GoogleClientSecret = $env:SMS_GOOGLE_CLIENT_SECRET }
if ([string]::IsNullOrWhiteSpace($RazorpayKeyId)) { $RazorpayKeyId = $env:SMS_RAZORPAY_KEY_ID }
if ([string]::IsNullOrWhiteSpace($RazorpayKeySecret)) { $RazorpayKeySecret = $env:SMS_RAZORPAY_KEY_SECRET }
if ([string]::IsNullOrWhiteSpace($WebhookSecret)) { $WebhookSecret = $env:SMS_RAZORPAY_WEBHOOK_SECRET }

if ([string]::IsNullOrWhiteSpace($GmailAppPassword)) { $GmailAppPassword = Read-Secret "Gmail app password" }
if ([string]::IsNullOrWhiteSpace($GoogleClientId) -and -not $NoPrompt) { $GoogleClientId = Read-Host "Google OAuth client id (blank to skip)" }
if ([string]::IsNullOrWhiteSpace($GoogleClientSecret) -and $GoogleClientId) { $GoogleClientSecret = Read-Secret "Google OAuth client secret" }
if ([string]::IsNullOrWhiteSpace($RazorpayKeyId) -and -not $NoPrompt) { $RazorpayKeyId = Read-Host "Razorpay TEST key id (blank to skip)" }
if ([string]::IsNullOrWhiteSpace($RazorpayKeySecret) -and $RazorpayKeyId) { $RazorpayKeySecret = Read-Secret "Razorpay TEST key secret" }

$razorpayReady = (-not [string]::IsNullOrWhiteSpace($RazorpayKeyId)) -and (-not [string]::IsNullOrWhiteSpace($RazorpayKeySecret))
if ($razorpayReady -and [string]::IsNullOrWhiteSpace($WebhookSecret)) { $WebhookSecret = New-WebhookSecret }
$googleReady = (-not [string]::IsNullOrWhiteSpace($GoogleClientId)) -and (-not [string]::IsNullOrWhiteSpace($GoogleClientSecret))
$mailReady = (-not [string]::IsNullOrWhiteSpace($GmailAddress)) -and (-not [string]::IsNullOrWhiteSpace($GmailAppPassword))

$connectionString = "Data Source=$Server;Initial Catalog=$Database;Integrated Security=True;Connect Timeout=10;MultipleActiveResultSets=True"
$conn = New-Object System.Data.SqlClient.SqlConnection $connectionString
try {
    $conn.Open()
} catch {
    Write-Host "FAILED: cannot open $Database on $Server. Install the app first." -ForegroundColor Red
    throw
}

function Exec([string]$Sql, [hashtable]$Params) {
    $cmd = $conn.CreateCommand()
    try {
        $cmd.CommandText = $Sql
        $cmd.CommandTimeout = 60
        foreach ($k in $Params.Keys) {
            $v = $Params[$k]
            if ($null -eq $v) { $v = [DBNull]::Value }
            [void]$cmd.Parameters.AddWithValue($k, $v)
        }
        return $cmd.ExecuteNonQuery()
    } finally { $cmd.Dispose() }
}

function Scalar([string]$Sql, [hashtable]$Params) {
    $cmd = $conn.CreateCommand()
    try {
        $cmd.CommandText = $Sql
        foreach ($k in $Params.Keys) {
            $v = $Params[$k]
            if ($null -eq $v) { $v = [DBNull]::Value }
            [void]$cmd.Parameters.AddWithValue($k, $v)
        }
        return $cmd.ExecuteScalar()
    } finally { $cmd.Dispose() }
}

try {
    Exec @"
IF OBJECT_ID('dbo.AppSecrets','U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSecrets (
        SecretKey NVARCHAR(100) NOT NULL CONSTRAINT PK_AppSecrets PRIMARY KEY,
        SecretValue NVARCHAR(MAX) NULL,
        IsEncrypted BIT NOT NULL CONSTRAINT DF_AppSecrets_IsEncrypted DEFAULT (1),
        Description NVARCHAR(200) NULL,
        UpdatedBy INT NULL,
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppSecrets_UpdatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END
"@ @{}

    function Set-Secret([string]$Key, [string]$Value, [bool]$Encrypt, [string]$Description) {
        if ([string]::IsNullOrWhiteSpace($Value)) { return }
        $stored = if ($Encrypt) { Protect-Secret $Value } else { $Value }
        Exec @"
MERGE dbo.AppSecrets AS t
USING (SELECT @K AS K, @V AS V, @E AS E, @D AS D) s
ON t.SecretKey = s.K
WHEN MATCHED THEN UPDATE SET SecretValue=s.V, IsEncrypted=s.E, Description=s.D, UpdatedAtUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SecretKey, SecretValue, IsEncrypted, Description) VALUES (s.K, s.V, s.E, s.D);
"@ @{ "@K" = $Key; "@V" = $stored; "@E" = $(if ($Encrypt) { 1 } else { 0 }); "@D" = $Description }
    }

    function Set-Setting([string]$Key, [string]$Value) {
        Exec @"
IF OBJECT_ID('dbo.InstitutionSettings','U') IS NOT NULL
BEGIN
    MERGE dbo.InstitutionSettings AS t USING (SELECT @K AS K, @V AS V) s ON t.SettingKey=s.K
    WHEN MATCHED THEN UPDATE SET SettingValue=s.V, UpdatedAtUtc=SYSUTCDATETIME()
    WHEN NOT MATCHED THEN INSERT (SettingKey, SettingValue) VALUES (s.K, s.V);
END
"@ @{ "@K" = $Key; "@V" = $Value }
    }

    Set-Secret "App:BaseUrl" $BaseUrl $false "Public base URL for links, OAuth and webhooks"
    Set-Secret "Email:FromName" $FromName $false "Sender display name"
    if ($mailReady) {
        Set-Secret "Email:Enabled" "true" $false "SMTP enabled"
        Set-Secret "Email:FromAddress" $GmailAddress $false "From address"
        Set-Secret "Email:SmtpHost" "smtp.gmail.com" $false "SMTP host"
        Set-Secret "Email:SmtpPort" "587" $false "SMTP port"
        Set-Secret "Email:EnableSsl" "true" $false "SMTP STARTTLS"
        Set-Secret "Email:User" $GmailAddress $true "SMTP username"
        Set-Secret "Email:Password" $GmailAppPassword $true "SMTP app password"
        Set-Setting "Mail.FromName" $FromName
    }
    if ($googleReady) {
        Set-Secret "GoogleAuth:Enabled" "true" $false "Google sign-in enabled"
        Set-Secret "GoogleAuth:ClientId" $GoogleClientId $true "Google OAuth client id"
        Set-Secret "GoogleAuth:ClientSecret" $GoogleClientSecret $true "Google OAuth client secret"
    }
    if ($razorpayReady) {
        Set-Secret "Razorpay:Enabled" "true" $false "Razorpay enabled"
        Set-Secret "Razorpay:KeyId" $RazorpayKeyId $true "Razorpay key id"
        Set-Secret "Razorpay:KeySecret" $RazorpayKeySecret $true "Razorpay key secret"
        Set-Secret "Razorpay:WebhookSecret" $WebhookSecret $true "Razorpay webhook signing secret"
        Set-Setting "Payments.Provider" "razorpay"
    }
    Set-Setting "Payments.DemoEnabled" "0"

    if ($mailReady -and -not $SkipMailAccount) {
        $existing = Scalar "SELECT TOP 1 AccountId FROM dbo.MailAccounts WHERE EmailAddress=@E" @{ "@E" = $GmailAddress }
        $smtpEnc = Protect-Secret $GmailAppPassword
        $imapEnc = Protect-Secret $GmailAppPassword
        if ($null -ne $existing -and "$existing" -ne "") {
            Exec @"
UPDATE dbo.MailAccounts SET Label='Gmail', DisplayName=@Display, SmtpHost='smtp.gmail.com', SmtpPort=587,
 SmtpSsl=1, SmtpUser=@Email, SmtpPasswordEnc=@SmtpPwd, ImapHost='imap.gmail.com', ImapPort=993, ImapSsl=1,
 ImapUser=@Email, ImapPasswordEnc=@ImapPwd, IsDefault=1, IsActive=1, LastError=NULL
WHERE AccountId=@Id;
UPDATE dbo.MailAccounts SET IsDefault=CASE WHEN AccountId=@Id THEN 1 ELSE 0 END;
"@ @{ "@Id" = [int]$existing; "@Email" = $GmailAddress; "@Display" = $FromName; "@SmtpPwd" = $smtpEnc; "@ImapPwd" = $imapEnc }
        } else {
            $newId = Scalar @"
INSERT INTO dbo.MailAccounts (Label, EmailAddress, DisplayName, SmtpHost, SmtpPort, SmtpSsl, SmtpUser, SmtpPasswordEnc,
 ImapHost, ImapPort, ImapSsl, ImapUser, ImapPasswordEnc, DailyLimit, PerMinute, IsDefault, IsActive)
OUTPUT INSERTED.AccountId
VALUES ('Gmail', @Email, @Display, 'smtp.gmail.com', 587, 1, @Email, @SmtpPwd,
 'imap.gmail.com', 993, 1, @Email, @ImapPwd, 400, 20, 1, 1);
"@ @{ "@Email" = $GmailAddress; "@Display" = $FromName; "@SmtpPwd" = $smtpEnc; "@ImapPwd" = $imapEnc }
            Exec "UPDATE dbo.MailAccounts SET IsDefault=CASE WHEN AccountId=@Id THEN 1 ELSE 0 END" @{ "@Id" = [int]$newId }
        }
    }

    Write-Host "Provisioned encrypted settings in $Database." -ForegroundColor Green
    Write-Host ("Mail: {0} | Google: {1} | Razorpay: {2}" -f $(if ($mailReady) { "configured" } else { "skipped" }), $(if ($googleReady) { "configured" } else { "skipped" }), $(if ($razorpayReady) { "configured" } else { "skipped" }))
    if ($razorpayReady) {
        Write-Host "Razorpay webhook URL: $($BaseUrl.TrimEnd('/'))/Handlers/RazorpayWebhook.ashx"
        Write-Host "RAZORPAY_WEBHOOK_SECRET=$WebhookSecret"
        Write-Host "Add the four events in the Razorpay dashboard: payment.captured, payment.failed, order.paid, refund.processed."
    }
    if ($googleReady) {
        Write-Host "Google redirect URI: $($BaseUrl.TrimEnd('/'))/Account/GoogleCallback"
    }
} finally {
    $conn.Dispose()
}
