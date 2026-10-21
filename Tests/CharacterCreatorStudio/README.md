# Character Creator Studio checks

The current shared harness passes **543 assertions**, including 52 passive-centralization checks. The latest output is `Artifacts/CharacterCreatorStudio/PassiveOverview/shared-evidence.json`. Counts below describe earlier milestone snapshots.

`StudioPassiveCentralizationChecks.Run()` verifies 25 checks through an attached Unity panel and a disposable native package: live section summaries, collapsible mechanic cards and passive preview, contextual modifier fields, imported receipt restrictions, native collision preservation, repeated removal/save/reopen, executable availability badges and source isolation. Global availability offers On-Field / Off-Field and ALL / PVP / PVE. Imported roster-wide or mixed gates remain visible until explicitly changed; preserved support hooks cannot be restricted to a battle mode. `RunRosterParity()` verifies all 39 native imports and 234 seeded production-engine cases across rank/tier/reserve configurations, including utility cards. See the [implementation report](../../ArtDirection/CharacterCreatorStudio/PassiveCentralizationImplementationReport.md) for remaining compatibility boundaries. These checks leave live roster assets unchanged.

`StudioKitAuthoringChecks.Run()` verifies 18 native direct-editor checks on a disposable Joe96-derived package and removes it afterward. Normal editing creates no StudioDraft asset. **`StudioJoe96AuthoringTrial.Run()` intentionally writes the existing Joe96 native five-asset set** through actual widgets; it is an authoring acceptance operation, not a read-only test. Its pre-conversion backup and 12 checks are under `Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96`. See the [native kit report](../../ArtDirection/CharacterCreatorStudio/NativeKitAuthoringReport.md).

`StudioNativeSessionReloadCheck.Start()` asynchronously verifies nine actual 3D native Joe96 and unsaved editor session recovery checks across Play Mode. Inspect `StudioNativeSessionReloadCheck.Status` until complete. It temporarily edits ATK in memory, keeps the saved SO at 250, and clears its verification edit after returning to the original scene. It checks committed damage/status results, not completion of the full animation cycle.

From stable Unity Edit Mode, `FightingAllstar.EditorTools.CharacterCreatorStudio.StudioBridgeChecks.Run()` verifies 26 native publisher, reimport, conflict, partial-failure recovery and rollback checks. Then `StudioBridgeLiveChecks.Start()` verifies 13 checks through actual Studio buttons, Play Mode domain reloads and a real 3D battle using normal native runtime discovery. Inspect `StudioBridgeLiveChecks.Status` until complete. These checks create disposable native packages and remove them afterward, preserving existing roster assets and the original editor scene. Results live in `Artifacts/CharacterCreatorStudio/Bridge/`. See the [bridge report](../../ArtDirection/CharacterCreatorStudio/BridgeImplementationReport.md) for the supported workflow and remaining limits.

Run from the repository root. The isolated output directory prevents this test build from overwriting Unity's active plugin DLLs:

```powershell
$studioChecksOutput = Join-Path (Get-Location) 'Temp/CharacterCreatorStudioChecks'
dotnet build Tests/CharacterCreatorStudio/CharacterCreatorStudioChecks.csproj --configuration Release -p:OutputPath="$studioChecksOutput" -p:AppendTargetFrameworkToOutputPath=false
dotnet "$studioChecksOutput/CharacterCreatorStudioChecks.dll" Artifacts/CharacterCreatorStudio/M0/KaeyaLegacyDefinition.json Artifacts/CharacterCreatorStudio/M3/core-evidence.json
```

`dotnet run` does not reliably discover a target when `OutputPath` is overridden; use the explicit DLL invocation above.

The original parity fixture is the actual frozen Kaeya definition captured in M0. Checks compile the flat saved graph into real `PassiveReactionDefinition`/`ExecuteEffect` operations and run `BattleEngine.Create` followed by a legal first skill plan. They compare authoritative fighter/card/status/RNG and damage/utility-event outcomes with the original character across seeds and opening configurations. Rule traces come from the production dispatcher and do not emit gameplay events or consume RNG. M3 also uses synthetic definitions to test reusable mechanics independently; it installs no Mian kit or production roster entry.

