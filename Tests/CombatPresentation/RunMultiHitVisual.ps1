$ErrorActionPreference = 'Stop'
$multiRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$multiProject = Join-Path $multiRepo 'Temp/MultiHitVisualProject'
$multiEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe'
New-Item -ItemType Directory -Force "$multiProject/Assets/Editor", "$multiProject/Assets/Plugins", "$multiProject/Assets/Runtime", "$multiProject/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$multiRepo/ProjectSettings/ProjectVersion.txt" -Destination "$multiProject/ProjectSettings/ProjectVersion.txt"
foreach ($multiFile in @('BattleStagePresenter.cs', 'BattleStagePresenter.Camera.cs', 'BattleStagePresenter.Cinematics.cs', 'BattleStagePresenter.MultiHit.cs', 'CardAnimationTiming.cs')) {
    Copy-Item -LiteralPath "$multiRepo/Assets/Project/Runtime/Presentation/Combat/$multiFile" -Destination "$multiProject/Assets/Runtime/$multiFile"
}
Copy-Item -LiteralPath "$PSScriptRoot/MultiHitVisualChecks.cs" -Destination "$multiProject/Assets/Editor/MultiHitVisualChecks.cs"
Copy-Item -LiteralPath "$multiRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$multiRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$multiProject/Assets/Plugins"
$multiNewtonsoft = Get-ChildItem "$multiRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
if ($null -eq $multiNewtonsoft) { throw 'Cannot find the project Newtonsoft.Json runtime dependency.' }
Copy-Item -LiteralPath $multiNewtonsoft.FullName -Destination "$multiProject/Assets/Plugins/Newtonsoft.Json.dll"
$multiProcess = Start-Process -FilePath $multiEditor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-projectPath', ('"' + $multiProject + '"'), '-executeMethod', 'MultiHitVisualChecks.Run', '-logFile', ('"' + "$multiProject/visual.log" + '"'))
Write-Output "Visual preview process: $($multiProcess.Id)"
Write-Output "Log: $multiProject/visual.log"
Write-Output "Images: $multiProject/Evidence"
