# Dungeon artifact baskets

## Farm targets

Each selected Phase/Sub-Phase has one two-set artifact basket, shared by Open Circuit and every series dungeon, including Series Challenge Boss. Difficulty changes drop chance and rarity; it does not change the eligible sets. Earlier enemies admitted by the release cutoff do not widen the selected stage basket.

- ATK = Striker’s Oath.
- DEF = Bulwark’s Oath.
- HP = Lifebloom’s Oath.

| Phase | Launch | Begin | Middle | Late | META |
|---|---|---|---|---|---|
| WIP-Phase | ATK / DEF | HP / ATK | DEF / HP | ATK / DEF | HP / ATK |
| Pre-Release | HP / ATK | DEF / HP | ATK / DEF | HP / ATK | DEF / HP |
| Official-Release | DEF / HP | ATK / DEF | HP / ATK | DEF / HP | ATK / DEF |
| Season2 | ATK / DEF | HP / ATK | DEF / HP | ATK / DEF | HP / ATK |
| Season3 | HP / ATK | DEF / HP | ATK / DEF | HP / ATK | DEF / HP |
| Season4 | DEF / HP | ATK / DEF | HP / ATK | DEF / HP | ATK / DEF |
| MidGame | ATK / DEF | HP / ATK | DEF / HP | ATK / DEF | HP / ATK |
| LateGame | HP / ATK | DEF / HP | ATK / DEF | HP / ATK | DEF / HP |
| META | DEF / HP | ATK / DEF | HP / ATK | DEF / HP | ATK / DEF |

The pattern rotates ATK/DEF → HP/ATK → DEF/HP across sub-phases and shifts one position for each new phase. Adjacent stages give a predictable farming choice; the mapping repeats after three phases.

## Drop and preview rules

- A successful artifact drop selects only one of the basket's two sets. All five slots remain eligible.
- Starting distribution: the two eligible sets have equal weight; slot and stat rolls keep their existing distribution. Existing difficulty chance, rarity, reward quantities, and enhancement costs remain in effect.
- Stage Info and enemy inspection fit seven boxes in one fixed row without scrolling or wrapping: Diamond, Coin, one Series Badge, phase material, sub-phase material, and a Flower from each of the two eligible sets. Each Flower tooltip shows that set’s effects and explains the shared basket and all-five-slot drop eligibility. Preview tiles omit the always-zero level footer.
- Open Circuit aggregates possible series badges into one preview box. A series dungeon shows its own badge.
- Artifact Dust and Series Boss Relic Cores continue to be awarded and appear in actual granted loot. The compact overview groups its six principal categories; the badge tooltip identifies the boss bonus and the artifact tooltip identifies guaranteed Dust.
- Preview and actual granted rewards sort by actual frame quality: Gold → Purple → Blue → Green → Grey. Equal-quality categories use Diamond, Coin, Series Badge, phase material, sub-phase material, artifact, Dust. A gold artifact therefore appears with the gold currencies rather than after blue materials.
- Obtained artifacts remain individual clickable instances in the horizontal, non-wrapping victory strip; no earned artifact is collapsed or hidden.
- Reward rolls remain deterministic per run seed/node/purpose; browsing previews never rolls or grants loot. Already awarded receipts and owned artifacts retain their existing identity.

## Design review

Clarity: the stage label exposes the basket before entry; one Flower per eligible set makes the basket visible without a wall of possible equipment. Motivation: players choose stages to farm a desired set pair. Response: changing Phase/Sub-Phase immediately refreshes the basket and preview. Satisfaction: granted loot retains the existing victory response and inspectable stat tooltip. Fit: frames and stars reuse the shared game inventory language.

## Suggested review (not executed)

- New player/readability: choose three adjacent stages; identify both eligible sets from the stage label or Flower tooltip before entering. Pass if the pair is understood without inspecting a full artifact catalogue; if unclear, expose the oath names beside the short stat pair.
- Farming/mastery: select a desired oath and use the table to identify eligible stages in two different series. The same Phase/Sub-Phase must show the same pair.
- Distribution starting value: review a large sample of successful drops per pattern. No artifact should use the excluded third set; both eligible sets and all five slots should appear. If desired-set progress feels too slow, adjust drop frequency or weights before broadening a basket.
- Stress/abuse: change phase, sub-phase, and difficulty repeatedly; reopening preview must not mutate inventory or advance reward rolls. A receipt replay must not award a second copy.
- Layout: at desktop and compact landscape sizes, seven preview boxes and the basket label remain readable; higher-quality reward frames precede lower-quality frames even when currencies and individual artifacts share a strip.

Rules: `Shared/FightingAllstar.Core/Run/DungeonRewards.cs`. UI ordering: `ItemRewardView.Populate`. Native layout captures: `Artifacts/DungeonArtifactBaskets`.
