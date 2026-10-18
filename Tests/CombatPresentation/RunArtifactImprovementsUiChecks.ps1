param([string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe', [switch]$SkillDetails, [switch]$ArtifactUpgrade, [switch]$MotionChecks)
$ErrorActionPreference = 'Stop'
$artifactRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$artifactProject = Join-Path $artifactRepo $(if ($MotionChecks) { 'Temp/ScreenMotionUiProject' } elseif ($SkillDetails) { 'Temp/SkillDetailsUiProject' } elseif ($ArtifactUpgrade) { 'Temp/ArtifactUpgradeUiProject' } else { 'Temp/ArtifactImprovementsUiProject' })
New-Item -ItemType Directory -Force "$artifactProject/Assets/Project", "$artifactProject/Assets/Editor", "$artifactProject/Assets/Plugins", "$artifactProject/ProjectSettings" | Out-Null
foreach ($artifactFolder in @('Runtime', 'UI', 'Resources', 'Data')) {
    Copy-Item -LiteralPath "$artifactRepo/Assets/Project/$artifactFolder" -Destination "$artifactProject/Assets/Project" -Recurse -Force
}
New-Item -ItemType Directory -Force "$artifactProject/Assets/Project/Art/Character", "$artifactProject/Assets/Project/Scenes" | Out-Null
Copy-Item -LiteralPath "$artifactRepo/Assets/Project/Art/UI" -Destination "$artifactProject/Assets/Project/Art" -Recurse -Force
Copy-Item -LiteralPath "$artifactRepo/Assets/Project/Art/CardSkills" -Destination "$artifactProject/Assets/Project/Art" -Recurse -Force
foreach ($artifactFolder in @('Cut', 'Icon', 'SplashArt')) {
    Copy-Item -LiteralPath "$artifactRepo/Assets/Project/Art/Character/$artifactFolder" -Destination "$artifactProject/Assets/Project/Art/Character" -Recurse -Force
}
Copy-Item -LiteralPath "$artifactRepo/Assets/Resources", "$artifactRepo/Assets/UI Toolkit" -Destination "$artifactProject/Assets" -Recurse -Force
Copy-Item -LiteralPath "$artifactRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset", "$artifactRepo/Assets/Project/Scenes/Fixed1920PanelSettings.asset.meta" -Destination "$artifactProject/Assets/Project/Scenes"
Copy-Item -LiteralPath "$artifactRepo/Assets/Project/Editor/ScreenLayoutPreview.cs", "$PSScriptRoot/ArtifactImprovementsUiChecks.cs", "$PSScriptRoot/ArtifactUpgradePresentationChecks.cs" -Destination "$artifactProject/Assets/Editor"
if ($MotionChecks) {
    Copy-Item -LiteralPath "$PSScriptRoot/ScreenMotionRuntimeChecks.cs" -Destination "$artifactProject/Assets/Editor"
    Copy-Item -LiteralPath "$PSScriptRoot/ScreenMotionRuntimeProbe.cs" -Destination "$artifactProject/Assets"
}
Copy-Item -LiteralPath "$artifactRepo/ProjectSettings/ProjectVersion.txt" -Destination "$artifactProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$artifactRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$artifactRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$artifactProject/Assets/Plugins"
foreach ($artifactDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll', 'UnityEditor.UI.dll', 'UnityEditor.UI.Analytics.dll', 'Unity.InternalAPIEngineBridge.004.dll')) {
    Copy-Item -LiteralPath "$artifactRepo/Library/ScriptAssemblies/$artifactDependency" -Destination "$artifactProject/Assets/Plugins"
}
$artifactNewtonsoft = Get-ChildItem "$artifactRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
Copy-Item -LiteralPath $artifactNewtonsoft.FullName -Destination "$artifactProject/Assets/Plugins/Newtonsoft.Json.dll"
$artifactEvidence = if ($MotionChecks) { 'Artifacts/ScreenMotion' } elseif ($SkillDetails) { 'Artifacts/SkillDetails' } elseif ($ArtifactUpgrade) { 'Artifacts/ArtifactUpgrade' } else { 'Artifacts/ArtifactImprovements' }
$artifactResult = Join-Path $artifactProject "$artifactEvidence/Verification.txt"
if (Test-Path -LiteralPath $artifactResult) { Remove-Item -LiteralPath $artifactResult }
$artifactMethod = if ($MotionChecks) { 'ScreenMotionRuntimeChecks.Run' } elseif ($ArtifactUpgrade) { 'ArtifactUpgradePresentationChecks.Run' } else { 'ArtifactImprovementsUiChecks.Run' }
$artifactArgs = @('-batchmode', '-disable-assembly-updater', '-projectPath', ('"' + $artifactProject + '"'), '-executeMethod', $artifactMethod, '-logFile', ('"' + "$artifactProject/artifact-improvements.log" + '"'))
if ($SkillDetails) { $artifactArgs += '-skillDetails' }
$artifactProcess = Start-Process -FilePath $UnityEditorPath -WindowStyle Hidden -PassThru -ArgumentList $artifactArgs
Write-Output "Artifact fixture process: $($artifactProcess.Id)"
Write-Output "Evidence: $artifactProject/$artifactEvidence"
