---
slug: fighting-allstar-experience
status: needs-human
gdd_tags: [items, dungeons, mechanics, architecture, roadmap]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Economy, map, battle states and Unity phase plan

## Completed design

Incorporated owner rates4%featuredSSR/36%SR/60%R,160/1600 pull costs and300 threshold. Added seriesId/series trait mapping, per-bucket individual rate rules, banner states/receipts, duplicate/constellation conversion and proposed pacing. Guarantee reset semantics were asked; no answer received during drafting, so selector milestone remains explicitly proposed.

Replaced forced stage order and automatic healing with a forward selectable route graph, persistent runHP, Rest heal/revive choice and locked0..100%difficulty. Reward baseline/slope and Rest amounts are labelled starting values. Reference screenshots inform composition only.

Delivered [economy/route design](../Specs/fighting-allstar-economy-route-design.md), [22 battle screen states](../Specs/fighting-allstar-battle-screen-states.md), and [Unity implementation plan](../Specs/fighting-allstar-unity-implementation-plan.md). Updated GDD, existing architecture, ADR and test plan. The later owner request explicitly authorizes the concrete implementation phase plan; no runtime implementation was requested or performed in this pass.

## Visual review

Created interactive banner, route and battle-state review screens. Browser checks verified banner purchase-review→fixed-result state,1600 debit in illustrative wallet,295+10→5/300 plus selector, battle card queue action decrement and exact queue reset. Screenshot inspection found legible desktop battlefield/queue/hand composition. These snapshots are not a combat engine and do not simulate random outcomes. Responsive CSS exists; real phone/device QA remains a phase gate.

## Verification / limitations

Existing source-data validator still passes29-character/rank/tier/source preservation checks. New documentation links and state coverage checked separately. Rates/cost/reward and milestone arithmetic are specified with boundary fixtures. No Unity tests/builds, backend provisioning, package installs, paid transactions or content publication occurred. First executable work is FA-001: pure damage resolver, typed unit policies, complete Kyo definition and golden vectors, followed by early Unity portability checks.
