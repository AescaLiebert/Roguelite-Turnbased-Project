param(
    [string]$ProjectPath=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$EditorPath='C:\Program Files\Unity\Hub\Editor\6000.3.4f1\Editor\Unity.exe',
    [switch]$SampleOnly,
    [switch]$VerifySaved,
    [switch]$Playback,
    [switch]$ExportOnly
)
$ErrorActionPreference='Stop'
if((@($SampleOnly,$VerifySaved,$Playback,$ExportOnly)|Where-Object {$_}).Count -gt 1){throw 'Choose one review mode.'}
$sourceRoot=(Resolve-Path -LiteralPath $ProjectPath).Path
$editorExecutable=(Resolve-Path -LiteralPath $EditorPath).Path
# Reuse the closed isolated art host; the main gameplay project is never launched.
$labRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot '.utmp\TeyvatWorldLab'))
if(-not $labRoot.StartsWith($sourceRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    throw 'Tree review must remain inside the workspace.'
}
if(Test-Path -LiteralPath (Join-Path $labRoot 'Assets\Project\Scenes\Battle.unity')){
    throw 'The stage review host contains the gameplay Battle scene; use the isolated art host.'
}
# Check known host PIDs without WMI: querying Win32_Process can stall for over a
# minute on this machine. Start time prevents an unrelated reused PID blocking.
foreach($knownRun in @(
    'Artifacts\TeyvatGrassland\WP07\Revision01\Playback\Process.json',
    'Artifacts\TeyvatGrassland\WP07\Revision01\Package\Process.json',
    'Artifacts\TeyvatGrassland\WP07\Revision01\Process.json',
    'Artifacts\TeyvatGrassland\WP07\Revision01\Reload\Process.json',
    'Artifacts\TeyvatGrassland\WP05\Revision01\Process.json',
    'Artifacts\TeyvatGrassland\WP05\Revision01\Reload\Process.json',
    'Artifacts\TeyvatGrassland\WP01\Revision03\Process.json',
    'Artifacts\TeyvatGrassland\WP01\Revision03\Reload\Process.json',
    'Artifacts\TeyvatGrassland\WP04\r1\Process.json',
    'Artifacts\TeyvatGrassland\WP03\r1\Process.json'
)){
    $knownRunPath=Join-Path $sourceRoot $knownRun
    if(-not(Test-Path -LiteralPath $knownRunPath)){continue}
    $runRecord=$null
    try{$runRecord=Get-Content -LiteralPath $knownRunPath -Raw|ConvertFrom-Json}catch{continue}
    if(-not $runRecord.startedUtc -or -not $runRecord.project -or -not $runRecord.processId){continue}
    if(-not ([string]$runRecord.project).TrimEnd('\','/').Equals($labRoot.TrimEnd('\','/'),[StringComparison]::OrdinalIgnoreCase)){continue}
    $knownUnity=Get-Process -Id $runRecord.processId -ErrorAction SilentlyContinue
    if($null -eq $knownUnity -or $knownUnity.ProcessName -ne 'Unity'){continue}
    $sameProcess=$false
    try{$sameProcess=[Math]::Abs(($knownUnity.StartTime.ToUniversalTime()-[DateTime]::Parse($runRecord.startedUtc).ToUniversalTime()).TotalSeconds) -lt 1}catch{}
    if($sameProcess){throw ('The isolated TeyvatWorldLab is already running in Unity process '+$knownUnity.Id+'. Close it before launching this review.')}
}
# Unity holds this project-specific file while the host is open, including when
# opened through Unity Hub rather than a launcher. Mere cached file presence is
# harmless: only a failed exclusive-open probe of an existing file blocks us.
$lockPath=Join-Path $labRoot 'Temp\UnityLockfile'
if(Test-Path -LiteralPath $lockPath){
    try{
        $lockProbe=[IO.File]::Open($lockPath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $lockProbe.Dispose()
    }
    catch [IO.FileNotFoundException]{}
    catch [IO.IOException]{throw 'The isolated TeyvatWorldLab has an actively held project lock. Close its editor before launching this review.'}
    catch [UnauthorizedAccessException]{throw 'The isolated TeyvatWorldLab project lock cannot be checked because access is denied.'}
}
$outputRoot=Join-Path $sourceRoot 'Artifacts\TeyvatGrassland\WP07\Revision01'
$runOutput=if($ExportOnly){Join-Path $outputRoot 'Package'}elseif($VerifySaved){Join-Path $outputRoot 'Reload'}else{$outputRoot}
$fresh=-not(Test-Path -LiteralPath (Join-Path $labRoot 'ProjectSettings\ProjectSettings.asset'))
if($VerifySaved -and $fresh){throw 'Saved-scene verification requires a previously generated stage lab; run the default review first.'}
New-Item -ItemType Directory -Path $labRoot,$runOutput -Force|Out-Null

function Copy-StageLabFileIfChanged {
    param([string]$SourceFile,[string]$DestinationFile)
    $sourceInfo=Get-Item -LiteralPath $SourceFile -Force
    $destinationInfo=Get-Item -LiteralPath $DestinationFile -Force -ErrorAction SilentlyContinue
    if($null -ne $destinationInfo -and $sourceInfo.Length -eq $destinationInfo.Length -and $sourceInfo.LastWriteTimeUtc -eq $destinationInfo.LastWriteTimeUtc){return}
    New-Item -ItemType Directory -Path (Split-Path -Parent $DestinationFile) -Force|Out-Null
    Copy-Item -LiteralPath $SourceFile -Destination $DestinationFile -Force
    # Preserve the timestamp comparison even on hosts where Copy-Item resets it.
    [IO.File]::SetLastWriteTimeUtc($DestinationFile,$sourceInfo.LastWriteTimeUtc)
}

function Copy-StageLabDirectoryIncremental {
    param([string]$SourceDirectory,[string]$DestinationDirectory)
    New-Item -ItemType Directory -Path $DestinationDirectory -Force|Out-Null
    $robocopy=Get-Command robocopy.exe -ErrorAction SilentlyContinue
    if($null -ne $robocopy){
        # /E copies subdirectories, including empty ones. It never mirrors,
        # purges, deletes or moves destination content; unchanged files stay put.
        # Short retry limits prevent a locked asset from stalling for minutes.
        $PSNativeCommandUseErrorActionPreference=$false
        & $robocopy.Source $SourceDirectory $DestinationDirectory /E /COPY:DAT /DCOPY:T /R:1 /W:1 /XJ /NFL /NDL /NJH /NJS /NP
        $copyExit=$LASTEXITCODE
        if($copyExit -ge 8){throw ('Incremental stage-lab dependency copy failed, robocopy exit '+$copyExit+': '+$SourceDirectory)}
    }
    else{
        # Native fallback uses exactly file length and UTC modification time.
        $prefix=$SourceDirectory.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
        foreach($directory in Get-ChildItem -LiteralPath $SourceDirectory -Directory -Recurse -Force){
            New-Item -ItemType Directory -Path (Join-Path $DestinationDirectory $directory.FullName.Substring($prefix.Length)) -Force|Out-Null
        }
        foreach($file in Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse -Force){
            Copy-StageLabFileIfChanged $file.FullName (Join-Path $DestinationDirectory $file.FullName.Substring($prefix.Length))
        }
    }
    # The source folder's own .meta sits beside it, outside the copied stage.
    if(Test-Path -LiteralPath ($SourceDirectory+'.meta')){
        Copy-StageLabFileIfChanged ($SourceDirectory+'.meta') ($DestinationDirectory+'.meta')
    }
}

if(-not $VerifySaved){
    $assetFolders=@(
        'Assets\Project\Art\Environment\TeyvatGrassland\Shared',
        'Assets\Project\Art\Environment\TeyvatGrassland\World',
        'Assets\Project\Art\Environment\TeyvatGrassland\FoundationR2',
        'Assets\Project\Art\Environment\TeyvatGrassland\Meadow',
        'Assets\Project\Art\Environment\TeyvatGrassland\LayoutR3',
        'Assets\Project\Art\Environment\TeyvatGrassland\Trees',
        'Assets\Project\Art\Environment\TeyvatGrassland\Water',
        'Assets\Project\Art\Environment\TeyvatGrassland\Integration',
        'Assets\Project\Art\Environment\TeyvatGrassland\Prefabs',
        'Assets\Project\Scenes\EnvironmentReview\TeyvatGrassland',
        'Assets\Fantasy Skybox FREE\Panoramics\FS003'
    )
    foreach($relative in $assetFolders){
        $source=Join-Path $sourceRoot $relative
        if(-not(Test-Path -LiteralPath $source -PathType Container)){throw ('Required stage-lab asset folder is missing: '+$relative)}
    }
    $sourceEditor=Join-Path $sourceRoot 'Assets\Project\Editor'
    $stageSources=@(Get-ChildItem -LiteralPath $sourceEditor -Filter 'TeyvatStage*.cs' -File)
    if($stageSources.Count -eq 0){throw 'No TeyvatStage*.cs review entry point exists yet.'}
    foreach($relative in $assetFolders){
        $source=Join-Path $sourceRoot $relative
        $destination=Join-Path $labRoot $relative
        Copy-StageLabDirectoryIncremental $source $destination
    }
    $editorDestination=Join-Path $labRoot 'Assets\Project\Editor'
    New-Item -ItemType Directory -Path $editorDestination -Force|Out-Null
    foreach($file in $stageSources){
        Copy-StageLabFileIfChanged $file.FullName (Join-Path $editorDestination $file.Name)
        if(Test-Path -LiteralPath ($file.FullName+'.meta')){Copy-StageLabFileIfChanged ($file.FullName+'.meta') (Join-Path $editorDestination ($file.Name+'.meta'))}
    }
    if($fresh){
        New-Item -ItemType Directory -Path (Join-Path $labRoot 'ProjectSettings'),(Join-Path $labRoot 'Packages') -Force|Out-Null
        foreach($setting in @('ProjectVersion.txt','ProjectSettings.asset','TagManager.asset')){
            Copy-Item -LiteralPath (Join-Path $sourceRoot ('ProjectSettings\'+$setting)) -Destination (Join-Path $labRoot ('ProjectSettings\'+$setting)) -Force
        }
        $settings=Join-Path $labRoot 'ProjectSettings\ProjectSettings.asset'
        $settingsText=Get-Content -LiteralPath $settings -Raw
        Set-Content -LiteralPath $settings -Value ($settingsText -replace 'm_ActiveColorSpace: 0','m_ActiveColorSpace: 1') -Encoding utf8
        $dependencies=[ordered]@{}
        $manifest=Get-Content -LiteralPath (Join-Path $sourceRoot 'Packages\manifest.json') -Raw|ConvertFrom-Json
        foreach($entry in $manifest.dependencies.PSObject.Properties){
            if($entry.Name.StartsWith('com.unity.modules.')){$dependencies[$entry.Name]=$entry.Value}
        }
        foreach($package in Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'Library\PackageCache') -Directory){
            $packageJson=Join-Path $package.FullName 'package.json'
            if(-not(Test-Path -LiteralPath $packageJson)){continue}
            $packageData=Get-Content -LiteralPath $packageJson -Raw|ConvertFrom-Json
            if(-not $packageData.name.StartsWith('com.unity.') -or $packageData.name.StartsWith('com.unity.ide.') -or $packageData.name -eq 'com.unity.multiplayer.center'){continue}
            $dependencies[$packageData.name]='file:'+$package.FullName.Replace('\','/')
        }
        @{dependencies=$dependencies}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $labRoot 'Packages\manifest.json') -Encoding utf8
    }
}

# The marker selects the output for every mode, including a separate Reload run.
Set-Content -LiteralPath (Join-Path $labRoot 'TG_ISOLATED_STAGE.txt') -Value $runOutput -Encoding utf8
foreach($marker in @('COMPLETE.txt','FAILED.txt')){
    $markerDirectory=if($Playback){Join-Path $runOutput 'Playback'}else{$runOutput}
    $markerPath=Join-Path $markerDirectory $marker
    if(Test-Path -LiteralPath $markerPath){Remove-Item -LiteralPath $markerPath}
}
$method=if($ExportOnly){'FightingAllstar.EditorTools.TeyvatStageLab.ExportVerifiedPackage'}elseif($Playback){'FightingAllstar.EditorTools.TeyvatStageLab.RunPlayback'}elseif($VerifySaved){'FightingAllstar.EditorTools.TeyvatStageLab.VerifySaved'}elseif($SampleOnly){'FightingAllstar.EditorTools.TeyvatStageLab.SampleOnly'}else{'FightingAllstar.EditorTools.TeyvatStageLab.RunIsolated'}
$mode=if($ExportOnly){'ExportOnly'}elseif($Playback){'Playback'}elseif($VerifySaved){'VerifySaved'}elseif($SampleOnly){'SampleOnly'}else{'RunIsolated'}
$processDirectory=if($Playback){Join-Path $runOutput 'Playback'}else{$runOutput}
New-Item -ItemType Directory -Path $processDirectory -Force|Out-Null
$log=Join-Path $processDirectory 'UnityStageLab.log'
$arguments=@('-batchmode','-projectPath',('"'+$labRoot+'"'),'-executeMethod',$method,'-force-d3d11','-job-worker-count','2','-shadercompiler-process-count','2','-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath $editorExecutable -ArgumentList $arguments -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
$startedUtc=$null
try{$startedUtc=$process.StartTime.ToUniversalTime().ToString('o')}catch{}
# Deliberately omit full process command lines and environment/authorization data.
@{processId=$process.Id;processName=$process.ProcessName;startedUtc=$startedUtc;mode=$mode;project=$labRoot;output=$runOutput;log=$log}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $processDirectory 'Process.json') -Encoding utf8
Write-Output ('WP07 stage review started ('+$mode+'). Process '+$process.Id)
Write-Output ('Evidence: '+$runOutput)
