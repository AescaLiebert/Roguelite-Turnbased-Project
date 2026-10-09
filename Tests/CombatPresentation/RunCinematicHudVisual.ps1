$ErrorActionPreference = 'Stop'
$cinemaRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$cinemaProject = Join-Path $cinemaRepo 'Temp/CinematicHudVisualProject'
$cinemaEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.4f1/Editor/Unity.exe'
# Build the runtime against installed Unity libraries without replacing project Core DLLs.
$cinemaBuild = Join-Path $cinemaRepo 'Temp/CinematicHudCompile'
New-Item -ItemType Directory -Force $cinemaBuild | Out-Null
$cinemaManaged = Join-Path (Split-Path $cinemaEditor) 'Data/Managed/UnityEngine'
$cinemaReferencePaths = @((Get-ChildItem -LiteralPath $cinemaManaged -Filter '*.dll').FullName)
$cinemaReferencePaths += @("$cinemaRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$cinemaRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll")
foreach ($cinemaAssembly in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll')) {
    $cinemaReferencePaths += "$cinemaRepo/Library/ScriptAssemblies/$cinemaAssembly"
}
$cinemaReferences = foreach ($cinemaPath in $cinemaReferencePaths) {
    $cinemaXmlPath = [System.Security.SecurityElement]::Escape($cinemaPath)
    '<Reference Include="' + [IO.Path]::GetFileNameWithoutExtension($cinemaPath) + '"><HintPath>' + $cinemaXmlPath + '</HintPath></Reference>'
}
$cinemaRuntimePath = [System.Security.SecurityElement]::Escape("$cinemaRepo/Assets/Project/Runtime/**/*.cs")
$cinemaProjectXml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><LangVersion>9.0</LangVersion><NoWarn>0649;0169;0414</NoWarn></PropertyGroup>
  <ItemGroup><Compile Include="$cinemaRuntimePath" />
  $($cinemaReferences -join "`n")
  </ItemGroup>
</Project>
"@
Set-Content -LiteralPath "$cinemaBuild/Runtime.csproj" -Value $cinemaProjectXml -Encoding utf8
dotnet build "$cinemaBuild/Runtime.csproj" --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed.' }
New-Item -ItemType Directory -Force "$cinemaProject/Assets/Editor", "$cinemaProject/Assets/Plugins", "$cinemaProject/Assets/Project/UI", "$cinemaProject/ProjectSettings" | Out-Null
Copy-Item -LiteralPath "$cinemaRepo/ProjectSettings/ProjectVersion.txt" -Destination "$cinemaProject/ProjectSettings/ProjectVersion.txt"
Copy-Item -LiteralPath "$cinemaBuild/bin/Debug/netstandard2.1/Runtime.dll" -Destination "$cinemaProject/Assets/Plugins"
Copy-Item -LiteralPath "$cinemaRepo/Assets/Project/Plugins/FightingAllstar.Core.dll", "$cinemaRepo/Assets/Project/Plugins/FightingAllstar.Contracts.dll" -Destination "$cinemaProject/Assets/Plugins"
foreach ($cinemaDependency in @('UnityEngine.UI.dll', 'Unity.TextMeshPro.dll', 'Unity.InputSystem.dll', 'UnityEditor.UI.dll', 'UnityEditor.UI.Analytics.dll', 'Unity.InternalAPIEngineBridge.004.dll')) {
    Copy-Item -LiteralPath "$cinemaRepo/Library/ScriptAssemblies/$cinemaDependency" -Destination "$cinemaProject/Assets/Plugins"
}
$cinemaNewtonsoft = Get-ChildItem "$cinemaRepo/Library/PackageCache" -Recurse -Filter Newtonsoft.Json.dll | Where-Object { $_.FullName -match 'Runtime' } | Select-Object -First 1
Copy-Item -LiteralPath $cinemaNewtonsoft.FullName -Destination "$cinemaProject/Assets/Plugins"
foreach ($cinemaHud in @('BattleCardHud.uxml', 'BattleCardHud.uxml.meta', 'BattleCardHud.uss', 'BattleCardHud.uss.meta')) {
    Copy-Item -LiteralPath "$cinemaRepo/Assets/Project/UI/$cinemaHud" -Destination "$cinemaProject/Assets/Project/UI"
}
Copy-Item -LiteralPath "$PSScriptRoot/CinematicHudChecks.cs" -Destination "$cinemaProject/Assets/Editor"
Copy-Item -LiteralPath "$cinemaRepo/Assets/Project/Art/Character/Cut/Cut_17_Mai94.png", "$cinemaRepo/Assets/Project/Art/Character/Cut/Cut_17_Mai94.png.meta" -Destination "$cinemaProject/Assets"
Copy-Item -LiteralPath "$cinemaRepo/Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss" -Destination "$cinemaProject/Assets"
Copy-Item -LiteralPath "$cinemaRepo/Assets/TextMesh Pro/Fonts/LiberationSans.ttf" -Destination "$cinemaProject/Assets"
$cinemaCapture = Get-Content -LiteralPath "$cinemaRepo/Assets/Project/Editor/BattlePreviewCapture.cs" -Raw
$cinemaCapture = $cinemaCapture.Replace('string path, UIDocument document)', 'string path, UIDocument document, int width = 1280, int height = 720)')
$cinemaCapture = $cinemaCapture.Replace('const int width = 1280;', '').Replace('const int height = 720;', '')
Set-Content -LiteralPath "$cinemaProject/Assets/Editor/BattlePreviewCapture.cs" -Value $cinemaCapture -Encoding utf8
$cinemaProcess = Start-Process -FilePath $cinemaEditor -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode', '-disable-assembly-updater', '-projectPath', ('"' + $cinemaProject + '"'), '-executeMethod', 'CinematicHudChecks.Run', '-logFile', ('"' + "$cinemaProject/visual.log" + '"'))
Write-Output "Cinematic HUD fixture process: $($cinemaProcess.Id)"
Write-Output "Log: $cinemaProject/visual.log"
Write-Output "Images: $cinemaProject/Evidence"
