# Character Creator Studio — M3 implementation report

20 October 2026 · Unity 6000.3.4f1 · Reusable combat rules and their Studio authoring contract.

**Audit qualification:** [The subsequent A–F audit](M3Audit.md) confirms engineering coverage in every package but identifies incomplete designer workflows and two reproduced validation defects. This report records implementation and its existing evidence; it does not establish a finished Studio or closure of every M3 authoring acceptance gate.

**Closure update:** [M3 closure](M3ClosureReport.md) records the subsequent fixes, schema3 shared libraries, redesigned rule workspace and UI-driven acceptance evidence. The schema2/333-assertion evidence below remains the original delivery record.

M3 extends the existing standalone UXML Studio with executable action composition, lifecycle controls, resource and status operations, target queries, interception policies, and ability variants. Open **Fighting Allstar → Character Creator Studio**. Existing production character ownership remains unchanged; compilation starts from a frozen import and test activation remains editor-only.

**Mian is a case study only.** The fixtures exercise reusable protection, layered absorption, threshold, and fatal-save mechanics with synthetic definitions. This milestone adds no playable Mian kit, roster registration, character-specific runtime branch, or production content switch.

## Implemented engineering scope

| Package | Implementation | Main shared-code acceptance |
| --- | --- | --- |
| M3-A Composition | Flat stable-ID action and module tables; ordered sequences, guarded branches, success/failure children, bounded target loops, reusable module expansion; parameter and event-value bindings | Nested filters, ordered resource guard and rewards, compiled modules, missing-binding rejection, cycle/depth/expansion validation |
| M3-B Execution controls | Versioned execution, complete supported lifecycle dispatch, stable phase/priority order, activation receipts, owner-turn cooldowns, explicit reaction origin, synchronous defeat interception, shared execution budget | Multi-hit/root/target scopes, deterministic ordering, readable skips, feedback-chain rejection without partial authoritative results |
| M3-C Resources and status lifecycle | Saturating resource changes, atomic spend and transfer, exact or partial layer consumption, frozen removal snapshots, removal reasons and source ownership; status break rules | Independent protection and layer-burst fixtures; insufficient costs and over-cap transfers leave balances intact; removal callbacks retain the removed recipe and layers |
| M3-D Queries and history | Explicit actor/owner/event-target anchors; filters, sort, sample, exclude, store/reuse bindings, active-slot adjacency, committed-card turn ledger, resource threshold entry/exit | Deterministic random-other selection, lowest-layer selection, frozen target reuse, reserve exclusion, owner-turn history and once-only threshold crossing |
| M3-E Interception | Incoming HP-loss caps, fatal-save policies, gauge-loss guards, explicit True profiles, per-stage damage bypasses and declared precedence | Real card, Additional, DOT, reflection, triggered, Overload, Swirl and Superconduct contacts; atomic fatal saves and bypass cases |
| M3-F Variants and upgrades | Skill variant readiness/unlock/relock, resource costs, stable existing card identity, independent card rank/fighter level/tier, typed value overrides | Base/alternate selection, maximum-level and rank-specific overrides, cost/relock on use, immutable scenario configuration and deterministic rerun |

The Rules workspace contains action/module, policy, and ability fields with bound parameter selectors and readable summaries. The same parameter IDs remain available to the description compiler and existing Toolkit/TMP previews. Trace entries retain rule/action identities, conditions, observed values, event ranges and skip reasons. The scenario configuration captures level, rank and tier alongside the seed.

The common authoring catalog is the allow-list used by pickers and compiler validation. General composed actions expose Damage, ApplyStatus, RemoveStatus, Cleanse, Heal, ChangePowerGauge and ModifyCardRank. Runtime declarations alone do not make an operation a Studio capability. In particular, synchronous `BeforeDamage` local modifiers are tested in the shared runtime but are not offered as a general side-effect action trigger.

## Execution and transaction semantics

