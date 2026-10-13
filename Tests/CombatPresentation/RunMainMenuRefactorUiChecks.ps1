param([string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$rewardRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$rewardProject = Join-Path $rewardRepo 'Temp/MainMenuRefactorUiProject'
New-Item -ItemType Directory -Force "$rewardProject/Assets/Project", "$rewardProject/Assets/Editor", "$rewardProject/Assets/Plugins", "$rewardProject/ProjectSettings" | Out-Null
dotnet build "$rewardRepo/Shared/FightingAllstar.Core/FightingAllstar.Core.csproj" --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
foreach ($rewardFolder in @('Runtime', 'UI', 'Resources', 'Data')) {
    Copy-Item -LiteralPath "$rewardRepo/Assets/Project/$rewardFolder" -Destination "$rewardProject/Assets/Project" -Recurse -Force
}
New-Item -ItemType Directory -Force "$rewardProject/Assets/Project/Art/Character", "$rewardProject/Assets/Project/Scenes" | Out-Null
Copy-Item -LiteralPath "$rewardRepo/Assets/Project/Art/UI" -Destination "$rewardProject/Assets/Project/Art" -Recurse -Force
foreach ($rewardFolder in @('Cut', 'Icon')) {
    Copy-Item -LiteralPath "$rewardRepo/Assets/Project/Art/Character/$rewardFolder" -Destination "$rewardProject/Assets/Project/Art/Character" -Recurse -Force
}
Copy-Item -LiteralPath "$rewardRepo/Assets/Resources" -Destination "$rewardProject/Assets" -Recurse -Force
Copy-Item -LiteralPath "$rewardRepo/Assets/UI Toolkit" -Destination "$rewardProject/Assets" -Recurse -Force
Copy-Item -LiteralPath "$rewardRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset", "$rewardRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset.meta" -Destination "$rewardProject/Assets/Project/Scenes"
Copy-Item -LiteralPath "$rewardRepo/Assets/Project/Editor/ScreenLayoutPreview.cs", "$PSScriptRoot/MainMenuRefactorUiChecks.cs" -Destination "$rewardProject/Assets/Editor"
Copy-Item -LiteralPath "$rewardRepo/ProjectSettings/ProjectVersion.txt" -Destination "$rewardProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$rewardRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$rewardRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$rewardProject/Assets/Plugins"
foreach ($rewardDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll', 'UnityEditor.UI.dll', 'UnityEditor.UI.Analytics.dll', 'Unity.InternalAPIEngineBridge.004.dll')) {
    Copy-Item -LiteralPath "$rewardRepo/Library/ScriptAssemblies/$rewardDependency" -Destination "$rewardProject/Assets/Plugins"
}
$rewardNewtonsoft = Get-ChildItem "$rewardRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
Copy-Item -LiteralPath $rewardNewtonsoft.FullName -Destination "$rewardProject/Assets/Plugins/Newtonsoft.Json.dll"
$rewardResult = Join-Path $rewardProject 'Artifacts/MainMenuRefactor/Verification.txt'
if (Test-Path -LiteralPath $rewardResult) { Remove-Item -LiteralPath $rewardResult }
$rewardProcess = Start-Process -FilePath $UnityEditorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-disable-assembly-updater', '-projectPath', ('"' + $rewardProject + '"'), '-executeMethod', 'MainMenuRefactorUiChecks.Run', '-logFile', ('"' + "$rewardProject/visual.log" + '"'))
Write-Output "Main menu UI fixture process: $($rewardProcess.Id)"
Write-Output "Evidence: $rewardProject/Artifacts/MainMenuRefactor"
Write-Output "Log: $rewardProject/visual.log"

