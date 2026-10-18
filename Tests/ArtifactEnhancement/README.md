# Artifact enhancement checks

Run from the project root:

```powershell
dotnet run --project Tests/ArtifactEnhancement/ArtifactEnhancementChecks.csproj -c Release
```

These checks exercise the real core artifact transactions without Unity. They verify every authored main endpoint, per-slot eligibility, all 19 substat bases, quality/rarity scaling and rounding, generation across 768 seeds and all rarities, main/substat exclusion, saved-artifact migration, actual stat application, and separate global/DOT/Additional damage effects. Across a further 128 seeds they verify random affix selection and gain, pure and repeatable draft previews, exact draft/commit agreement, saved upgrade preservation, repeated milestones on the same line, and material selection without rerolling. They also cover stale or modified reviews, resource protection, maximum level, and the same final result from one large enhancement or many small ones.

An enhancement enters review only with available, eligible materials and sufficient Coins. Canceling a draft spends nothing. Confirming revalidates the reviewed outcome, spends all costs together, and produces a detached inventory; the account adapter saves it or restores the previous inventory on a save failure. Affix rolls remain tied to each artifact and its earned milestones, so reopening drafts or changing materials cannot select a better outcome. These checks preserve existing economy values and milestone rules.

Unity presentation checks remain responsible for the draft animation, EXP fill, sound, changed stat arrow and sparkle effects. Manually exercise rapid material selection, cancel/reopen, and confirmation at a milestone; the stat reached by the fill must match the reviewed stat and the persisted result.

## Recorded result — 2026-10-17

The focused authored-table suite passed **35,122 assertions**. The Core DLL was rebuilt for Unity.

The broader `Tests/CombatPresentation/Run.ps1` default, `-ElementalOnly`, and `-DungeonRewardsOnly` paths stop at `MultiHitChecks.cs:145`, `ElementalExpansionChecks.cs:35`, and `DungeonRewardChecks.cs:66`, respectively. Each failure was reproduced in a disposable baseline containing the current unrelated work with the four artifact balance implementation files restored to their prior versions. These are existing failures rather than passing regression gates; they were not changed by this artifact work. The elemental path passed its preceding 279 neutral aura assertions before reaching its failure.
