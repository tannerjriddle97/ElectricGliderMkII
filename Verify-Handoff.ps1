param([string]$ProjectRoot = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ProjectRoot).Path
$metadata = Get-Content -LiteralPath (Join-Path $root 'modinfo.json') -Raw | ConvertFrom-Json
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'GliderMk2.csproj') -Raw
$context = Get-Content -LiteralPath (Join-Path $root 'PROJECT.md') -Raw
$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
$verification = Get-Content -LiteralPath (Join-Path $root 'VERIFICATION.md') -Raw
$failures = [Collections.Generic.List[string]]::new()
if ($metadata.modid -ne 'electricglidermk2') { $failures.Add('Unexpected mod ID') }
if ([string]$project.Project.PropertyGroup.Version -ne $metadata.version) { $failures.Add('Project/metadata version mismatch') }
if (!$context.Contains(('**Current version:** `{0}`.' -f $metadata.version))) { $failures.Add('Handoff version is stale') }
if (!$readme.StartsWith("# Electric Glider Mk II — $($metadata.version)")) { $failures.Add('README version is stale') }
if (!$verification.StartsWith("# Verification — $($metadata.version)")) { $failures.Add('Verification version is stale') }
foreach ($file in @('PROJECT.md','README.md','VERIFICATION.md')) {
    $text = Get-Content -LiteralPath (Join-Path $root $file) -Raw
    foreach ($link in [regex]::Matches($text, '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $link.Groups[1].Value.Split('#')[0]
        if ($target -and $target -notmatch '^[a-zA-Z][a-zA-Z0-9+.-]*:') {
            if (!(Test-Path -LiteralPath (Join-Path $root $target))) { $failures.Add("Broken link in ${file}: $target") }
        }
    }
}
$model = Get-Content -LiteralPath (Join-Path $root 'src/FlightModel.cs') -Raw
foreach ($name in @('MaxGlideBlocksPerSecond','MaxBoostBlocksPerSecond','CoastBlocksPerSecond','PositioningBlocksPerSecond','BoostAcceleration','ReverseThrustAcceleration','LaunchSafetySeconds')) {
    $match = [regex]::Match($model, "public const double $name = ([0-9.]+);")
    if (!$match.Success -or !$context.Contains("$name = $($match.Groups[1].Value)")) { $failures.Add("Handoff/source setting mismatch: $name") }
}
foreach ($file in @('Verify-Handoff.ps1','src/Mk2System.cs','src/ItemMk2Glider.cs','tests/Program.cs','tests/physics/Program.cs','probe/OriginalFlightRegression.cs','build.ps1')) {
    if (!(Test-Path -LiteralPath (Join-Path $root $file))) { $failures.Add("Missing continuation source: $file") }
}
if ($failures.Count) { throw ($failures -join "`n") }
Write-Output "PASS: Mk II $($metadata.version) handoff versions, source settings, continuation files and relative links agree. No runtime or packaging performed."
