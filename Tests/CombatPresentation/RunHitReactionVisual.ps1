$ErrorActionPreference = 'Stop'
$reactionRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$reactionProject = Join-Path $reactionRepo 'Temp/HitReactionVisualProject'
$reactionEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe'
New-Item -ItemType Directory -Force "$reactionProject/Assets/Editor", "$reactionProject/Assets/Plugins", "$reactionProject/Assets/Runtime", "$reactionProject/Assets/Resources", "$reactionProject/Assets/Production", "$reactionProject/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$reactionRepo/Assets/Resources/ActorVisualEffects.shader", "$reactionRepo/Assets/Resources/ActorStatusParticles.shader" -Destination "$reactionProject/Assets/Resources"
Copy-Item -LiteralPath "$reactionRepo/Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab", "$reactionRepo/Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab.meta" -Destination "$reactionProject/Assets/Production"
Copy-Item -LiteralPath "$reactionRepo/ProjectSettings/ProjectVersion.txt" -Destination "$reactionProject/ProjectSettings/ProjectVersion.txt"
$reactionScripts = Get-ChildItem "$reactionRepo/Assets/Project/Runtime/Presentation/Combat" -Filter '*.cs' | Where-Object {
    $_.Name -like 'BattleStagePresenter*' -or $_.Name -like 'ActorVisualEffect*' -or $_.Name -in @('ActorDebuffVisual.cs', 'CardAnimationTiming.cs', 'ActorCardPresentation.cs', 'BattleFighterView.cs')
}
foreach ($reactionScript in $reactionScripts) { Copy-Item -LiteralPath $reactionScript.FullName -Destination "$reactionProject/Assets/Runtime/$($reactionScript.Name)" }
Copy-Item -LiteralPath "$reactionRepo/Assets/Project/Runtime/Content/StatusVisualData.cs", "$reactionRepo/Assets/Project/Runtime/Content/StatusVisualDataCatalog.cs" -Destination "$reactionProject/Assets/Runtime"
Copy-Item -LiteralPath "$reactionRepo/Assets/Project/Runtime/Content/Cards/UltimateCardSO.cs", "$reactionRepo/Assets/Project/Runtime/Content/Cards/SkillCardSO.cs" -Destination "$reactionProject/Assets/Runtime"
Copy-Item -LiteralPath "$PSScriptRoot/HitReactionVisualChecks.cs" -Destination "$reactionProject/Assets/Editor/HitReactionVisualChecks.cs"
Copy-Item -LiteralPath "$reactionRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$reactionRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$reactionProject/Assets/Plugins"
$reactionNewtonsoft = Get-ChildItem "$reactionRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
if ($null -eq $reactionNewtonsoft) { throw 'Cannot find the project Newtonsoft.Json runtime dependency.' }
Copy-Item -LiteralPath $reactionNewtonsoft.FullName -Destination "$reactionProject/Assets/Plugins/Newtonsoft.Json.dll"
$reactionProcess = Start-Process -FilePath $reactionEditor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-projectPath', ('"' + $reactionProject + '"'), '-executeMethod', 'HitReactionVisualChecks.Run', '-logFile', ('"' + "$reactionProject/visual.log" + '"'))
Write-Output "Reaction visual process: $($reactionProcess.Id)"
Write-Output "Log: $reactionProject/visual.log"
Write-Output "Images: $reactionProject/Evidence"
