$ErrorActionPreference = 'Stop'
$multiRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$multiProject = Join-Path $multiRepo 'Temp/MultiHitVisualProject'
$multiEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe'
New-Item -ItemType Directory -Force "$multiProject/Assets/Editor", "$multiProject/Assets/Plugins", "$multiProject/Assets/Project", "$multiProject/ProjectSettings" | Out-Null
# Compile the production runtime together, including presentation dependencies added
# since this fixture originally copied individual stage files.
$multiLegacyRuntime = [System.IO.Path]::GetFullPath("$multiProject/Assets/Runtime")
$multiWorkspace = [System.IO.Path]::GetFullPath($multiProject) + [System.IO.Path]::DirectorySeparatorChar
if (-not $multiLegacyRuntime.StartsWith($multiWorkspace, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture path escaped its isolated project.' }
if (Test-Path -LiteralPath $multiLegacyRuntime) { Remove-Item -LiteralPath $multiLegacyRuntime -Recurse -Force }
foreach ($multiFolder in @('Runtime', 'UI', 'Resources', 'Data')) {
    Copy-Item -LiteralPath "$multiRepo/Assets/Project/$multiFolder" -Destination "$multiProject/Assets/Project" -Recurse -Force
}
New-Item -ItemType Directory -Force "$multiProject/Assets/Project/Art/Character", "$multiProject/Assets/Project/Scenes" | Out-Null
Copy-Item -LiteralPath "$multiRepo/Assets/Project/Art/UI" -Destination "$multiProject/Assets/Project/Art" -Recurse -Force
foreach ($multiFolder in @('Cut', 'Icon')) {
    Copy-Item -LiteralPath "$multiRepo/Assets/Project/Art/Character/$multiFolder" -Destination "$multiProject/Assets/Project/Art/Character" -Recurse -Force
}
Copy-Item -LiteralPath "$multiRepo/Assets/Resources" -Destination "$multiProject/Assets" -Recurse -Force
Copy-Item -LiteralPath "$multiRepo/Assets/UI Toolkit" -Destination "$multiProject/Assets" -Recurse -Force
Copy-Item -LiteralPath "$multiRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset", "$multiRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset.meta" -Destination "$multiProject/Assets/Project/Scenes"
Copy-Item -LiteralPath "$multiRepo/ProjectSettings/ProjectVersion.txt" -Destination "$multiProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$PSScriptRoot/MultiHitVisualChecks.cs" -Destination "$multiProject/Assets/Editor/MultiHitVisualChecks.cs"
Copy-Item -LiteralPath "$multiRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$multiRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$multiProject/Assets/Plugins"
foreach ($multiDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll', 'UnityEditor.UI.dll', 'UnityEditor.UI.Analytics.dll', 'Unity.InternalAPIEngineBridge.004.dll')) {
    Copy-Item -LiteralPath "$multiRepo/Library/ScriptAssemblies/$multiDependency" -Destination "$multiProject/Assets/Plugins"
}
$multiNewtonsoft = Get-ChildItem "$multiRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
if ($null -eq $multiNewtonsoft) { throw 'Cannot find the project Newtonsoft.Json runtime dependency.' }
Copy-Item -LiteralPath $multiNewtonsoft.FullName -Destination "$multiProject/Assets/Plugins/Newtonsoft.Json.dll"
$multiProcess = Start-Process -FilePath $multiEditor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-projectPath', ('"' + $multiProject + '"'), '-executeMethod', 'MultiHitVisualChecks.Run', '-logFile', ('"' + "$multiProject/visual.log" + '"'))
Write-Output "Visual preview process: $($multiProcess.Id)"
Write-Output "Log: $multiProject/visual.log"
Write-Output "Images: $multiProject/Evidence"
