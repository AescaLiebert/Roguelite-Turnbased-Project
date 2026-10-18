param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.4f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$trialRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '.utmp\TeyvatRenderingTrial'))
if (-not $trialRoot.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Trial project must stay inside this workspace.'
}
$manifestPath = Join-Path $sourceRoot 'Artifacts\TeyvatGrassland\WP00\TrialInputs.txt'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'Run TeyvatFoundationBuilder.ExportTrialInputs in the main Unity Editor first.'
}
$outputRoot = Join-Path $sourceRoot 'Artifacts\TeyvatGrassland\WP00\RenderingTrial'
New-Item -ItemType Directory -Path $trialRoot, $outputRoot -Force | Out-Null
foreach ($relative in (Get-Content -LiteralPath $manifestPath)) {
    if (-not $relative.StartsWith('Assets/')) { throw ('Unexpected dependency path: ' + $relative) }
    $source = Join-Path $sourceRoot $relative
    $destination = [IO.Path]::GetFullPath((Join-Path $trialRoot $relative))
    if (-not $destination.StartsWith($trialRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Dependency escapes trial project: ' + $relative)
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if (Test-Path -LiteralPath ($source + '.meta')) { Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta') -Force }
}
New-Item -ItemType Directory -Path (Join-Path $trialRoot 'ProjectSettings'), (Join-Path $trialRoot 'Packages') -Force | Out-Null
foreach ($setting in @('ProjectVersion.txt','ProjectSettings.asset','GraphicsSettings.asset','QualitySettings.asset','TagManager.asset')) {
    Copy-Item -LiteralPath (Join-Path $sourceRoot ('ProjectSettings\' + $setting)) -Destination (Join-Path $trialRoot ('ProjectSettings\' + $setting)) -Force
}
$sourceManifest = Get-Content -LiteralPath (Join-Path $sourceRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json
$trialDependencies = [ordered]@{}
foreach ($dependency in $sourceManifest.dependencies.PSObject.Properties) {
    if ($dependency.Name -eq 'com.coplaydev.unity-mcp' -or $dependency.Name.StartsWith('com.unity.ide.') -or $dependency.Name -eq 'com.unity.multiplayer.center') { continue }
    $trialDependencies[$dependency.Name] = $dependency.Value
}
# Pin the isolated trial to the already installed package sources, including transitive dependencies.
# This avoids a second registry resolve and does not edit or duplicate those cached package sources.
foreach ($cachedPackage in (Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Library\PackageCache') -Directory)) {
    $cachedManifest = Join-Path $cachedPackage.FullName 'package.json'
    if (-not (Test-Path -LiteralPath $cachedManifest)) { continue }
    $cachedMetadata = Get-Content -LiteralPath $cachedManifest -Raw | ConvertFrom-Json
    if (-not $cachedMetadata.name.StartsWith('com.unity.') -or $cachedMetadata.name.StartsWith('com.unity.ide.') -or $cachedMetadata.name -eq 'com.unity.multiplayer.center') { continue }
    $trialDependencies[$cachedMetadata.name] = 'file:' + $cachedPackage.FullName.Replace('\','/')
}
@{ dependencies = $trialDependencies } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $trialRoot 'Packages\manifest.json') -Encoding utf8
Set-Content -LiteralPath (Join-Path $trialRoot 'TG_ISOLATED_RENDERING_TRIAL.txt') -Value $outputRoot -Encoding utf8
foreach ($oldMarker in @('COMPLETE.txt','FAILED.txt')) {
    $markerPath = Join-Path $outputRoot $oldMarker
    if (Test-Path -LiteralPath $markerPath) { Remove-Item -LiteralPath $markerPath }
}
$logPath = Join-Path $outputRoot 'UnityTrial.log'
$unityArgs = @('-batchmode','-projectPath',('"' + $trialRoot + '"'),'-executeMethod','FightingAllstar.EditorTools.TeyvatFoundationBuilder.RunIsolatedTrial',
    '-force-d3d11','-job-worker-count','2','-shadercompiler-process-count','2','-logFile',('"' + $logPath + '"'))
$trialProcess = Start-Process -FilePath $EditorPath -ArgumentList $unityArgs -WorkingDirectory $trialRoot -WindowStyle Hidden -PassThru
@{ processId = $trialProcess.Id; trialProject = $trialRoot; output = $outputRoot; log = $logPath } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'Process.json') -Encoding utf8
Write-Output ('Rendering trial started. Process: ' + $trialProcess.Id)
Write-Output ('Evidence: ' + $outputRoot)
