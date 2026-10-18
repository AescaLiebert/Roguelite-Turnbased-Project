param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.4f1\Editor\Unity.exe',
    [switch]$VerifySaved
)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$labRoot = [IO.Path]::GetFullPath((Join-Path $sourceRoot '.utmp\TeyvatMaterialLab'))
if (-not $labRoot.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Lab must remain inside workspace.' }
$outputRoot = Join-Path $sourceRoot 'Artifacts\TeyvatGrassland\WP02\r1'
if($VerifySaved -and -not(Test-Path -LiteralPath (Join-Path $labRoot 'Assets\Project\Art\Environment\TeyvatGrassland\Shared\Scenes\TG_MaterialLab.unity'))){throw 'Build the isolated material lab before verifying its saved scene.'}
New-Item -ItemType Directory -Path $labRoot,$outputRoot -Force | Out-Null
if(-not $VerifySaved){
$sharedRelative = 'Assets\Project\Art\Environment\TeyvatGrassland\Shared'
$sharedSource = Join-Path $sourceRoot $sharedRelative
$sharedDestination = Join-Path $labRoot $sharedRelative
New-Item -ItemType Directory -Path $sharedDestination -Force | Out-Null
# Copy reusable source and stable GUIDs. Generated lab outputs remain in this isolated project.
Get-ChildItem -LiteralPath $sharedSource -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $sharedDestination -Recurse -Force }
foreach ($editorFile in @('TeyvatMaterialLab.cs','TeyvatMaterialLabValidation.cs','TeyvatMaterialLabWaterValidation.cs','TeyvatMaterialLabColorValidation.cs','TeyvatMaterialLabInstancingValidation.cs')) {
    $source = Join-Path $sourceRoot ('Assets\Project\Editor\'+$editorFile)
    $destination = Join-Path $labRoot ('Assets\Project\Editor\'+$editorFile)
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if(Test-Path -LiteralPath ($source+'.meta')) {Copy-Item -LiteralPath ($source+'.meta') -Destination ($destination+'.meta') -Force}
}
$settingsRoot=Join-Path $sharedDestination 'Settings'
New-Item -ItemType Directory -Path $settingsRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceRoot 'Assets\Project\Art\Environment\TeyvatGrassland\FoundationR2\TG_Layout.json') -Destination (Join-Path $settingsRoot 'TG_WP01Layout.json') -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot 'ArtSource\TeyvatGrassland\Shared\TG_MaterialSamples.manifest.json') -Destination (Join-Path $sharedDestination 'Models\TG_MaterialSamples.manifest.json') -Force
# Preserve the complete sky material/texture dependencies and GUIDs without importing unrelated scenes.
$skyMaterial=Join-Path $sourceRoot 'Assets\Project\Art\Environment\TeyvatGrassland\FoundationR2\Materials\TG_FantasyDaySky.mat'
$skyDestination=Join-Path $settingsRoot 'TG_FantasyDaySky.mat'
Copy-Item -LiteralPath $skyMaterial -Destination $skyDestination -Force
if(-not(Test-Path -LiteralPath ($skyDestination+'.meta'))) {Copy-Item -LiteralPath ($skyMaterial+'.meta') -Destination ($skyDestination+'.meta') -Force}
$textureMeta=Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Assets\Fantasy Skybox FREE\Panoramics\FS003') -Filter '*.meta' | Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match '99b254ad4e7668d419be526ebb66b549' } | Select-Object -First 1
if($null -eq $textureMeta){throw 'Fantasy FS003 sky texture dependency not found.'}
$skyTextureSource=$textureMeta.FullName.Substring(0,$textureMeta.FullName.Length-5)
$textureRelative=$skyTextureSource.Substring($sourceRoot.Length+1)
$textureDestination=Join-Path $labRoot $textureRelative
New-Item -ItemType Directory -Path (Split-Path -Parent $textureDestination) -Force | Out-Null
Copy-Item -LiteralPath $skyTextureSource -Destination $textureDestination -Force
Copy-Item -LiteralPath $textureMeta.FullName -Destination ($textureDestination+'.meta') -Force
New-Item -ItemType Directory -Path (Join-Path $labRoot 'ProjectSettings'),(Join-Path $labRoot 'Packages') -Force | Out-Null
foreach($setting in @('ProjectVersion.txt','ProjectSettings.asset','TagManager.asset')) {
    Copy-Item -LiteralPath (Join-Path $sourceRoot ('ProjectSettings\'+$setting)) -Destination (Join-Path $labRoot ('ProjectSettings\'+$setting)) -Force
}
$projectSettings=Join-Path $labRoot 'ProjectSettings\ProjectSettings.asset'
$settingsText=Get-Content -LiteralPath $projectSettings -Raw
$settingsText=$settingsText -replace 'm_ActiveColorSpace: 0','m_ActiveColorSpace: 1'
Set-Content -LiteralPath $projectSettings -Value $settingsText -Encoding utf8
$dependencies=[ordered]@{}
$mainManifest=Get-Content -LiteralPath (Join-Path $sourceRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json
foreach($dependency in $mainManifest.dependencies.PSObject.Properties) {if($dependency.Name.StartsWith('com.unity.modules.')){$dependencies[$dependency.Name]=$dependency.Value}}
foreach($package in (Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Library\PackageCache') -Directory)) {
    $manifest=Join-Path $package.FullName 'package.json'
    if(-not(Test-Path -LiteralPath $manifest)){continue}
    $metadata=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if(-not $metadata.name.StartsWith('com.unity.') -or $metadata.name.StartsWith('com.unity.ide.') -or $metadata.name -eq 'com.unity.multiplayer.center'){continue}
    $dependencies[$metadata.name]='file:'+$package.FullName.Replace('\','/')
}
@{dependencies=$dependencies}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $labRoot 'Packages\manifest.json') -Encoding utf8
Set-Content -LiteralPath (Join-Path $labRoot 'TG_ISOLATED_MATERIAL_LAB.txt') -Value $outputRoot -Encoding utf8
}
# Verification deliberately preserves the just-built assets and project settings.
# A cold check must not overwrite them with the main project's delivered snapshot.
$runOutput=$outputRoot
if($VerifySaved){$runOutput=Join-Path $outputRoot 'Reload';New-Item -ItemType Directory -Path $runOutput -Force|Out-Null}
foreach($marker in @('COMPLETE.txt','FAILED.txt')) { $markerPath=Join-Path $runOutput $marker;if(Test-Path -LiteralPath $markerPath){Remove-Item -LiteralPath $markerPath} }
$logPath=Join-Path $runOutput 'UnityMaterialLab.log'
$method=if($VerifySaved){'FightingAllstar.EditorTools.TeyvatMaterialLab.VerifySavedIsolated'}else{'FightingAllstar.EditorTools.TeyvatMaterialLab.RunIsolated'}
$arguments=@('-batchmode','-projectPath',('"'+$labRoot+'"'),'-executeMethod',$method,'-force-d3d11','-job-worker-count','2','-shadercompiler-process-count','2','-logFile',('"'+$logPath+'"'))
$process=Start-Process -FilePath $EditorPath -ArgumentList $arguments -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
@{processId=$process.Id;project=$labRoot;output=$runOutput;log=$logPath}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $runOutput 'Process.json') -Encoding utf8
Write-Output ('WP02 material lab started. Process '+$process.Id)
Write-Output ('Evidence: '+$runOutput)
