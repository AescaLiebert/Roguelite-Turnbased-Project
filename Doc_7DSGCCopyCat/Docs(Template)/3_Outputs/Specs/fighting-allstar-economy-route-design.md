---
slug: fighting-allstar-economy-route
status: needs-human
source: user-chat-and-image-reference
gdd_tags: [items, dungeons, core-loop, roster, tuning]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Fighting Allstar — banners, economy and dungeon journey

## 1. Decisions and reference use

**Confirmed by owner:** series-specific banners (KOF, Tekken, DOA, Street Fighter, etc.);4% total featuredSSR,36%SR,60%R;160 Diamonds/single and1600/ten;300-pull featured guarantee. Dungeon is a forward linear map where the player chooses which available encounter to fight/avoid. Run HP persists; Rest stages heal. Entry difficulty up to+100% doubles enemy basic stats.

**Proposals:** the unresolved300-pull guarantee uses a selector milestone, not a reset-on-SSR hard pity; rewards scale1:1 with difficulty; nine map rows per floor; Rest heals40% MaxHP or revives one fighter at30%; complete-run baseline320 Diamonds. These remain starting values. The separate question about pity can change the guarantee without changing the confirmed rarity rates/costs.

Image1 supplies the idea of upward progress, connected alternative nodes, recognizable node types and floor goals. It does not establish its displayed optimal route as this game's rule. Image2 supplies the battlefield composition: enemy trio above, player trio below, health/PG near fighters, action queue above the bottom hand. Enemy NEXT-card icons are not copied as disclosure of the AI's hidden hand.

This revision supersedes equal eight-character odds,100-Diamond pulls, the5-pull unowned guarantee, forced single encounter at each step, and automatic40% healing/revival after every battle. Permanent roster survival on run loss remains confirmed.

## 2. Banner identity and pool

Add canonical `seriesId` (e.g. `series.kof`) and a matching searchable series trait. A combat trait such as Japan, Women, American or Fire Elements does not determine franchise. Existing29 rows lack an explicit series field: proposed reviewed mapping is KOF for this batch. Preserve source traits; add normalized metadata rather than rewriting source CSV. Do not create playable Tekken/DOA/Street Fighter banners until their pools contain validated characters.

Each immutable banner revision defines series, display art, featuredSSR IDs/weights, SR IDs/weights, R IDs/weights, rates, price, guarantee policy, effective time, and carryover group. No fighter from another series appears accidentally. Cost and rates shown in the UI must come from the same revision used for settlement.

| Rarity bucket | Confirmed aggregate rate | Proposed within-bucket rule |
| --- | --- | --- |
| Featured SSR |4% | Equal weights among featuredSSR; no off-bannerSSR in this draft |
| SR |36% | Equal weights among eligible seriesSR |
| R |60% | Equal weights among eligible seriesR |

For N featuredSSR, each has4/N percent chance, not4% each. A banner with an empty required bucket cannot activate; never silently redistribute rates. Future unequal featured weights need explicit individual disclosure. No soft pity or guaranteedSR ten-pull is inferred.

**Eight-kit prototype KOF pool:** Athena94 is the soleSSR at4%; Kyo94/King94/Mai94/Shingo97 each9%; Chin94/Kensou94/Benimaru94 each20%. The other21 WIP fighters join only after their runtime kits are complete. A singleSSR prototype banner is useful for proving transactions, not proof of final collection variety. Full KOF banner publication uses all approved rows with the same aggregate rarity split.

## 3. Summon and guarantee semantics

### Price and ordinary pulls

One pull160; ten pulls1600, with no discount. Every draw uses the disclosed bucket rates, independent of previous ordinary draws. Ten draws are settled in order in one transaction and returned as one immutable receipt. At4%, the chance of at least oneSSR in ten ordinary draws is `1−.96^10 ≈33.52%`; ten is not a guaranteedSSR.300 ordinary draws cost48000 Diamonds and have expected12SSR total; these calculations do not promise12 specific featured characters.

### Proposed300-pull selector milestone

