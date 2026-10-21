# Skill effect ownership implementation

21 October 2026 · Implements [the redesign](SkillEffectOwnershipRedesign.md)

## Delivered behavior

- Skill and ultimate sequences execute damage follow-ups as individual packets. Their canonical `DamageRecipe` supports target/owner stat scaling, damage family and damage policy. A bonus packet does not replay the primary attack's hit choreography.
- Sequence condition contexts carry actual card identity, category, rank and ultimate tier. Primary crit/block facts are retained per recipient, independently of later bonus packets. Nested All/Any/Not conditions use the existing evaluator.
- Skill and ultimate pages expose the shared condition controls. Granted statuses expose WHEN / WHO / IF / DO, sharing the existing event filters, command editor and packet controls. Status reactions live in the selected rank/tier's status recipe.
- Passive flat-node authoring and native skill/status authoring retain their existing storage authorities. Shared controls adapt native conditions directly into the versioned SO JSON payload; they do not create a second set of generated passive rules. Unity does not serialize the optional packet or recursive condition graph inline.
- Status snapshots retain granting ability/rank/tier. Successful RefreshDuration application replaces a skill-granted reactive payload with the newly applied payload, retaining existing duration-refresh behavior. Other stacking policies retain their existing semantics.
- Immediate/deferred reaction events preserve structured origin and granting metadata, including through cloning and public projections. Skill-granted responses do not announce `Active Unique` or consume the real passive's presentation receipt. Internal BeforeDefeat/DamageCapReached notification markers also do not announce passive activation.
- Granted incoming-attack responses appear in active-status inspector descriptions; their applied-status badges appear with the skill. More complex programs keep their authored skill wording rather than receiving an inaccurate generated tooltip.
- Skill condition execution/skips enter the existing Battle Lab trace when trace capture is enabled. Validation rejects unresolved hit predicates and predicates lacking their required event context.

## Native migrations

| Character | Migration |
| --- | --- |
| Taki | Removed the Skill 2-specific damage reaction from PassiveSO. Every Skill 2 rank now owns its target-Max-HP Additional packet. Innate modifiers remain passive-owned. |
| Billy97 | Removed guard-burn from PassiveSO; each Skill 2 rank's Burning Guard owns it. His low-health Crit reaction remains passive-owned. |
| Benimaru99 | Removed the Women-attacker Defense-reduction reaction from PassiveSO. The ranks granting `status.buff.apply-debuff` own their rank-specific reaction and potency. His innate aura remains passive-owned. |

Identity, stats, traits and existing rank wording were compared before/after and preserved. Existing asset GUIDs are retained through the native five-asset save path. Before/after definitions and original native asset backups are in `Artifacts/SkillEffectOwnership`.

Intentional behavior corrections accompany ownership:

- Billy cannot Ignite attackers without the granting guard.
- Benimaru's stale native target query no longer overrides TriggerActor and redirects Defense reduction to himself. Migration clears that query and retains the intended attacker recipient.
- Migrated defensive responses require incoming card attacks; additional/status damage does not create another card attack.
- Additional-damage immunity is enforced by the shared packet execution path.
- Taki's bonus belongs only to Skill 2, independent of another card sharing its category/rank.

The pre-release roster generator applies the same migration before publication. Taki's generator now authors the packet in Skill 2 and publishes that skill through its canonical JSON adapter. Benimaru's legacy draft-only importer does not generate the old runtime reaction; Studio-owned native data also retains its existing protection against legacy replacement.

The explicit **Fighting Allstar → Content → Migrate Skill Effect Ownership** menu remains available and is idempotent. Battle loading never performs a hidden migration.

## Verification