Existing imported reactions retain **ExecutionVersion 0**. Their shipped frequency/origin behavior is preserved. A Studio rule with a composed action root compiles to **ExecutionVersion 1**. Converting the original opening-gauge slice is explicit; schema migration alone does not convert its gameplay semantics.

The newly connected production action, status, healing and reserve lifecycle hooks carry `VersionedLifecycleOnly`: they activate opted-in version-one rules and leave dormant version-zero declarations inactive, with an explicit compatibility skip trace. Explicit manual `EmitFact` calls retain their existing legacy-compatible dispatch behavior. This distinction prevents adding a production hook from silently activating previously inert imported content.

Version-one reactions execute by phase (`Before`, `Normal`, `After`), descending priority, then stable owner/source/rule ordering. Nested ordinary facts queue deterministically. `BeforeDefeat` uses a synchronous interception path so a save completes before defeat even when lethal damage was caused while another reaction queue was draining. Both paths share the same receipts and budget.

Eligibility includes presence, mode, tier, event filters, relation, origin, cooldown and activation scope. Once an eligible rule has a selected branch, it reserves its activation receipt **before entering guarded child actions or paying their costs**. An insufficient guarded cost therefore does not grant a reward and does not allow unlimited retries through the same activation scope. Failed top-level eligibility, or a condition with no selected branch, does not reserve an activation.

Receipts support Battle, OwnerTurn, RootAction, RootActionTarget and Event scopes with an authored maximum count. Saved snapshots and clones retain them. Owner-turn cooldowns use the owner's `OwnerTurnsCompleted` counter: a cooldown of two set at counter zero is ineligible at one and eligible at two. Card moves, merges and fizzles do not count as committed card use.

One resolution lease shares a limit of **256 passive facts and 1024 rule steps** across nested work; a nested dispatch cannot reset it. The production plan resolver holds the lease over its working snapshot. A missing required value binding, exceeded deterministic budget or other bounded execution failure returns a rejected resolution with `next == null`. It does not activate the partially mutated working snapshot or change the input snapshot. Low-level callers that deliberately use the execution helpers directly must discard their own working snapshot on `PassiveExecutionException`.

The authored graph is validated before expansion. Current limits include 64 levels of action nesting, 1024 expanded action/condition nodes, target loops of at most 32, and bounded authoring tables. Dangling references, recursive modules, invalid units and invalid substituted values block compilation.

## Resources, layers and targeting

Resource balances saturate at zero and their declared cap. A spend preflights the full amount. A transfer preflights the source balance and destination capacity, commits both balances, then notifies observers. Insufficient operations commit neither partial cost nor reward.

Layer consumption can require the full requested count or consume the available subset. It selects stable oldest matching instances, can restrict matching to the effect owner's source ID, and returns frozen snapshots containing recipe, source, removed layers, remaining layers and reason. Explicit reasons include Dispelled, Cleansed, Absorbed, Expired, Replaced, Consumed, Broken and OwnerDefeated. A fully removed status can execute its own removal reaction using the frozen recipe; another fighter's same-named status is not silently adopted as owned content.

Status lifecycle facts cover utility application/removal, expiry, absorption, periodic consumption, freeze/barrier breakage and authored break rules. The damage paths report Broken snapshots once; `ConsumeOnTrigger` periodic statuses report Consumed snapshots. Orphaned dependent statuses and their remaining shield contribution are cleaned up with their parent.

Target query scopes use an explicit **Anchor**. Actor preserves the existing scope convention; Owner selects relative to the passive owner even when an enemy caused the event; EventTarget selects relative to the event recipient. Relation filters are owner-relative. Logical formation slots determine adjacency, independently of model position. Reserves and unavailable fighters are filtered according to the declared query.

Queries filter before sorting/sampling and apply a bounded limit. Equal sort values have stable side/formation/team/ID tie breaks. Random selection samples without replacement using authoritative combat RNG and persists its updated state/draw count. Nonrandom selection and reuse consume no RNG. Stored bindings contain IDs. Reuse retains the selected order, skips unavailable recipients without replacement, and rejects duplicate application to the same stored ID; exclusion bindings filter those frozen IDs.

