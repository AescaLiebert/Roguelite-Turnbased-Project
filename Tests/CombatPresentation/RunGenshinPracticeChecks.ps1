param([string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$practiceRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$practiceProject = Join-Path $practiceRepo 'Temp/CombatTextProject'
New-Item -ItemType Directory -Force "$practiceProject/Assets/Editor" | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/GenshinPracticeChecks.cs", "$practiceRepo/Assets/Project/Editor/BattlePreviewCapture.cs" -Destination "$practiceProject/Assets/Editor"
& "$PSScriptRoot/RunCombatTextChecks.ps1" -UnityEditorPath $UnityEditorPath
$practiceResult = "$practiceProject/Artifacts/GenshinPractice/result.txt"
if (Test-Path -LiteralPath $practiceResult) { Remove-Item -LiteralPath $practiceResult }
$practiceProcess = Start-Process -FilePath $UnityEditorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-disable-assembly-updater', '-projectPath', ('"' + $practiceProject + '"'), '-executeMethod', 'GenshinPracticeChecks.Run', '-logFile', ('"' + "$practiceProject/practice.log" + '"'))
Write-Output "Practice fixture process: $($practiceProcess.Id)"
$practiceDeadline = [DateTime]::UtcNow.AddMinutes(4)
while (-not $practiceProcess.WaitForExit(30000)) {
    if ([DateTime]::UtcNow -gt $practiceDeadline) { throw "Practice audit timed out. See $practiceProject/practice.log" }
    Write-Output 'Unity is testing the Practice picker and all Genshin cards.'
}
if ($practiceProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $practiceResult)) { throw "Practice audit failed. See $practiceProject/practice.log" }
New-Item -ItemType Directory -Force "$practiceRepo/Artifacts/GenshinElementSystem/Practice" | Out-Null
Copy-Item -Path "$practiceProject/Artifacts/GenshinPractice/*" -Destination "$practiceRepo/Artifacts/GenshinElementSystem/Practice" -Force
Get-Content -LiteralPath $practiceResult
