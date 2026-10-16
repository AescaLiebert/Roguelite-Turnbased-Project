# Elemental system A–C: implementation evidence

Date: 2026-10-15. Scope: authorized slices A–C, implemented in shared authority and integrated with Unity assemblies. No new character kit, scene, or acquisition pool was published.

## Behavior delivered

- Starting roster eligibility is frozen per side, including SUB; no extra Gnosis passive required. Death/entry cannot change the flag.
- Explicit element/application metadata is independent of attribute, damage family and physical hit reaction. Unauthored legacy cards remain neutral.
- Directional Melt/Vaporize/reverse variants amplify their designated contact. Authored priority selects amplification before Overload, independently of status insertion order.
- Overload uses 150% of the trigger's contact ATK as a starting coefficient, deals Pyro to the target/adjacent occupied formation slots, cannot crit/block, bypasses DEF and respects shields and its configured cap.
- All mapped eligible participating stacks/aliases/owners are consumed; unrelated statuses survive. Status-dependent delivery checks acceptance on a clone and caches its single probability/critical outcome. A spent rider is not reapplied after damage; a failed optional rider cannot block a direct elemental hit.
- Root recipient formulas settle before consumption/child damage. Overlapping Overload receives a shared per-victim/action damage limit while valid local reactions still spend their fuel. Secondary deaths join the established action boundary.
- Reaction damage is terminal and excluded from normal card DOT snapshots, keywords, ordinary lifesteal and reflect. DOT ticks remain terminal. Generic reflection is distinct from the future authored Waltz attack.
- Versioned rule definitions, exact element modifier stages, deep clones, safe public event metadata, old-snapshot neutral defaults and incompatible-schema rejection are covered by checks.

## Slice A content inventory

| Entry | Status |
| --- | --- |
| Existing `status.debuff.ignite` | Registered Pyro fuel; ordinary stat vulnerability, not a new HP tick. |
| Existing `status.debuff.shock` | Registered Electro fuel; original 36% periodic coefficient retained. |
| Internal Flame / Lightning aliases | Not guessed; future mapping requires an actual validated recipe. |
| Wet / Nature / Frost Bite | Missing complete production recipes remain excluded; explicit isolated Cryo/Hydro fixture mappings test the core. |
| Poison / Bleed / Conductive / Bunny marker | Not automatically elemental fuel; generic DOT tags are insufficient. |
| Swirl / Crystallize / other reactions | Not implemented by A–C; unsupported reaction definitions fail validation. |
| Five supplied fighters | Design/stat drafts remain separate from runtime-ready assets. |

Default rules version: `elemental-ac-v1`; elemental schema: 1. Existing damage-family/event enum meanings are preserved by appending new values. The rules can be passed explicitly to `BattleEngine.Create` and `RunBattleBridge.CreateLocalBattle`; the SO catalog default exports A–C rules and the catalog-driven combat entry passes them through. Existing route/training entry points use the same A–C defaults unless explicitly supplied rules; custom frozen route-content plumbing is later integration work.

## Formula policy

Normal elemental packet: existing exact Normal calculation → source DOT-dealt bridge once → source element bonus → signed target element resistance → amplification → final floor/cap → existing visual-hit partition → shield/HP allocation. No intermediate resolved damage is fed back as a second damage basis. Proc preflight calculations are pure probes; damage/state commit occurs once.

Element resistance clamps to −100%…100% as an explicit starting bound. Normal packets retain Normal cap/crit/block/attribute/DEF/pierce behavior. DOT-received bonuses do not cross into Normal. Separate reaction packets use their own family modifiers and element policy, not Normal or DOT bridge flags. Final balance remains untested.

The bridge reads explicit DOT-family dealt modifiers only. Global artifact damage bonuses stay in each packet's ordinary policy and apply once, including the new reaction family; they cannot cross through the bridge again. Regression checks cover this interaction and ensure artifact lifesteal does not attach to terminal reaction damage.

## Verification performed

| Check | Result |
| --- | --- |
| `Tests/CombatPresentation/Run.ps1 -ElementalOnly` | PASS: 264 assertions covering directions, both sides, eligibility, aliases, acceptance, immunity/evasion/crit/proc, contact snapshots, overlap, empty slots, terminal chains/reflect, shield/cap/DEF policy, artifact modifier isolation, secondary deaths, replay/retry, public metadata, clone/version compatibility and validation. |
| Existing `--neutral-status` regression group | PASS: 101 status, stance, counter, healing, immunity and attribute assertions. |
| Existing `--dungeon-enemies` regression group | PASS: 9879 dungeon, battle, progression, save, entropy and action-passive assertions. |
| Existing `--additional-damage` regression group | PASS: physical hit reaction/replay and Additional damage overkill/ultimate exclusion checks. |
| Shared core build and server application Release build | PASS, no warnings/errors. Shared assemblies rebuilt through existing project output configuration. |
| Connected Unity 6000.3.4f1 editor | Refresh/import and compilation completed; editor ready; error console returned zero entries after compilation. |
| Scoped whitespace and preservation checks | PASS; pre-existing CardEffectSystem/CharacterPassiveRuntime edits byte-identical to the starting copies; catalog-builder diff against starting workspace contains only the rule-set default. |
| Full broad harness | Stops at pre-existing `First-hit status ran late` in `MultiHitChecks.cs:145`; isolated pre-change source baseline reproduces the same assertion. Not reported as an A–C pass. |

The baseline was isolated under ignored `Temp/ElementalBaselineRegression`, using the original edited BattleEngine saved before this implementation and pre-elemental versions of otherwise unchanged source files. No shared checkout reset was used to obtain this evidence. The issue was left untouched.

## Limits and next slice

No visual/Android/player-feel pass is claimed. Swirl (D), forecasts/feedback (E), full reactions/character effects (F), AI combo scoring (G) and integrated milestone (H) remain planned. Public reaction metadata exists; a reaction forecast does not. The first fully visible Swirl→Overload milestone still needs D/E.

Design truth and follow-up: [GDD](../../Doc_7DSGCCopyCat/Docs(Template)/1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions), [implementation plan](../../Doc_7DSGCCopyCat/Docs(Template)/3_Outputs/Specs/genshin-element-system-arch-plan.md), [test plan](../../Doc_7DSGCCopyCat/Docs(Template)/3_Outputs/TestPlans/genshin-element-system-test-plan.md).