- Track `milestoneProgress`0..299 and `unclaimedFeaturedChoices` separately for each series guarantee group. Each paid pull adds1. Reaching300 creates one selector entitlement and subtracts300. It does not replace any of the ordinary draw results.
- EarlySSR does not reset progress. This is visibly named **Featured guarantee: X /300** with detail “Choose a featuredSSR every300 pulls; earlySSR does not reset progress.” This text is proposed until the owner answers the pity question.
- Ten-pull starting295 ends5/300 and creates one selector. Multiple milestones across accumulated requests create separate entitlements; each can be claimed once. An unclaimed selector does not block future summons.
- Selector snapshots the eligible featured IDs at earning time. Expiry/rotation cannot remove earned choices. Proposed carryover: progress stays within the same series guarantee group across revisions; it never transfers between KOF and Tekken. A seasonal limited group must disclose a different rule before spending.
- Select an owned character if desired; grant its duplicate crest. No unowned-only condition. Selecting a C6 fighter uses capped-duplicate conversion with a preview.
- If the owner chooses hard pity instead, replace this with a featuredSSR on the300th consecutive qualifying miss, reset on any qualifying featuredSSR, and explicitly define which featured fighter is guaranteed. Do not combine both guarantee types or silently ship this alternative.

### Ownership and duplicate handling — proposed

First copy unlocks C0. Later copies give one character-specific crest; one crest buys the next tier, six total toC6. Upgrades require a separate preview/confirm and do not auto-spend at summon time. AtC6, duplicates grant one Collection Token. Prototype tokens are visible and persisted with “Exchange unavailable in prototype”; no Diamond refund, cash value or unannounced future exchange rate. Duplicate receipts reference stable definition IDs, not display names.

### Gacha screen states

| State | Visible information | Allowed action | Exit / failure behavior |
| --- | --- | --- | --- |
| G00 Loading | Series tabs, loading pool, known wallet marked refreshing | Back | Disable draw until authoritative revision/wallet available |
| G01 Banner browse | Series, featured art/name, rates, wallet,160/1600 buttons, guarantee progress, selector count | Switch published series, inspect character/rates, choose pull count | Unpublished series labelled unavailable; no paid CTA |
| G02 Rates | Bucket and individual rates, included characters, guarantee reset/carryover rules | Close, inspect fighter | Return without changing selection or wallet |
| G03 Purchase review | Banner revision/name, pull count, total debit, resulting balance, guarantee impact | Confirm or cancel | Insufficient funds: explain shortfall and link dungeon; no top-up UI assumed |
| G04 Submitting | “Summoning…” and pending receipt status | Wait or leave to menu | Block repeated tap; leaving does not cancel a submitted purchase |
| G05 Reveal | Cards from committed receipt, New/Crest/Token badges, current wallet | Reveal next/all, skip animation | Skip has no economy effect; disconnect reopens same results |
| G06 Results | All1/10 rewards, conversion details, progress before/after, selector earned banner | Done, inspect, another purchase review | No automatic second purchase |
| G07 Featured choice | Frozen eligibleSSR roster, C0/C-tier and duplicate outcome preview | Select, confirm, or leave unclaimed | Retry same claim is idempotent; a rejected choice consumes nothing |
| G08 Recovering | “Checking your summon…” and request reference | Retry status/back | Never show a fresh paid button for an unknown transaction outcome |

Layout: series tabs at top; centered featured character/series identity; rate and character-detail links adjacent; wallet upper-right; guarantee bar directly above draw buttons; bottom1×160 /10×1600. Rates are not hidden in the reveal screen. Use restrainedSSR accent; the cost/result remain legible when animation is skipped.

## 4. Currency sources, sinks and pacing

| Resource | Source | Sink / lifetime |
| --- | --- | --- |
| Diamonds | Completed dungeon reward; proposed one-time onboarding grant |160/1600 summons; persistent, server ledger |
| Character crest | Duplicate belowC6 or selected duplicate | One constellation upgrade; persistent |
| Collection Token | Duplicate alreadyC6 | Persist only in P0; future exchange requires separate economy design |
| Run boons | Route reward nodes | Current run only; no exchange to Diamonds |
| Run HP | Battle result, heals, Rest | Current run only; no purchase of HP in P0 |

**Starting pacing proposal:** baseline completed run320 Diamonds; at+100% difficulty640. Onboarding grants1600 once after the teaching encounter so the player can experience a ten-pull; guaranteed tutorial allies remain independent from gacha. First Open Circuit clear still gives its one-time roster selector (separate from300-pull featured guarantee). No stamina or paid entry fee in P0, so defeat cannot remove a paid admission cost.

