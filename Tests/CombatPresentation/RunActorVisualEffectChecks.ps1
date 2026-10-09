$ErrorActionPreference = 'Stop'
$aveRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$aveProject = Join-Path $aveRepo 'Temp/ActorVisualEffectPlayProject'
$aveEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe'
New-Item -ItemType Directory -Force "$aveProject/Assets/Editor", "$aveProject/Assets/Plugins", "$aveProject/Assets/Runtime", "$aveProject/Assets/Resources", "$aveProject/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$aveRepo/ProjectSettings/ProjectVersion.txt" -Destination "$aveProject/ProjectSettings/ProjectVersion.txt"
foreach ($aveFile in @('BattleStagePresenter.cs', 'BattleStagePresenter.Camera.cs', 'BattleStagePresenter.Cinematics.cs', 'BattleStagePresenter.Execution.cs', 'BattleStagePresenter.MultiHit.cs', 'BattleStagePresenter.Ultimate.cs', 'BattleStagePresenter.Reactions.cs', 'BattleStagePresenter.ActorVisualEffects.cs', 'ActorVisualEffect.cs', 'ActorVisualEffect.Debuffs.cs', 'ActorDebuffVisual.cs', 'ActorVisualEffectGeometry.cs', 'ActorVisualEffectRules.cs', 'ActorVisualEffectSettings.cs', 'CardAnimationTiming.cs', 'ActorCardPresentation.cs', 'BattleFighterView.cs')) {
    Copy-Item -LiteralPath "$aveRepo/Assets/Project/Runtime/Presentation/Combat/$aveFile" -Destination "$aveProject/Assets/Runtime/$aveFile"
}
foreach ($aveContentFile in @('StatusVisualData.cs', 'StatusVisualDataCatalog.cs')) {
    Copy-Item -LiteralPath "$aveRepo/Assets/Project/Runtime/Content/$aveContentFile" -Destination "$aveProject/Assets/Runtime/$aveContentFile"
}
Copy-Item -LiteralPath "$aveRepo/Assets/Resources/ActorVisualEffects.shader" -Destination "$aveProject/Assets/Resources/ActorVisualEffects.shader"
Copy-Item -LiteralPath "$aveRepo/Assets/Resources/ActorStatusParticles.shader" -Destination "$aveProject/Assets/Resources/ActorStatusParticles.shader"
Copy-Item -LiteralPath "$PSScriptRoot/ActorVisualEffectPlayChecks.cs" -Destination "$aveProject/Assets/Editor/ActorVisualEffectPlayChecks.cs"
Copy-Item -LiteralPath "$aveRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$aveRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$aveProject/Assets/Plugins"
$aveNewtonsoft = Get-ChildItem "$aveRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
Copy-Item -LiteralPath $aveNewtonsoft.FullName -Destination "$aveProject/Assets/Plugins/Newtonsoft.Json.dll"
$aveProcess = Start-Process -FilePath $aveEditor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-projectPath', ('"' + $aveProject + '"'), '-executeMethod', 'ActorVisualEffectPlayChecks.Run', '-logFile', ('"' + "$aveProject/visual.log" + '"'))
Write-Output "AVE fixture process: $($aveProcess.Id)"
Write-Output "Log: $aveProject/visual.log"
Write-Output "Result: $aveProject/Evidence/play-checks.txt"
