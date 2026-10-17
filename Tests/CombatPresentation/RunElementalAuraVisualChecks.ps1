param([string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$textRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$textProject = Join-Path $textRepo 'Temp/CombatTextProject'
New-Item -ItemType Directory -Force "$textProject/Assets/Project", "$textProject/Assets/Editor", "$textProject/Assets/Plugins", "$textProject/ProjectSettings" | Out-Null
dotnet build "$textRepo/Shared/FightingAllstar.Core/FightingAllstar.Core.csproj" --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
foreach ($textFolder in @('Runtime', 'UI', 'Resources', 'Data')) {
    Copy-Item -LiteralPath "$textRepo/Assets/Project/$textFolder" -Destination "$textProject/Assets/Project" -Recurse -Force
}
New-Item -ItemType Directory -Force "$textProject/Assets/Project/Art/Character", "$textProject/Assets/Project/Scenes" | Out-Null
Copy-Item -LiteralPath "$textRepo/Assets/Project/Art/UI" -Destination "$textProject/Assets/Project/Art" -Recurse -Force
foreach ($textFolder in @('Cut', 'Icon')) {
    Copy-Item -LiteralPath "$textRepo/Assets/Project/Art/Character/$textFolder" -Destination "$textProject/Assets/Project/Art/Character" -Recurse -Force
}
Copy-Item -LiteralPath "$textRepo/Assets/Resources" -Destination "$textProject/Assets" -Recurse -Force
Copy-Item -LiteralPath "$textRepo/Assets/UI Toolkit" -Destination "$textProject/Assets" -Recurse -Force
Copy-Item -LiteralPath "$textRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset", "$textRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset.meta" -Destination "$textProject/Assets/Project/Scenes"
Copy-Item -LiteralPath "$PSScriptRoot/ElementalAuraVisualChecks.cs", "$textRepo/Assets/Project/Editor/BattlePreviewCapture.cs" -Destination "$textProject/Assets/Editor"
Copy-Item -LiteralPath "$textRepo/ProjectSettings/ProjectVersion.txt" -Destination "$textProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$textRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$textRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$textProject/Assets/Plugins"
foreach ($textDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll', 'UnityEditor.UI.dll', 'UnityEditor.UI.Analytics.dll', 'Unity.InternalAPIEngineBridge.004.dll')) {
    Copy-Item -LiteralPath "$textRepo/Library/ScriptAssemblies/$textDependency" -Destination "$textProject/Assets/Plugins"
}
$textNewtonsoft = Get-ChildItem "$textRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
Copy-Item -LiteralPath $textNewtonsoft.FullName -Destination "$textProject/Assets/Plugins/Newtonsoft.Json.dll"
$textResult = Join-Path $textProject 'Artifacts/ElementalAuras/result.txt'
if (Test-Path -LiteralPath $textResult) { Remove-Item -LiteralPath $textResult }
$textProcess = Start-Process -FilePath $UnityEditorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-disable-assembly-updater', '-projectPath', ('"' + $textProject + '"'), '-executeMethod', 'ElementalAuraVisualChecks.Run', '-logFile', ('"' + "$textProject/visual.log" + '"'))
Write-Output "Aura fixture process: $($textProcess.Id)"
Write-Output "Evidence: $textProject/Artifacts/ElementalAuras"
Write-Output "Log: $textProject/visual.log"
$textDeadline = [DateTime]::UtcNow.AddMinutes(4)
while (-not $textProcess.WaitForExit(30000)) {
    if ([DateTime]::UtcNow -gt $textDeadline) { throw "Elemental aura validation timed out. See $textProject/visual.log" }
    Write-Output 'Unity is importing the isolated fixture and checking neutral aura HUD and spread feedback.'
}
if (-not (Test-Path -LiteralPath $textResult)) { throw "Unity did not produce the aura audit. See $textProject/visual.log" }
New-Item -ItemType Directory -Force "$textRepo/Artifacts/ElementalAuras" | Out-Null
Copy-Item -Path "$textProject/Artifacts/ElementalAuras/*" -Destination "$textRepo/Artifacts/ElementalAuras" -Force
Get-Content -LiteralPath $textResult -TotalCount 1
if ($textProcess.ExitCode -ne 0) { throw "Elemental aura validation failed. See $textRepo/Artifacts/ElementalAuras/result.txt" }

