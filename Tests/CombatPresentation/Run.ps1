$ErrorActionPreference = 'Stop'
$battleRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$battleScratch = Join-Path $battleRepo 'Temp/BattlePlaybackChecks'
New-Item -ItemType Directory -Force $battleScratch | Out-Null
$battleCore = Join-Path $battleRepo 'Shared/FightingAllstar.Core/FightingAllstar.Core.csproj'
$battleChecks = Join-Path $PSScriptRoot '*.cs'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
  <ItemGroup><ProjectReference Include="$battleCore"/><Compile Include="$battleChecks"/></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $battleScratch 'Checks.csproj')
dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Battle playback checks failed.' }
