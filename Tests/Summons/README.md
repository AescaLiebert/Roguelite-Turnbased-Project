# Summon and Geo Construct backend checks

Run from the repository root:

```powershell
dotnet restore Tests/Summons/SummonChecks.csproj --ignore-failed-sources
dotnet build Tests/Summons/SummonChecks.csproj --no-restore -m:1 -o Temp/SummonChecksBackend
dotnet Temp/SummonChecksBackend/SummonChecks.dll
```

The separate output folder avoids writing Unity's imported plugin binaries while the Editor is loading them. After backend changes, rebuild the official shared project to update those binaries:

```powershell
dotnet build Shared/FightingAllstar.Core/FightingAllstar.Core.csproj --configuration Release -m:1
```

The harness covers entity lifetime and ownership, structural grey-status protection, deterministic recasting/actions, independent clones, snapshot JSON, public projection and replay, AI isolation, shared construct capacity, overflow, personal shields, HP caps, bypass policies, deferred defeat, actual DOT/reaction/reflection commits, source character ranks and ultimate tiers, and the saved native character packages. Run from the root because persisted-package checks read the three filesets in `Assets/Project/Data/Character/Pre-Release_Phase/PR_Genshin-Impact/`.

`AuditChecks.cs` adds generated-status origin, opening aura reconstruction, ordinary-card validation, persisted structural invariants, per-contact healing receipts, allied summon defeat credit, and AoE root-before-reaction ordering regressions.

The follow-up audit also checks zero-formula contacts versus fully construct-absorbed contacts, turn-end creation with the full lifetime and final activation, and both teams' entity/marker replay through expiry. The focused suite currently passes 291 assertions.

Models, animation, VFX, camera behavior, and visual game feel are outside these backend checks. Implementation and validation evidence: `Artifacts/Summons/Backend/Implementation.md`.
