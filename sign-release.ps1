param([Parameter(Mandatory = $true)][string]$Package, [Parameter(Mandatory = $true)][string]$Setup, [Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a numeric three-part release version.' }
$signingXml = $env:PARKLECTURE_SIGNING_KEY
if (-not $signingXml) {
    $signingKeyPath = Join-Path $env:LOCALAPPDATA 'ParkLecture\release-signing-key.xml'
    if (-not (Test-Path -LiteralPath $signingKeyPath)) {
        $fallbackKeyPath = Join-Path $env:LOCALAPPDATA 'ParkCapture\release-signing-key.xml'
        if (Test-Path -LiteralPath $fallbackKeyPath) { $signingKeyPath = $fallbackKeyPath }
        else { throw 'Set PARKLECTURE_SIGNING_KEY to the private update signing key.' }
    }
    $signingXml = [System.IO.File]::ReadAllText($signingKeyPath)
}
$signingRsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
$signingRsa.PersistKeyInCsp = $false
try {
    $signingRsa.FromXmlString($signingXml)
    $publicXml = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'assets\update-public-key.xml'))
    if ($signingRsa.ToXmlString($false) -ne $publicXml.Trim()) { throw 'Signing key does not match the public key embedded in the app.' }
    $packageInfo = Get-Item -LiteralPath $Package
    if ($packageInfo.Name -ne "ParkLecture-$Version-portable.zip") { throw 'Package filename does not match its version.' }
    $setupInfo = Get-Item -LiteralPath $Setup
    if ($setupInfo.Name -ne 'ParkLecture-Setup.exe' -or $setupInfo.VersionInfo.FileVersion.Trim() -ne "$Version.0") { throw 'Installer filename or version does not match the release.' }
    $manifest = [ordered]@{
        schema = 1
        version = $Version
        asset = $packageInfo.Name
        sha256 = (Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash.ToLowerInvariant()
        size = $packageInfo.Length
        setup = [ordered]@{
            asset = $setupInfo.Name
            sha256 = (Get-FileHash -LiteralPath $Setup -Algorithm SHA256).Hash.ToLowerInvariant()
            size = $setupInfo.Length
        }
    }
    $manifestBytes = [System.Text.Encoding]::UTF8.GetBytes(($manifest | ConvertTo-Json -Compress))
    $manifestPath = Join-Path $packageInfo.DirectoryName 'update.json'
    [System.IO.File]::WriteAllBytes($manifestPath, $manifestBytes)
    $signature = $signingRsa.SignData($manifestBytes, [System.Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
    [System.IO.File]::WriteAllText("$manifestPath.sig", [Convert]::ToBase64String($signature), [System.Text.Encoding]::ASCII)
    Write-Output "Signed update manifest: $manifestPath"
}
finally { $signingRsa.Dispose(); $signingXml = $null }
