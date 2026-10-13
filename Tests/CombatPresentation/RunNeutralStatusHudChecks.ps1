param([string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$neutralRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$neutralProject = Join-Path $neutralRepo 'Temp/NeutralStatusHudProject'
$neutralBuild = Join-Path $neutralRepo 'Temp/NeutralStatusHudCompile'
New-Item -ItemType Directory -Force $neutralBuild, "$neutralProject/Assets/Editor", "$neutralProject/Assets/Plugins", "$neutralProject/ProjectSettings" | Out-Null
dotnet build "$neutralRepo/Shared/FightingAllstar.Core/FightingAllstar.Core.csproj" --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
$neutralManaged = Join-Path (Split-Path $UnityEditorPath) 'Data/Managed/UnityEngine'
$neutralPaths = @((Get-ChildItem -LiteralPath $neutralManaged -Filter '*.dll').FullName)
$neutralPaths += @("$neutralRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$neutralRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll")
foreach ($neutralDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll')) {
    $neutralPaths += "$neutralRepo/Library/ScriptAssemblies/$neutralDependency"
}
$neutralReferences = foreach ($neutralPath in $neutralPaths) {
    '<Reference Include="' + [IO.Path]::GetFileNameWithoutExtension($neutralPath) + '"><HintPath>' + [System.Security.SecurityElement]::Escape($neutralPath) + '</HintPath></Reference>'
}
$neutralRuntime = [System.Security.SecurityElement]::Escape("$neutralRepo/Assets/Project/Runtime/**/*.cs")
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><LangVersion>9.0</LangVersion><NoWarn>0649;0169;0414</NoWarn></PropertyGroup>
  <ItemGroup><Compile Include="$neutralRuntime" />
  $($neutralReferences -join "`n")
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath "$neutralBuild/Runtime.csproj" -Encoding utf8
dotnet build "$neutralBuild/Runtime.csproj" --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
Copy-Item -LiteralPath "$neutralRepo/ProjectSettings/ProjectVersion.txt" -Destination "$neutralProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$neutralBuild/bin/Debug/netstandard2.1/Runtime.dll", "$neutralRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$neutralRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$neutralProject/Assets/Plugins"
foreach ($neutralDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll', 'UnityEditor.UI.dll', 'UnityEditor.UI.Analytics.dll', 'Unity.InternalAPIEngineBridge.004.dll')) {
    Copy-Item -LiteralPath "$neutralRepo/Library/ScriptAssemblies/$neutralDependency" -Destination "$neutralProject/Assets/Plugins"
}
$neutralNewtonsoft = Get-ChildItem "$neutralRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
Copy-Item -LiteralPath $neutralNewtonsoft.FullName -Destination "$neutralProject/Assets/Plugins/Newtonsoft.Json.dll"
Copy-Item -LiteralPath "$PSScriptRoot/NeutralStatusHudChecks.cs" -Destination "$neutralProject/Assets/Editor"
$neutralResult = Join-Path $neutralProject 'Evidence/neutral-status-hud.txt'
if (Test-Path -LiteralPath $neutralResult) { Remove-Item -LiteralPath $neutralResult }
$neutralProcess = Start-Process -FilePath $UnityEditorPath -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-disable-assembly-updater', '-projectPath', ('"' + $neutralProject + '"'), '-executeMethod', 'NeutralStatusHudChecks.Run', '-logFile', ('"' + "$neutralProject/visual.log" + '"'))
Write-Output "Neutral HUD fixture process: $($neutralProcess.Id)"
Write-Output "Log: $neutralProject/visual.log"
Write-Output "Result: $neutralProject/Evidence/neutral-status-hud.txt"
