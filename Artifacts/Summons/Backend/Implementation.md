# Summon / Geo Construct backend implementation

Verified on 2026-10-21. User approved the plans and requested the backend plus source-described Fischl, Xiangling, and Ningguang integration. Models, animation, VFX, camera treatment, and new entity visual presentation remain the next phase.

Post-implementation self-audits: eight gaps fixed and focused coverage expanded to 291 assertions. See [Audit.md](Audit.md) for the reproductions, changes, and verification. Counts below describe the initial implementation pass unless noted there.

## Implemented behavior

- Generic typed Summon effect from cards/passives; separate `Summon` and `GeoConstruct` categories. Entities do not become roster fighters or grant cards/action budget.
- Reciprocal owner marker/entity identity, grey structural status protection, one lifetime clock, deterministic recast replacement, owner defeat/expiry/battle-end cleanup, and saved-state link validation.
- Owner-team-end actions before lifetime expiry; live owner/target checks; explicit both-side activation for Pyronado; bounded Oz follow-ups on Electro-involving allied reactions.
- Optional shared team absorption before personal shields and HP caps/survival. Overflow reaches the original ally. Card, passive, triggered, DOT, reflection, and elemental output paths share the same allocation seam. Personal-shield bypass and construct bypass remain separate.
- Immutable pre-allocation formula damage and construct receipts, public entity/status views, clone/JSON persistence, opening and subsequent replay facts, and detached AI state.
- Studio operation allowlisting/validation and versioned inline entity definitions. Unity's native serializer skips the complex Summon properties while existing authoritative Studio JSON persists them.

## Character integration

| Character | Backend |
| --- | --- |
| Fischl | Oz from Skill 2 and Ultimate; owner-end 250% Electro AoE; 80% Electro allied-reaction follow-up; separate Evasion; Mondstadt Attack aura; source Skill 1/Ultimate damage and additional Max HP clause |
| Xiangling | Guoba from Skill 2; owner-end 300% Pyro AoE, Ignite stacks and Pyro RES reduction; owner DOT/protection aura while Guoba exists; separate team Attack buff; source Pyro passive; Pyronado from Ultimate, acting on both side ends |
| Ningguang | Opening Jade Screen, shared capacity snapshotted at 20% effective owner Max HP; overflow, depletion-only Geo ally reward; Liyue aura; Geo-only Star Jade gains, per-stack Geo bonus, source Skill 2 scaling/consumption and Ultimate pre-damage grants |

Original source: `ArtDirection/Character Database/Pre-Release-Character.csv`. The migration changes only the five native asset package files per character, plus the roster generator hook. Source descriptions, artwork references, GUIDs, unrelated characters, and saved authoring graphs are preserved. The active `studioDefinitionJson` payloads replace the old neutral marker/self-barrier approximations.

Retained interpretations are explicit in metadata: owner turn means owner-side turn end, including the casting turn; Oz uses the approved AoE target; Fischl's unqualified additional Max HP clause retains owner Max HP and no specified element; maximum Ultimate values map to C5; DOT-scaled clauses multiply the explicit DOT bonus. Ningguang Skill 2 removes all Star Jade after queued action/passive effects, including the layer gained by its own Geo hit. Pyronado's two-owner-end lifetime includes the creation turn, giving three side-end actions when created during the owner's action phase and uninterrupted; C5 extends the owner-end lifetime.

## Verification

| Check | Result |
| --- | --- |
| Official Shared Core/Contracts Release build | Passed, zero warnings/errors |
| Focused summon harness | Passed 229 assertions |
| Saved three-character packages | Parsed, Studio compiled, and executed in focused harness |
| Targeted migration repeatability | `--check`: 0 of 15 targeted files differ |
| Existing Studio regression harness | Passed all reported suites, including composition, lifecycle, interception, snapshots, authoring, Battle Lab, and timeline checks |
| Existing artifact regression harness | Passed 35,122 assertions |
| Live Unity asset loading | Passed through `CharacterObjectCatalogBuilder.CreateDefinition` for all three native assets |
| Live Unity shared-core battle | Both sides created opening screens; player Oz/Guoba and reaction follow-ups resolved; opposing screen absorbed 1,584 damage and depleted; 135 facts; next phase Planning |
| Live Unity snapshot round trip | Passed; three surviving entities retained |
| Unity console after final import | No errors; no serialization-depth warning |

Focused checks cover real reaction/reflection/DOT commits, caps plus construct/personal shields, pending owner defeat, grey marker removal, exact/fractional overflow, multiple pools, independent entity buffs, all skill ranks/Ultimate tiers, opening replay, and public/AI isolation. Existing regression checks protect the broader shared formula and authoring paths. These are backend checks, not a visual/play-feel sign-off.

The live Editor test read the native assets and simulated an isolated in-memory battle; it did not enter Play mode, modify a scene, or add entity art. JSON result: [UnityBattle.json](UnityBattle.json).

## Reproduce

See `Tests/Summons/README.md` for focused checks and `Tools/Summons/README.md` for targeted migration. Build test harnesses into `Temp/` while Unity is open to avoid concurrent plugin writes; rebuild the Shared project into its normal plugin output when deploying backend changes locally.

Next phase: bind the existing presentation keys and entity facts to the three models, idle/action clips, placement profiles, grey icons/capacity feedback, audio, and Skip/replay-safe view cleanup from the approved art plan.
