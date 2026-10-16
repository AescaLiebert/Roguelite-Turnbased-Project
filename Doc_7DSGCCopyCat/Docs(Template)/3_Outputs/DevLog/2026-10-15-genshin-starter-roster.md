---
slug: genshin-starter-roster-log
status: approved
source: manual
gdd_tags: [genshin-roster, elemental-reactions, roster, tuning, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-10-15 — Five Genshin character design entries

## Request and scope

The owner approved the elemental documentation/plan and introduced Amber, Kaeya, Lisa, Barbara, and Anemo Lumine, authorizing generation of their missing stats. This follow-up updates design/content drafts and the implementation plan. No runtime characters, SO/card assets, summon pools, source-catalog JSON, dependencies, builds, commits, or publishing were changed.

## Recorded decisions

- Source regular skills are R3 and ultimates C0/5. Sword selects one enemy; bow affects all active enemies. Kaeya's team Waltz is C5/5.
- Bunny detonates on natural expiry only; cleanse is safe. Bunny expiry and Waltz retaliation are explicit reaction-capable authored attacks; ordinary DOT and reflect stay terminal.
- Amber doubles Bunny, Overload, and explicitly tagged Explosion packets, once per eligible packet.
- Barbara's Dissolve reduces target ultimate gauge at the target's turn end; magnitude and duration are not supplied.

## Deliverables

- Canonical [GDD starter roster](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-starter-roster) and reaction exception/approval updates.
- [Detailed five-character authoring draft](../Specs/CharacterData/genshin-character-roster.md): complete generated level-one stats, proposed growth, preserved source kits, lower-rank/ultimate starting tables, traits, validation and K-01–K-05 source gaps.
- [Plan extension](../Specs/genshin-element-system-arch-plan.md#five-character-content-extension): typed removal causes, delayed grouping, explosion category, retaliation origins/limits, gauge rate/Dissolve operations, healing attribution and staged content rollout.
- [Roster tests](../TestPlans/genshin-element-system-test-plan.md#roster-extension-checks) and approval metadata updated to reflect the owner's actual answer.

## Design observations

The five fighters compete for four formation slots. Lumine provides SUB support but does not cast while reserved. Barbara's Hydro damage does not imply Wet; this constrains preparation routes. Waltz is not generic reflected damage and must not bounce between protected teams. AOE Bunny expiry must have an explicit group/budget policy. Generated stats have validation/adjustment directions and are not claimed balanced.

## Verification

Documentation validation passed across nine touched documents: 73 local links/anchors resolved; metadata referenced existing GDD tags; elemental/roster tags were unique; fences and whitespace passed; all five character sections and core stat-table widths were present. Scoped diff whitespace checks also passed. Gameplay, generated-stat balance, progression/platform and kit tests remain not run. Unspecified burn/Dissolve/keyword/progression details are recorded rather than silently fabricated.
