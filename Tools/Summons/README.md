This tool migrates only the native Fischl, Xiangling and Ningguang five-asset packages. It replaces their active Studio gameplay JSON and metadata review notes, preserving artwork references, GUIDs, source descriptions and saved authoring graphs. It compiles all three migrated characters before writing any file.

From the project root:

```powershell
dotnet restore Tools/Summons/MigrateGenshinSummons.csproj --ignore-failed-sources
dotnet build Tools/Summons/MigrateGenshinSummons.csproj --no-restore -m:1 -o Temp/SummonMigrationBuild
dotnet Temp/SummonMigrationBuild/MigrateGenshinSummons.dll --check
dotnet Temp/SummonMigrationBuild/MigrateGenshinSummons.dll
```

`--check` exits with code 1 when any targeted asset differs; a clean migrated package exits with code 0. An optional absolute project path may be supplied.

The source is `ArtDirection/Character Database/Pre-Release-Character.csv`. Owner turn means owner-side turn end. Maximum-level Ultimate values map to C5. DOT-scaled clauses multiply explicit DOT Dealt, without adding a base 100%. Ningguang Skill 2 snapshots layers after BeforeAction and removes all layers after queued action effects, including its new Geo-hit layer. Fischl's unqualified Max HP uses the prior owner's Max HP interpretation, and Oz retains the approved AoE periodic target.
