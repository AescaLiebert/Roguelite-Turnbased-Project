param(
    [string]$ProjectPath=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$EditorPath='C:\Program Files\Unity\Hub\Editor\6000.3.4f1\Editor\Unity.exe',
    [switch]$VerifySaved
)
$ErrorActionPreference='Stop'
$sourceRoot=(Resolve-Path -LiteralPath $ProjectPath).Path
$labRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot '.utmp\TeyvatWorldLab'))
if(-not $labRoot.StartsWith($sourceRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'World lab must remain inside workspace.'}
$outputRoot=Join-Path $sourceRoot 'Artifacts\TeyvatGrassland\WP03\r1'
New-Item -ItemType Directory -Path $labRoot,$outputRoot -Force|Out-Null
if(-not $VerifySaved){
    foreach($relative in @('Assets\Project\Art\Environment\TeyvatGrassland\Shared','Assets\Project\Art\Environment\TeyvatGrassland\World','Assets\Project\Art\Environment\TeyvatGrassland\FoundationR2','Assets\Fantasy Skybox FREE\Panoramics\FS003')){
        $source=Join-Path $sourceRoot $relative;$destination=Join-Path $labRoot $relative
        New-Item -ItemType Directory -Path $destination -Force|Out-Null
        Get-ChildItem -LiteralPath $source -Force|ForEach-Object{Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force}
        if(Test-Path -LiteralPath ($source+'.meta')){Copy-Item -LiteralPath ($source+'.meta') -Destination ($destination+'.meta') -Force}
    }
    $editorDestination=Join-Path $labRoot 'Assets\Project\Editor';New-Item -ItemType Directory -Path $editorDestination -Force|Out-Null
    foreach($file in @('TeyvatWorldLab.cs','TeyvatWorldLabValidation.cs')){
        $source=Join-Path $sourceRoot ('Assets\Project\Editor\'+$file)
        Copy-Item -LiteralPath $source -Destination (Join-Path $editorDestination $file) -Force
        if(Test-Path -LiteralPath ($source+'.meta')){Copy-Item -LiteralPath ($source+'.meta') -Destination (Join-Path $editorDestination ($file+'.meta')) -Force}
    }
    New-Item -ItemType Directory -Path (Join-Path $labRoot 'ProjectSettings'),(Join-Path $labRoot 'Packages') -Force|Out-Null
    foreach($setting in @('ProjectVersion.txt','ProjectSettings.asset','TagManager.asset')){Copy-Item -LiteralPath (Join-Path $sourceRoot ('ProjectSettings\'+$setting)) -Destination (Join-Path $labRoot ('ProjectSettings\'+$setting)) -Force}
    $settings=Join-Path $labRoot 'ProjectSettings\ProjectSettings.asset'
    $text=Get-Content -LiteralPath $settings -Raw
    Set-Content -LiteralPath $settings -Value ($text -replace 'm_ActiveColorSpace: 0','m_ActiveColorSpace: 1') -Encoding utf8
    $dependencies=[ordered]@{}
    $manifest=Get-Content -LiteralPath (Join-Path $sourceRoot 'Packages\manifest.json') -Raw|ConvertFrom-Json
    foreach($entry in $manifest.dependencies.PSObject.Properties){if($entry.Name.StartsWith('com.unity.modules.')){$dependencies[$entry.Name]=$entry.Value}}
    foreach($package in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Library\PackageCache') -Directory){
        $file=Join-Path $package.FullName 'package.json';if(-not(Test-Path -LiteralPath $file)){continue}
        $data=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
        if(-not $data.name.StartsWith('com.unity.') -or $data.name.StartsWith('com.unity.ide.') -or $data.name -eq 'com.unity.multiplayer.center'){continue}
        $dependencies[$data.name]='file:'+$package.FullName.Replace('\','/')
    }
    @{dependencies=$dependencies}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $labRoot 'Packages\manifest.json') -Encoding utf8
    Set-Content -LiteralPath (Join-Path $labRoot 'TG_ISOLATED_WORLD_LAB.txt') -Value $outputRoot -Encoding utf8
}
$runOutput=if($VerifySaved){Join-Path $outputRoot 'Reload'}else{$outputRoot}
New-Item -ItemType Directory -Path $runOutput -Force|Out-Null
foreach($marker in @('COMPLETE.txt','FAILED.txt')){$p=Join-Path $runOutput $marker;if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p}}
$method=if($VerifySaved){'FightingAllstar.EditorTools.TeyvatWorldLab.VerifySaved'}else{'FightingAllstar.EditorTools.TeyvatWorldLab.RunIsolated'}
$log=Join-Path $runOutput 'UnityWorldLab.log'
$arguments=@('-batchmode','-projectPath',('"'+$labRoot+'"'),'-executeMethod',$method,'-force-d3d11','-job-worker-count','2','-shadercompiler-process-count','2','-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath $EditorPath -ArgumentList $arguments -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
@{processId=$process.Id;project=$labRoot;output=$runOutput;log=$log}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $runOutput 'Process.json') -Encoding utf8
Write-Output ('WP03 world lab started. Process '+$process.Id)
Write-Output ('Evidence: '+$runOutput)