Turn history reads committed `CardPlayed` events between owner-turn boundaries. During the enemy turn, PreviousOwnerTurn means the most recently completed owner turn; at the next owner turn it still refers to that completed turn. Threshold entry/exit uses the resource event's previous/current values, rather than repeatedly firing while a resource remains above or below a threshold.

## Damage and interception rules

An incoming cap limits **HP-directed loss per committed contact after shield absorption**, as a fraction of the target's effective maximum HP. It is not a whole-card aggregate cap. Contacting the cap emits the cap fact and trace. The strictest eligible cap wins; earlier survival protection is not reversed by applying a cap.

The same interception path is installed for direct card damage, generated passive damage, DOT, reflection, natural-expiry/triggered attacks and terminal elemental reaction contacts. Legacy Additional/DOT bypass of the global formula cap does not implicitly bypass an authored incoming HP-loss policy.

Fatal-save policies run before defeat side effects. They preflight positive permitted recovery, removal of recovery prevention when cleanse is authored, and the entire resource cost. An unremovable recovery block or insufficient resource fails without a partial cost. A successful save commits the cleanse, restored HP and cost before dependent observers run, emits ordinary removal/heal facts, and clears a resolved Destructive execution so it cannot defeat the saved fighter afterward.

Damage profiles declare bypass of defense, resistance, generic reduction, final reduction, flat reduction, shields, damage caps and fatal saves. A bypassable policy yields to the corresponding damage-profile flag; an explicitly unbypassable policy still applies. `BypassFatalSave` suppresses ordinary generic `BeforeDefeat` save callbacks, while an unbypassable authored fatal policy remains eligible. Policies use descending priority and stable-ID tie breaks. Gauge guards check conditions before any negative gauge mutation and do not block positive gain or ordinary ultimate spending.

An absent explicit True profile preserves legacy True damage semantics, including its existing 30% bonus. An explicit True profile uses its authored bonus (20% by default) and independently selected bypass stages. Explicit profiles can therefore model a True attack that bypasses defense/shields while still declaring precisely how final reduction or incoming caps behave. The compiler and runtime clone these payloads; editing a preview cannot mutate the source profile.

## Ability and document identity

Variant readiness is authoritative: unlock state, level, tier, required resources and available rank effects determine selection. The highest priority ready variant wins with a stable-ID tie break. A variant changes the effect of an existing skill card; it does not replace that card's stable ID or silently change its rank. Cost and optional relock commit at use. Future hand readiness refreshes after those changes.

Card rank, fighter level and constellation tier are independent axes. Base-skill overrides apply first, followed by selected-variant overrides. Overrides address a known effect/field, have validated level/rank/tier ranges, and reject overlapping definitions for the same field and range. Supported fields are coefficient, magnitude, heal coefficient, gauge amount and applied status stack count. Scenario snapshots freeze their selected configuration for rerun and comparison.

New Studio documents use **schema 2**. The schema-1 migration clones the original and adds the M3 tables; existing opening-gauge rules retain version-zero execution and matching gameplay/text identity. Migration does not rewrite the saved source until an explicit edit/save. Unknown future schemas are rejected while retaining the original source and last compiled/test bundle. Duplication remaps local action/module/parameter/variant/override references and preserves external source identity.

## Verification evidence

The shared harness currently passes **333 assertions**:

