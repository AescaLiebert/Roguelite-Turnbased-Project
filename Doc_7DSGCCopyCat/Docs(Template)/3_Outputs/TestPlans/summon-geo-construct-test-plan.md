---
slug: summon-geo-construct
status: draft
source: manual
gdd_tags: [mechanics, architecture, actor-camera, elemental-reactions, platform-input]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Summon and Geo Construct validation plan

Backend update 2026-10-21: focused runtime checks, existing Studio/artifact regressions, targeted migration checks, and live Unity native-asset/core simulation passed. See `Artifacts/Summons/Backend/Implementation.md` for exact evidence and limitations. Visual/play-feel cases below are still unexecuted; the original planned matrix is retained for the next phase.

Date: 2026-10-21. Planned tests only; none of the runtime or art checks below has been executed for this new feature. Documentation inspection is not gameplay validation. Rules: [design](../Specs/summon-geo-construct-design-spec.md). Implementation map: [architecture](../Specs/summon-geo-construct-arch-plan.md).

## Core correctness

| Case | Setup / action | Required result |
| --- | --- | --- |
| Creation | Resolve a valid Summon operation | Exactly one entity and linked grey Buff marker; no extra fighter, hand card, action budget, or PG |
| Reciprocal links | Two definitions on one owner; invalid shared marker recipe; orphaned loaded state | Distinct valid marker links; reject ambiguous definitions and invalid snapshots without merging lifetimes |
| Fizzle | Kill/disable actor or fail effect conditions before Summon resolves | No half-created entity/marker; existing card fizzle policy preserved |
| Duration | Create Guoba/Oz with fixture duration N | At most N owner-side-end actions, including creation turn; action before final expiry |
| Several/no owner cards | Play several owner cards, then compare with a pass/controlled-owner turn | One eligible turn-end activation in each living-owner case |
| Opponent turn | End only opposing side's turn | No Guoba/Oz activation or lifetime consumption |
| Boundary creation | Create a summon from a turn-end callback | No same-boundary action or duration loss; eligible next owner boundary |
| Recast | Recast same owner/definition; retain a stale queued job ID | New instance replaces old; no duplicate action, old job or capacity; replacement is not break |
| Multiple owners | Both sides or different owners use the same definition | Independent IDs, lifetimes, actions, and capacity |
| Different definitions | Owner has Guoba and a separate non-absorbing construct | Both coexist and only explicitly capable entity intercepts |
| Removal protection | Generic/recipe-specific dispel, cleanse, steal, transfer, fuel consumption | Structural marker/entity remain; normal eligible statuses still behave normally |
| Keyword regression | Target has only the grey summon marker | Ordinary buff-count, Rupture, Amplify and fuel behavior follow structural exclusion |
| Owner defeat | Defeat by card, DOT, reflection, reaction, triggered attack | Entity becomes ineligible; committed defeat removes marker/entity once; no later queued action |
| Pending defeat | Owner has zero HP inside existing deferred defeat scope | No new entity action/protection; finalization order and lethal-save behavior preserved |
| DOT before action | Kill owner during turn-end DOT | No summon attack after death |
| Revival | Revive an owner after committed defeat | No previous summon restored; no battle-start passive replay |
| Reserve | Spawn while active, then use supported reserve/swap fixture | Draft persistence/clock rule holds; no opening spawn from reserve entry |
| Battle end | End encounter with jobs pending | Clear all entities; no next-encounter state or post-victory attacks |

## Absorption and damage accounting

Use fixed rolls and authored fixture packets; isolate allocation from random variance. Values below are arithmetic fixtures, not balance recommendations.

