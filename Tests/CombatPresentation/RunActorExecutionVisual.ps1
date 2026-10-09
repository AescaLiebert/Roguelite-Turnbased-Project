param([switch]$Focused, [switch]$Facing)
$ErrorActionPreference = 'Stop'
$actorRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$actorProject = Join-Path $actorRepo 'Temp/ActorExecutionVisualProject'
$actorEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe'
New-Item -ItemType Directory -Force "$actorProject/Assets/Editor", "$actorProject/Assets/Plugins", "$actorProject/Assets/Runtime", "$actorProject/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$actorRepo/ProjectSettings/ProjectVersion.txt" -Destination "$actorProject/ProjectSettings/ProjectVersion.txt"
foreach ($actorFile in @('BattleStagePresenter.cs', 'BattleStagePresenter.Camera.cs', 'BattleStagePresenter.Cinematics.cs', 'BattleStagePresenter.Execution.cs', 'BattleStagePresenter.MultiHit.cs', 'BattleStagePresenter.Ultimate.cs', 'BattleStagePresenter.Reactions.cs', 'BattleStagePresenter.ActorVisualEffects.cs', 'ActorVisualEffect.cs', 'ActorVisualEffect.Debuffs.cs', 'ActorDebuffVisual.cs', 'ActorVisualEffectGeometry.cs', 'ActorVisualEffectRules.cs', 'ActorVisualEffectSettings.cs', 'CardAnimationTiming.cs', 'ActorCardPresentation.cs', 'BattleFighterView.cs')) {
    Copy-Item -LiteralPath "$actorRepo/Assets/Project/Runtime/Presentation/Combat/$actorFile" -Destination "$actorProject/Assets/Runtime/$actorFile"
}
Copy-Item -LiteralPath "$PSScriptRoot/CameraReferenceChecks.cs" -Destination "$actorProject/Assets/Editor/CameraReferenceChecks.cs"
New-Item -ItemType Directory -Force "$actorProject/Assets/Resources" | Out-Null
Copy-Item -LiteralPath "$actorRepo/Assets/Resources/ActorVisualEffects.shader" -Destination "$actorProject/Assets/Resources/ActorVisualEffects.shader"
Copy-Item -LiteralPath "$actorRepo/Assets/Resources/ActorStatusParticles.shader" -Destination "$actorProject/Assets/Resources/ActorStatusParticles.shader"
Copy-Item -LiteralPath "$actorRepo/Assets/Project/Runtime/Content/StatusVisualData.cs", "$actorRepo/Assets/Project/Runtime/Content/StatusVisualDataCatalog.cs" -Destination "$actorProject/Assets/Runtime"
Copy-Item -LiteralPath "$actorRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$actorRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$actorProject/Assets/Plugins"
$actorNewtonsoft = Get-ChildItem "$actorRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
if ($null -eq $actorNewtonsoft) { throw 'Cannot find the project Newtonsoft.Json runtime dependency.' }
Copy-Item -LiteralPath $actorNewtonsoft.FullName -Destination "$actorProject/Assets/Plugins/Newtonsoft.Json.dll"
$actorMethod = if ($Facing) { 'CameraReferenceChecks.RunFacing' } elseif ($Focused) { 'CameraReferenceChecks.RunFocused' } else { 'CameraReferenceChecks.Run' }
$actorProcess = Start-Process -FilePath $actorEditor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-projectPath', ('"' + $actorProject + '"'), '-executeMethod', $actorMethod, '-logFile', ('"' + "$actorProject/visual.log" + '"'))
Write-Output "Actor camera fixture process: $($actorProcess.Id)"
Write-Output "Log: $actorProject/visual.log"
Write-Output "Images: $actorProject/Evidence"