| Suite | Assertions | Evidence |
| --- | ---: | --- |
| Existing Studio core | 71 | Frozen real Kaeya import, parity, hashing, persistence contracts and immutable snapshots |
| Existing description contracts | 51 | Bindings, snippets, locale/source preservation, validation and source maps |
| M3 variants | 19 | [M3VariantChecks.cs](../../Tests/CharacterCreatorStudio/M3VariantChecks.cs) |
| M3 queries/policies | 80 | [M3PolicyChecks.cs](../../Tests/CharacterCreatorStudio/M3PolicyChecks.cs) |
| M3 runtime execution | 56 | [M3ExecutionChecks.cs](../../Tests/CharacterCreatorStudio/M3ExecutionChecks.cs) |
| M3 Studio compiler | 47 | [M3StudioChecks.cs](../../Tests/CharacterCreatorStudio/M3StudioChecks.cs) |
| Immutable event/operation context | 9 | [M3ContextChecks.cs](../../Tests/CharacterCreatorStudio/M3ContextChecks.cs) |

The consolidated output is [M3 core evidence](../../Artifacts/CharacterCreatorStudio/M3/core-evidence.json), generated by the [Studio harness](../../Tests/CharacterCreatorStudio/Program.cs). It records the frozen-source identity, suite counts, parity hashes and production traces. Regenerate it after any implementation change:

```powershell
$studioChecksOutput = Join-Path (Get-Location) 'Temp/CharacterCreatorStudioChecks'
dotnet build Tests/CharacterCreatorStudio/CharacterCreatorStudioChecks.csproj --configuration Release -p:OutputPath="$studioChecksOutput" -p:AppendTargetFrameworkToOutputPath=false
dotnet "$studioChecksOutput/CharacterCreatorStudioChecks.dll" Artifacts/CharacterCreatorStudio/M0/KaeyaLegacyDefinition.json Artifacts/CharacterCreatorStudio/M3/core-evidence.json
```

The complete current check index is [final verification evidence](../../Artifacts/CharacterCreatorStudio/M3/final-verification.json).

**All 15 native Editor checks pass.** [Editor evidence](../../Artifacts/CharacterCreatorStudio/M3/editor-evidence.json) records real save/reimport, Undo, module/reference duplication, layer snapshots, bound costs, exact replay, reversible test activation, production-source nonmutation and scene/Play Mode isolation. [Designer workflow evidence](../../Artifacts/CharacterCreatorStudio/M3/designer-workflow.json) records a cost changed through the actual UI: remaining charges and wording update together; saving, reopening and rerunning reproduce the exact state. [Preview checks](../../Artifacts/CharacterCreatorStudio/M3/preview-checks.json) pass all five actor/text/font-lifecycle tests. The existing elemental/Genshin regression subset also passes 873 assertions.

`TriggerTarget` and `ConditionSubject.EventTarget` refer to the immutable event target. `OperationTarget` refers to the selected member of a target loop. Event-target anchors, adjacency and exclusions continue to use the original event target while the loop advances. The nine dedicated context assertions include a real nested rule that separately applies original-target and operation-target effects.

Native window captures show the [rule workspace](../../Artifacts/CharacterCreatorStudio/M3/studio-rule.png), [nested guarded actions](../../Artifacts/CharacterCreatorStudio/M3/studio-actions.png), [scenario and actor playback](../../Artifacts/CharacterCreatorStudio/M3/studio-scenario.png), and [bound description preview](../../Artifacts/CharacterCreatorStudio/M3/studio-description.png). The [UI workflow check](../../Artifacts/CharacterCreatorStudio/M3/ui-workflow.json) confirms the description editor opens without an opening-gauge rule and leaves the saved draft untouched. Following a trace expands the relevant ancestors and positions the action header in view, including when its full body exceeds the viewport.

## Boundaries

M3 supplies reusable mechanics and their authoring controls. The complete wording/keyword workflow remains M4; the general battlefield debugger remains M5; full rigged actor animation/timeline tooling remains M6; production migration/activation remains a later verified rollout. The existing zh-cn Editor standard and M2 renderer profiles remain in use. M3 does not certify every future prose/locale/variant combination or replace the game's production font assets.

The earlier [M0–M2 report](ImplementationReport.md) remains historical evidence for that delivery. This report records M3's semantics and current verification status; it does not turn the case-study kit into a production implementation commitment.