| Case | Input | Required result |
| --- | --- | --- |
| Partial capacity use | Pool 100, incoming 40 | Absorb 40, pool 60, no shield/HP loss |
| Exact depletion | Pool 100, incoming 100 | Absorb 100, remove entity/marker once, no spill |
| Confirmed overflow | Pool 100, incoming 140 | Absorb 100; remaining 40 reaches personal allocation without reroll/recalculation |
| Personal shield | Above overflow with personal shield 30 | Construct 100, shield 30, HP-directed remainder 10 before applicable HP policies |
| Personal bypass | Same packet with `BypassShield` | Construct still absorbs; overflow bypasses personal shield only |
| Construct exception | Explicit construct bypass fixture | Construct unchanged; ordinary personal allocation still follows its own policy |
| Avoidance / immunity | Evaded or immune packet | No capacity spent; no false absorption hit |
| All origins | Card/passive/DOT/reflection/triggered/reaction/output, including True/Additional | Same shared seam used; no family silently escapes default interception |
| HP cost | HP payment, direct HP setting, Max HP change | No absorption event/capacity loss |
| Multi-hit | Several hits deplete pool mid-action | Early hits consume live pool, later hits spill; no duplicated full pool per hit |
| AoE | Multiple protected recipients | Explicit target order; shared total absorbed never exceeds original pool |
| Multiple pools | Two protective constructs with distinct priorities | Each receives only previous remainder; stable tie-breaking and receipts |
| Formula preservation | Repeat fixed packet with and without pool | Immutable `FormulaDamage` and rolls unchanged; legacy `CalculatedDamage`/event `Amount` retain documented cap-aware semantics |
| HP caps / lethal save | Intercept damage against capped/fatal-save target | HP-directed cap and survival use remainder; no shield/HP/cap recomputation from original amount |
| Trigger accounting | Absorb all, then partially absorb a hit | HP-loss healing/retaliation use actual HP loss; absorption separately attributed |
| Element contact | Fully/partially intercept elemental packet | Existing barrier-contact reaction/application semantics preserved; no duplicate fuel or reaction |
| Removal cause | Break, expire, replace, owner death, battle end | Distinct typed causes; configured depletion reward fires only once for depletion |
| Numeric boundary | Large damage/capacity and tiny split amounts | No integer overflow, negative capacity, rounding duplication, or lost remainder |

## Determinism, content, and presentation

- Clone, save/load, public snapshot plus events, and uninterrupted execution end with identical entity/status/capacity state and event ordering.
- Opening replay through `BattlePlaybackState.BeforeOpeningDeal` starts before Jade Screen exists, then creates its marker/entity exactly once at the opening fact; no early reveal or duplicate pool.
- AI observed-state construction includes public entities and independent simulation state; drafting/forecasting does not mutate live capacity, RNG, or private information.
- Reject invalid entity references, capacity formulas, unsupported trigger/origin combinations, recursive actions, and version mismatches. Old snapshots retain legacy behavior and do not invent entities.
- Migrate only selected kit content. Confirm old Guoba/Oz turn-end attacks are removed, Ningguang's personal barrier is replaced, and separate Evasion/other unconverted effects remain. Inventory source riders and authored overrides explicitly.
- Studio author/export/import round-trip preserves operation, category, capability, origin, lifetime, and presentation key.
- Spawn/attack/break/expiry render from immutable facts; reverse-team facing and clip yaw cannot alter protection or targeting.
- Skip during reveal/contact/break, fast playback, missing clip/prefab, replay seeking, reconnect, and scene exit leave the same logical outcome with no orphan objects/audio/VFX.
- Grey status inspection identifies owner, category, remaining owner-turns, next trigger, non-dispellability, and shared capacity. Entity views do not capture enemy-selection touches.
- Regenerate/import the Blender sample and then the three assets; preserve GUIDs, bind pose, textures, sockets, clip names and loop seams. See the art plan for camera/material and performance gates.

## Playtest scripts

| Test | Script | Pass/fail and next adjustment |
| --- | --- | --- |
| New player | Summon Guoba, inspect it, queue several Xiangling cards, end turn | Player predicts one activation and final expiry. If not, revise turn wording/next-trigger display before tuning damage |
| Readability | Observer watches summon action, full absorb, overflow, depletion, natural expiry | Observer identifies owner and distinguishes every removal cause. If not, revise labels/contact VFX/sound |
| Stress | Duplicate inputs, recast, Skip at every visual phase, opposing summons, low-frame-rate playback | No duplicate activation or stuck queue; final state matches baseline. Fix scheduler/cleanup before adding polish |
| Skill | Compare summoning before a safe turn versus imminent owner defeat; compare enemy focus fire versus team AoE | Player can explain a useful timing/targeting choice. If every line is equivalent, adjust encounter pressure before coefficients |
| Abuse | Recast at expiry, kill/revive owner, trigger creation inside turn end, repeatedly reconnect | No free extra actions, pool duplication, duration freezing, or repeated opening rewards |
| Screen clarity | Preview shared pool and observe a multi-target breaking attack | Spill to later targets matches the documented order and displayed pool; if confusing, improve ordered receipts/capacity feedback before considering another allocation design |
| Art feel | View all entities from planning, attack, and opposing cameras with sound | Reference silhouettes read, loops do not pop, screen does not hide critical action. Adjust visible detail/clip contact/frame opacity first |

Keep tests focused on observable contracts, not implementation-shaped private method assertions. Reuse existing combat/elemental/status/playback fixtures where they exercise the changed seam. Run full relevant regression once after integration; repeat only when changes or unresolved failures justify it.

## Evidence to record after implementation

Record code/content revision, rules/schema version, fixture inputs, assertion outcomes, replay hashes, target hardware, measured frame/memory delta, import manifests, camera captures, and human playtest observations. Mark runtime/model acceptance only from that evidence, not from this planning document.