| Verification | Result |
| --- | --- |
| Shared core build | Pass, no compiler warnings/errors on the final build |
| Unity Editor compilation | Pass, no C# errors; existing project warnings remain |
| Focused skill ownership suite | 154 assertions pass |
| Existing training regression | 2,383 assertions pass |
| Existing elemental/Genshin regression | 873 assertions pass |
| Existing Character Creator Studio suite | Pass: conditions, actions, lifecycle, variants, descriptions, hashes, native ownership and bridge regressions |
| Actual attached editor controls and persistence | Ten checks pass on a disposable character: status-rule UI, widget callbacks, grouped-condition save, ownership, reopen/repeat-save, Undo/Redo, failed-save rollback, original asset preservation |
| Native migration | All three kits saved and reopened; repeated saves preserve their definitions without duplicate rules |
| Visual inspection | Actual migrated Billy Skill 2 editor captured and inspected; skill preview includes Taunt and Ignite |

Focused coverage includes all three native Skill 2 ranks, unrelated Taki skill/ultimate exclusion, multi-hit and area recipients, target-Max-HP scaling, shield contact, evade, overkill, Additional immunity, Women/non-Women attackers, status removal/natural expiry, refresh from another rank, ultimate-tier snapshots, condition tracing and invalid context rejection.

Run the focused suite after creating the existing combat harness with `Tests/CombatPresentation/Run.ps1`:

```powershell
dotnet run --project Temp/BattlePlaybackChecks/Checks.csproj --configuration Release -- --skill-effects
```

The native migration exports the three fixtures used by this suite. Editor checks are callable as `SkillEffectAuthoringChecks.Run()` from the Unity editor tool. They create and remove a disposable native character, preserving the original five Billy assets.

Evidence: [native authoring checks](../../Artifacts/SkillEffectOwnership/native-authoring-evidence.json), [editor capture](../../Artifacts/SkillEffectOwnership/skill-status-editor.png).

## Scope

No balance values were introduced. Existing source values and descriptions were retained. Complex composed/else programs remain available in the existing advanced controls; the new normal status-authoring path emphasizes ordered supported commands. No new runtime rank-inheritance system, additional character migration or scene change is included.

## Correction: individual stance-child ownership

Billy's guard-burn response now belongs to the `status.stance.apply` stance child in every rank. The parent Burning Guard recipe has no guard-burn reaction. Stance children now preserve their own reactions through cloning, JSON persistence, validation and runtime conversion. Removing just the child ends Ignite responses while its parent stance can remain active.

The skill editor exposes separate child-status editors and binds each reaction panel to its actual recipe. The misleading rank-ownership header description is removed. The owning status appears with its icon in the reaction's WHILE condition; status-reference predicates also use the icon-backed status picker. Existing wording is retained.

Verification after this correction: **166 focused assertions**, **13 native editor/persistence checks**, the full Studio suite and **2,383 training assertions** pass. Unity compilation has no C# errors. [Corrected editor capture](../../Artifacts/SkillEffectOwnership/ChildOwnership/child-reaction-editor.png).

The broader `--neutral-status` runner reaches an unrelated existing assertion, `Rank merge must update gameplay category and target scope`, which expects a per-rank target override while the current CardRules uses the skill's shared target. This correction does not modify CardRules or rank merging.

## Description icons and node previews

Apply Status main/sequence nodes now show their status sprite, alongside the existing stance-child, condition and reaction-action previews. The node respects status polarity and uses the runtime sprite fallback when no exact visual exists.

Skill/ultimate description badges come from direct, unconditional status applications and their immediately granted stance children. Statuses produced by reactions or conditional operations are excluded. A card's `statusVisuals` list supplies available presentation assets; listing an icon there no longer creates a description badge on its own.

Billy therefore displays his directly granted stance effects without a separate Ignite badge. His authored sentence explaining conditional Ignite, and Ignite's icon inside the reaction action editor, remain intact.

Verified six presentation cases (including direct versus conditional Ignite and stale icon-list references), the actual Apply Status sequence-node sprite, a live editor capture and error-free Unity compilation. Evidence: [presentation checks](../../Artifacts/SkillEffectOwnership/DescriptionBadges/verification.json), [preview](../../Artifacts/SkillEffectOwnership/DescriptionBadges/direct-status-preview.png). No gameplay or character assets were changed for this presentation correction.
