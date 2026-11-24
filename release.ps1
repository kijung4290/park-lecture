param([switch]$SkipSigning)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -Test
$releaseDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
$releaseVersion = (Get-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\ParkLecture.exe')).VersionInfo.FileVersion -replace '\.0$', ''
$releaseZip = Join-Path $releaseDir "ParkLecture-$releaseVersion-portable.zip"
Compress-Archive -LiteralPath @((Join-Path $PSScriptRoot 'bin\ParkLecture.exe'), (Join-Path $PSScriptRoot 'bin\ParkLecture.exe.config'), (Join-Path $PSScriptRoot 'bin\ParkLecture.Updater.exe'), (Join-Path $PSScriptRoot 'README.md')) -DestinationPath $releaseZip -Force
$iscc = $env:PARKLECTURE_ISCC
if (-not $iscc) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $iscc = $command.Source }
    else { $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
}
if (-not (Test-Path -LiteralPath $iscc -PathType Leaf)) { throw 'Install Inno Setup 6 or set PARKLECTURE_ISCC to the path to ISCC.exe.' }
& $iscc /Qp "/DAppVersion=$releaseVersion" (Join-Path $PSScriptRoot 'installer\ParkLecture.iss')
if ($LASTEXITCODE -ne 0) { throw 'Windows installer build failed.' }
$setup = Join-Path $releaseDir 'ParkLecture-Setup.exe'
if (-not $SkipSigning) {
    & (Join-Path $PSScriptRoot 'sign-release.ps1') -Package $releaseZip -Setup $setup -Version $releaseVersion
}
Write-Output "Portable package: $releaseZip"
Write-Output "Windows installer: $setup"