Coverage includes nested All/Any/Not execution, typed health/gauge/roster conditions, schema/reference/arity/cycle/expansion validation, complete source preservation, parameter/snippet reference remapping, text/presentation/gameplay hash separation, deterministic recompilation, frozen run snapshots, and the separate description authoring suite. Unity save/reimport/domain-reload, Undo, actual text renderers, actor playback, and activation/revert checks are owned by the editor verification fixture; .NET checks alone do not prove those integration gates.

The current shared harness passes **365 assertions**: 71 original core, 51 description, 19 variant, 80 query/policy, 56 execution, 47 Studio compiler, 9 immutable event/operation-context and 32 closure checks. M3 coverage includes:

- Schema 1→2→3 migration, flat sequences/branches/modules, bounded loops, scoped bindings, shared resource/status definitions, validation and duplication.
- Stable phase/priority ordering, activation receipts, cooldowns, origin controls and atomic rejection of an exhausted shared budget.
- Atomic resources, owned layer consumption, removal snapshots/reasons, threshold transitions and newly wired lifecycle hooks restricted to opted-in version-one rules; manual fact emission retains legacy compatibility.
- Explicit target anchors, logical adjacency, deterministic sampling/reuse, original-event versus operation targets and committed owner-turn history.
- Incoming caps across real damage paths, fatal-save cost/bypass rules, gauge guards, explicit True profiles and legacy behavior.
- Variant unlock/readiness/relock/costs, stable card identity, independent rank/level/tier, bound overrides and deterministic configured scenarios.

See [M3 implementation report](../../ArtDirection/CharacterCreatorStudio/M3ImplementationReport.md) for the exact execution contracts and separate live Unity evidence.

The [M3 closure report](../../ArtDirection/CharacterCreatorStudio/M3ClosureReport.md) adds invalid variant payloads, binding definite assignment, shared status removal callbacks, cap/fatal presets and UI authoring evidence. To retain the historical M3 output, direct the harness command above to `Artifacts/CharacterCreatorStudio/M3Closure/core-evidence.json`. Run `StudioClosureAuthoringTrial.Start()` in Unity to author the acceptance example through actual widget callbacks; inspect `Status` or its JSON evidence for completion.

M4 adds 45 shared assertions (410 total) for configuration coverage/overlap, hidden invalid branches, typed arbitrary snippets and detachment, localization, vocabulary identities/aliases/tooltips, resolved production override values, review-hash isolation and bundle reference remapping. Use `Artifacts/CharacterCreatorStudio/M4/core-evidence.json` as the output path to retain historical evidence. In the Unity editor, run `StudioM4AuthoringTrial.Start()` and inspect its `Status` plus `Artifacts/CharacterCreatorStudio/M4/ui-author-trial.json`. `StudioM4RendererChecks.Start()` measures actual Toolkit and TMP consumers across the English/Thai profile matrix. Missing runtime-font glyphs remain explicit blocked profile results.


M5 adds 36 shared Battle Lab assertions (446 total): cloned setup/definitions, real state/deck/defeat rules, enemy AI, idle practice targets, rank/ultimate availability, DOT snapshots and clocks, deterministic restart, setup overrides, independent RNG streams and reserve mapping. Use `Artifacts/CharacterCreatorStudio/M5/core-evidence.json` as the harness evidence path. `StudioM5LiveChecks.Start()` exercises the actual editor launch, 3D Play Mode, contact playback, FCT/audio, runtime inspector, restart, mode switch, AI/DOT and scene/account/source isolation across domain reloads. Start it from Edit Mode with no unrelated Play Mode session active. It uses volatile SessionState; it creates verification artifacts, not scenario assets.

M6 adds 20 shared immutable timeline checks (466 total). Use `Artifacts/CharacterCreatorStudio/M6/core-evidence.json` as the harness evidence path. In Edit Mode, `StudioM6Checks.Run()` verifies real-engine action recording, a synthetic authored rig/clip, backwards scrubbing, FX, frozen assets, reaction priority, final HP and preview disposal. It creates a clearly named `M6VisualVerification` fixture folder, not a roster character. Then `StudioM6LiveChecks.Start()` verifies the same driver in actual Battle Lab Play Mode, authored clip/FX/audio, pause, exact core damage/HP/RNG parity and scene restoration. Disable any optional actor profiles before running the M5 default-presentation regression.
