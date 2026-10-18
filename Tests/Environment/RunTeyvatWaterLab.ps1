param(
    [string]$ProjectPath=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$EditorPath='C:\Program Files\Unity\Hub\Editor\6000.3.4f1\Editor\Unity.exe',
    [switch]$SampleOnly,
    [switch]$VerifySaved
)
$ErrorActionPreference='Stop'
if($SampleOnly -and $VerifySaved){throw 'Choose either SampleOnly or VerifySaved, not both.'}
$sourceRoot=(Resolve-Path -LiteralPath $ProjectPath).Path
$editorExecutable=(Resolve-Path -LiteralPath $EditorPath).Path
# Reuse the closed isolated art host; the main gameplay project is never launched.
$labRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot '.utmp\TeyvatWorldLab'))
if(-not $labRoot.StartsWith($sourceRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    throw 'Tree review must remain inside the workspace.'
}
if(Test-Path -LiteralPath (Join-Path $labRoot 'Assets\Project\Scenes\Battle.unity')){
    throw 'The water review host contains the gameplay Battle scene; use the isolated art host.'
}
# Check known host PIDs without WMI: querying Win32_Process can stall for over a
# minute on this machine. Start time prevents an unrelated reused PID blocking.
foreach($knownRun in @(
    'Artifacts\TeyvatGrassland\WP06\Revision01\Process.json',
    'Artifacts\TeyvatGrassland\WP06\Revision01\Reload\Process.json',
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
$outputRoot=Join-Path $sourceRoot 'Artifacts\TeyvatGrassland\WP06\Revision01'
$runOutput=if($VerifySaved){Join-Path $outputRoot 'Reload'}else{$outputRoot}
$fresh=-not(Test-Path -LiteralPath (Join-Path $labRoot 'ProjectSettings\ProjectSettings.asset'))
if($VerifySaved -and $fresh){throw 'Saved-scene verification requires a previously generated water lab; run the default review first.'}
New-Item -ItemType Directory -Path $labRoot,$runOutput -Force|Out-Null

function Copy-WaterLabFileIfChanged {
    param([string]$SourceFile,[string]$DestinationFile)
    $sourceInfo=Get-Item -LiteralPath $SourceFile -Force
    $destinationInfo=Get-Item -LiteralPath $DestinationFile -Force -ErrorAction SilentlyContinue
    if($null -ne $destinationInfo -and $sourceInfo.Length -eq $destinationInfo.Length -and $sourceInfo.LastWriteTimeUtc -eq $destinationInfo.LastWriteTimeUtc){return}
    New-Item -ItemType Directory -Path (Split-Path -Parent $DestinationFile) -Force|Out-Null
    Copy-Item -LiteralPath $SourceFile -Destination $DestinationFile -Force
    # Preserve the timestamp comparison even on hosts where Copy-Item resets it.
    [IO.File]::SetLastWriteTimeUtc($DestinationFile,$sourceInfo.LastWriteTimeUtc)
}

function Copy-WaterLabDirectoryIncremental {
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
        if($copyExit -ge 8){throw ('Incremental water-lab dependency copy failed, robocopy exit '+$copyExit+': '+$SourceDirectory)}
    }
    else{
        # Native fallback uses exactly file length and UTC modification time.
        $prefix=$SourceDirectory.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
        foreach($directory in Get-ChildItem -LiteralPath $SourceDirectory -Directory -Recurse -Force){
            New-Item -ItemType Directory -Path (Join-Path $DestinationDirectory $directory.FullName.Substring($prefix.Length)) -Force|Out-Null
        }
        foreach($file in Get-ChildItem -LiteralPath $SourceDirectory -File -Recurse -Force){
            Copy-WaterLabFileIfChanged $file.FullName (Join-Path $DestinationDirectory $file.FullName.Substring($prefix.Length))
        }
    }
    # The source folder's own .meta sits beside it, outside the copied water.
    if(Test-Path -LiteralPath ($SourceDirectory+'.meta')){
        Copy-WaterLabFileIfChanged ($SourceDirectory+'.meta') ($DestinationDirectory+'.meta')
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
        'Assets\Fantasy Skybox FREE\Panoramics\FS003'
    )
    foreach($relative in $assetFolders){
        $source=Join-Path $sourceRoot $relative
        if(-not(Test-Path -LiteralPath $source -PathType Container)){throw ('Required water-lab asset folder is missing: '+$relative)}
    }
    $sourceEditor=Join-Path $sourceRoot 'Assets\Project\Editor'
    $waterSources=@(Get-ChildItem -LiteralPath $sourceEditor -Filter 'TeyvatWater*.cs' -File)
    if($waterSources.Count -eq 0){throw 'No TeyvatWater*.cs review entry point exists yet.'}
    foreach($relative in $assetFolders){
        $source=Join-Path $sourceRoot $relative
        $destination=Join-Path $labRoot $relative
        Copy-WaterLabDirectoryIncremental $source $destination
    }
    $editorDestination=Join-Path $labRoot 'Assets\Project\Editor'
    New-Item -ItemType Directory -Path $editorDestination -Force|Out-Null
    foreach($file in $waterSources){
        Copy-WaterLabFileIfChanged $file.FullName (Join-Path $editorDestination $file.Name)
        if(Test-Path -LiteralPath ($file.FullName+'.meta')){Copy-WaterLabFileIfChanged ($file.FullName+'.meta') (Join-Path $editorDestination ($file.Name+'.meta'))}
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
Set-Content -LiteralPath (Join-Path $labRoot 'TG_ISOLATED_WATER.txt') -Value $runOutput -Encoding utf8
foreach($marker in @('COMPLETE.txt','FAILED.txt')){
    $markerPath=Join-Path $runOutput $marker
    if(Test-Path -LiteralPath $markerPath){Remove-Item -LiteralPath $markerPath}
}
$method=if($VerifySaved){'FightingAllstar.EditorTools.TeyvatWaterLab.VerifySaved'}elseif($SampleOnly){'FightingAllstar.EditorTools.TeyvatWaterLab.SampleOnly'}else{'FightingAllstar.EditorTools.TeyvatWaterLab.RunIsolated'}
$mode=if($VerifySaved){'VerifySaved'}elseif($SampleOnly){'SampleOnly'}else{'RunIsolated'}
$log=Join-Path $runOutput 'UnityWaterLab.log'
$arguments=@('-batchmode','-projectPath',('"'+$labRoot+'"'),'-executeMethod',$method,'-force-d3d11','-job-worker-count','2','-shadercompiler-process-count','2','-logFile',('"'+$log+'"'))
$process=Start-Process -FilePath $editorExecutable -ArgumentList $arguments -WorkingDirectory $labRoot -WindowStyle Hidden -PassThru
$startedUtc=$null
try{$startedUtc=$process.StartTime.ToUniversalTime().ToString('o')}catch{}
# Deliberately omit full process command lines and environment/authorization data.
@{processId=$process.Id;processName=$process.ProcessName;startedUtc=$startedUtc;mode=$mode;project=$labRoot;output=$runOutput;log=$log}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $runOutput 'Process.json') -Encoding utf8
Write-Output ('WP06 water review started ('+$mode+'). Process '+$process.Id)
Write-Output ('Evidence: '+$runOutput)
