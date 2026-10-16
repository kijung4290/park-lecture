param([switch]$Test, [string]$OutputDirectory = 'bin')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '.NET Framework 4.8 is required.' }
$outputDir = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$compileDir = Join-Path ([System.IO.Path]::GetTempPath()) ('parklecture-build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $compileDir | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'app.manifest') -Destination $compileDir
    Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\parklecture.ico') -Destination $compileDir
    $updateReferences = @('/reference:System.Web.Extensions.dll', '/reference:System.Net.Http.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll')
    $updateKey = "/resource:$projectRoot\assets\update-public-key.xml,ParkLecture.UpdateKey.xml"
    $fontResources = @(
        "/resource:$projectRoot\assets\fonts\Pretendard-Regular.ttf,ParkLecture.Pretendard.Regular.ttf",
        "/resource:$projectRoot\assets\fonts\Pretendard-Bold.ttf,ParkLecture.Pretendard.Bold.ttf",
        "/resource:$projectRoot\assets\fonts\OFL.txt,ParkLecture.Pretendard.License.txt"
    )
    & $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$compileDir\ParkLecture.exe" "/win32manifest:$compileDir\app.manifest" "/win32icon:$compileDir\parklecture.ico" "/resource:$projectRoot\assets\parkjuim-logo.png,ParkLecture.Logo.png" "/resource:$projectRoot\assets\parklecture.ico,ParkLecture.App.ico" $updateKey $fontResources /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:Microsoft.VisualBasic.dll $updateReferences $sourceFiles
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Copy-Item -LiteralPath (Join-Path $compileDir 'ParkLecture.exe') -Destination $outputDir -Force
    & $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$compileDir\ParkLecture.Updater.exe" "/win32manifest:$compileDir\app.manifest" $updateKey /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll $updateReferences (Join-Path $projectRoot 'updater\Program.cs') (Join-Path $projectRoot 'src\UpdatePackage.cs') (Join-Path $projectRoot 'src\UpdateInstaller.cs') (Join-Path $projectRoot 'src\AssemblyInfo.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Updater build failed.' }
    Copy-Item -LiteralPath (Join-Path $compileDir 'ParkLecture.Updater.exe') -Destination $outputDir -Force
}
finally {
    Get-ChildItem -LiteralPath $compileDir -File -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    if (Test-Path -LiteralPath $compileDir) { Remove-Item -LiteralPath $compileDir -Force }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'ParkLecture.exe.config') -Destination $outputDir -Force
Write-Output "Built: $outputDir\ParkLecture.exe"
if ($Test) {
    $reportPath = Join-Path $outputDir 'self-test.txt'
    $testStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $testStartInfo.FileName = Join-Path $outputDir 'ParkLecture.exe'
    $testStartInfo.Arguments = '--self-test "' + $reportPath + '"'
    $testStartInfo.UseShellExecute = $false
    $testStartInfo.CreateNoWindow = $true
    $testProcess = [System.Diagnostics.Process]::Start($testStartInfo)
    $testProcess.WaitForExit()
    if (Test-Path -LiteralPath $reportPath) { Get-Content -LiteralPath $reportPath -Encoding UTF8 }
    if ($testProcess.ExitCode -ne 0) { throw 'Self tests failed.' }
}
