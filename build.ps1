param(
    [Parameter(Mandatory=$true)][string]$DotnetPath,
    [Parameter(Mandatory=$true)][string]$GamePath,
    [string]$OutputPath = (Join-Path $PSScriptRoot 'artifacts'),
    [string]$PhysicsReferencesPath,
    [string]$RenderReferencesPath
)
$ErrorActionPreference='Stop'
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot 'obj/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='1'
$gameRoot=(Resolve-Path -LiteralPath $GamePath).Path
& $DotnetPath build (Join-Path $PSScriptRoot 'GliderMk2.csproj') -c Release --ignore-failed-sources --disable-build-servers -m:1 '-p:UseSharedCompilation=false' "-p:GamePath=$gameRoot"
if($LASTEXITCODE -ne 0){throw 'Mod build failed'}
$testRun=Join-Path $PSScriptRoot ('obj/test-run-'+[Guid]::NewGuid().ToString('N'))
& $DotnetPath build (Join-Path $PSScriptRoot 'tests/FlightTests.csproj') -c Release -o $testRun --ignore-failed-sources --disable-build-servers -m:1 '-p:UseSharedCompilation=false'
if($LASTEXITCODE -ne 0){throw 'Flight test build failed'}
$testOutput=& $DotnetPath (Join-Path $testRun 'FlightTests.dll')
if($LASTEXITCODE -ne 0){throw 'Flight tests failed'}
$testOutput | Write-Output
if ($PhysicsReferencesPath) {
    $physicsRun=Join-Path $PSScriptRoot ('obj/physics-test-run-'+[Guid]::NewGuid().ToString('N'))
    & $DotnetPath build (Join-Path $PSScriptRoot 'tests/physics/PhysicsTests.csproj') -c Release -o $physicsRun --ignore-failed-sources --disable-build-servers -m:1 '-p:UseSharedCompilation=false' "-p:GamePath=$gameRoot"
    if($LASTEXITCODE -ne 0){throw 'Offline physics test build failed'}
    $physicsArgs=@($gameRoot, (Resolve-Path -LiteralPath $PhysicsReferencesPath).Path)
    if ($RenderReferencesPath) { $physicsArgs += (Resolve-Path -LiteralPath $RenderReferencesPath).Path }
    $physicsOutput=& $DotnetPath (Join-Path $physicsRun 'PhysicsTests.dll') @physicsArgs
    if($LASTEXITCODE -ne 0){throw 'Offline physics tests failed'}
    $physicsOutput | Write-Output
}
Get-ChildItem (Join-Path $PSScriptRoot 'assets') -Recurse -File -Filter '*.json' | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json | Out-Null
}
$version=(Get-Content (Join-Path $PSScriptRoot 'modinfo.json') -Raw | ConvertFrom-Json).version
$stage=Join-Path $PSScriptRoot ('obj/package-'+[Guid]::NewGuid().ToString('N'))
$sourceStage=Join-Path $PSScriptRoot ('obj/source-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage,$sourceStage,$OutputPath -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin/Release/net10.0/ElectricGliderMk2.dll'),(Join-Path $PSScriptRoot 'modinfo.json'),(Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets') -Destination $stage -Recurse
$package=Join-Path $OutputPath "electricglidermk2_$version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $package -Force
foreach($file in @('GliderMk2.csproj','modinfo.json','README.md','PROJECT.md','Verify-Handoff.ps1','THIRD-PARTY-NOTICES.md','VERIFICATION.md','build.ps1')){
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $sourceStage
}
foreach($folder in @('src','assets','tests','probe','scripts')) {
    Get-ChildItem (Join-Path $PSScriptRoot $folder) -File -Recurse | Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]' | ForEach-Object {
        $relative=[IO.Path]::GetRelativePath($PSScriptRoot,$_.FullName)
        $destination=Join-Path $sourceStage $relative
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destination
    }
}
$sourceArchive=Join-Path $OutputPath "electricglidermk2_${version}_source.zip"
Compress-Archive -Path (Join-Path $sourceStage '*') -DestinationPath $sourceArchive -Force
$testOutput | Set-Content -LiteralPath (Join-Path $OutputPath 'flight-tests.txt')
if ($PhysicsReferencesPath) { $physicsOutput | Set-Content -LiteralPath (Join-Path $OutputPath 'physics-tests.txt') }
Get-FileHash -LiteralPath $package,$sourceArchive -Algorithm SHA256 | Format-Table Path,Hash -AutoSize
