param([switch]$TrainingOnly, [switch]$DungeonPolicyOnly, [switch]$NeutralStatusOnly, [switch]$DungeonRewardsOnly, [switch]$DungeonArchitectureOnly, [switch]$DungeonEnemiesOnly, [switch]$ElementalOnly, [switch]$ElementalAiOnly)
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
if ($ElementalAiOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --elemental-ai
} elseif ($ElementalOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --elemental
} elseif ($DungeonEnemiesOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --dungeon-enemies
} elseif ($DungeonArchitectureOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --dungeon-architecture
} elseif ($DungeonRewardsOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --dungeon-rewards
} elseif ($NeutralStatusOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --neutral-status
} elseif ($DungeonPolicyOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --dungeon-policy
} elseif ($TrainingOnly) {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo -- --training
} else {
    dotnet run --project (Join-Path $battleScratch 'Checks.csproj') --configuration Release --nologo
}
if ($LASTEXITCODE -ne 0) { throw 'Battle playback checks failed.' }