Completion pays once for the run, not once per map click or defeated fighter. No partial run Diamonds in the first prototype; abandoned/lost runs keep previous permanent holdings but grant no completion reward. Route choices trade survival, matchup and run boons; they do not secretly change the quoted Diamond completion reward. First-clear/onboarding/selector grants are not multiplied by difficulty.

| Difficulty bonus | Enemy ATK/DEF/MaxHP | Proposed Diamond multiplier | Reward | Runs for1600 | Runs for48000 |
| --- | --- | --- | --- | --- | --- |
|0% |×1.00 |×1.00 |320 |5 |150 |
|25% |×1.25 |×1.25 |400 |4 |120 |
|50% |×1.50 |×1.50 |480 |4 (1920 earned) |100 |
|75% |×1.75 |×1.75 |560 |3 (1680 earned) |86 (48160 earned) |
|100% |×2.00 |×2.00 |640 |3 (1920 earned) |75 |

These run counts exclude grants and assume wins. Do not call300-pull pacing final: at20 minutes/run,150 wins is50 hours. Measure median successful runs/time per new character and per chosen featured. If roster restrictions stall players, improve deterministic roster rewards or Diamond earn rate before increasing duplicate power. Difficulty has a nonlinear effect on combat because ATK, DEF and HP all rise; “2×stats” is not “2×combat difficulty.”

## 5. Forward route map

“Linear” means the run only advances toward its floor goal. Within the next row the player chooses one reachable node; alternatives are skipped permanently. There is no backward farming or arbitrary floor teleport. This is a layered directed route, consistent with the reference's forward movement and side choices.

**Starting floor template:**9 rows: Start → Battle choice → Battle choice → Rest/Elite choice → Boon → Battle choice → Rest/Battle choice → Elite choice → Boss. Most battle rows offer2 choices; rows with a required checkpoint offer1. P0 has one complete floor; the data can compose3 floors later. Display the full floor with links and enemy previews; do not hide a “wrong” route behind unidentified enemy icons.

Edges connect only row r to r+1. Start, Boon and Boss rows converge; other rows may link both next choices. Validate at least one Start→Boss route and a reachable Rest on a baseline recommended route, but allow the player to choose a risky Rest-skipping route. Every node is immutable for the run seed/content version; leaving/reopening previews cannot reroll its team. Defeating one node commits the row choice and marks unchosen alternatives bypassed.

| Node | Preview | Completion | Choice tradeoff |
| --- | --- | --- | --- |
| Battle | Full enemy3+1, attributes, enabled passives, scaled basic stats, CC and tactical tags | Victory advances row and saves HP | Choose the matchup the current injured roster can handle |
| Elite | Same data, coordinated team and disclosed progression | Victory advances; offers one temporary boon | Stronger synergy with same chosen difficulty multiplier; no hidden extra stat multiplier |
| Rest | Exact heal/revive preview for each fighter | Confirm one Rest action | Survival now versus Elite/Battle benefit on alternate node |
| Boon | Three compatible unowned run boons | Choose one, persist and advance | Adapt current roster without permanent power gain |
| Boss | Full final character team, clearly marked mandatory | Win closes floor/run | No guaranteed bypass of the final test |

Map layout: top floor/row/progress and locked difficulty/reward; central rising connected nodes with a small party marker; right/bottom drawer for selected node; bottom persistent4-fighter HP strip and reserve marker. Unreachable nodes dim with reason; hovered/tapped nodes preview without commitment. “Travel” confirms selection; marker walks along the chosen edge after server acceptance. Animation skipping never changes node choice.

## 6. Run HP and Rest

On run start, frozen four-character roster begins at resolved MaxHP. Store each fighter's current HP and defeat flag by run fighter ID. Winning a battle saves exact HP, including zeros; **no automatic heal or revive**. Reset cards/PG/shields/combat statuses between encounters, preserve HP/boons. Normal in-battle healing and Regeneration still work and the resulting HP carries out.

Partial casualties remain unavailable until revived. Before next combat, automatically fill vacant active positions from living run fighters in stable formation order; preview/reorder before entry. A living reserve prevents a premature defeat. Battles may start with one, two, or three active fighters; the action field provides two slots for one or two active fighters and three slots for three active fighters. Do not borrow new inventory fighters midrun. A total wipe ends the run; owned characters are safe and a new run starts healthy.

