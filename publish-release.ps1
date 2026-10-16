param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version.' }
$releaseTag = "v$Version"
$releaseRepo = 'kijung4290/park-lecture'
$releaseAssets = @((Join-Path $PSScriptRoot "dist\ParkLecture-$Version-portable.zip"), (Join-Path $PSScriptRoot 'dist\update.json'), (Join-Path $PSScriptRoot 'dist\update.json.sig'))
foreach ($releaseAsset in $releaseAssets) { if (-not (Test-Path -LiteralPath $releaseAsset)) { throw "Missing release asset: $releaseAsset" } }
$releaseList = gh release list --repo $releaseRepo --limit 100 --json tagName,isDraft | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw 'Could not read repository releases.' }
$releaseExisting = $releaseList | Where-Object { $_.tagName -eq $releaseTag }
$releaseExists = $null -ne $releaseExisting
if ($releaseExists -and -not $releaseExisting.isDraft) { throw 'This version is already published. Increase the app version.' }
if (-not $releaseExists) {
    $releaseCommit = git rev-parse HEAD
    gh release create $releaseTag --repo $releaseRepo --target $releaseCommit --draft --title "Park Lecture $releaseTag" --notes-file (Join-Path $PSScriptRoot 'RELEASE_NOTES.md')
    if ($LASTEXITCODE -ne 0) { throw 'Could not create release draft.' }
}
gh release upload $releaseTag --repo $releaseRepo @releaseAssets --clobber
if ($LASTEXITCODE -ne 0) { throw 'Could not upload release files. Release remains a draft.' }
gh release edit $releaseTag --repo $releaseRepo --draft=false --latest
if ($LASTEXITCODE -ne 0) { throw 'Could not publish release.' }
