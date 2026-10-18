param(
    [string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe',
    [switch]$Motion,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$summonRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$summonProject = Join-Path $summonRepo 'Temp/SummonSequencePreviewProject'
$summonEvidence = Join-Path $summonRepo 'Artifacts/SummonSequence'
New-Item -ItemType Directory -Force "$summonProject/Assets/Project", "$summonProject/Assets/Editor", "$summonProject/Assets/Plugins", "$summonProject/ProjectSettings", "$summonProject/Packages" | Out-Null

# Preserve script GUIDs so the copied, authored character registry/assets deserialize normally.
Copy-Item -LiteralPath "$summonRepo/Assets/Project/Runtime" -Destination "$summonProject/Assets/Project" -Recurse -Force
foreach ($summonEditorFile in @('SummonSequencePreview.cs', 'SummonSequencePreviewWindow.cs')) {
    Copy-Item -LiteralPath "$summonRepo/Assets/Project/Editor/$summonEditorFile" -Destination "$summonProject/Assets/Editor"
}
Copy-Item -LiteralPath "$PSScriptRoot/SummonSequenceBatchChecks.cs" -Destination "$summonProject/Assets/Editor"
Copy-Item -LiteralPath "$PSScriptRoot/ObtainFlowIntegrationChecks.cs" -Destination "$summonProject/Assets/Editor"
Copy-Item -LiteralPath "$summonRepo/ProjectSettings/ProjectVersion.txt" -Destination "$summonProject/ProjectSettings/ProjectVersion.txt"
$summonSourceManifest = Get-Content -LiteralPath "$summonRepo/Packages/manifest.json" -Raw | ConvertFrom-Json
$summonBuiltins = @{}
foreach ($summonModule in $summonSourceManifest.dependencies.PSObject.Properties) {
    if ($summonModule.Name.StartsWith('com.unity.modules.')) { $summonBuiltins[$summonModule.Name] = $summonModule.Value }
}
@{ dependencies = $summonBuiltins } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$summonProject/Packages/manifest.json" -Encoding utf8
foreach ($summonPlugin in @('FightingAllstar.Core.dll', 'FightingAllstar.Contracts.dll')) {
    Copy-Item -LiteralPath "$summonRepo/Assets/Project/Plugins/$summonPlugin" -Destination "$summonProject/Assets/Plugins"
}
foreach ($summonDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll', 'UnityEditor.UI.dll', 'UnityEditor.UI.Analytics.dll', 'Unity.InternalAPIEngineBridge.004.dll')) {
    Copy-Item -LiteralPath "$summonRepo/Library/ScriptAssemblies/$summonDependency" -Destination "$summonProject/Assets/Plugins"
}
$summonNewtonsoft = Get-ChildItem -LiteralPath "$summonRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
if ($null -eq $summonNewtonsoft) { throw 'The cached Newtonsoft runtime dependency was not found.' }
Copy-Item -LiteralPath $summonNewtonsoft.FullName -Destination "$summonProject/Assets/Plugins/Newtonsoft.Json.dll"

# Follow the real UI/portrait dependency graph, while excluding unrelated battle cards and 3D models.
$summonGuids = @{}
foreach ($summonMetaAbsolute in (& rg --files (Join-Path $summonRepo 'Assets') -g '*.meta')) {
    $summonMeta = $summonMetaAbsolute.Substring($summonRepo.Length + 1)
    $summonMetaText = [IO.File]::ReadAllText($summonMetaAbsolute)
    $summonGuid = [regex]::Match($summonMetaText, '(?m)^guid:\s*([a-fA-F0-9]{32})')
    if ($summonGuid.Success) { $summonGuids[$summonGuid.Groups[1].Value.ToLowerInvariant()] = $summonMeta.Substring(0, $summonMeta.Length - 5).Replace('\', '/') }
}
$summonQueue = New-Object 'System.Collections.Generic.Queue[string]'
foreach ($summonSeed in @('Assets/Resources/CharacterObjectRegistry.asset', 'Assets/Project/Resources/CharacterIconArt.asset', 'Assets/Project/Resources/UI/ItemIconArt.asset', 'Assets/Project/Resources/UI/ArtifactArt.asset', 'Assets/Project/Resources/UI/ItemRewards.uss', 'Assets/Project/Resources/UI/SummonSequence.uss', 'Assets/Project/Resources/UI/CharacterIcon.uxml', 'Assets/Project/UI/GachaBanner.uxml', 'Assets/Project/Scenes/Fixed1920PanelSettings.asset')) { $summonQueue.Enqueue($summonSeed) }
$summonCopied = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$summonVisualExtensions = @('.png', '.jpg', '.jpeg', '.webp', '.ttf', '.otf', '.uss', '.uxml', '.tss')
function Add-SummonDependency([string]$summonRelative) {
    $summonRelative = $summonRelative.Replace('\', '/')
    $summonExtension = [IO.Path]::GetExtension($summonRelative).ToLowerInvariant()
    if ($summonVisualExtensions -contains $summonExtension -or ($summonExtension -eq '.asset' -and $summonRelative -match '^Assets/Project/Data/Character/')) { $summonQueue.Enqueue($summonRelative) }
}
while ($summonQueue.Count -gt 0) {
    $summonRelative = $summonQueue.Dequeue()
    if (-not $summonCopied.Add($summonRelative)) { continue }
    $summonSource = Join-Path $summonRepo $summonRelative
    if (-not (Test-Path -LiteralPath $summonSource -PathType Leaf)) { throw "Preview dependency does not exist: $summonRelative" }
    $summonDestination = Join-Path $summonProject $summonRelative
    New-Item -ItemType Directory -Force (Split-Path $summonDestination) | Out-Null
    Copy-Item -LiteralPath $summonSource -Destination $summonDestination -Force
    if (Test-Path -LiteralPath ($summonSource + '.meta')) { Copy-Item -LiteralPath ($summonSource + '.meta') -Destination ($summonDestination + '.meta') -Force }
    if ([IO.Path]::GetExtension($summonSource).ToLowerInvariant() -notin @('.asset', '.uxml', '.uss', '.tss')) { continue }
    $summonText = [Net.WebUtility]::HtmlDecode([IO.File]::ReadAllText($summonSource))
    foreach ($summonMatch in [regex]::Matches($summonText, '[a-fA-F0-9]{32}')) {
        $summonReferenced = $summonGuids[$summonMatch.Value.ToLowerInvariant()]
        if ($summonReferenced) { Add-SummonDependency $summonReferenced }
    }
    foreach ($summonMatch in [regex]::Matches($summonText, '(?:url\(\s*["'']?|\bsrc=["''])([^"''\)]+)')) {
        $summonUri = ($summonMatch.Groups[1].Value -split '\?')[0]
        if ($summonUri.StartsWith('project://database/')) { $summonUri = $summonUri.Substring('project://database/'.Length) }
        if ($summonUri -match '^[a-zA-Z][a-zA-Z0-9+.-]*://') { continue }
        if ($summonUri.StartsWith('Assets/')) { Add-SummonDependency $summonUri; continue }
        $summonAbsolute = [IO.Path]::GetFullPath((Join-Path (Split-Path $summonSource) $summonUri))
        if ($summonAbsolute.StartsWith((Join-Path $summonRepo 'Assets') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            Add-SummonDependency $summonAbsolute.Substring($summonRepo.Length + 1)
        }
    }
}
Write-Output "Prepared isolated summon project with $($summonCopied.Count) visual assets; current scenes and account data are excluded."
if ($PrepareOnly) { exit 0 }
$summonResult = Join-Path $summonProject 'Artifacts/SummonSequence/Verification.txt'
if (Test-Path -LiteralPath $summonResult) { Remove-Item -LiteralPath $summonResult }
$summonArguments = @('-batchmode', '-disable-assembly-updater', '-projectPath', ('"' + $summonProject + '"'), '-executeMethod', 'SummonSequenceBatchChecks.Run', '-summonEvidencePath', ('"' + $summonEvidence + '"'), '-logFile', ('"' + "$summonProject/visual.log" + '"'))
if ($Motion) { $summonArguments += '-summonMotion' }
$summonProcess = Start-Process -FilePath $UnityEditorPath -WindowStyle Hidden -PassThru -ArgumentList $summonArguments
Write-Output "Summon preview process: $($summonProcess.Id)"
Write-Output "Evidence: $summonEvidence"
Write-Output "Log: $summonProject/visual.log"