**Rest starting choices, pick one:** (A) Recover40% of each living fighter's run MaxHP, capped; does not revive. (B) Revive one defeated fighter at30% MaxHP, no healing to others. Both are free but consume the node. If all living fighters are full and none defeated, Continue advances without benefit; no banked Rest charge. If only one choice is meaningful, show why the other is disabled. Reconnect/duplicate confirms cannot apply Rest twice. Rest ignores expired combat healing debuffs; no surviving combat Infect crosses the encounter boundary.

For initial boons, avoid changing MaxHP so HP persistence is unambiguous. A future MaxHP boon must specify whether it adds currentHP; do not auto-heal by swapping slots or recalculating stats. In-battle maxHP changes expire before persistence: retain absolute currentHP clamped to runMaxHP, with no refill. Healing stall before a lethal victory is possible: track turn count and regeneration in playtests; the existing40-owner-turn draw limit remains.

## 7. Entry difficulty

Slider0..100% in proposed5-point steps before entering. Confirmed endpoint: enemy basic ATK/DEF/MaxHP at+100%=double. Freeze the selected bonus for the entire run and show it on map/encounter/result. No changing it before payout or lowering it for boss then raising it again.

Compute each enemy basic stat once from the immutable encounter build: `floor(baseResolvedStat * (100+d)/100)`. Do not multiply Pierce, Resistance, crit/block, regeneration, card coefficients, proc rates, PG, skill rank or constellation. Later status modifiers apply to these scaled stats normally. Proposed displayed enemyCC=`floor(authoredCC*(100+d)/100)` per fighter for initiative; this CC estimate is explicitly separate from basic stat scaling and needs first-turn-bias tests. Player stats/HP do not scale.

Proposed reward=`floor(baseCompletionDiamonds*(100+d)/100)` once at settlement. Entry preview shows exact old→new ATK/DEF/HP for an example enemy and the final reward; banner rates remain unchanged at every difficulty. Difficulty selections never create extra first-clear entitlements.

## 8. Route state transitions

| State | Visible / available | Transition and persistence |
| --- | --- | --- |
| R00 Setup | Profile restriction, roster, difficulty slider, completion reward | Confirm atomically creates run/versioned map/full runHP; invalid team starts nothing |
| R01 Map idle | Current marker, reachable alternatives, rosterHP | Inspect any revealed node; only reachable nodes can be selected |
| R02 Node preview | Full encounter or Rest details, Travel/Back | Back free; Travel commits reachable edge using expected run revision |
| R03 Traveling | Marker moves, destination locked | Server node already chosen; skip moves to same arrival; disconnect resumes accepted node |
| R04 Encounter | Battle setup, saved HP, opposing team | Load Combat; unresolved battle must resume, not reroll/restart |
| R05 Rest / Boon | Exact available choices | Commit once, persist HP/boon and completion, then return map |
| R06 Battle return | Victory/casualties, runHP before/after | Result receipt updates run once; completed node unlocks next row |
| R07 Run victory | Base reward, bonus, total, first-clear separate | Authoritative completion receipt; claim presentation can retry |
| R08 Defeat / abandon | Run ended, permanent roster preserved | No completion payout; menu/new run. Abandon requires confirmation, unresolved battle cannot be replayed for a new result |
| R09 Resume | Last accepted node, HP and pending result | Resolve receipt/status before allowing next traversal |

Map is a MainMenu view; battles use Combat. The walking marker is presentation; no physics navigation or third runtime scene is required for the prototype.

## 9. Acceptance and tuning

All unconfirmed numbers above are starting values. Test a new account reaching one ten-pull and a legal restricted-dungeon roster without random luck. Compare Rest vs Elite choices at low/medium/full HP: if Rest always dominates even at fullHP, improve tradeoff; if ignored at lowHP, raise recovery or lower encounter spike. Validate every generated route has a legal boss path and declared Rest access. Test nine-row run pacing against12–25min and shorten rows/playback if it exceeds that before reducing decision time.

Economy boundaries: rates sum100%; individual rates sum their bucket;159 Diamonds cannot buy1;160 can;1599 cannot buy10;1600 can. Retry/disconnect grants identical receipt.295+10 gives5 and one selector under proposed milestone. DuplicateC6 converts once. Difficulty0/100 gives exact1×/2× basic stats and320/640 reward; substats unchanged. Route HP0 persists, Rest heal/revive distinct, skipped nodes cannot pay rewards, difficulty cannot mutate midrun. These are planned tests, not shipped features.
