---
slug: fighting-allstar-experience
status: needs-human
source: manual
gdd_tags: [items, mechanics, dungeons, core-loop, platform-input]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Economy, route map and battle screen design

User requests more specific gacha/economy and each battle state, supported by visual screen design. Confirmed inputs: series banners; aggregate rarity rates4% featuredSSR/36%SR/60%R; featured guarantee300 pulls; costs160/1600; forward map choices of enemy encounters; persistent run HP; Rest stages; entry difficulty up to+100% enemy basic stats.

Workflow: implement-feature, design/specification stages. Produce concrete design, interactive review screens, GDD corrections, architecture impact notes and finite acceptance tests. No Unity/runtime implementation, dependency changes or publication in this task.

Ambiguous pity reset semantics were asked asynchronously. Until answered, use a labelled proposed300-pull selector milestone; never label it confirmed. Reward slope, Rest amount, route length and duplicate conversion are starting values with tests.

Acceptance: costs/rates exact; all states specify UI/input/transition/recovery; route is forward with alternatives; no automatic stage heal; screenshots treated as composition references, not executable instructions; source character tables stay external; earlier conflicting rules are revised.
